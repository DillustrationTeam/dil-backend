using ArtCommission.Application.ArtistStudio.Ai;
using ArtCommission.Infrastructure.Services;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ArtCommission.API.Controllers;
[Authorize, Route("api/v1")]
public class ArtworkAiController(IArtworkMetadataService metadata, ArtworkAiScanService scans) : ApiControllerBase
{
    [Authorize(Roles = "Creator"), HttpPost("artworks/suggest-metadata"), EnableRateLimiting("artwork-ai")]
    [Consumes("multipart/form-data"), RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Suggest(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length is <= 0 or > 5242880 || file.ContentType is not ("image/png" or "image/jpeg" or "image/webp")) return BadRequestEnvelope("Chọn ảnh PNG, JPG hoặc WebP tối đa 5 MB.");
        await using var stream = file.OpenReadStream();
        return OkEnvelope(await metadata.SuggestAsync(stream, ct));
    }
    [HttpGet("me/artwork-recommendations")]
    public async Task<IActionResult> Recommendations([FromQuery] int take = 12, CancellationToken ct = default)
        => OkEnvelope(await Mediator.Send(new GetArtworkRecommendationsQuery(CurrentUserId, take), ct));
    [HttpGet("artworks/{id:guid}/ai-scan")]
    public async Task<IActionResult> Status(Guid id, CancellationToken ct)
        => OkEnvelope(await scans.GetAsync(id, CurrentUserId, User.IsInRole(UserRoleNames.Moderator) || User.IsInRole(UserRoleNames.Administrator), ct));
    [Authorize(Roles = $"{UserRoleNames.Moderator},{UserRoleNames.Administrator}"), HttpPost("admin/moderation/{id:guid}/ai-scan"), EnableRateLimiting("artwork-ai")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct) => OkEnvelope(await scans.RetryAsync(id, CurrentUserId, ct));
}
