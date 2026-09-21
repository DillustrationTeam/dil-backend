using ArtCommission.Infrastructure.ExternalServices.Cloudinary;
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

    public UploadsController(ICloudinarySignatureService signatureService)
    {
        _signatureService = signatureService;
    }

    public record SignatureRequest(string? Folder);

    /// <summary>
    /// Lấy chữ ký để upload file lên Cloudinary (vd: ảnh CCCD, ảnh portfolio).
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
        var signature = _signatureService.GenerateUploadSignature(folder);

        return OkEnvelope(signature);
    }
}
