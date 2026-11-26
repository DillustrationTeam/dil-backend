using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.BackgroundJobs;

/// <summary>
/// Dọn ảnh phác thảo bố cục AI tạm (PLAN.md T06): ảnh không được chọn hoặc đơn bỏ dở
/// quá 24h (<see cref="TempImageRetention"/>) vẫn còn ở prefix "ai-ref-temp/" trên R2.
/// Chạy mỗi giờ qua Hangfire recurring job, idempotent (ảnh đã xoá thì bỏ qua ở lượt sau).
/// </summary>
public class AiReferenceTempCleanupJob
{
    private static readonly TimeSpan TempImageRetention = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storage;
    private readonly ILogger<AiReferenceTempCleanupJob> _logger;

    public AiReferenceTempCleanupJob(
        IApplicationDbContext db,
        IStorageService storage,
        ILogger<AiReferenceTempCleanupJob> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TempImageRetention;

        var expiredImages = await _db.AiReferenceImages
            .Where(i => !i.IsDeleted && i.CommissionId == null && i.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var image in expiredImages)
        {
            try
            {
                await _storage.DeletePrivateAsync(image.TempStorageKey, cancellationToken);
                image.IsDeleted = true;
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Không xoá được ảnh phác thảo bố cục tạm {ImageId} (key={Key}); bỏ qua để xử lý ảnh còn lại.",
                    image.Id, image.TempStorageKey);
            }
        }

        if (expiredImages.Count > 0)
        {
            _logger.LogInformation(
                "Đã dọn {Count} ảnh phác thảo bố cục tạm quá {Hours}h.",
                expiredImages.Count, TempImageRetention.TotalHours);
        }
    }
}
