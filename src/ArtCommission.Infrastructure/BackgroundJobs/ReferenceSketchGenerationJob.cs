using System.Diagnostics;
using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.BackgroundJobs;

/// <summary>
/// Background job thực hiện 1 lượt sinh ảnh phác thảo bố cục reference (xem PLAN.md T04):
/// kiểm tra quy định (10%) → viết 2 phương án (25%) → sinh 2 ảnh song song (25-90%,
/// nội suy theo thời gian ước tính) → đóng dấu watermark + lưu tạm (100%).
/// Chạy qua Hangfire để không chặn request tạo lượt sinh.
/// </summary>
public class ReferenceSketchGenerationJob : IReferenceSketchGenerationJob
{
    private const int PolicyCheckPercent = 10;
    private const int PromptPercent = 25;
    private const int ImageStartPercent = 25;
    private const int ImageEndPercent = 90;
    private const int DonePercent = 100;
    private const string ImageSize = "1024x1536";
    private const string WatermarkText = "AI REFERENCE";

    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan EstimatedImageDuration = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan TempDownloadExpiry = TimeSpan.FromHours(1);

    private readonly IApplicationDbContext _db;
    private readonly ReferenceSketchPromptService _promptService;
    private readonly IAiImageClient _imageClient;
    private readonly IStorageService _storage;
    private readonly IWatermarkService _watermark;
    private readonly ReferenceSketchImageProtector _protector;
    private readonly IReferenceSketchProgressPublisher _progress;
    private readonly ILogger<ReferenceSketchGenerationJob> _logger;

    public ReferenceSketchGenerationJob(
        IApplicationDbContext db,
        ReferenceSketchPromptService promptService,
        IAiImageClient imageClient,
        IStorageService storage,
        IWatermarkService watermark,
        ReferenceSketchImageProtector protector,
        IReferenceSketchProgressPublisher progress,
        ILogger<ReferenceSketchGenerationJob> logger)
    {
        _db = db;
        _promptService = promptService;
        _imageClient = imageClient;
        _storage = storage;
        _watermark = watermark;
        _protector = protector;
        _progress = progress;
        _logger = logger;
    }

    public async Task RunAsync(Guid generationId, CancellationToken cancellationToken = default)
    {
        var generation = await _db.AiReferenceGenerations
            .FirstOrDefaultAsync(g => g.Id == generationId && !g.IsDeleted, cancellationToken);

        if (generation is null || generation.Status != AiReferenceGenerationStatus.Pending)
        {
            _logger.LogWarning(
                "Lượt sinh ảnh phác thảo {GenerationId} không tồn tại hoặc không ở trạng thái Pending, bỏ qua job.",
                generationId);
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(OverallTimeout);
        var ct = timeoutCts.Token;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            generation.Status = AiReferenceGenerationStatus.Running;
            generation.ProgressPercent = 0;
            await _db.SaveChangesAsync(cancellationToken);

            var creator = await _db.CreatorProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == generation.CreatorProfileId && !p.IsDeleted, ct);
            var terms = await _db.CreatorTerms.AsNoTracking()
                .FirstOrDefaultAsync(t => t.CreatorProfileId == generation.CreatorProfileId && !t.IsDeleted, ct);

            if (creator is null || terms is null || !terms.AllowAiReferenceSketch)
            {
                await FailAsync(generation, "Họa sĩ chưa/không còn cho phép dùng AI phác thảo bố cục.", cancellationToken);
                return;
            }

            var extended = CreatorTermsHelper.ParseExtended(terms.ExtendedTermsJson);

            var policy = await _promptService.CheckPolicyAsync(
                generation.BriefText, generation.LayoutHint, extended.WillDraw, extended.WontDraw, ct);

            if (!policy.Success)
            {
                await FailAsync(generation, policy.Error ?? "Không thể kiểm tra quy định lúc này.", cancellationToken);
                return;
            }

            generation.ProgressPercent = PolicyCheckPercent;
            await _db.SaveChangesAsync(cancellationToken);
            await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
                generation.Id, "policy_check", PolicyCheckPercent, null, null, null, null, null), cancellationToken);

            if (!policy.Allowed)
            {
                generation.Status = AiReferenceGenerationStatus.Blocked;
                generation.BlockReason = JsonSerializer.Serialize(new { vi = policy.ReasonVi, en = policy.ReasonEn });
                generation.CompletedAt = DateTimeOffset.UtcNow;
                generation.DurationMs = stopwatch.ElapsedMilliseconds;
                await _db.SaveChangesAsync(cancellationToken);

                await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
                    generation.Id, "blocked", PolicyCheckPercent, null, null, null, policy.ReasonVi, policy.ReasonEn),
                    cancellationToken);
                return;
            }

            var variants = await _promptService.BuildVariantsAsync(
                generation.BriefText, generation.LayoutHint, extended.WontDraw, ct);

            if (!variants.Success || variants.Variants is null || variants.Variants.Length != 2)
            {
                await FailAsync(generation, variants.Error ?? "Không thể tạo phương án bố cục.", cancellationToken);
                return;
            }

            generation.VariantsJson = JsonSerializer.Serialize(variants.Variants);
            generation.ProgressPercent = PromptPercent;
            await _db.SaveChangesAsync(cancellationToken);
            await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
                generation.Id, "writing_prompt", PromptPercent, new[] { 0, 0 }, null, null, null, null),
                cancellationToken);

            using var imageProgressCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var imageStopwatch = Stopwatch.StartNew();
            var progressLoopTask = RunProgressInterpolationAsync(generation, imageStopwatch, imageProgressCts.Token);

            var genTask0 = _imageClient.GenerateAsync(variants.Variants[0].PromptEn, ImageSize, ct);
            var genTask1 = _imageClient.GenerateAsync(variants.Variants[1].PromptEn, ImageSize, ct);

            AiImageResult[] results;
            try
            {
                results = await Task.WhenAll(genTask0, genTask1);
            }
            finally
            {
                imageProgressCts.Cancel();
                try
                {
                    await progressLoopTask;
                }
                catch (OperationCanceledException)
                {
                    // Dừng vòng lặp nội suy tiến trình — bình thường.
                }
            }

            var failedResult = results.FirstOrDefault(r => !r.Success);
            if (failedResult is not null)
            {
                await FailAsync(generation, failedResult.Error ?? "Không sinh được ảnh phác thảo.", cancellationToken);
                return;
            }

            var images = new List<AiReferenceImage>();
            for (var i = 0; i < results.Length; i++)
            {
                var png = results[i].Png!;
                // 1) visible protection: downscale, soften, tiled "AI REFERENCE" mark + badge
                // 2) invisible DWT digital watermark on top, for forensic tracing
                using var source = await _protector.ProtectAsync(png, cancellationToken);
                using var watermarked = await _watermark.ApplyWatermarkAsync(source, WatermarkText, cancellationToken);
                var key = $"ai-ref-temp/{generation.Id}/{i}.png";
                await _storage.UploadPrivateAsync(watermarked, key, "image/png", cancellationToken);

                images.Add(new AiReferenceImage
                {
                    GenerationId = generation.Id,
                    VariantIndex = i,
                    TempStorageKey = key,
                    LabelVi = variants.Variants[i].LabelVi,
                    LabelEn = variants.Variants[i].LabelEn
                });
            }

            foreach (var image in images)
            {
                _db.AiReferenceImages.Add(image);
            }

            generation.Status = AiReferenceGenerationStatus.Succeeded;
            generation.ProgressPercent = DonePercent;
            generation.Model = results.Select(r => r.Model).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? string.Empty;
            generation.CompletedAt = DateTimeOffset.UtcNow;
            generation.DurationMs = stopwatch.ElapsedMilliseconds;
            await _db.SaveChangesAsync(cancellationToken);

            var imageDtos = images
                .OrderBy(i => i.VariantIndex)
                .Select(i => new ReferenceSketchImageDto(
                    i.Id,
                    i.VariantIndex,
                    _storage.GeneratePresignedDownloadUrl(i.TempStorageKey, TempDownloadExpiry),
                    i.LabelVi,
                    i.LabelEn))
                .ToArray();

            await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
                generation.Id, "done", DonePercent, new[] { DonePercent, DonePercent }, imageDtos, null, null, null),
                cancellationToken);

            _logger.LogInformation(
                "Hoàn tất lượt sinh ảnh phác thảo bố cục {GenerationId} trong {Ms}ms.",
                generation.Id, generation.DurationMs);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await FailAsync(generation, "Quá thời gian xử lý (90 giây). Vui lòng thử lại.", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi xử lý job sinh ảnh phác thảo bố cục {GenerationId}.", generationId);
            await FailAsync(generation, "Lỗi hệ thống khi sinh ảnh phác thảo.", cancellationToken);
        }
    }

    private async Task FailAsync(AiReferenceGeneration generation, string error, CancellationToken cancellationToken)
    {
        generation.Status = AiReferenceGenerationStatus.Failed;
        generation.Error = error;
        generation.CompletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
            generation.Id, "failed", generation.ProgressPercent, null, null, error, null, null), cancellationToken);
    }

    /// <summary>
    /// Nội suy % trong khoảng [25, 90] theo thời gian ước tính cho mỗi ảnh (không có tín hiệu
    /// % thật từ OpenAI giữa lúc sinh) — dừng ngay khi cả 2 ảnh xong qua <paramref name="ct"/>.
    /// </summary>
    private async Task RunProgressInterpolationAsync(AiReferenceGeneration generation, Stopwatch imageStopwatch, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            var fraction = Math.Clamp(
                imageStopwatch.Elapsed.TotalMilliseconds / EstimatedImageDuration.TotalMilliseconds, 0, 1);
            var percent = ImageStartPercent + (int)(fraction * (ImageEndPercent - ImageStartPercent));

            generation.ProgressPercent = percent;
            await _db.SaveChangesAsync(ct);

            await _progress.PublishAsync(generation.ClientId, new ReferenceSketchProgressEvent(
                generation.Id, "generating", percent, new[] { percent, percent }, null, null, null, null), ct);
        }
    }
}
