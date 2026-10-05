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

    /// <summary>Kết quả duyệt đơn đăng ký Creator thay đổi — Approved/Rejected/AdditionalProofRequested (UC29).</summary>
    CreatorApplicationStatusChanged,

    /// <summary>
    /// Khuyến mãi / ưu đãi của sàn (SCR-45 — tab "Khuyến mãi").
    /// Chưa có worker tự phát; Admin phát khi mở chiến dịch ưu đãi.
    /// </summary>
    PromotionAnnouncement,
    
    /// <summary>Creator nhận được lời mời tham gia Ban giám khảo sự kiện.</summary>
    InvitationReceived,

    /// <summary>Creator đã chấp nhận lời mời tham gia Ban giám khảo sự kiện.</summary>
    InvitationAccepted,

    /// <summary>Creator đã từ chối lời mời tham gia Ban giám khảo sự kiện.</summary>
    InvitationDeclined,

    /// <summary>Lời mời tham gia Ban giám khảo sự kiện đã bị hủy.</summary>
    InvitationCanceled,

    /// <summary>Lời mời tham gia Ban giám khảo sự kiện đã hết hạn.</summary>
    InvitationExpired,

    /// <summary>Phiên đấu giá bắt đầu, kết thúc hoặc được gia hạn.</summary>
    AuctionLifecycle,

    /// <summary>Yêu cầu đặt vẽ hoặc trạng thái đơn đặt vẽ thay đổi.</summary>
    CommissionStatusChanged,

    /// <summary>Tiền ký quỹ của đơn đặt vẽ thay đổi trạng thái.</summary>
    EscrowStatusChanged,

    /// <summary>Tranh chấp của đơn đặt vẽ được mở hoặc phân xử.</summary>
    DisputeStatusChanged
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
    public const string CreatorApplicationStatusChanged = nameof(NotificationType.CreatorApplicationStatusChanged);
    public const string PromotionAnnouncement = nameof(NotificationType.PromotionAnnouncement);
    public const string InvitationReceived = nameof(NotificationType.InvitationReceived);
    public const string InvitationAccepted = nameof(NotificationType.InvitationAccepted);
    public const string InvitationDeclined = nameof(NotificationType.InvitationDeclined);
    public const string InvitationCanceled = nameof(NotificationType.InvitationCanceled);
    public const string InvitationExpired = nameof(NotificationType.InvitationExpired);
    public const string CommissionStatusChanged = nameof(NotificationType.CommissionStatusChanged);
    public const string EscrowStatusChanged = nameof(NotificationType.EscrowStatusChanged);
    public const string DisputeStatusChanged = nameof(NotificationType.DisputeStatusChanged);

    public static readonly string[] All =
    [
        OutbidAlert,
        AuctionEndingSoon,
        AuctionWon,
        DeadlineRiskWarning,
        PaymentSucceeded,
        PayoutStatusChanged,
        UserSanctionAlert,
        CreatorApplicationStatusChanged,
        PromotionAnnouncement,
        InvitationReceived,
        InvitationAccepted,
        InvitationDeclined,
        InvitationCanceled,
        InvitationExpired,
        CommissionStatusChanged,
        EscrowStatusChanged,
        DisputeStatusChanged
    ];
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

    /// <summary>Hệ thống: chế tài, thay đổi quy định, sự kiện & lời mời.</summary>
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
            [NotificationType.PaymentSucceeded, NotificationType.PayoutStatusChanged, NotificationType.EscrowStatusChanged],

        NotificationCategory.Order =>
        [
            NotificationType.OutbidAlert,
            NotificationType.AuctionEndingSoon,
            NotificationType.AuctionWon,
            NotificationType.DeadlineRiskWarning,
            NotificationType.CommissionStatusChanged,
            NotificationType.DisputeStatusChanged
        ],

        NotificationCategory.System =>
        [
            NotificationType.UserSanctionAlert,
            NotificationType.CreatorApplicationStatusChanged,
            NotificationType.InvitationReceived,
            NotificationType.InvitationAccepted,
            NotificationType.InvitationDeclined,
            NotificationType.InvitationCanceled,
            NotificationType.InvitationExpired
        ],

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
