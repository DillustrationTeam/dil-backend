using ArtCommission.Application.ArtTrade.Commands;
using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.ArtTrade.Queries;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
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
    private readonly IFileStorageService _fileStorage;

    public ArtTradesController(IFileStorageService fileStorage)
    {
        _fileStorage = fileStorage;
    }
    /// <summary>
    /// Tìm kiếm nghệ sĩ (Creator) để gửi đề nghị trao đổi tranh.
    /// </summary>
    [HttpGet("creators/search")]
    [ProducesResponseType(typeof(List<CreatorTradeSearchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchCreators(
        [FromQuery] string? query,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Send(
            new SearchCreatorsForTradeQuery(query, CurrentUserId, limit),
            cancellationToken);

        return OkEnvelope(result);
    }

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
            DurationDays: request.DurationDays,
            ReceiverDisplayName: request.ReceiverDisplayName,
            InitiatorRequestNote: request.InitiatorRequestNote,
            ReceiverRequestNote: request.ReceiverRequestNote
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }

    /// <summary>
    /// Tải lên ảnh đính kèm / ảnh mẫu tham khảo (Reference image) cho giao dịch trao đổi tranh.
    /// </summary>
    [HttpPost("attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10MB
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken ct)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return BadRequestEnvelope(new[] { "File ảnh phải có dung lượng tối đa 10MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
            return BadRequestEnvelope(new[] { "Chỉ chấp nhận ảnh định dạng JPG, PNG, WebP." });

        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        await using var stream = file.OpenReadStream();
        var url = await _fileStorage.SaveAsync(stream, uniqueName, file.ContentType, $"trades/references/{CurrentUserId}", ct);

        var absoluteUrl = Uri.TryCreate(url, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? url : $"{Request.Scheme}://{Request.Host}{url}";

        return OkEnvelope(new { url = absoluteUrl });
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
            RequestNote: request.ResponseNote ?? request.RequestNote,
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
    /// Side A xác nhận đề nghị sau khi Side B đã chấp nhận (cả 2 bên accept mới bắt đầu).
    /// </summary>
    [HttpPost("{id:guid}/confirm-proposal")]
    [ProducesResponseType(typeof(ArtTradeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmTradeProposal(Guid id, CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(new ConfirmTradeProposalCommand(id, CurrentUserId), cancellationToken);
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
        [FromQuery] string? side = null,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var command = new ConfirmTradeCommand(
            TradeId: id,
            UserId: CurrentUserId,
            Side: side
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

        var isAdmin = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);
        var query = new GetArtTradeByIdQuery(TradeId: id, UserId: CurrentUserId, IsAdmin: isAdmin);
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

        try
        {
            var query = new GetMyTradeSlotStatusQuery(UserId: CurrentUserId);
            var (success, data) = await Mediator.Send(query, cancellationToken);
            return OkEnvelope(data);
        }
        catch (Exception ex)
        {
            var fallback = new TradeSlotStatusDto(
                UserId: CurrentUserId,
                TotalSlots: TradeSlotService.DefaultMaxSlots,
                ActiveTradesCount: 0,
                FrozenSlotsCount: 0,
                AvailableSlots: TradeSlotService.DefaultMaxSlots,
                CanTrade: true,
                FrozenDetails: new List<FrozenSlotDetailDto>()
            );
            return OkEnvelope(fallback, new { warning = "Slot fallback used: " + ex.Message });
        }
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

    /// <summary>
    /// [Admin/Moderator] Lấy danh sách tất cả các cuộc trao đổi tranh trên nền tảng phục vụ quản lý và vận hành (Trade Operations).
    /// </summary>
    [HttpGet("admin")]
    [HttpGet("/api/v1/admin/trades")]
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    [ProducesResponseType(typeof(IEnumerable<ArtTradeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTradesForAdmin(
        [FromQuery] TradeStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminArtTradesQuery(
            Status: status,
            Search: search,
            Page: page,
            PageSize: pageSize
        );

        var result = await Mediator.Send(query, cancellationToken);

        return OkEnvelope(result.Items, new
        {
            page,
            pageSize,
            totalCount = result.TotalCount,
            totalPages = (int)Math.Ceiling(result.TotalCount / (double)(pageSize <= 0 ? 10 : pageSize)),
            activeCount = result.ActiveCount,
            completedCount = result.CompletedCount,
            disputedCount = result.DisputedCount,
            cancelledCount = result.CancelledCount
        });
    }

    /// <summary>
    /// [Admin/Moderator] Can thiệp cưỡng chế hủy giao dịch trao đổi tranh.
    /// </summary>
    [HttpPost("admin/{id:guid}/force-cancel")]
    [HttpPost("/api/v1/admin/trades/{id:guid}/force-cancel")]
    [Authorize(Roles = $"{UserRoleNames.Administrator},{UserRoleNames.Moderator}")]
    public async Task<IActionResult> AdminForceCancel(
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
            Reason: string.IsNullOrWhiteSpace(request.Reason) ? "Quản trị viên cưỡng chế dừng giao dịch." : request.Reason,
            IsViolation: request.IsViolation,
            ViolatorUserId: request.ViolatorUserId,
            IsAdmin: true
        );

        var (success, data, errors) = await Mediator.Send(command, cancellationToken);
        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(data);
    }
}

