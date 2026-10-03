using ArtCommission.Application.ArtistStudio.ClientProfiles.Commands.UpdateClientProfile;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientFollowing;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientProfileById;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientPublicCollections;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientReviews;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientStatistics;
using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetUsernameAvailability;
using ArtCommission.Application.ArtistStudio.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Authorize]
[Route("api/v1/clients")]
public class ClientProfilesController : ApiControllerBase
{
    public record UpdateClientProfileRequest(
        string? Username,
        List<string>? InterestTags,
        string? Country,
        string? Timezone,
        List<string>? PreferredLanguages,
        string? CurrentPassword = null);

    /// <summary>Hồ sơ công khai của Client — không cần đăng nhập.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetClientProfileByIdQuery(id), cancellationToken);
        return success ? OkEnvelope(data) : NotFoundEnvelope(errors);
    }

    /// <summary>Thống kê công khai: số commission hoàn thành, số dispute.</summary>
    [HttpGet("{id:guid}/statistics")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatistics(Guid id, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetClientStatisticsQuery(id), cancellationToken);
        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Đánh giá từ Creator về Client — hiện luôn rỗng cho tới khi có luồng ghi (giai đoạn sau).</summary>
    [HttpGet("{id:guid}/reviews")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReviews(Guid id, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetClientReviewsQuery(id), cancellationToken);
        return OkEnvelope(data);
    }

    /// <summary>Danh sách Creator mà Client đang theo dõi.</summary>
    [HttpGet("{id:guid}/following")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFollowing(Guid id, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetClientFollowingQuery(id), cancellationToken);
        return OkEnvelope(data);
    }

    /// <summary>Tranh Client đã thích (Favorites/Wishlist) — tái dùng query sẵn có của marketplace.</summary>
    [HttpGet("{id:guid}/favorites")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavorites(Guid id, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetMyFavoritesQuery(id), cancellationToken);
        return OkEnvelope(data);
    }

    /// <summary>Album công khai (IsPublic = true) của Client.</summary>
    [HttpGet("{id:guid}/collections")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCollections(Guid id, CancellationToken cancellationToken)
    {
        var data = await Mediator.Send(new GetClientPublicCollectionsQuery(id), cancellationToken);
        return OkEnvelope(data);
    }

    /// <summary>Kiểm tra 1 username có thể đặt được không (định dạng, reserved, trùng lặp) — dùng cho kiểm tra trực tiếp khi đang gõ.</summary>
    [HttpGet("username-availability")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUsernameAvailability([FromQuery] string username, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var available = await Mediator.Send(new GetUsernameAvailabilityQuery(CurrentUserId, username ?? string.Empty), cancellationToken);
        return OkEnvelope(new { available });
    }

    /// <summary>Cập nhật hồ sơ Client công khai của chính mình (Username, Tags, Quốc gia, Múi giờ, Ngôn ngữ).</summary>
    [HttpPut("me/profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateClientProfileRequest request, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new UpdateClientProfileCommand(CurrentUserId, request.Username, request.InterestTags, request.Country, request.Timezone, request.PreferredLanguages, request.CurrentPassword),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }
}
