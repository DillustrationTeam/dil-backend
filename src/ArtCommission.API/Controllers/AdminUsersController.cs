using ArtCommission.Application.Admin.Commands;
using ArtCommission.Application.Admin.DTOs;
using ArtCommission.Application.Admin.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý tài khoản người dùng, phân quyền vai trò &amp; Chế tài xử phạt vi phạm (SCR-23 / UC31).
/// Dành riêng cho Quản trị viên (Administrator).
/// </summary>
[Authorize(Roles = UserRoleNames.Administrator)]
[Route("api/v1/admin/users")]
public class AdminUsersController : ApiControllerBase
{
    /// <summary>
    /// Lấy danh sách tài khoản người dùng trong hệ thống (SCR-23 / UC31).
    /// Hỗ trợ tìm kiếm theo từ khóa (Email, FullName, UserName), lọc theo vai trò (Role), lọc theo trạng thái (Active, LockedOut, All) và phân trang.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserAdminListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = "All",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetUsersAdminQuery(search, role, status, page, pageSize);
        var (items, totalCount, activeCount, lockedCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            activeCount,
            lockedCount
        });
    }

    /// <summary>
    /// Xem chi tiết hồ sơ người dùng, vai trò phân quyền, số dư ví và lịch sử xử phạt (SCR-23 / UC31).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserAdminDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetUserAdminDetailQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy người dùng yêu cầu.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Áp dụng chế tài xử phạt (Cảnh cáo, Đình chỉ tạm thời, Khóa vĩnh viễn, Mở khóa) cho tài khoản (SCR-23 / UC31).
    /// </summary>
    [HttpPost("{id:guid}/sanction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ApplySanction(
        Guid id,
        [FromBody] ApplyUserSanctionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ApplyUserSanctionCommand(
            UserId: id,
            ActionType: request.ActionType,
            Reason: request.Reason,
            DurationDays: request.DurationDays,
            AdminId: CurrentUserId
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }

    /// <summary>
    /// Cập nhật / gán danh sách vai trò (Roles) cho tài khoản người dùng (SCR-23 / UC31).
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateRoles(
        Guid id,
        [FromBody] UpdateUserRolesRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new UpdateUserRolesCommand(
            UserId: id,
            Roles: request.Roles,
            AdminId: CurrentUserId
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }
}
