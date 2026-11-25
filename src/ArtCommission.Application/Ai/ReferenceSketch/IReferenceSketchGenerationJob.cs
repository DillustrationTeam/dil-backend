namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>
/// Hợp đồng background job xử lý 1 lượt sinh ảnh phác thảo bố cục reference.
/// Cho phép tầng enqueue (API, có Hangfire) gọi vào mà không phụ thuộc trực tiếp
/// vào cài đặt thật nằm ở Infrastructure.
/// </summary>
public interface IReferenceSketchGenerationJob
{
    /// <summary>
    /// Thực thi toàn bộ luồng: kiểm tra quy định → viết 2 phương án → sinh 2 ảnh song song →
    /// đóng dấu watermark → lưu tạm → cập nhật trạng thái + đẩy tiến trình qua SignalR.
    /// Tự idempotent: bỏ qua nếu <paramref name="generationId"/> không còn ở trạng thái Pending.
    /// </summary>
    Task RunAsync(Guid generationId, CancellationToken cancellationToken = default);
}
