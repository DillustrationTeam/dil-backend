namespace ArtCommission.Domain.Enums;

/// <summary>
/// Loại thông báo (UC45) — quyết định icon, màu và đích đến khi người dùng bấm vào.
///
/// Chỉ khai báo những loại hệ thống THỰC SỰ phát ra. Muốn thêm loại mới:
/// thêm vào enum + <see cref="NotificationTypeNames.All"/> — không cần sửa bảng.
/// </summary>
public enum NotificationType
{
    /// <summary>Có người trả giá cao hơn mình trong phiên đấu giá (UC32).</summary>
    OutbidAlert,

    /// <summary>Phiên đấu giá sắp kết thúc (UC32).</summary>
    AuctionEndingSoon,

    /// <summary>Mình thắng phiên đấu giá (UC34).</summary>
    AuctionWon,

    /// <summary>Cảnh báo mốc hoàn thành có nguy cơ trễ (UC46).</summary>
    DeadlineRiskWarning,

    /// <summary>Nạp tiền thành công, ví đã cộng (UC48).</summary>
    PaymentSucceeded,

    /// <summary>Yêu cầu rút tiền đổi trạng thái — duyệt hoặc từ chối (UC49).</summary>
    PayoutStatusChanged,

    /// <summary>Thông báo chế tài xử phạt tài khoản (SCR-23 / UC31).</summary>
    UserSanctionAlert,

    /// <summary>
    /// Khuyến mãi / ưu đãi của sàn (SCR-45 — tab "Khuyến mãi").
    /// Chưa có worker tự phát; Admin phát khi mở chiến dịch ưu đãi.
    /// </summary>
    PromotionAnnouncement
}

public static class NotificationTypeNames
{
    public const string OutbidAlert = nameof(NotificationType.OutbidAlert);
    public const string AuctionEndingSoon = nameof(NotificationType.AuctionEndingSoon);
    public const string AuctionWon = nameof(NotificationType.AuctionWon);
    public const string DeadlineRiskWarning = nameof(NotificationType.DeadlineRiskWarning);
    public const string PaymentSucceeded = nameof(NotificationType.PaymentSucceeded);
    public const string PayoutStatusChanged = nameof(NotificationType.PayoutStatusChanged);
    public const string UserSanctionAlert = nameof(NotificationType.UserSanctionAlert);
    public const string PromotionAnnouncement = nameof(NotificationType.PromotionAnnouncement);

    public static readonly string[] All =
        [OutbidAlert, AuctionEndingSoon, AuctionWon, DeadlineRiskWarning, PaymentSucceeded, PayoutStatusChanged, UserSanctionAlert, PromotionAnnouncement];
}

/// <summary>
/// Nhóm thông báo — dùng cho 4 tab của Notification Center (SCR-45):
/// Tài chính / Đơn hàng / Hệ thống / Khuyến mãi.
///
/// VÌ SAO không lọc thẳng theo <see cref="NotificationType"/> ở FE: một tab gồm nhiều loại,
/// nếu FE tự gộp thì phân trang cursor sẽ sai (mỗi tab phải phân trang trên đúng tập của nó).
/// Bộ lọc nhóm được đẩy xuống DB qua <c>?category=</c>.
/// </summary>
public enum NotificationCategory
{
    /// <summary>Tiền nong: nạp tiền, trạng thái rút tiền.</summary>
    Finance,

    /// <summary>Đơn hàng: đấu giá, nguy cơ trễ hạn của đơn đặt vẽ.</summary>
    Order,

    /// <summary>Hệ thống: chế tài, thay đổi quy định.</summary>
    System,

    /// <summary>Khuyến mãi, ưu đãi.</summary>
    Promotion
}

public static class NotificationCategoryNames
{
    public const string Finance = nameof(NotificationCategory.Finance);
    public const string Order = nameof(NotificationCategory.Order);
    public const string System = nameof(NotificationCategory.System);
    public const string Promotion = nameof(NotificationCategory.Promotion);

    public static readonly string[] All = [Finance, Order, System, Promotion];
}

/// <summary>Ánh xạ nhóm → các loại thông báo thuộc nhóm đó. Một loại chỉ thuộc ĐÚNG một nhóm.</summary>
public static class NotificationCategoryMap
{
    public static NotificationType[] TypesOf(NotificationCategory category) => category switch
    {
        NotificationCategory.Finance =>
            [NotificationType.PaymentSucceeded, NotificationType.PayoutStatusChanged],

        NotificationCategory.Order =>
        [
            NotificationType.OutbidAlert,
            NotificationType.AuctionEndingSoon,
            NotificationType.AuctionWon,
            NotificationType.DeadlineRiskWarning
        ],

        NotificationCategory.System => [NotificationType.UserSanctionAlert],

        NotificationCategory.Promotion => [NotificationType.PromotionAnnouncement],

        _ => []
    };
}

/// <summary>
/// Kênh gửi thông báo (UC45).
/// Hiện chỉ <see cref="InApp"/> được phát thật (ghi DB + đẩy SignalR).
/// Email/Push đã có cột trong DB nhưng CHƯA có worker gửi — xem <c>SentAt</c>/<c>FailedReason</c>.
/// </summary>
public enum NotificationChannel
{
    /// <summary>Chuông trong app + SignalR real-time.</summary>
    InApp,

    /// <summary>Email — chưa triển khai.</summary>
    Email,

    /// <summary>Push notification — chưa triển khai.</summary>
    Push
}

public static class NotificationChannelNames
{
    public const string InApp = nameof(NotificationChannel.InApp);
    public const string Email = nameof(NotificationChannel.Email);
    public const string Push = nameof(NotificationChannel.Push);

    public static readonly string[] All = [InApp, Email, Push];
}
