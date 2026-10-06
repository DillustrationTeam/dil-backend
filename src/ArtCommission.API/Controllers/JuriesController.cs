using ArtCommission.Application.Event.Commands;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Application.Event.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý danh sách ban giám khảo cuộc thi/sự kiện (Juries).
/// </summary>
[Route("api/v1/juries")]
public class JuriesController : ApiControllerBase
{
    private const string AdminOrModRoles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}";

    /// <summary>
    /// Lấy danh sách ban giám khảo trên toàn hệ thống (Hỗ trợ lọc theo EventId, Role, IsHeadJury, tìm kiếm theo Name/Search và phân trang).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJuries(
        [FromQuery] Guid? eventId = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isHeadJury = null,
        [FromQuery] string? name = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetJuriesQuery(
            EventId: eventId,
            Role: role,
            IsHeadJury: isHeadJury,
            Name: name,
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
    /// Lấy thông tin chi tiết một giám khảo theo Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJuryById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetJuryByIdQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy thành viên ban giám khảo.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Lấy danh sách ban giám khảo của một sự kiện cụ thể (Hỗ trợ lọc theo Role, IsHeadJury, tìm kiếm theo Name/Search).
    /// </summary>
    [HttpGet("by-event/{eventId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJuryByEventId(
        Guid eventId,
        [FromQuery] string? role = null,
        [FromQuery] bool? isHeadJury = null,
        [FromQuery] string? name = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetJuryByEventIdQuery(
            EventId: eventId,
            Role: role,
            IsHeadJury: isHeadJury,
            Name: name,
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
    /// Thêm giám khảo vào sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// Điều kiện bắt buộc: Giám khảo được thêm/mời phải có vai trò CREATOR.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddJury(
        [FromBody] AddJuryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new AddJuryCommand(
            EventId: request.EventId,
            CreatorId: request.CreatorId,
            AdminId: CurrentUserId,
            IsHeadJury: request.IsHeadJury
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Xóa giám khảo khỏi sự kiện (Dành cho Quản trị viên &amp; Kiểm duyệt viên).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AdminOrModRoles)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveJury(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = RemoveJuryCommand.ByJuryId(id, CurrentUserId);

        var (success, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message = "Đã xóa giám khảo khỏi sự kiện thành công." });
    }
}


public sealed record AddJuryRequest(
    Guid EventId,
    Guid CreatorId,
    bool IsHeadJury = false
);
