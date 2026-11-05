using ArtCommission.Application.Dispute.TradeDispute.Commands;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Application.Dispute.TradeDispute.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý khiếu nại tranh chấp trong trao đổi tranh (Trade Disputes).
/// Hỗ trợ cả hai định tuyến: /api/v1/trade-disputes và /api/v1/admin/disputes/trades
/// </summary>
[Authorize]
[Route("api/v1/trade-disputes")]
[Route("api/v1/admin/disputes/trades")]
public class TradeDisputesController : ApiControllerBase
{
    /// <summary>
    /// Gửi khiếu nại tranh chấp cho một lượt trao đổi tranh (User khởi kiện: tranh AI, sao chép, không đúng thỏa thuận).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateDispute(
        [FromBody] CreateTradeDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new CreateTradeDisputeCommand(
            ArtTradeId: request.ArtTradeId,
            RaisedById: CurrentUserId,
            Reason: request.Reason,
            EvidenceUrls: request.EvidenceUrls
        );

        var (success, message, disputeId, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message, disputeId });
    }

    /// <summary>
    /// Xem chi tiết hồ sơ khiếu nại tranh chấp trao đổi tranh theo Id.
    /// Dành cho các bên tham gia giao dịch hoặc Quản trị viên/Điều hành viên.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TradeDisputeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDisputeById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetTradeDisputeByIdQuery(DisputeId: id, CurrentUserId: CurrentUserId);
        var (success, data, errors) = await Mediator.Send(query, cancellationToken);

        if (!success)
        {
            if (errors.Any(e => e.Contains("Không tìm thấy")))
            {
                return NotFoundEnvelope(errors);
            }
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy danh sách các khiếu nại tranh chấp trao đổi tranh (Hỗ trợ lọc theo trạng thái, giao dịch, và phân trang).
    /// Dành cho Quản trị viên và Điều hành viên.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [ProducesResponseType(typeof(IEnumerable<TradeDisputeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDisputes(
        [FromQuery] string? status = null,
        [FromQuery] Guid? artTradeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetTradeDisputesQuery(
            Status: status,
            ArtTradeId: artTradeId,
            Page: page,
            PageSize: pageSize
        );

        var (success, items, totalCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)(pageSize <= 0 ? 10 : pageSize))
        });
    }

    /// <summary>
    /// Phán quyết phân xử khiếu nại tranh chấp trao đổi tranh.
    /// Dành cho Quản trị viên và Điều hành viên.
    /// Các phán quyết hỗ trợ: FaultInitiator, FaultReceiver, MutualCancel, Dismissed.
    /// Nếu một bên có lỗi, hệ thống sẽ tự động đóng băng 1 slot trao đổi tranh của bên đó trong 30 ngày (TradeSlotFreeze).
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDispute(
        Guid id,
        [FromBody] ResolveTradeDisputeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ResolveTradeDisputeCommand(
            DisputeId: id,
            AdminId: CurrentUserId,
            Resolution: request.Resolution,
            AdminNote: request.AdminNote
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }
}
