using ArtCommission.Application.ArtistStudio.Moderation;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job thực hiện quét AI cho tác phẩm (SCR-20 / UC28).
/// Chạy bất đồng bộ qua Hangfire để không làm nghẽn tiến trình tải ảnh của người dùng.
/// </summary>
public class ArtworkScanJob : IArtworkScanJob
{
    private readonly IApplicationDbContext _db;
    private readonly IArtworkScanService _scanService;
    private readonly ILogger<ArtworkScanJob> _logger;

    public ArtworkScanJob(
        IApplicationDbContext db,
        IArtworkScanService scanService,
        ILogger<ArtworkScanJob> logger)
    {
        _db = db;
        _scanService = scanService;
        _logger = logger;
    }

    public async Task ProcessArtworkScanAsync(Guid artworkId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bắt đầu job quét AI cho tác phẩm {ArtworkId}.", artworkId);

        var artwork = await _db.Artworks
            .Include(a => a.ArtworkTags)
                .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == artworkId && !a.IsDeleted, cancellationToken);

        if (artwork == null)
        {
            _logger.LogWarning("Không tìm thấy tác phẩm {ArtworkId} hoặc đã bị xóa. Bỏ qua quét AI.", artworkId);
            return;
        }

        if (string.IsNullOrWhiteSpace(artwork.ImageUrl))
        {
            _logger.LogWarning("Tác phẩm {ArtworkId} không có URL ảnh hợp lệ.", artworkId);
            return;
        }

        var scanResult = await _scanService.ScanArtworkAsync(artwork.ImageUrl, cancellationToken);

        if (!scanResult.Success)
        {
            _logger.LogError("Quét AI thất bại cho tác phẩm {ArtworkId}: {Error}", artworkId, scanResult.Error);
            artwork.FlagReason = string.IsNullOrWhiteSpace(artwork.FlagReason)
                ? $"Quét AI chưa hoàn tất: {scanResult.Error}"
                : artwork.FlagReason;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Đọc cấu hình ngưỡng AI scan từ PlatformConfig (nếu Admin có cấu hình)
        var configs = await _db.PlatformConfigs
            .AsNoTracking()
            .Where(c => !c.IsDeleted && (
                c.Key == PlatformConfigKeys.AiScanAdultThreshold ||
                c.Key == PlatformConfigKeys.AiScanViolenceThreshold ||
                c.Key == PlatformConfigKeys.AiScanAiDetectionThreshold ||
                c.Key == PlatformConfigKeys.AiScanAutoTaggingEnabled))
            .ToDictionaryAsync(c => c.Key, c => c.Value, cancellationToken);

        decimal adultThreshold = 0.70m;
        if (configs.TryGetValue(PlatformConfigKeys.AiScanAdultThreshold, out var adultVal) &&
            decimal.TryParse(adultVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAdult))
        {
            adultThreshold = Math.Clamp(parsedAdult, 0.0m, 1.0m);
        }

        decimal violenceThreshold = 0.70m;
        if (configs.TryGetValue(PlatformConfigKeys.AiScanViolenceThreshold, out var violenceVal) &&
            decimal.TryParse(violenceVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedViolence))
        {
            violenceThreshold = Math.Clamp(parsedViolence, 0.0m, 1.0m);
        }

        decimal aiThreshold = 0.75m;
        if (configs.TryGetValue(PlatformConfigKeys.AiScanAiDetectionThreshold, out var aiVal) &&
            decimal.TryParse(aiVal, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAi))
        {
            aiThreshold = Math.Clamp(parsedAi, 0.0m, 1.0m);
        }

        bool autoTaggingEnabled = true;
        if (configs.TryGetValue(PlatformConfigKeys.AiScanAutoTaggingEnabled, out var tagVal) &&
            bool.TryParse(tagVal, out var parsedTag))
        {
            autoTaggingEnabled = parsedTag;
        }

        var adultScore = scanResult.AdultScore ?? 0m;
        var violenceScore = scanResult.ViolenceScore ?? 0m;
        var aiScore = scanResult.AiDetectionScore ?? 0m;

        bool isAiGenerated = scanResult.IsAiGenerated || (aiScore >= aiThreshold);
        string? flagReason = scanResult.FlagReason;

        // Đánh giá lại FlagReason theo ngưỡng PlatformConfig hiện hành
        if (adultScore >= adultThreshold)
        {
            flagReason = "AI NSFW Threshold Exceeded";
        }
        else if (violenceScore >= violenceThreshold)
        {
            flagReason = "Violence/Gore Detected";
        }
        else if (isAiGenerated && string.IsNullOrWhiteSpace(flagReason))
        {
            flagReason = "AI Generated Elements Detected";
        }

        // Cập nhật các chỉ số AI
        artwork.SafeScore = scanResult.SafeScore;
        artwork.AdultScore = scanResult.AdultScore;
        artwork.ViolenceScore = scanResult.ViolenceScore;
        artwork.IsAiGenerated = isAiGenerated;
        artwork.AiDetectionScore = scanResult.AiDetectionScore;
        artwork.FlagReason = flagReason;
        artwork.UpdatedAt = DateTimeOffset.UtcNow;

        // Auto-tagging: Gợi ý thêm tag phong cách từ AI nếu được bật trong PlatformConfig
        if (autoTaggingEnabled && scanResult.SuggestedTags is { Count: > 0 })
        {
            foreach (var tagName in scanResult.SuggestedTags.Take(5))
            {
                var cleanTag = tagName.Trim();
                if (string.IsNullOrWhiteSpace(cleanTag))
                {
                    continue;
                }

                // Kiểm tra xem tranh đã có tag này chưa
                var alreadyHasTag = artwork.ArtworkTags.Any(at => 
                    at.Tag != null && string.Equals(at.Tag.Name, cleanTag, StringComparison.OrdinalIgnoreCase));

                if (alreadyHasTag)
                {
                    continue;
                }

                var existingTag = await _db.Tags
                    .FirstOrDefaultAsync(t => t.Name == cleanTag, cancellationToken);

                if (existingTag == null)
                {
                    existingTag = new Tag
                    {
                        Id = Guid.NewGuid(),
                        Name = cleanTag,
                        IsAiGenerated = scanResult.IsAiGenerated
                    };
                    _db.Tags.Add(existingTag);
                }

                artwork.ArtworkTags.Add(new ArtworkTag
                {
                    ArtworkId = artwork.Id,
                    TagId = existingTag.Id
                });
            }
        }

        // Cập nhật điều kiện AiScanPassed cho các bài dự thi sự kiện liên quan (nếu có)
        var relatedSubmissions = await _db.EventSubmissions
            .Where(s => s.ArtworkId == artwork.Id)
            .ToListAsync(cancellationToken);

        foreach (var submission in relatedSubmissions)
        {
            submission.AiScanPassed = !artwork.IsAiGenerated && 
                (artwork.AiDetectionScore == null || artwork.AiDetectionScore < 0.5m);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Hoàn tất quét AI tác phẩm {ArtworkId}. Safe: {SafeScore}, Adult: {AdultScore}, Violence: {ViolenceScore}, AiScore: {AiScore}, Flag: {FlagReason}",
            artwork.Id, artwork.SafeScore, artwork.AdultScore, artwork.ViolenceScore, artwork.AiDetectionScore, artwork.FlagReason);
    }
}

