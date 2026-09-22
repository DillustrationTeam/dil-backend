namespace ArtCommission.Infrastructure.ExternalServices.Gemini;

/// <summary>
/// Cấu hình Google AI Studio (Gemini). Bind từ section "Gemini" trong configuration.
///
/// Key KHÔNG để trong appsettings.json — đọc từ biến môi trường <c>Gemini__ApiKey</c>,
/// hoặc từ User Secrets khi dev. Tệp .env ở thư mục cha của Code là nguồn tham chiếu
/// của nhóm, không phải nguồn cấu hình runtime của ASP.NET Core.
/// </summary>
public class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>Khoá API Google AI Studio (dạng AIza...).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model dùng cho chatbot và dịch. Mặc định theo phân công: 3.5 Flash Lite.</summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>
    /// Model dự phòng, dùng khi model chính không phục vụ được
    /// (hết quota, model bị gỡ/tên sai, lỗi 5xx kéo dài, hoặc trả nội dung rỗng).
    ///
    /// VÌ SAO cần: model chính là điểm hỏng duy nhất của CẢ tính năng chat lẫn dịch
    /// tin nhắn. Google gỡ/đổi tên model khá thường xuyên, và khi đó mọi endpoint AI
    /// trả lỗi dù code không có gì sai. Một model dự phòng biến sự cố "AI chết hẳn"
    /// thành "AI chậm hơn một nhịp".
    ///
    /// Để trống ("") là tắt dự phòng.
    /// </summary>
    public string FallbackModel { get; set; } = "gemini-3.1-flash-lite";

    /// <summary>
    /// Danh sách model sẽ thử theo thứ tự: model chính trước, rồi model dự phòng.
    /// Bỏ trùng và bỏ giá trị rỗng để không gọi cùng một model hai lần.
    /// </summary>
    public IReadOnlyList<string> CandidateModels
    {
        get
        {
            var models = new List<string>();

            if (!string.IsNullOrWhiteSpace(Model))
            {
                models.Add(Model.Trim());
            }

            if (!string.IsNullOrWhiteSpace(FallbackModel)
                && !models.Contains(FallbackModel.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                models.Add(FallbackModel.Trim());
            }

            return models;
        }
    }

    /// <summary>Base URL của Generative Language API.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";

    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>Trần token cho một lượt trả lời — chặn chi phí phát sinh ngoài kiểm soát.</summary>
    public int MaxOutputTokens { get; set; } = 1024;

    /// <summary>Nhiệt độ. Thấp để câu trả lời ổn định, ít bịa.</summary>
    public double Temperature { get; set; } = 0.4;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
