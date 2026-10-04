using ArtCommission.Application.ArtistStudio.Commands.CreateCreatorProfile;
using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.ArtistStudio.Queries.GetArtworkById;
using ArtCommission.Application.ArtistStudio.Queries.GetArtworks;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorProfile;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorProfileById;
using ArtCommission.Application.ArtistStudio.Queries.ManageCreatorArtworks;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.ArtistStudio.Workstation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[Authorize]
[Route("api/v1")]
public class CreatorProfilesController : ApiControllerBase
{
    private const long MaxArtworkImageSizeBytes = 25 * 1024 * 1024;
    private static readonly HashSet<string> ArtworkImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif"
    };

    private readonly IStorageService _storageService;

    public CreatorProfilesController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <summary>Thống kê công khai của creator profile.</summary>
    [HttpGet("creator-profiles/{creatorId:guid}/statistics")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatistics(Guid creatorId, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(
            new ArtCommission.Application.ArtistStudio.Queries.GetCreatorStatistics.GetCreatorStatisticsQuery(creatorId),
            cancellationToken);
        return success ? OkEnvelope(data) : NotFoundEnvelope(errors);
    }

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
            request.IsAcceptingOrders,
            request.AvailableSlots);

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

    [HttpPut("creator/me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateCreatorProfileRequest request, CancellationToken cancellationToken)
    {
        var command = new ArtCommission.Application.ArtistStudio.Commands.UpdateCreatorProfile.UpdateCreatorProfileCommand(
            CurrentUserId,
            request.DisplayName,
            request.Headline,
            request.Bio,
            request.Specialties,
            request.Location,
            request.WebsiteUrl,
            request.BannerUrl,
            request.IsAcceptingOrders,
            request.AvailableSlots);

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
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

    [HttpGet("creator/me/rate-card")]
    public async Task<IActionResult> GetMyRateCard(CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.GetCreatorRateCardQuery(CurrentUserId), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data ?? new List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>());
    }

    [HttpGet("creator/{id:guid}/rate-card")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRateCardByCreatorId(Guid id, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.GetCreatorRateCardQuery(id), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data ?? new List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>());
    }

    [HttpGet("creator/{id:guid}/terms")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTermsByCreatorId(Guid id, CancellationToken cancellationToken)
    {
        var terms = await Mediator.Send(new GetCreatorTermsQuery(id), cancellationToken);
        return terms is null ? NotFound() : OkEnvelope(terms);
    }

    [HttpPut("creator/me/rate-card")]
    public async Task<IActionResult> SaveRateCard([FromBody] ArtCommission.Application.ArtistStudio.Commands.SaveCreatorRateCard.SaveRateCardRequest request, CancellationToken cancellationToken)
    {
        var (success, data, errors) = await Mediator.Send(new ArtCommission.Application.ArtistStudio.Commands.SaveCreatorRateCard.SaveCreatorRateCardCommand(CurrentUserId, request.Packages), cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data ?? new List<ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard.RateCardPackageDto>());
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

    [HttpPost("artworks/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxArtworkImageSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadArtworkImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequestEnvelope("Vui lòng chọn ảnh tác phẩm.");
        }

        if (file.Length > MaxArtworkImageSizeBytes)
        {
            return BadRequestEnvelope("Ảnh tác phẩm không được vượt quá 25 MB.");
        }

        if (!ArtworkImageContentTypes.Contains(file.ContentType))
        {
            return BadRequestEnvelope("Chỉ hỗ trợ ảnh JPEG, PNG, WEBP hoặc GIF.");
        }

        var extension = file.ContentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => throw new InvalidOperationException("Unsupported artwork image type.")
        };

        await using var stream = file.OpenReadStream();
        var key = $"artworks/{CurrentUserId}/{Guid.NewGuid():N}{extension}";
        var imageUrl = await _storageService.UploadPublicAsync(stream, key, file.ContentType, cancellationToken);

        return OkEnvelope(new { imageUrl });
    }

    [HttpGet("creator/me/artworks")]
    public async Task<IActionResult> GetMyArtworks([FromQuery] string? search, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var artworks = await Mediator.Send(new GetCreatorArtworksQuery(CurrentUserId, search, status), cancellationToken);
        return OkEnvelope(artworks);
    }

    [HttpPut("artworks/{id:guid}")]
    public async Task<IActionResult> UpdateArtwork(Guid id, [FromBody] UpdateCreatorArtworkRequest request, CancellationToken cancellationToken)
    {
        var (data, errors) = await Mediator.Send(
            new UpdateCreatorArtworkCommand(CurrentUserId, id, request.Title, request.Description, request.Tags, request.ImageUrl, request.ThumbnailUrl),
            cancellationToken);

        return data is null ? BadRequestEnvelope(errors) : OkEnvelope(data);
    }

    [HttpDelete("artworks/{id:guid}")]
    public async Task<IActionResult> DeleteArtwork(Guid id, CancellationToken cancellationToken)
    {
        var (success, errors) = await Mediator.Send(new DeleteCreatorArtworkCommand(CurrentUserId, id), cancellationToken);
        return success ? OkEnvelope(new { deleted = true }) : NotFoundEnvelope(errors);
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
