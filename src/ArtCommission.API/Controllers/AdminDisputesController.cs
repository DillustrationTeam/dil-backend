using ArtCommission.Application.Commission.Disputes.Commands;
using ArtCommission.Application.Commission.Disputes.DTOs;
using ArtCommission.Application.Commission.Disputes.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý phân xử khiếu nại tranh chấp đơn đặt vẽ &amp; Phân bổ tiền cọc Escrow nguyên tử (SCR-22 / UC30).
/// Dành cho Moderator và Administrator.
/// </summary>
[Authorize(Roles = $"{UserRoleNames.Moderator},{UserRoleNames.Administrator}")]
[Route("api/v1/admin/disputes")]
public class AdminDisputesController : ApiControllerBase
{
    /// <summary>
    /// Lấy danh sách hàng đợi các vụ khiếu nại tranh chấp cần xử lý, kèm tổng tiền escrow bị lock (SCR-22 / UC30).
    /// Hỗ trợ lọc theo trạng thái (Pending, UnderReview, Resolved, All), tìm kiếm và phân trang.
    /// </summary>
    [HttpGet]
    [HttpGet("queue")]
    [ProducesResponseType(typeof(IEnumerable<DisputeQueueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDisputeQueue(
        [FromQuery] string? status = "Pending",
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDisputeQueueQuery(status, search, page, pageSize);
        var (items, totalLockedEscrow, totalCount, pendingCount, underReviewCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            pendingCount,
            underReviewCount,
            totalLockedEscrow
        });
    }

    /// <summary>
    /// Xem chi tiết hồ sơ trọng tài tranh chấp, claim, commission, escrow amount và chat snapshot (SCR-22 / UC30).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DisputeArbitrationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDisputeArbitrationDetail(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetDisputeArbitrationDetailQuery(id);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy vụ việc tranh chấp yêu cầu.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Thực thi phán quyết trọng tài và phân bổ tiền ký quỹ Escrow nguyên tử (SCR-22 / UC30).
    /// </summary>
    [HttpPost("{id:guid}/arbitrate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDisputeArbitration(
        Guid id,
        [FromBody] ResolveDisputeArbitrationRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ResolveDisputeArbitrationCommand(
            DisputeId: id,
            ClientRefundPercent: request.ClientRefundPercent,
            AdminNote: request.AdminNote,
            ModeratorId: CurrentUserId
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }
}
