using ArtCommission.Application.ArtistStudio.Moderation;
using Hangfire;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Cài đặt hàng đợi quét AI sử dụng Hangfire (SCR-20 / UC28).
/// </summary>
public class HangfireArtworkScanQueue : IArtworkScanQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<HangfireArtworkScanQueue> _logger;

    public HangfireArtworkScanQueue(
        IBackgroundJobClient backgroundJobClient,
        ILogger<HangfireArtworkScanQueue> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public void Enqueue(Guid artworkId)
    {
        _logger.LogInformation("Đưa tác phẩm {ArtworkId} vào hàng đợi quét AI của Hangfire.", artworkId);
        _backgroundJobClient.Enqueue<IArtworkScanJob>(job => job.ProcessArtworkScanAsync(artworkId, CancellationToken.None));
    }
}

