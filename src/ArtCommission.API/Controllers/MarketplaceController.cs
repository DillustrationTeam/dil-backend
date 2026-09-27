using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.ArtistStudio.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Route("api/v1/marketplace")]
public class MarketplaceController : ApiControllerBase
{
    /// <summary>Returns the public discovery feed or applies advanced artwork filters.</summary>
    [AllowAnonymous, HttpGet("feed"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Feed([FromQuery] ArtworkSearchRequest request, CancellationToken cancellationToken)
        => OkEnvelope(await Mediator.Send(new SearchMarketplaceQuery(request), cancellationToken));

    /// <summary>Returns a homepage payload with featured banners and discovery items for the current user.</summary>
    [AllowAnonymous, HttpGet("home"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Home([FromQuery] int take = 12, CancellationToken cancellationToken = default)
        => OkEnvelope(await Mediator.Send(new GetMarketplaceHomeQuery(CurrentUserId, take), cancellationToken));

    /// <summary>Returns homepage event banners or feature cards.</summary>
    [AllowAnonymous, HttpGet("banners"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Banners(CancellationToken cancellationToken)
        => OkEnvelope(await Mediator.Send(new GetFeaturedBannersQuery(), cancellationToken));

    /// <summary>Returns recent portfolio uploads from creators followed by the authenticated user.</summary>
    [Authorize, HttpGet("following"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Following([FromQuery] int take, CancellationToken cancellationToken)
        => OkEnvelope(await Mediator.Send(new GetFollowingFeedQuery(CurrentUserId, take), cancellationToken));

    /// <summary>Finds artworks similar to a reference image's supplied style or AI-generated tags.</summary>
    [AllowAnonymous, HttpPost("visual-search"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> VisualSearch([FromBody] VisualSearchRequest request, CancellationToken cancellationToken)
    {
        var tag = request.Tags?.FirstOrDefault();
        var results = await Mediator.Send(new SearchMarketplaceQuery(new ArtworkSearchRequest(tag, request.Style, null, null, null, null, null, request.Take)), cancellationToken);
        return OkEnvelope(results, new { referenceImageUrl = request.ReferenceImageUrl, matchingStrategy = "style-and-tags" });
    }

    [AllowAnonymous, HttpGet("artworks/{artworkId:guid}/detail"), ProducesResponseType(StatusCodes.Status200OK), ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArtworkDetail(Guid artworkId, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetArtworkDetailQuery(artworkId, CurrentUserId), cancellationToken);
        return data is null ? NotFound() : OkEnvelope(data);
    }

    [AllowAnonymous, HttpGet("artworks/{artworkId:guid}/comments"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Comments(Guid artworkId, CancellationToken cancellationToken)
        => OkEnvelope(await Mediator.Send(new GetArtworkCommentsQuery(artworkId), cancellationToken));

    [Authorize, HttpPost("artworks/{artworkId:guid}/comments"), ProducesResponseType(StatusCodes.Status200OK), ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateComment(Guid artworkId, [FromBody] CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new MarketplaceMutationCommand("comment", CurrentUserId, artworkId, request.Body), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [Authorize, HttpPut("artworks/{artworkId:guid}/favorite"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Favorite(Guid artworkId, CancellationToken cancellationToken) => await Mutate("favorite", artworkId, cancellationToken);
    [Authorize, HttpDelete("artworks/{artworkId:guid}/favorite"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unfavorite(Guid artworkId, CancellationToken cancellationToken) => await Mutate("unfavorite", artworkId, cancellationToken);

    [AllowAnonymous, HttpGet("creators/{creatorId:guid}/services"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Services(Guid creatorId, CancellationToken cancellationToken) => OkEnvelope(await Mediator.Send(new GetCreatorServicesQuery(creatorId), cancellationToken));
    [AllowAnonymous, HttpGet("creators/{creatorId:guid}/reviews"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Reviews(Guid creatorId, CancellationToken cancellationToken) => OkEnvelope(await Mediator.Send(new GetCreatorReviewsQuery(creatorId), cancellationToken));
    [Authorize, HttpPut("creators/{creatorId:guid}/follow"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Follow(Guid creatorId, CancellationToken cancellationToken) => await Mutate("follow", creatorId, cancellationToken);
    [Authorize, HttpDelete("creators/{creatorId:guid}/follow"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unfollow(Guid creatorId, CancellationToken cancellationToken) => await Mutate("unfollow", creatorId, cancellationToken);
    [Authorize, HttpPost("creators/{creatorId:guid}/reviews"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateReview(Guid creatorId, [FromBody] CreateCreatorReviewRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new MarketplaceMutationCommand("review", CurrentUserId, creatorId, Review: request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }
    [Authorize(Roles = "Creator"), HttpPost("creator/services"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateService([FromBody] CreateCommissionServiceRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new MarketplaceMutationCommand("service", CurrentUserId, Guid.Empty, Service: request), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }

    [Authorize, HttpGet("me/favorites"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Favorites(CancellationToken cancellationToken) => OkEnvelope(await Mediator.Send(new GetMyFavoritesQuery(CurrentUserId), cancellationToken));
    [Authorize, HttpGet("me/collections"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Collections(CancellationToken cancellationToken) => OkEnvelope(await Mediator.Send(new GetMyCollectionsQuery(CurrentUserId), cancellationToken));
    [Authorize, HttpPost("me/collections"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCollection([FromBody] CreateCollectionRequest request, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new MarketplaceMutationCommand("collection", CurrentUserId, Guid.Empty, request.Name, request.IsPublic), cancellationToken);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }
    [Authorize, HttpPut("me/collections/{collectionId:guid}/artworks/{artworkId:guid}"), ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddToCollection(Guid collectionId, Guid artworkId, CancellationToken cancellationToken) => await Mutate("add-collection-artwork", collectionId, cancellationToken, artworkId.ToString());

    private async Task<IActionResult> Mutate(string action, Guid targetId, CancellationToken ct, string? text = null)
    {
        var result = await Mediator.Send(new MarketplaceMutationCommand(action, CurrentUserId, targetId, text), ct);
        return result.Success ? OkEnvelope(result.Data) : BadRequestEnvelope(result.Errors);
    }
}
