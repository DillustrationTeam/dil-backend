using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Notifications.Commands.MarkNotificationRead;

/// <summary>
/// UC45 — PUT /api/v1/notifications/{notificationId}/read
/// Người dùng bấm vào một thông báo → đánh dấu đã đọc.
/// Idempotent: bấm lại lần 2 không đổi <c>ReadAt</c> (giữ nguyên thời điểm đọc đầu tiên).
/// </summary>
public record MarkNotificationReadCommand(Guid UserId, Guid NotificationId)
    : IRequest<(bool Success, bool NotFound, NotificationReadDto? Data, string[] Errors)>;

public class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, (bool, bool, NotificationReadDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public MarkNotificationReadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, bool, NotificationReadDto?, string[])> Handle(
        MarkNotificationReadCommand request,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(
            n => n.Id == request.NotificationId && n.UserId == request.UserId && !n.IsDeleted,
            cancellationToken);

        if (notification is null)
        {
            return (false, true, null, ["Không tìm thấy thông báo."]);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            notification.UpdatedAt = notification.ReadAt;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, false,
            new NotificationReadDto(notification.Id, true, notification.ReadAt), []);
    }
}
