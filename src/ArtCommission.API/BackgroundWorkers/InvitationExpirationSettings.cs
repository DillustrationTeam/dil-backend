namespace ArtCommission.API.BackgroundWorkers;

/// <summary>
/// Cấu hình job quét lời mời giám khảo đã hết hạn. Bind từ section "InvitationExpiration".
/// </summary>
public class InvitationExpirationSettings
{
    public const string SectionName = "InvitationExpiration";

    /// <summary>Bật/tắt background worker. Mặc định bật.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Chu kỳ chạy job (phút). Mặc định 10 phút.</summary>
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>Thời hạn tối đa của lời mời tính từ lúc gửi (ngày). Mặc định 7 ngày.</summary>
    public int LifetimeDays { get; set; } = 7;

    /// <summary>Tự động hết hạn lời mời nếu sự kiện đã kết thúc hoặc hủy. Mặc định true.</summary>
    public bool ExpireWhenEventEnded { get; set; } = true;

    /// <summary>Số lượng lời mời tối đa quét và cập nhật mỗi lượt.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Thời gian chờ ban đầu sau khi app khởi động (giây). Mặc định 30 giây.</summary>
    public int InitialDelaySeconds { get; set; } = 30;
}
