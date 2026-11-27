using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Auction.Recommendation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Personalized Auction Recommendation - read-only rails plus impression telemetry.
///
/// Both rails are anonymous-friendly: without a token the recommender falls back to trending, which
/// is why the controller does not require authentication for reads.
/// </summary>
[Route("api/v1/auctions")]
public class AuctionRecommendationsController : ApiControllerBase
{
    /// <summary>Personalised rail for the auctions page ("Dành cho bạn").</summary>
    [AllowAnonymous]
    [HttpGet("recommended")]
    [ProducesResponseType(typeof(IEnumerable<RecommendedAuctionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecommended(
        [FromQuery] int take = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = User.Identity?.IsAuthenticated == true ? CurrentUserId : (Guid?)null;
        var (success, data, errors) = await Mediator.Send(
            new GetRecommendedAuctionsQuery(userId, take), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Similar auctions rail on the auction detail page.</summary>
    [AllowAnonymous]
    [HttpGet("{auctionId:guid}/similar")]
    [ProducesResponseType(typeof(IEnumerable<RecommendedAuctionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimilar(
        Guid auctionId,
        [FromQuery] int take = 6,
        CancellationToken cancellationToken = default)
    {
        var (success, data, errors) = await Mediator.Send(
            new GetSimilarAuctionsQuery(auctionId, take), cancellationToken);

        return success ? OkEnvelope(data) : NotFoundEnvelope(errors);
    }

    /// <summary>Batch impression logging (telemetry only; never blocks the page).</summary>
    [AllowAnonymous]
    [HttpPost("recommendations/impressions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecordImpressions(
        [FromBody] RecommendationImpressionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity?.IsAuthenticated == true ? CurrentUserId : (Guid?)null;
        // Accept both payload shapes: `items` (preferred, keeps score/rank) and the legacy id list.
        var items = request.Items
                    ?? (request.AuctionIds ?? [])
                        .Select(id => new RecommendationImpressionItem(id))
                        .ToList();

        var (success, recorded, errors) = await Mediator.Send(
            new RecordRecommendationImpressionsCommand(userId, items, request.Surface, request.Clicked),
            cancellationToken);

        return success ? OkEnvelope(new { recorded }) : BadRequestEnvelope(errors);
    }

    /// <summary>Force a profile rebuild after a strong signal (bid/follow).</summary>
    [Authorize]
    [HttpPost("recommendations/rebuild-profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RebuildProfile(
        [FromServices] IInterestProfileBuilder builder,
        CancellationToken cancellationToken)
    {
        var profile = await builder.GetOrBuildAsync(CurrentUserId, forceRebuild: true, cancellationToken);

        return OkEnvelope(new
        {
            sourceEvents = profile.SourceEventCount,
            tagCount = profile.Tags.Count,
            builtAt = profile.BuiltAt
        });
    }
}
