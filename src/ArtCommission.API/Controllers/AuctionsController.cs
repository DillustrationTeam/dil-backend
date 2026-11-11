using ArtCommission.Application.Auction.Commands.BuyNow;
using ArtCommission.Application.Auction.Commands.CreateAuction;
using ArtCommission.Application.Auction.Commands.DeleteAuction;
using ArtCommission.Application.Auction.Commands.Delivery;
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
using ArtCommission.Application.Auction.Queries.GetPhysicalDeliveries;
using SixLabors.ImageSharp;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>Thân request tạo phiên đấu giá — tách khỏi command để không lộ UserId ra ngoài.</summary>
public sealed record CreateAuctionRequest(
    Guid ArtworkId,
    decimal StartPrice,
    decimal BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    bool HasPhysicalCopy = false,
    string? PhysicalDescription = null);

/// <summary>Thân request người thắng yêu cầu nhận tranh vật lý.</summary>
public sealed record RequestPhysicalDeliveryRequest(
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string? NoteToSeller = null);

/// <summary>Thân request người bán cập nhật mã vận đơn & ảnh biên lai bưu cục.</summary>
public sealed record UpdateShippingInfoRequest(
    string CarrierName,
    string TrackingCode,
    string? TrackingUrl = null,
    string? ShippingReceiptUrl = null,
    string? PackageImageUrl = null);

/// <summary>Thân request người mua xác nhận đã nhận tranh vật lý.</summary>
public sealed record ConfirmDeliveryRequest(string? DeliveryReceiptUrl = null);

/// <summary>Thân request người mua khiếu nại giao nhận tranh.</summary>
public sealed record DisputeDeliveryRequest(string Reason, string? EvidenceUrl = null);

/// <summary>Thân request Admin phân xử khiếu nại giao nhận.</summary>
public sealed record ResolveDeliveryDisputeRequest(string Decision, string? Note = null);

/// <summary>Người mua đánh giá người bán sau khi giao dịch đấu giá đã khép lại (1–5 sao).</summary>
public sealed record RateAuctionSellerRequest(int Rating, string? Comment = null);

/// <summary>Thân request sửa phiên đấu giá.</summary>
public sealed record UpdateAuctionRequest(
    decimal? StartPrice,
    decimal? BidStep,
    decimal? BuyNowPrice,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    bool ClearBuyNowPrice = false);

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
                request.BidStep,
                request.BuyNowPrice,
                request.StartAt,
                request.EndAt,
                request.HasPhysicalCopy,
                request.PhysicalDescription),
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
                request.BidStep,
                request.BuyNowPrice,
                request.StartAt,
                request.EndAt,
                request.ClearBuyNowPrice),
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

    /// <summary>Danh sách tranh người dùng đang sở hữu (My Collection / Exclusive Vault).</summary>
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
            new ArtCommission.Application.Auction.Queries.GetCommercialRightsDeed.GetCommercialRightsDeedQuery(
                auctionId,
                CurrentUserId == Guid.Empty ? null : CurrentUserId,
                User.IsInRole("Administrator") || User.IsInRole("Admin")),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return statusCode switch
        {
            403 => StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = errors.FirstOrDefault() ?? "Bạn không có quyền xem văn bản này.",
                    traceId = HttpContext.TraceIdentifier
                }
            }),
            404 => NotFoundEnvelope(errors),
            _ => BadRequestEnvelope(errors)
        };
    }

    /// <summary>Lấy chứng nhận sở hữu số Certificate cho phiên đấu giá đã chốt (UC35).</summary>
    [HttpGet("{auctionId:guid}/certificate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCertificate(Guid auctionId, CancellationToken cancellationToken)
    {
        var (success, data, statusCode, errors) = await Mediator.Send(
            new ArtCommission.Application.Auction.Queries.GetAuctionCertificate.GetAuctionCertificateQuery(
                auctionId,
                CurrentUserId == Guid.Empty ? null : CurrentUserId,
                User.IsInRole("Administrator") || User.IsInRole("Admin")),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return statusCode switch
        {
            403 => StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = errors.FirstOrDefault() ?? "Báº¡n khÃ´ng cÃ³ quyá»n xem chá»©ng nháº­n nÃ y.",
                    traceId = HttpContext.TraceIdentifier
                }
            }),
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

    /// <summary>Người thắng yêu cầu nhận tranh vật lý và gửi thông tin nhận hàng (UC35).</summary>
    [HttpPost("{auctionId:guid}/delivery/request-physical")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RequestPhysicalDelivery(
        Guid auctionId,
        [FromBody] RequestPhysicalDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new RequestPhysicalDeliveryCommand(
                CurrentUserId,
                auctionId,
                request.RecipientName,
                request.RecipientPhone,
                request.ShippingAddress,
                request.NoteToSeller),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Người thắng từ chối nhận tranh vật lý, chuyển sang nhận file số (UC35).</summary>
    [HttpPost("{auctionId:guid}/delivery/decline-physical")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeclinePhysicalDelivery(
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, errors) = await Mediator.Send(
            new DeclinePhysicalDeliveryCommand(CurrentUserId, auctionId),
            cancellationToken);

        return success ? OkEnvelope(new { message = "Đã chuyển sang hình thức nhận file số." }) : BadRequestEnvelope(errors);
    }

    /// <summary>Người bán cập nhật mã vận đơn và ảnh biên lai gửi hàng bưu cục (UC35).</summary>
    [HttpPost("{auctionId:guid}/delivery/ship")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateShipping(
        Guid auctionId,
        [FromBody] UpdateShippingInfoRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new UpdateShippingInfoCommand(
                CurrentUserId,
                auctionId,
                request.CarrierName,
                request.TrackingCode,
                request.TrackingUrl,
                request.ShippingReceiptUrl,
                request.PackageImageUrl),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Người thắng phiên có bản vật lý chọn NHẬN TRANH ĐIỆN TỬ NGAY, bỏ bước giao nhận bưu cục.
    /// Ký quỹ được giải ngân ngay cho người bán và phiên đóng lại (UC35).
    /// </summary>
    [HttpPost("{auctionId:guid}/delivery/accept-digital")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AcceptDigitalDelivery(
        Guid auctionId,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new AcceptDigitalDeliveryCommand(CurrentUserId, auctionId),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Người mua xác nhận đã nhận tranh vật lý và hài lòng -> Nhả tiền cho người bán (UC35).</summary>
    [HttpPost("{auctionId:guid}/delivery/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ConfirmDelivery(
        Guid auctionId,
        [FromBody] ConfirmDeliveryRequest? request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new ConfirmPhysicalDeliveryCommand(
                CurrentUserId,
                auctionId,
                request?.DeliveryReceiptUrl),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Admin phân xử khiếu nại giao nhận: giải ngân cho người bán (release), hoàn tiền cho
    /// người mua và thu hồi quyền sở hữu (refund), hoặc đóng hồ sơ khi ký quỹ đã giải ngân
    /// nên không thể hoàn (close). Gọi `refund` trên phiên đã giải ngân sẽ tự hạ thành `close`.
    /// </summary>
    [HttpPost("{auctionId:guid}/delivery/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ResolveDeliveryDispute(
        Guid auctionId,
        [FromBody] ResolveDeliveryDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        if (!User.IsInRole("Administrator") && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "Chỉ Ban quản trị mới được phân xử khiếu nại giao nhận.",
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        var (success, data, errors) = await Mediator.Send(
            new ResolveDeliveryDisputeCommand(CurrentUserId, auctionId, request.Decision, request.Note),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Người mua khiếu nại bưu phẩm chưa nhận được hoặc bị hư hại (UC35).</summary>
    [HttpPost("{auctionId:guid}/delivery/dispute")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisputeDelivery(
        Guid auctionId,
        [FromBody] DisputeDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new DisputePhysicalDeliveryCommand(
                CurrentUserId,
                auctionId,
                request.Reason,
                request.EvidenceUrl),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Người thắng phiên đánh giá người bán (1–5 sao) sau khi giao dịch đã khép lại — đặc biệt
    /// cần khi ký quỹ đã giải ngân nên sàn không thể hoàn tiền, đánh giá là kênh phản ánh còn lại.
    /// </summary>
    [HttpPost("{auctionId:guid}/rate-seller")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RateSeller(
        Guid auctionId,
        [FromBody] RateAuctionSellerRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var (success, data, errors) = await Mediator.Send(
            new RateAuctionSellerCommand(CurrentUserId, auctionId, request.Rating, request.Comment),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Người thắng tải ảnh bằng chứng khiếu nại trực tiếp từ máy (UC35).
    /// Trả URL tuyệt đối để dùng làm `evidenceUrl` khi gửi khiếu nại.
    /// </summary>
    [HttpPost("{auctionId:guid}/delivery/dispute-evidence")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadDisputeEvidence(
        Guid auctionId,
        IFormFile file,
        [FromServices] IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var auction = await Mediator.Send(
            new GetAuctionByIdQuery(auctionId, CurrentUserId), cancellationToken);

        if (!auction.Success || auction.Data is null)
        {
            return NotFoundEnvelope(auction.Errors);
        }

        var isAdmin = User.IsInRole("Administrator") || User.IsInRole("Admin");
        if (!isAdmin && auction.Data.WinnerId != CurrentUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "Chỉ người thắng phiên (hoặc Ban quản trị) mới được tải bằng chứng khiếu nại.",
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequestEnvelope(["Vui lòng chọn ảnh bằng chứng."]);
        }

        const long maxBytes = 8 * 1024 * 1024; // 8MB
        if (file.Length > maxBytes)
        {
            return BadRequestEnvelope(["Ảnh bằng chứng tối đa 8MB."]);
        }

        try
        {
            await using var probe = file.OpenReadStream();
            var format = await Image.DetectFormatAsync(probe, cancellationToken);
            if (format is null || (format.Name is not ("JPEG" or "PNG" or "WEBP")))
            {
                return BadRequestEnvelope(["Chỉ nhận ảnh JPG, PNG hoặc WebP."]);
            }
        }
        catch (Exception)
        {
            return BadRequestEnvelope(["Tệp tải lên không phải ảnh hợp lệ."]);
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

        string fileUrl;
        await using (var stream = file.OpenReadStream())
        {
            fileUrl = await fileStorage.SaveAsync(
                stream,
                originalFileName,
                contentType,
                $"auction-disputes/{auctionId}",
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

    /// <summary>
    /// Người bán tải ảnh biên lai bưu cục / ảnh kiện hàng trực tiếp từ máy khi gửi tranh.
    /// `kind = receipt | package`. Trả URL tuyệt đối để dùng cho `shippingReceiptUrl` /
    /// `packageImageUrl` khi gọi endpoint ship.
    /// </summary>
    [HttpPost("{auctionId:guid}/delivery/ship-evidence")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadShippingEvidence(
        Guid auctionId,
        IFormFile file,
        [FromQuery] string? kind,
        [FromServices] IFileStorageService fileStorage,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty) return UnauthorizedEnvelope();

        var auction = await Mediator.Send(
            new GetAuctionByIdQuery(auctionId, CurrentUserId), cancellationToken);

        if (!auction.Success || auction.Data is null)
        {
            return NotFoundEnvelope(auction.Errors);
        }

        var isAdmin = User.IsInRole("Administrator") || User.IsInRole("Admin");
        if (!isAdmin && auction.Data.Seller?.UserId != CurrentUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "Chỉ người bán của phiên mới được tải ảnh biên lai / kiện hàng.",
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        var folder = string.Equals(kind, "package", StringComparison.OrdinalIgnoreCase)
            ? "auction-shipping-packages"
            : "auction-shipping-receipts";

        if (file is null || file.Length == 0)
        {
            return BadRequestEnvelope(["Vui lòng chọn ảnh."]);
        }

        const long maxBytes = 8 * 1024 * 1024; // 8MB
        if (file.Length > maxBytes)
        {
            return BadRequestEnvelope(["Ảnh tối đa 8MB."]);
        }

        try
        {
            await using var probe = file.OpenReadStream();
            var format = await Image.DetectFormatAsync(probe, cancellationToken);
            if (format is null || (format.Name is not ("JPEG" or "PNG" or "WEBP")))
            {
                return BadRequestEnvelope(["Chỉ nhận ảnh JPG, PNG hoặc WebP."]);
            }
        }
        catch (Exception)
        {
            return BadRequestEnvelope(["Tệp tải lên không phải ảnh hợp lệ."]);
        }

        var originalFileName = Path.GetFileName(file.FileName);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;

        string fileUrl;
        await using (var stream = file.OpenReadStream())
        {
            fileUrl = await fileStorage.SaveAsync(
                stream,
                originalFileName,
                contentType,
                $"{folder}/{auctionId}",
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

    [HttpGet("deliveries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDeliveries(
        [FromQuery] string? role,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var isAdmin = User.IsInRole("Administrator") || User.IsInRole("Admin");
        var (success, data, errors) = await Mediator.Send(
            new GetPhysicalDeliveriesQuery(
                CurrentUserId == Guid.Empty ? null : CurrentUserId,
                isAdmin,
                role,
                status),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Xem toàn bộ danh sách biên lai giao dịch của phiên (Ký quỹ, Bưu cục, Quyết toán) (UC35).</summary>
    [HttpGet("{auctionId:guid}/receipts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipts(Guid auctionId, CancellationToken cancellationToken)
    {
        // BiÃªn lai chá»©a sá»‘ tiá»n vÃ  thÃ´ng tin hai bÃªn: chá»‰ ngÆ°á»i trong cuá»™c hoáº·c Admin.
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, _, errors) = await Mediator.Send(
            new GetAuctionByIdQuery(auctionId, CurrentUserId), cancellationToken);

        if (!success || data is null)
        {
            return NotFoundEnvelope(errors);
        }

        // NgÆ°á»i ngoÃ i nháº­n 403 thay vÃ¬ 200 + máº£ng rá»—ng: biÃªn lai lÃ  dá»¯ liá»‡u tÃ i chÃ­nh.
        var isAdmin = User.IsInRole("Administrator") || User.IsInRole("Admin");
        var isParticipant = data.WinnerId == CurrentUserId || data.Seller?.UserId == CurrentUserId;
        if (!isAdmin && !isParticipant)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new
                {
                    type = "https://httpstatuses.com/403",
                    title = "Forbidden",
                    status = 403,
                    detail = "Chá»‰ ngÆ°á»i tháº¯ng, ngÆ°á»i bÃ¡n hoáº·c Ban quáº£n trá»‹ má»›i xem Ä‘Æ°á»£c biÃªn lai cá»§a phiÃªn nÃ y.",
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }

        return OkEnvelope(data.Receipts ?? []);
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
