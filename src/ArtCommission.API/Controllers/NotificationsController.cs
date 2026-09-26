using ArtCommission.Application.Notifications.Commands.MarkAllNotificationsRead;
using ArtCommission.Application.Notifications.Commands.MarkNotificationRead;
using ArtCommission.Application.Notifications.Queries.GetMyNotifications;
using ArtCommission.Application.Notifications.Queries.GetUnreadCount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC45 — Notification Center.
///
/// Người dùng CHỈ thấy thông báo của chính mình: mọi truy vấn đều lọc theo
/// <c>CurrentUserId</c> lấy từ JWT, không nhận <c>userId</c> từ client.
/// Real-time đẩy qua <c>/hubs/notifications</c> (xem <c>NotificationHub</c>).
/// </summary>
[Authorize]
[Route("api/v1/notifications")]
public class NotificationsController : ApiControllerBase
{
    /// <summary>
    /// Danh sách thông báo của tôi (UC45).
    /// </summary>
    /// <remarks>
    /// Lọc theo <c>isRead</c>, <c>notificationType</c> và <c>category</c>
    /// (nhóm tab SCR-45: Finance / Order / System / Promotion); phân trang bằng <c>cursor</c>.
    /// <c>meta.unreadCount</c> luôn là tổng số chưa đọc (không tính theo bộ lọc) để badge topbar luôn đúng.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isRead,
        [FromQuery] string? notificationType,
        [FromQuery] string? category,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, unreadCount, errors) = await Mediator.Send(
            new GetMyNotificationsQuery(
                UserId: CurrentUserId,
                IsRead: isRead,
                NotificationType: notificationType,
                Category: category,
                Cursor: cursor,
                Limit: limit),
            cancellationToken);

        return success
            ? OkEnvelope(data, new { unreadCount })
            : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Số thông báo chưa đọc — hiển thị badge trên icon chuông (UC45).
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, unreadCount) = await Mediator.Send(
            new GetUnreadNotificationCountQuery(CurrentUserId), cancellationToken);

        return success
            ? OkEnvelope(new { unreadCount })
            : BadRequestEnvelope("Không lấy được số thông báo chưa đọc.");
    }

    /// <summary>
    /// Đánh dấu một thông báo đã đọc (UC45).
    /// </summary>
    /// <remarks>Idempotent: gọi lại lần 2 giữ nguyên <c>readAt</c> của lần đọc đầu tiên.</remarks>
    [HttpPut("{notificationId:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, notFound, data, errors) = await Mediator.Send(
            new MarkNotificationReadCommand(CurrentUserId, notificationId), cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return notFound ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Đánh dấu TẤT CẢ thông báo của tôi đã đọc (UC45).
    /// </summary>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, updatedCount) = await Mediator.Send(
            new MarkAllNotificationsReadCommand(CurrentUserId), cancellationToken);

        return success
            ? OkEnvelope(new { updatedCount })
            : BadRequestEnvelope("Không cập nhật được thông báo.");
    }
}
