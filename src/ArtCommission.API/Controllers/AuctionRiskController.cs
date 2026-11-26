using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Auction.Risk.Commands;
using ArtCommission.Application.Auction.Risk.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// AI Bid Fraud Detection - moderation surface.
///
/// Administrator/Moderator see the queue, the evidence sheet and the decision endpoints.
/// The seller only sees their own flags through a neutral endpoint (no score, no evidence).
/// Detection itself is never automatic punishment: confirming a flag just records the decision.
/// </summary>
[Route("api/v1/auctions")]
public class AuctionRiskController : ApiControllerBase
{
    /// <summary>Risk queue for the admin risk tab. Cursor keyset, highest score first.</summary>
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [HttpGet("risk-alerts")]
    [ProducesResponseType(typeof(IEnumerable<RiskAlertListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRiskAlerts(
        [FromQuery] string? level = null,
        [FromQuery] string? status = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var (success, data, meta, errors) = await Mediator.Send(
            new GetRiskAlertsQuery(level, status, cursor, limit), cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Full evidence sheet of one auction (drawer view).</summary>
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [HttpGet("{auctionId:guid}/risk")]
    [ProducesResponseType(typeof(AuctionRiskAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRisk(Guid auctionId, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetAuctionRiskQuery(auctionId), cancellationToken);
        return success ? OkEnvelope(data) : NotFoundEnvelope(errors);
    }

    /// <summary>Re-run the full detector for one auction. Administrator only.</summary>
    [Authorize(Roles = UserRoleNames.Administrator)]
    [HttpPost("{auctionId:guid}/risk/rescan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Rescan(Guid auctionId, CancellationToken cancellationToken)
    {
        var (success, score, level, signalCount, errors) = await Mediator.Send(
            new RescanAuctionRiskCommand(auctionId), cancellationToken);

        return success
            ? OkEnvelope(new { score, riskLevel = level, signalCount })
            : BadRequestEnvelope(errors);
    }

    /// <summary>Resolve a sheet: dismiss it or confirm fraud with a mandatory note.</summary>
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [HttpPost("{auctionId:guid}/risk/review")]
    [ProducesResponseType(typeof(AuctionRiskAssessmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        Guid auctionId,
        [FromBody] ReviewAuctionRiskRequest request,
        CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(
            new ReviewAuctionRiskCommand(auctionId, CurrentUserId, request.Status, request.Note),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Neutral list of my auctions currently under extra review (seller view).</summary>
    [Authorize]
    [HttpGet("me/risk-flags")]
    [ProducesResponseType(typeof(IEnumerable<MyAuctionRiskFlagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyFlags(CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(
            new GetMyAuctionRiskFlagsQuery(CurrentUserId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }
}
