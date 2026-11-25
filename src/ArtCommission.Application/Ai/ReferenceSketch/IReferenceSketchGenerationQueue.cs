namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>
/// Cổng đưa job sinh ảnh phác thảo bố cục vào hàng đợi nền.
///
/// VÌ SAO khai ở Application: command khởi tạo (Application) cần enqueue job nhưng
/// Hangfire (<c>IBackgroundJobClient</c>) chỉ được tham chiếu ở API — giống
/// <c>IArtworkScanQueue</c>/<c>HangfireArtworkScanQueue</c>.
/// </summary>
public interface IReferenceSketchGenerationQueue
{
    void Enqueue(Guid generationId);
}
