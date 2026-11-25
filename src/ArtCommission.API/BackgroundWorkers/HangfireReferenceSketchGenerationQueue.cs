using ArtCommission.Application.Ai.ReferenceSketch;
using Hangfire;

namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Cài đặt hàng đợi sinh ảnh phác thảo bố cục reference bằng Hangfire (PLAN.md T04).
/// </summary>
public class HangfireReferenceSketchGenerationQueue : IReferenceSketchGenerationQueue
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<HangfireReferenceSketchGenerationQueue> _logger;

    public HangfireReferenceSketchGenerationQueue(
        IBackgroundJobClient backgroundJobClient,
        ILogger<HangfireReferenceSketchGenerationQueue> logger)
    {
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public void Enqueue(Guid generationId)
    {
        _logger.LogInformation("Đưa lượt sinh ảnh phác thảo bố cục {GenerationId} vào hàng đợi Hangfire.", generationId);
        _backgroundJobClient.Enqueue<IReferenceSketchGenerationJob>(
            job => job.RunAsync(generationId, CancellationToken.None));
    }
}
