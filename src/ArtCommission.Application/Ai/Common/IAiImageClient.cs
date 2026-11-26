namespace ArtCommission.Application.Ai.Common;

/// <summary>
/// Kết quả một lượt gọi mô hình sinh ảnh.
/// <paramref name="ContentBlocked"/> tách riêng khỏi lỗi mạng/quota: handler dùng cờ này
/// để báo lý do cụ thể cho người dùng mà KHÔNG trừ lượt (ảnh bị chặn nội dung không phải
/// lỗi hệ thống).
/// </summary>
public sealed record AiImageResult(
    bool Success,
    byte[]? Png,
    string? Model,
    string? Error,
    bool ContentBlocked)
{
    public static AiImageResult Ok(byte[] png, string model) =>
        new(true, png, model, null, false);

    public static AiImageResult Fail(string error, bool contentBlocked = false) =>
        new(false, null, null, error, contentBlocked);
}

/// <summary>
/// Cổng gọi mô hình sinh ảnh cho tính năng AI phác thảo bố cục (reference sketch).
///
/// VÌ SAO khai ở Application mà không gọi thẳng SDK ở Infrastructure: giống
/// <see cref="IAiChatClient"/>, chiều phụ thuộc chỉ đi vào trong — handler (Application)
/// cần gọi AI nhưng không được tham chiếu Infrastructure.
/// </summary>
public interface IAiImageClient
{
    /// <summary>
    /// Sinh một ảnh PNG từ prompt tiếng Anh.
    /// </summary>
    /// <param name="prompt">Prompt tiếng Anh đầy đủ (đã ghép phong cách cố định + ràng buộc cấm).</param>
    /// <param name="size">Kích thước ảnh, ví dụ "1024x1536".</param>
    Task<AiImageResult> GenerateAsync(string prompt, string size, CancellationToken cancellationToken = default);
}
