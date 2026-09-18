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
    PayoutStatusChanged
}

public static class NotificationTypeNames
{
    public const string OutbidAlert = nameof(NotificationType.OutbidAlert);
    public const string AuctionEndingSoon = nameof(NotificationType.AuctionEndingSoon);
    public const string AuctionWon = nameof(NotificationType.AuctionWon);
    public const string DeadlineRiskWarning = nameof(NotificationType.DeadlineRiskWarning);
    public const string PaymentSucceeded = nameof(NotificationType.PaymentSucceeded);
    public const string PayoutStatusChanged = nameof(NotificationType.PayoutStatusChanged);

    public static readonly string[] All =
        [OutbidAlert, AuctionEndingSoon, AuctionWon, DeadlineRiskWarning, PaymentSucceeded, PayoutStatusChanged];
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
