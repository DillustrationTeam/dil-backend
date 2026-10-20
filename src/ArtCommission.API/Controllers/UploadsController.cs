using ArtCommission.Infrastructure.ExternalServices.Cloudinary;
using ArtCommission.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Cấp chữ ký Cloudinary Signed Upload cho client tự upload file thẳng lên Cloudinary
/// (server không proxy file, ApiSecret không rời server).
/// </summary>
[Authorize]
[Route("api/v1/uploads")]
public class UploadsController : ApiControllerBase
{
    private readonly ICloudinarySignatureService _signatureService;
    private readonly IFileStorageService _fileStorage;

    public UploadsController(ICloudinarySignatureService signatureService, IFileStorageService fileStorage)
    {
        _signatureService = signatureService;
        _fileStorage = fileStorage;
    }

    public record SignatureRequest(string? Folder, string? Transformation);

    [HttpPost("profile-avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfileAvatar([FromForm] IFormFile file, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();
        if (file is null || file.Length == 0 || file.Length > 5 * 1024 * 1024)
            return BadRequestEnvelope(new[] { "Avatar must be an image up to 5MB." });
        if (!new[] { "image/jpeg", "image/png", "image/webp" }.Contains(file.ContentType))
            return BadRequestEnvelope(new[] { "Only JPG, PNG and WebP avatars are supported." });
        try
        {
            await using var input = file.OpenReadStream();
            var format = await Image.DetectFormatAsync(input, ct);
            if (!new[] { "JPEG", "PNG", "WEBP" }.Contains(format.Name))
                return BadRequestEnvelope(new[] { "Invalid avatar image format." });
            input.Position = 0;
            var info = await Image.IdentifyAsync(input, ct);
            if (info is null || (long)info.Width * info.Height > 16_000_000)
                return BadRequestEnvelope(new[] { "Avatar dimensions are too large." });
            input.Position = 0;
            using var image = await Image.LoadAsync(input, ct);
            image.Mutate(context => context.AutoOrient().Resize(new ResizeOptions { Size = new Size(400, 400), Mode = ResizeMode.Crop }));
            await using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, ct);
            output.Position = 0;
            var url = await _fileStorage.SaveAsync(output, "avatar.jpg", "image/jpeg", $"profile-avatars/{CurrentUserId}", ct);
            var absoluteUrl = Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
                ? url : $"{Request.Scheme}://{Request.Host}{url}";
            return OkEnvelope(new { url = absoluteUrl });
        }
        catch (UnknownImageFormatException)
        {
            return BadRequestEnvelope(new[] { "The file is not a supported image." });
        }
        catch (InvalidImageContentException)
        {
            return BadRequestEnvelope(new[] { "The image is damaged or invalid." });
        }
    }

    /// <summary>
    /// Lấy chữ ký để upload file lên Cloudinary (vd: ảnh CCCD, ảnh portfolio, avatar, cover).
    /// </summary>
    [HttpPost("signature")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetUploadSignature([FromBody] SignatureRequest? request)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var folder = string.IsNullOrWhiteSpace(request?.Folder) ? "creator-applications" : request.Folder;
        var signature = _signatureService.GenerateUploadSignature(folder, request?.Transformation);

        return OkEnvelope(signature);
    }
}
