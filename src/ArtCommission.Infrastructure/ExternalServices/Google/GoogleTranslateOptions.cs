namespace ArtCommission.Infrastructure.ExternalServices.Google;

/// <summary>
/// Cấu hình dịch bằng Google Translate. Bind từ section "GoogleTranslate" trong configuration.
///
/// HAI CHẾ ĐỘ, chọn tự động theo việc có ApiKey hay không:
///   1. CÓ <see cref="ApiKey"/>  → Cloud Translation API v2 chính thức
///      (<c>translation.googleapis.com/language/translate/v2</c>) — cần bật billing, hợp ToS.
///   2. KHÔNG có key            → endpoint công khai <c>translate.googleapis.com/translate_a/single</c>
///      (client=gtx) — chạy ngay, không cần key, nhưng là endpoint KHÔNG chính thức:
///      có thể bị giới hạn/đổi, không dùng cho production tải lớn.
///
/// Nhờ vậy tính năng dịch chạy được ngay ở môi trường dev mà vẫn nâng cấp được lên API
/// chính thức chỉ bằng cách đặt <c>GoogleTranslate__ApiKey</c>.
/// </summary>
public class GoogleTranslateOptions
{
    public const string SectionName = "GoogleTranslate";

    /// <summary>Khoá Cloud Translation API v2. Rỗng ⇒ dùng endpoint công khai không cần key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base URL khi dùng endpoint công khai (không key).</summary>
    public string FreeBaseUrl { get; set; } = "https://translate.googleapis.com/translate_a/single";

    /// <summary>
    /// Endpoint công khai DỰ PHÒNG (host khác, quota khác).
    ///
    /// ĐÃ GẶP THẬT: <c>translate.googleapis.com/...?client=gtx</c> trả HTTP 429 kèm trang
    /// "your computer or network may be sending automated queries" cho IP của máy dev,
    /// trong khi <c>clients5.google.com/translate_a/t</c> vẫn trả 200. Thử lần lượt hai host
    /// giúp tính năng dịch không chết chỉ vì một host bị chặn.
    /// </summary>
    public string FreeFallbackBaseUrl { get; set; } = "https://clients5.google.com/translate_a/t";

    /// <summary>Base URL của Cloud Translation API v2 (khi có key).</summary>
    public string OfficialBaseUrl { get; set; } = "https://translation.googleapis.com/language/translate/v2";

    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>Trần ký tự cho một lần dịch — endpoint công khai giới hạn độ dài URL.</summary>
    public int MaxCharactersPerRequest { get; set; } = 4000;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
