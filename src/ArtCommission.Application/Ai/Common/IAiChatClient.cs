namespace ArtCommission.Application.Ai.Common;

/// <summary>
/// Kết quả một lượt gọi mô hình sinh văn bản.
/// <paramref name="Success"/> = false nghĩa là ĐÃ thử và lỗi (hết quota, mạng, chặn nội dung),
/// khác hẳn "chưa gọi" — nhờ vậy handler lưu được <c>TranslationError</c> để retry có căn cứ.
/// </summary>
public sealed record AiCompletionResult(
    bool Success,
    string? Content,
    int? TokenCount,
    string? ModelVersion,
    string? Error)
{
    public static AiCompletionResult Ok(string content, int? tokenCount, string modelVersion) =>
        new(true, content, tokenCount, modelVersion, null);

    public static AiCompletionResult Fail(string error) =>
        new(false, null, null, null, error);
}

/// <summary>
/// Một lượt hội thoại gửi lên mô hình. Tách khỏi entity để tầng Infrastructure
/// không phải biết tới Domain.
/// </summary>
public sealed record AiChatTurn(string Role, string Content);

/// <summary>
/// Cổng gọi mô hình ngôn ngữ cho module AI Assistant (UC44).
///
/// VÌ SAO khai ở Application mà không gọi thẳng SDK ở Infrastructure:
/// chiều phụ thuộc chỉ đi vào trong — handler (Application) cần gọi AI nhưng
/// không được tham chiếu Infrastructure. Interface ở đây, cài đặt ở Infrastructure.
/// </summary>
public interface IAiChatClient
{
    /// <summary>
    /// Sinh câu trả lời cho một hội thoại.
    /// </summary>
    /// <param name="systemInstruction">
    /// Chỉ dẫn hệ thống — mô tả vai trò và ngữ cảnh đơn hàng/tranh. Null nghĩa là không có.
    /// </param>
    /// <param name="history">Lịch sử hội thoại theo thứ tự thời gian, KHÔNG gồm lượt hỏi mới.</param>
    /// <param name="userMessage">Câu hỏi mới của người dùng.</param>
    /// <param name="maxOutputTokens">
    /// Đè trần token cho RIÊNG lượt gọi này (null = dùng mặc định cấu hình chung
    /// <c>GeminiOptions.MaxOutputTokens</c>). Dùng khi 1 tính năng cần JSON có cấu trúc
    /// dài hơn mức mặc định mà không muốn đổi mặc định chung ảnh hưởng mọi tính năng AI khác.
    /// </param>
    /// <param name="jsonResponse">
    /// true = yêu cầu provider trả JSON thuần (Gemini <c>responseMimeType = application/json</c>),
    /// giảm lỗi JSON hỏng/bọc markdown cho các tính năng cần đầu ra có cấu trúc.
    /// </param>
    Task<AiCompletionResult> CompleteAsync(
        string? systemInstruction,
        IReadOnlyList<AiChatTurn> history,
        string userMessage,
        CancellationToken cancellationToken = default,
        int? maxOutputTokens = null,
        bool jsonResponse = false);

    /// <summary>
    /// Sinh câu trả lời có hỗ trợ gọi hàm tra cứu (<see cref="IAiTool"/>).
    /// Khi <paramref name="tools"/> rỗng, hành vi tương đương <see cref="CompleteAsync"/>.
    /// </summary>
    Task<AiCompletionResult> CompleteWithToolsAsync(
        string? systemInstruction,
        IReadOnlyList<AiChatTurn> history,
        string userMessage,
        IReadOnlyList<IAiTool> tools,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cổng dịch văn bản cho Workroom Chat (UC43 — AI Real-time Translation).
/// Tách khỏi <see cref="IAiChatClient"/> vì prompt và hợp đồng đầu ra khác hẳn:
/// dịch yêu cầu trả về ĐÚNG bản dịch, không thêm lời dẫn.
/// </summary>
public interface ITranslationClient
{
    Task<AiCompletionResult> TranslateAsync(
        string text,
        string targetLang,
        CancellationToken cancellationToken = default);
}
