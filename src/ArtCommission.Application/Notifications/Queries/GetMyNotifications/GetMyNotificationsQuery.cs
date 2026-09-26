using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Queries.GetPaymentOrders;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Notifications.Queries.GetMyNotifications;

/// <summary>
/// UC45 — GET /api/v1/notifications
/// Người dùng mở Notification Center: danh sách thông báo của mình, mới nhất trước.
/// Input: query isRead, notificationType, category, cursor, limit.
/// </summary>
public record GetMyNotificationsQuery(
    Guid UserId,
    bool? IsRead = null,
    string? NotificationType = null,
    /// <summary>Nhóm tab của Notification Center (SCR-45): Finance / Order / System / Promotion.</summary>
    string? Category = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, CursorPage<NotificationDto>? Data, int UnreadCount, string[] Errors)>;

public class GetMyNotificationsQueryHandler
    : IRequestHandler<GetMyNotificationsQuery, (bool, CursorPage<NotificationDto>?, int, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetMyNotificationsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, CursorPage<NotificationDto>?, int, string[])> Handle(
        GetMyNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        // Badge trên topbar luôn cần số chưa đọc → đếm trên toàn bộ thông báo của user,
        // KHÔNG tính theo bộ lọc đang xem.
        var unreadCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == request.UserId && !n.IsRead && !n.IsDeleted, cancellationToken);

        var query = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == request.UserId && !n.IsDeleted);

        if (request.IsRead.HasValue)
        {
            query = query.Where(n => n.IsRead == request.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.NotificationType))
        {
            if (!Enum.TryParse<NotificationType>(request.NotificationType, ignoreCase: true, out var type))
            {
                return (false, null, unreadCount,
                    [$"Loại thông báo không hợp lệ. Hợp lệ: {string.Join(", ", NotificationTypeNames.All)}."]);
            }

            query = query.Where(n => n.NotificationType == type);
        }

        // Tab nhóm của Notification Center (SCR-45): lọc theo tập loại thuộc nhóm.
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            if (!Enum.TryParse<NotificationCategory>(request.Category, ignoreCase: true, out var category))
            {
                return (false, null, unreadCount,
                    [$"Nhóm thông báo không hợp lệ. Hợp lệ: {string.Join(", ", NotificationCategoryNames.All)}."]);
            }

            var typesInCategory = NotificationCategoryMap.TypesOf(category);
            query = query.Where(n => typesInCategory.Contains(n.NotificationType));
        }

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!DateTimeOffset.TryParse(
                    request.Cursor,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var cursorTime))
            {
                return (false, null, unreadCount, ["Cursor không hợp lệ."]);
            }

            query = query.Where(n => n.CreatedAt < cursorTime);
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (rows.Count > limit)
        {
            rows.RemoveAt(rows.Count - 1);
            nextCursor = rows[^1].CreatedAt.ToString(
                "O", System.Globalization.CultureInfo.InvariantCulture);
        }

        var items = rows.Select(NotificationMapper.ToDto).ToList();

        return (true, new CursorPage<NotificationDto>(items, nextCursor), unreadCount, []);
    }
}
