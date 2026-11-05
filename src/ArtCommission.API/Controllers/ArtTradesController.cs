using ArtCommission.Application.ArtTrade.Commands;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.ArtTrade.Queries;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// Quản lý tính năng Trao đổi tranh (Art Trade) giữa 2 nghệ sĩ.
/// </summary>
[Authorize]
[Route("api/v1/art-trades")]
public class ArtTradesController : ApiControllerBase
{
    /// <summary>
    /// Khởi tạo đề nghị trao đổi tranh mới (Propose Art Trade).
    /// </summary>
    [HttpPost]
    [HttpPost("propose")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProposeTrade(
        [FromBody] CreateArtTradeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ProposeTradeCommand(
            InitiatorId: CurrentUserId,
            ReceiverId: request.ReceiverId,
            Title: request.Title,
            Description: request.Description,
            RequestNote: request.RequestNote,
            DurationDays: request.DurationDays
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Phản hồi đề nghị trao đổi tranh (Chấp nhận hoặc Từ chối).
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RespondTradeProposal(
        Guid id,
        [FromBody] RespondTradeProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new RespondTradeProposalCommand(
            TradeId: id,
            UserId: CurrentUserId,
            Accept: request.Accept,
            RequestNote: request.RequestNote,
            RejectionReason: request.RejectionReason
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Nộp file tranh gốc (PNG, JPG, PSD, CLIP) cho lượt trao đổi tranh.
    /// File gốc được lưu vào Private Bucket, hệ thống tự động gắn watermark và tải preview lên CDN.
    /// </summary>
    [HttpPost("{id:guid}/artwork")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)] // Tối đa 100MB cho file gốc chất lượng cao / PSD / CLIP
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SubmitArtwork(
        Guid id,
        IFormFile file,
        [FromForm] string? artworkTitle = null,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequestEnvelope("Vui lòng tải lên file tranh hợp lệ.");
        }

        await using var stream = file.OpenReadStream();

        var command = new SubmitTradeArtworkCommand(
            TradeId: id,
            UserId: CurrentUserId,
            FileStream: stream,
            ContentType: file.ContentType,
            FileName: file.FileName,
            ArtworkTitle: artworkTitle
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Xác nhận hài lòng với tác phẩm của đối tác (Confirm Satisfaction).
    /// Khi cả hai bên đều xác nhận, giao dịch hoàn tất, cấp quyền sở hữu chéo và mở khóa URL tải file gốc.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmTrade(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ConfirmTradeCommand(
            TradeId: id,
            UserId: CurrentUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Hủy bỏ giao dịch trao đổi tranh (Trước khi hoàn tất).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelTrade(
        Guid id,
        [FromBody] CancelTradeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new CancelTradeCommand(
            TradeId: id,
            UserId: CurrentUserId,
            Reason: request.Reason,
            IsViolation: request.IsViolation,
            ViolatorUserId: request.ViolatorUserId
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Xem chi tiết một cuộc trao đổi tranh theo Id.
    /// Chỉ người tham gia giao dịch (Initiator hoặc Receiver) mới có quyền xem.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTradeById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetArtTradeByIdQuery(TradeId: id, UserId: CurrentUserId);
        var (success, data, errors) = await Mediator.Send(query, cancellationToken);

        if (!success)
        {
            if (errors.Any(e => e.Contains("Không tìm thấy")))
            {
                return NotFoundEnvelope(errors);
            }
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy danh sách các cuộc trao đổi tranh của người dùng hiện tại (hỗ trợ phân trang và lọc theo trạng thái).
    /// </summary>
    [HttpGet]
    [HttpGet("my-trades")]
    [ProducesResponseType(typeof(IEnumerable<ArtTradeSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTrades(
        [FromQuery] TradeStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetMyArtTradesQuery(
            UserId: CurrentUserId,
            Status: status,
            Page: page,
            PageSize: pageSize
        );

        var (success, items, totalCount) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(items, new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)(pageSize <= 0 ? 10 : pageSize))
        });
    }

    /// <summary>
    /// Kiểm tra trạng thái hạn mức slot trao đổi tranh (Trade Slots Quota) của người dùng hiện tại.
    /// Slot Khả Dụng = 2 - (ActiveTrades + FrozenSlots).
    /// </summary>
    [HttpGet("my-slots")]
    [ProducesResponseType(typeof(TradeSlotStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySlotStatus(CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetMyTradeSlotStatusQuery(UserId: CurrentUserId);
        var (success, data) = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(data);
    }

    /// <summary>
    /// Lấy presigned URL có thời hạn để tải về file tranh gốc (chỉ khả dụng sau khi giao dịch Completed).
    /// </summary>
    [HttpGet("{id:guid}/download-url")]
    [ProducesResponseType(typeof(TradeDownloadUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDownloadUrl(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var query = new GetTradeDownloadUrlQuery(TradeId: id, UserId: CurrentUserId);
        var (success, data, errors) = await Mediator.Send(query, cancellationToken);

        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}

