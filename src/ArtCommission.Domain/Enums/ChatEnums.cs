namespace ArtCommission.Domain.Enums;

/// <summary>
/// Loại phòng chat Workroom (UC43).
/// Phòng gắn với đơn đặt vẽ hoặc gắn với phiên đấu giá.
/// </summary>
public enum ChatRoomType
{
    /// <summary>Phòng làm việc giữa Client và Creator cho một commission.</summary>
    Commission,

    /// <summary>Phòng trao đổi giữa người bán và người mua tiềm năng của phiên đấu giá.</summary>
    Auction,

    /// <summary>Phòng hỗ trợ giữa người dùng và Moderator/Admin.</summary>
    Support
}

/// <summary>
/// Loại nội dung tin nhắn Workroom — lưu dạng CHUỖI trong cột <c>Messages.MessageType</c>.
///
/// VÌ SAO không dùng enum cho trường này: module Commission/Dispute đang đọc thẳng
/// giá trị và đổ vào DTO kiểu string. Giữ string để không phá module khác, và vì
/// thêm loại tin nhắn mới không cần migration.
/// </summary>
public static class MessageTypes
{
    public const string Text = "Text";
    public const string Image = "Image";
    public const string File = "File";
    public const string SystemEvent = "SystemEvent";
    public const string RevisionNotice = "RevisionNotice";

    public static readonly string[] All = [Text, Image, File, SystemEvent, RevisionNotice];

    /// <summary>Kiểm tra giá trị có nằm trong danh sách hợp lệ, không phân biệt hoa/thường.</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && All.Any(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Chuẩn hoá về đúng chữ hoa/thường của hằng số; giá trị lạ trả về Text.</summary>
    public static string Normalize(string? value) =>
        All.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase)) ?? Text;
}

/// <summary>
/// Trạng thái dịch tự động một tin nhắn (AI Real-time Translation).
/// Lưu lại để KHÔNG gọi AI lại cho cùng một tin nhắn, và để retry được khi lỗi.
/// </summary>
public enum TranslationStatus
{
    /// <summary>Chưa từng yêu cầu dịch.</summary>
    NotRequested,

    /// <summary>Đang chờ / đang gọi AI.</summary>
    Pending,

    /// <summary>Đã có translated_body dùng được.</summary>
    Completed,

    /// <summary>Gọi AI lỗi — được phép thử lại.</summary>
    Failed
}
