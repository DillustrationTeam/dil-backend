using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Notifications.Queries.GetUnreadCount;

/// <summary>
/// UC45 — GET /api/v1/notifications/unread-count
/// Badge số thông báo chưa đọc trên icon chuông ở topbar.
/// </summary>
public record GetUnreadNotificationCountQuery(Guid UserId)
    : IRequest<(bool Success, int UnreadCount)>;

public class GetUnreadNotificationCountQueryHandler
    : IRequestHandler<GetUnreadNotificationCountQuery, (bool, int)>
{
    private readonly IApplicationDbContext _db;

    public GetUnreadNotificationCountQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, int)> Handle(
        GetUnreadNotificationCountQuery request,
        CancellationToken cancellationToken)
    {
        var count = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDeleted, cancellationToken);

        return (true, count);
    }
}
