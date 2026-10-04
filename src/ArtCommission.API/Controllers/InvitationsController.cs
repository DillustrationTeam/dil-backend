using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý lời mời ban giám khảo (Invitations) cho các sự kiện.
/// </summary>
[Authorize]
[Route("api/v1/invitations")]
public class InvitationsController : ApiControllerBase
{
    private const string AdminOrModRoles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}";

    /// <summary>
    /// Lấy danh sách toàn bộ lời mời trong hệ thống (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// Hỗ trợ lọc theo EventId, Status, Creator, Admin, Role, từ khóa tìm kiếm và phân trang.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] Guid? eventId = null,
        [FromQuery] InvitationStatus? status = null,
        [FromQuery] Guid? sentToCreatorId = null,
        [FromQuery] Guid? sentFromAdminId = null,
        [FromQuery] bool? isHeadJury = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetInvitationsQuery(
            EventId: eventId,
            Status: status,
            SentToCreatorId: sentToCreatorId,
            SentFromAdminId: sentFromAdminId,
            IsHeadJury: isHeadJury,
            Search: search,
            Page: page,
            PageSize: pageSize
        );

        var (items, totalCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Lấy danh sách lời mời của chính người dùng hiện tại (Creator).
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyInvitations(
        [FromQuery] InvitationStatus? status = null,
        [FromQuery] Guid? eventId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetMyInvitationsQuery(
            UserId: CurrentUserId,
            Status: status,
            EventId: eventId,
            Page: page,
            PageSize: pageSize
        );

        var (items, totalCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Xem chi tiết một lời mời theo Id.
    /// Cho phép Quản trị viên, Kiểm duyệt viên hoặc chính Creator được mời xem.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvitationById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetInvitationByIdQuery(id);
        var invitation = await Mediator.Send(query, cancellationToken);

        if (invitation == null)
        {
            return NotFoundEnvelope("Không tìm thấy lời mời hoặc lời mời đã bị xóa.");
        }

        var isAdminOrMod = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);
        var isRecipient = invitation.SentToCreatorId == CurrentUserId;

        if (!isAdminOrMod && !isRecipient)
        {
            return Forbid();
        }

        return OkEnvelope(invitation);
    }

    /// <summary>
    /// Gửi lời mời ban giám khảo mới (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendInvitation(
        [FromBody] SendInvitationDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new SendInvitationCommand(
            adminId: CurrentUserId,
            dto: dto
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Chấp nhận lời mời tham gia ban giám khảo (Dành cho Creator được mời).
    /// </summary>
    [HttpPut("{id:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AcceptInvitation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new AcceptInvitationCommand(
            InvitationId: id,
            UserId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Từ chối lời mời tham gia ban giám khảo (Dành cho Creator được mời).
    /// </summary>
    [HttpPut("{id:guid}/decline")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeclineInvitation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new DeclineInvitationCommand(
            InvitationId: id,
            UserId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Hủy lời mời tham gia ban giám khảo (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpPut("{id:guid}/cancel")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CancelInvitation(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new CancelInvitationCommand(
            InvitationId: id,
            AdminId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Cập nhật trạng thái lời mời chung (Dùng cho cả Creator và Admin).
    /// Accepted/Declined: Creator được mời.
    /// Canceled: Quản trị viên / Kiểm duyệt viên.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateInvitationStatus(
        Guid id,
        [FromBody] UpdateInvitationStatusDto dto,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var isAdminOrMod = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);

        if (dto.Status == InvitationStatus.Canceled && !isAdminOrMod)
        {
            return Forbid();
        }

        var command = new UpdateInvitationStatusCommand(
            InvitationId: id,
            CurrentUserId: CurrentUserId,
            Status: dto.Status,
            IsAdmin: isAdminOrMod
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}
