namespace ArtCommission.Application.Notifications.Common;

/// <summary>Một thông báo trả ra API (UC45).</summary>
public record NotificationDto(
    Guid NotificationId,
    string NotificationType,
    string NotificationTitle,
    string Body,
    string? RefType,
    Guid? RefId,
    string Channel,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt
);

/// <summary>Kết quả đánh dấu đã đọc 1 thông báo.</summary>
public record NotificationReadDto(Guid NotificationId, bool IsRead, DateTimeOffset? ReadAt);

public static class NotificationMapper
{
    public static NotificationDto ToDto(Domain.Entities.Notifications.Notification n) => new(
        n.Id,
        n.NotificationType.ToString(),
        n.NotificationTitle,
        n.Body,
        n.RefType,
        n.RefId,
        n.Channel.ToString(),
        n.IsRead,
        n.ReadAt,
        n.CreatedAt
    );
}
