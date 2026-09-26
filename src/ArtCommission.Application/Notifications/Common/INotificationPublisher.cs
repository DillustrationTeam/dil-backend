using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Notifications.Common;

/// <summary>
/// Cổng phát thông báo dùng chung cho MỌI module (UC45).
///
/// Module khác (Auction, Commission, Payment, Payout...) chỉ cần gọi
/// <see cref="PublishAsync"/> — không phải biết thông báo lưu ở đâu, đẩy qua kênh nào.
/// Việc ghi DB + đẩy SignalR nằm sau interface này.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Phát 1 thông báo tới đúng một người dùng.
    /// </summary>
    /// <param name="dedupKey">
    /// Khoá chống gửi trùng, nên có dạng <c>"{Loại}:{id đối tượng}:{id người nhận}"</c>.
    /// Truyền null nếu chấp nhận nhiều thông báo giống nhau.
    /// </param>
    /// <returns>Thông báo đã ghi (kể cả trường hợp bị chặn trùng → trả bản ghi cũ).</returns>
    Task<NotificationDto?> PublishAsync(
        Guid userId,
        NotificationType notificationType,
        string title,
        string body,
        string? refType = null,
        Guid? refId = null,
        NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null,
        CancellationToken cancellationToken = default);
}
