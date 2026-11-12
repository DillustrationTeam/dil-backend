using System.Security.Cryptography;
using ArtCommission.API.Helpers;
using ArtCommission.Application.ArtistStudio.Moderation;
using ArtCommission.Application.ArtistStudio.Moderation.Commands;
using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.ArtistStudio.Moderation.Queries;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý hàng đợi kiểm duyệt nội dung và quét AI (SCR-20 / UC28).
/// Dành cho Moderator và Administrator.
/// </summary>
[Authorize(Roles = $"{UserRoleNames.Moderator},{UserRoleNames.Administrator}")]
[Route("api/v1/admin/moderation")]
public class AdminModerationController : ApiControllerBase
{
    /// <summary>
    /// Lấy danh sách tác phẩm trong hàng đợi kiểm duyệt (SCR-20).
    /// Hỗ trợ lọc theo trạng thái (Pending, Flagged, Approved, Rejected, Hidden, All), tìm kiếm và phân trang.
    /// </summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(IEnumerable<ModerationQueueItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetModerationQueue(
        [FromQuery] string? status = "Pending",
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetModerationQueueQuery(status, search, page, pageSize);
        var (items, totalCount, pendingCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            pendingCount
        });
    }

    /// <summary>
    /// Xem chi tiết thanh tra tác phẩm, kết quả scan NSFW của AI và danh sách tag (SCR-20).
    /// </summary>
    [HttpGet("{artworkId:guid}")]
    [ProducesResponseType(typeof(ArtworkInspectionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtworkInspectionDetail(
        Guid artworkId,
        CancellationToken cancellationToken)
    {
        var query = new GetArtworkInspectionDetailQuery(artworkId);
        var result = await Mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFoundEnvelope("Không tìm thấy tác phẩm kiểm duyệt.");
        }

        return OkEnvelope(result);
    }

    /// <summary>
    /// Thực thi quyết định kiểm duyệt: Duyệt, Từ chối xóa, Ẩn khỏi gợi ý, hoặc Gắn cờ tranh AI (SCR-20).
    /// </summary>
    [HttpPost("{artworkId:guid}/decision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExecuteModerationDecision(
        Guid artworkId,
        [FromBody] ExecuteModerationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ExecuteModerationDecisionCommand(
            ArtworkId: artworkId,
            ModeratorId: CurrentUserId,
            Action: request.Action,
            ModerationNote: request.ModerationNote
        );

        var (success, message, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            if (errors.Any(e => e.Contains("Xung đột")))
            {
                return ConflictEnvelope(errors);
            }
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }

    /// <summary>
    /// Admin / Moderator chỉnh sửa lại Danh mục lớn và Tag con của tác phẩm trước hoặc sau khi duyệt.
    /// </summary>
    [HttpPut("{artworkId:guid}/metadata")]
    [ProducesResponseType(typeof(ArtworkInspectionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateArtworkMetadata(
        Guid artworkId,
        [FromBody] UpdateModerationArtworkMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new UpdateModerationArtworkMetadataCommand(artworkId, CurrentUserId, request.CategoryId, request.Tags),
            cancellationToken);

        return success && data is not null
            ? OkEnvelope(data)
            : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Yêu cầu quét lại AI cho tác phẩm (SCR-20 / UC28).
    /// Đưa tác phẩm vào hàng đợi quét nền của Hangfire để cập nhật các chỉ số NSFW và AI generation.
    /// </summary>
    [HttpPost("{artworkId:guid}/rescan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RescanArtwork(
        Guid artworkId,
        [FromServices] IArtworkScanQueue scanQueue,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var artwork = await Mediator.Send(new GetArtworkInspectionDetailQuery(artworkId), cancellationToken);
        if (artwork == null)
        {
            return NotFoundEnvelope("Không tìm thấy tác phẩm để quét lại.");
        }

        scanQueue.Enqueue(artworkId);

        return OkEnvelope(new { message = "Yêu cầu quét lại AI đã được đưa vào hàng đợi xử lý." });
    }

    /// <summary>
    /// Kiểm tra trùng lặp tác phẩm (SHA-256, mã DWT thuỷ vân và dấu vân tay thị giác dHash) phục vụ duyệt tranh (SCR-20).
    /// </summary>
    [HttpGet("{artworkId:guid}/duplicates")]
    [ProducesResponseType(typeof(ArtworkDuplicateCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckArtworkDuplicates(
        Guid artworkId,
        [FromServices] IApplicationDbContext db,
        [FromServices] IArtworkWatermarkService watermarkService,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] IWebHostEnvironment env,
        [FromServices] IConfiguration config,
        CancellationToken cancellationToken)
    {
        var artwork = await db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == artworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return NotFoundEnvelope("Không tìm thấy tác phẩm kiểm duyệt.");
        }

        var existingReceipt = await db.ArtworkWatermarkReceipts
            .AsNoTracking()
            .Where(r => r.ArtworkId == artworkId && r.WatermarkType == "DwtPublish")
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        long? fallbackVisual = null;
        string? fallbackSha = null;
        long? fallbackDwt = null;

        if (existingReceipt?.VisualHash is null || string.IsNullOrWhiteSpace(existingReceipt.FileSha256))
        {
            var srcUrl = !string.IsNullOrWhiteSpace(artwork.WatermarkedUrl) ? artwork.WatermarkedUrl : artwork.ImageUrl;
            await using var stream = await ArtworkSourceStreamHelper.LoadSourceImageStreamAsync(
                srcUrl, httpClientFactory, env, config, cancellationToken);
            if (stream is not null)
            {
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;
                fallbackSha = Convert.ToHexString(await SHA256.HashDataAsync(ms, cancellationToken)).ToLowerInvariant();
                ms.Position = 0;
                fallbackVisual = await watermarkService.ComputeVisualHashAsync(ms, trimBorders: false, cancellationToken);
                ms.Position = 0;
                try
                {
                    var dwt = await watermarkService.ExtractInvisibleWatermarkAsync(ms, cancellationToken);
                    if (dwt.HasValue && dwt.Value != 0)
                    {
                        fallbackDwt = (long)dwt.Value;
                    }
                }
                catch
                {
                    // Ảnh không đọc được DWT pixel
                }
            }
        }

        var result = await Mediator.Send(
            new CheckArtworkDuplicatesQuery(artworkId, fallbackVisual, fallbackSha, fallbackDwt),
            cancellationToken);

        return result is null
            ? NotFoundEnvelope("Không tìm thấy tác phẩm kiểm duyệt.")
            : OkEnvelope(result);
    }
}
