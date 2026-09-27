using ArtCommission.Application.ArtistStudio.Commands.CreateCreatorProfile;
using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.ArtistStudio.Queries.GetArtworkById;
using ArtCommission.Application.ArtistStudio.Queries.GetArtworks;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorProfile;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorProfileById;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Authorize]
[Route("api/v1")]
public class CreatorProfilesController : ApiControllerBase
{
    [HttpPost("profile/setup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProfile([FromBody] CreateCreatorProfileRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateCreatorProfileCommand(
            CurrentUserId,
            request.DisplayName,
            request.Headline,
            request.Bio,
            request.Specialties,
            request.Location,
            request.WebsiteUrl,
            request.BannerUrl,
            request.IsAcceptingOrders);

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success || data == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    [HttpGet("creator/me")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetCreatorProfileQuery(CurrentUserId), cancellationToken);
        if (!success || data == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    [HttpGet("creator/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetCreatorProfileByIdQuery(id), cancellationToken);
        if (!success || data == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    [HttpPost("artworks")]
    public async Task<IActionResult> UploadArtwork([FromBody] CreateArtworkRequest request, CancellationToken cancellationToken)
    {
        var command = new UploadArtworkCommand(
            CurrentUserId,
            request.Title,
            request.Description,
            request.ImageUrl,
            request.ThumbnailUrl,
            request.IsAiGenerated,
            request.AiDetectionScore,
            request.Tags);

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success || data == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Thư viện tranh công khai — KHÔNG cần đăng nhập.
    /// Controller có [Authorize] ở cấp class, nhưng trang chủ Marketplace gọi
    /// endpoint này ngay khi tải trang nên khách vãng lai luôn bị 401.
    /// Thư viện tranh là nội dung công khai nên hai endpoint chỉ đọc dưới đây
    /// được mở bằng [AllowAnonymous].
    /// </summary>
    [HttpGet("artworks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetArtworks(CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetArtworksQuery(), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    [HttpGet("artworks/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetArtworkById(Guid id, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new GetArtworkByIdQuery(id), cancellationToken);
        if (!success || data == null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}
