using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Notifications.Commands.MarkAllNotificationsRead;

/// <summary>
/// UC45 — PUT /api/v1/notifications/read-all
/// Đánh dấu TẤT CẢ thông báo của người dùng là đã đọc.
/// Dùng 1 câu UPDATE (không nạp từng dòng) để không chậm khi có hàng nghìn thông báo.
/// </summary>
public record MarkAllNotificationsReadCommand(Guid UserId)
    : IRequest<(bool Success, int UpdatedCount)>;

public class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, (bool, int)>
{
    private readonly IApplicationDbContext _db;

    public MarkAllNotificationsReadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, int)> Handle(
        MarkAllNotificationsReadCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var updated = await _db.Notifications
            .Where(n => n.UserId == request.UserId && !n.IsRead && !n.IsDeleted)
            .ExecuteUpdateAsync(
                s => s.SetProperty(n => n.IsRead, true)
                      .SetProperty(n => n.ReadAt, now)
                      .SetProperty(n => n.UpdatedAt, now),
                cancellationToken);

        return (true, updated);
    }
}
