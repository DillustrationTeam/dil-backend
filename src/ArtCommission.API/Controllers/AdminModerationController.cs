using ArtCommission.Application.ArtistStudio.Moderation.Commands;
using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.ArtistStudio.Moderation.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new { message });
    }
}
