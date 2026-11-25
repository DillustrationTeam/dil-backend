namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>Label + URL presigned của 1 ảnh phác thảo đã sinh (tạm hoặc đã chọn).</summary>
public sealed record ReferenceSketchImageDto(Guid Id, int VariantIndex, string Url, string LabelVi, string LabelEn);

/// <summary>
/// Một mốc tiến trình của 1 lượt sinh ảnh phác thảo bố cục, đẩy qua SignalR tới đúng khách
/// đang chờ (event <c>AiReferenceSketchProgress</c>) — xem quyết định thiết kế trong PLAN.md.
/// </summary>
public sealed record ReferenceSketchProgressEvent(
    Guid GenerationId,
    string Stage,
    int Percent,
    int[]? PerImage,
    ReferenceSketchImageDto[]? Images,
    string? Error,
    string? BlockReasonVi,
    string? BlockReasonEn);

/// <summary>
/// Cổng đẩy tiến trình sinh ảnh phác thảo real-time tới khách qua SignalR.
///
/// VÌ SAO khai ở Application mà không gọi thẳng <c>IHubContext&lt;NotificationHub&gt;</c>:
/// <c>NotificationHub</c> nằm ở tầng API — Infrastructure (nơi job sinh ảnh thực thi) không
/// được tham chiếu API, giống <c>INotificationPublisher</c>/<c>SignalRNotificationPublisher</c>.
/// </summary>
public interface IReferenceSketchProgressPublisher
{
    Task PublishAsync(Guid clientUserId, ReferenceSketchProgressEvent progressEvent, CancellationToken cancellationToken = default);
}
