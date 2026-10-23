using ArtCommission.Application.Auction.Commands.BuyNow;
using ArtCommission.Application.Auction.Commands.CreateAuction;
using ArtCommission.Application.Auction.Commands.DeleteAuction;
using ArtCommission.Application.Auction.Commands.ExpireSettlement;
using ArtCommission.Application.Auction.Commands.PlaceBid;
using ArtCommission.Application.Auction.Commands.SettleAuction;
using ArtCommission.Application.Auction.Commands.UnwatchAuction;
using ArtCommission.Application.Auction.Commands.UpdateAuction;
using ArtCommission.Application.Auction.Commands.WatchAuction;
using ArtCommission.Application.Auction.Queries.GetAuctionBids;
using ArtCommission.Application.Auction.Queries.GetAuctionById;
using ArtCommission.Application.Auction.Queries.GetAuctions;
using ArtCommission.Application.Auction.Queries.GetAuctionSettlement;
using ArtCommission.Application.Auction.Queries.GetDeliverableDownloadUrl;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>Thân request tạo phiên đấu giá — tách khỏi command để không lộ UserId ra ngoài.</summary>
public sealed record CreateAuctionRequest(
    Guid ArtworkId,
    decimal StartPrice,
    decimal? ReservePrice,
    decimal BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? MasterFileUrl = null,
    string? MasterFileName = null,
    long? MasterFileSizeBytes = null);

/// <summary>Thân request sửa phiên đấu giá.</summary>
public sealed record UpdateAuctionRequest(
    decimal? StartPrice,
    decimal? ReservePrice,
    decimal? BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    bool ClearBuyNowPrice = false,
    bool ClearReservePrice = false);

/// <summary>Thân request đặt giá.</summary>
public sealed record PlaceBidRequest(
    decimal Amount,
    bool IsAuto = false,
    decimal? MaxAutoBid = null);

/// <summary>Thân request chốt phiên.</summary>
public sealed record SettleAuctionRequest(bool Force = false);

/// <summary>
/// UC32–UC35 — Auction &amp; Art Trade (14 endpoint).
///
/// Các endpoint đọc dữ liệu chợ (danh sách, chi tiết, lịch sử bid) cho phép khách xem.
/// Các thao tác thay đổi dữ liệu vẫn yêu cầu đăng nhập và quyền sở hữu tài nguyên
/// được kiểm tra trong handler, KHÔNG tin tham số từ client.
/// </summary>
[Authorize]
[Route("api/v1/auctions")]
public class AuctionsController : ApiControllerBase
{
    /// <summary>Danh sách phiên người dùng đang tham gia và tổng tiền cọc còn khóa.</summary>
    [HttpGet("my-active-bids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyActiveBids(
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.MyActiveBids.MyActiveBidsQuery(CurrentUserId, cursor, limit),
            cancellationToken);
        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Danh sách tranh người dùng đang sở hữu và chưa thuộc phiên còn hiệu lực.</summary>
    [HttpGet("eligible-artworks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetEligibleArtworks(
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, meta, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.EligibleArtworks.EligibleArtworksQuery(CurrentUserId, cursor, limit),
            cancellationToken);
        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC32 — Tạo / danh sách / chi tiết / sửa / huỷ
    // =================================================================

    /// <summary>Tải lên tệp Master File Pack (.zip, .rar, .7z, .psd) đính kèm phiên đấu giá (UC32).</summary>
    [HttpPost("upload-master-pack")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(150 * 1024 * 1024)] // 150MB
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UploadMasterPack(
        IFormFile file,
        [FromServices] IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequestEnvelope("Vui lòng chọn tệp Master File Pack.");
        }

        const long maxBytes = 150 * 1024 * 1024; // 150MB
        if (file.Length > maxBytes)
        {
            return BadRequestEnvelope("Kích thước tệp không được vượt quá 150MB.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".psd", ".clip", ".png", ".tif", ".tiff" };
        if (!allowedExtensions.Contains(ext))
        {
            return BadRequestEnvelope("Định dạng tệp không được hỗ trợ. Vui lòng tải lên tệp nén (.zip, .rar, .7z) hoặc tệp đồ họa gốc (.psd, .clip).");
        }

        var originalFileName = Path.GetFileName(file.FileName);
        string fileUrl;
        await using (var stream = file.OpenReadStream())
        {
            fileUrl = await fileStorage.SaveAsync(
                stream,
                originalFileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                $"auction-deliverables/{CurrentUserId}",
                cancellationToken);
        }

        var absoluteUrl = Uri.TryCreate(fileUrl, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"
            ? fileUrl
            : $"{Request.Scheme}://{Request.Host}{fileUrl}";

        return OkEnvelope(new
        {
            fileUrl = absoluteUrl,
            fileName = originalFileName,
            fileSizeBytes = file.Length
        });
    }

    /// <summary>Tạo phiên đấu giá cho một tranh mình đang sở hữu (UC32).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAuctionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors, conflict) = await Mediator.Send(
            new CreateAuctionCommand(
                CurrentUserId,
                request.ArtworkId,
                request.StartPrice,
                request.ReservePrice,
                request.BidStep,
                request.BuyNowPrice,
                request.StartAt,
                request.EndAt,
                request.MasterFileUrl,
                request.MasterFileName,
                request.MasterFileSizeBytes),
            cancellationToken);

        if (conflict)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    code = "AuctionArtworkConflict",
                    message = string.Join(" ", errors),
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Danh sách phiên đấu giá cho chợ, lọc và phân trang cursor (UC32).</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? sellerId,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? sort,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var (success, data, meta, errors) = await Mediator.Send(
            new GetAuctionsQuery(status, sellerId, minPrice, maxPrice, sort, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    /// <summary>Chi tiết phiên kèm lịch sử bid gần nhất (UC32).</summary>
    /// <remarks>
    /// Đặc tả API ghi output gồm cả <c>auction</c> và <c>recentBids</c>, nên hai phần
    /// này nằm CÙNG trong <c>data</c>. Trước đây <c>recentBids</c> bị đặt trong <c>meta</c>,
    /// mà <c>meta</c> theo quy ước chỉ chứa thông tin phân trang — FE đọc theo đặc tả
    /// sẽ không thấy dữ liệu.
    /// </remarks>
    [HttpGet("{auctionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetById(
        Guid auctionId,
        [FromQuery] int recentBids,
        CancellationToken cancellationToken)
    {
        var (success, data, recentBidList, errors) = await Mediator.Send(
            new GetAuctionByIdQuery(auctionId, CurrentUserId, recentBids),
            cancellationToken);

        if (!success)
        {
            return NotFoundEnvelope(errors);
        }

        return OkEnvelope(new { auction = data, recentBids = recentBidList ?? [] });
    }

    /// <summary>Sửa phiên khi chưa có bid và phiên còn ở trạng thái đã lên lịch (UC32).</summary>
    [HttpPut("{auctionId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid auctionId,
        [FromBody] UpdateAuctionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new UpdateAuctionCommand(
                CurrentUserId,
                auctionId,
                request.StartPrice,
                request.ReservePrice,
                request.BidStep,
                request.BuyNowPrice,
                request.StartAt,
                request.EndAt,
                request.ClearBuyNowPrice,
                request.ClearReservePrice),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Huỷ phiên chưa phát sinh bid (UC32).</summary>
    /// <remarks>
    /// Lý do huỷ nhận qua query-string, KHÔNG qua body: đặc tả chỉ ghi input là
    /// <c>auctionId</c>, và nhiều proxy/client bỏ body của request DELETE —
    /// khi đó lý do sẽ âm thầm mất.
    /// </remarks>
    [HttpDelete("{auctionId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(
        Guid auctionId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, errors) = await Mediator.Send(
            new DeleteAuctionCommand(CurrentUserId, auctionId, reason, User.IsInRole("Administrator")),
            cancellationToken);

        return success
            ? OkEnvelope(new { message = "Đã huỷ phiên đấu giá." })
            : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC32 — Theo dõi phiên
    // =================================================================

    /// <summary>Theo dõi phiên để nhận cảnh báo bị đè giá / sắp kết thúc (UC32).</summary>
    [HttpPost("{auctionId:guid}/watch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Watch(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new WatchAuctionCommand(CurrentUserId, auctionId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Bỏ theo dõi phiên (UC32).</summary>
    [HttpDelete("{auctionId:guid}/watch")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unwatch(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new UnwatchAuctionCommand(CurrentUserId, auctionId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC32 — Đặt giá
    // =================================================================

    /// <summary>Đặt giá; hệ thống khoá tiền cọc trong một transaction ACID (UC32).</summary>
    [HttpPost("{auctionId:guid}/bids")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PlaceBid(
        Guid auctionId,
        [FromBody] PlaceBidRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors, conflict) = await Mediator.Send(
            new PlaceBidCommand(CurrentUserId, auctionId, request.Amount, request.IsAuto, request.MaxAutoBid),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        if (conflict is not null)
        {
            return StatusCode(StatusCodes.Status409Conflict, new
            {
                data = conflict,
                meta = (object?)null,
                error = new
                {
                    code = "AuctionBidConflict",
                    message = string.Join(" ", errors),
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        return BadRequestEnvelope(errors);
    }

    /// <summary>Lịch sử đặt giá của phiên, cursor theo placed_at (UC32).</summary>
    [HttpGet("{auctionId:guid}/bids")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBids(
        Guid auctionId,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        var (success, data, meta, errors) = await Mediator.Send(
            new GetAuctionBidsQuery(auctionId, cursor, limit), cancellationToken);

        return success ? OkEnvelope(data, meta) : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC33 — Mua ngay
    // =================================================================

    /// <summary>Chấp nhận giá mua ngay để kết thúc phiên (UC33).</summary>
    [HttpPost("{auctionId:guid}/buy-now")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> BuyNow(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new BuyNowCommand(CurrentUserId, auctionId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    // =================================================================
    // UC35 — Chốt phiên, kết quả, tải file gốc, xử lý quá hạn
    // =================================================================

    /// <summary>Chốt phiên khi hết hạn — xác định winner và chuyển quyền sở hữu (UC35).</summary>
    [HttpPost("{auctionId:guid}/settle")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Settle(
        Guid auctionId,
        [FromBody] SettleAuctionRequest? request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var isAdministrator = User.IsInRole("Administrator");

        var (success, data, errors) = await Mediator.Send(
            new SettleAuctionCommand(
                CurrentUserId,
                auctionId,
                isAdministrator,
                request?.Force ?? false),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Xem kết quả chốt phiên và trạng thái thanh toán (UC35).</summary>
    [HttpGet("{auctionId:guid}/settlement")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSettlement(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetAuctionSettlementQuery(auctionId, CurrentUserId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Winner lấy link tải file gốc có hạn sau khi đã thanh toán (UC35).</summary>
    [HttpPost("{auctionId:guid}/settlement/download-url")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDownloadUrl(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, statusCode, errors) = await Mediator.Send(
            new GetDeliverableDownloadUrlQuery(CurrentUserId, auctionId), cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        // Handler phân biệt 403 (không phải winner) và 409 (chưa thanh toán) —
        // giữ đúng mã trạng thái thay vì gộp hết thành 400.
        return statusCode switch
        {
            403 => StatusCode(StatusCodes.Status403Forbidden, ApiEnvelopeError(errors)),
            409 => StatusCode(StatusCodes.Status409Conflict, ApiEnvelopeError(errors)),
            404 => NotFoundEnvelope(errors),
            _ => BadRequestEnvelope(errors)
        };
    }

    /// <summary>Danh sách tranh 1-of-1 người dùng đang sở hữu (My Collection / Exclusive 1-of-1 Vault).</summary>
    [HttpGet("my-collection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyCollection(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.GetMyCollection.GetMyCollectionQuery(CurrentUserId),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Lịch sử chuỗi sở hữu vĩnh viễn của tác phẩm (Provenance: Tác giả ➔ Collector A ➔ Collector B).</summary>
    [HttpGet("artworks/{artworkId:guid}/provenance")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtworkProvenance(Guid artworkId, CancellationToken cancellationToken)
    {
        var (success, data, statusCode, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.GetArtworkProvenance.GetArtworkProvenanceQuery(artworkId),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return statusCode == 404 ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>Văn bản pháp lý bàn giao toàn diện quyền thương mại (Commercial Rights Transfer Deed).</summary>
    [HttpGet("{auctionId:guid}/commercial-rights")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCommercialRights(Guid auctionId, CancellationToken cancellationToken)
    {
        var (success, data, statusCode, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.GetCommercialRightsDeed.GetCommercialRightsDeedQuery(auctionId),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return statusCode == 404 ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>Lấy chứng nhận sở hữu số 1-of-1 Certificate cho phiên đấu giá đã chốt (UC35).</summary>
    [HttpGet("{auctionId:guid}/certificate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCertificate(Guid auctionId, CancellationToken cancellationToken)
    {
        var (success, data, statusCode, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.GetAuctionCertificate.GetAuctionCertificateQuery(auctionId),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return statusCode switch
        {
            404 => NotFoundEnvelope(errors),
            _ => BadRequestEnvelope(errors)
        };
    }

    /// <summary>Xử lý winner quá hạn thanh toán — huỷ kết quả và đề xuất bidder kế tiếp (UC35).</summary>
    [HttpPost("{auctionId:guid}/settlement/expire")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExpireSettlement(Guid auctionId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new ExpireAuctionSettlementCommand(
                CurrentUserId,
                auctionId,
                User.IsInRole("Administrator")),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Dựng envelope lỗi cho các mã trạng thái không phải 400.</summary>
    private object ApiEnvelopeError(string[] errors) =>
        new
        {
            data = (object?)null,
            meta = (object?)null,
            error = new
            {
                code = "RequestFailed",
                message = string.Join(" ", errors),
                traceId = HttpContext.TraceIdentifier
            }
        };
}
