using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.ArtistStudio.Commands.CreateArtworkViewReceipt;
using ArtCommission.Application.ArtistStudio.Queries.TraceArtworkForensic;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

[ApiController]
[Route("api/v1")]
public class ArtworkForensicController : ApiControllerBase
{
    private readonly IArtworkWatermarkService _watermarkService;

    public ArtworkForensicController(IArtworkWatermarkService watermarkService)
    {
        _watermarkService = watermarkService;
    }

    /// <summary>
    /// Tạo phiên xem tranh và sinh "Biên lai thuỷ vân số" (Forensic Watermark Receipt).
    /// Được gọi bởi Frontend (SCR-12 Viewer) để lấy thông số hiển thị bảo mật và lớp overlay.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("artworks/{id:guid}/view-session")]
    public async Task<IActionResult> CreateViewSession(Guid id, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var userAgent = Request.Headers.UserAgent.ToString();
        var sessionId = Request.Headers["X-Session-Id"].ToString();

        Guid? userId = CurrentUserId != Guid.Empty ? CurrentUserId : null;
        string? email = User.FindFirst(ClaimTypes.Email)?.Value;
        string? name = User.FindFirst(ClaimTypes.Name)?.Value;

        var command = new CreateArtworkViewReceiptCommand(
            ArtworkId: id,
            ViewerUserId: userId,
            ViewerEmail: email,
            ViewerName: name,
            IpAddress: ip,
            UserAgent: userAgent,
            SessionId: sessionId
        );

        var result = await Mediator.Send(command, ct);
        return OkEnvelope(result);
    }

    /// <summary>
    /// Tra cứu lịch sử biên lai để truy vết nguồn gốc tranh rò rỉ.
    /// </summary>
    [Authorize]
    [HttpGet("artworks/forensic/trace")]
    public async Task<IActionResult> TraceReceipt(
        [FromQuery] string? receiptToken,
        [FromQuery] long? numericPayload,
        [FromQuery] Guid? artworkId,
        CancellationToken ct)
    {
        var query = new TraceArtworkForensicQuery(receiptToken, numericPayload, artworkId);
        var results = await Mediator.Send(query, ct);
        return OkEnvelope(results);
    }

    /// <summary>
    /// Tải lên ảnh nghi ngờ bị rò rỉ hoặc ảnh chụp màn hình để trích xuất DWT Watermark và tra cứu danh tính kẻ phát tán.
    /// </summary>
    [Authorize]
    [HttpPost("artworks/forensic/decode-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> DecodeSuspectImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequestEnvelope("Vui lòng tải lên file ảnh nghi ngờ rò rỉ.");
        }

        await using var stream = file.OpenReadStream();
        var extractedPayload = await _watermarkService.ExtractInvisibleWatermarkAsync(stream, ct);

        if (!extractedPayload.HasValue || extractedPayload.Value == 0)
        {
            return OkEnvelope(new
            {
                Found = false,
                Message = "Không phát hiện mã thuỷ vân DWT hợp lệ trong ảnh này (ảnh có thể bị bóp méo quá nặng hoặc không xuất phát từ hệ thống).",
                Matches = Array.Empty<ArtworkForensicTraceDto>()
            });
        }

        long payloadLong = (long)extractedPayload.Value;
        var query = new TraceArtworkForensicQuery(null, payloadLong, null);
        var matches = await Mediator.Send(query, ct);

        return OkEnvelope(new
        {
            Found = true,
            ExtractedPayload = $"0x{extractedPayload.Value:X16}",
            NumericPayload = payloadLong,
            TotalMatches = matches.Count,
            Matches = matches
        });
    }
}
