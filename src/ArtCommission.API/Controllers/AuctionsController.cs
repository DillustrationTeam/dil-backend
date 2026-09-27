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
    DateTimeOffset EndAt);

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
/// Mọi endpoint đều yêu cầu đăng nhập. Quyền sở hữu tài nguyên (chỉ seller sửa phiên
/// của mình, chỉ winner tải file gốc) được kiểm tra trong handler, KHÔNG tin tham số
/// từ client.
/// </summary>
[Authorize]
[Route("api/v1/auctions")]
public class AuctionsController : ApiControllerBase
{
    // =================================================================
    // UC32 — Tạo / danh sách / chi tiết / sửa / huỷ
    // =================================================================

    /// <summary>Tạo phiên đấu giá cho một tranh mình đang sở hữu (UC32).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAuctionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new CreateAuctionCommand(
                CurrentUserId,
                request.ArtworkId,
                request.StartPrice,
                request.ReservePrice,
                request.BidStep,
                request.BuyNowPrice,
                request.StartAt,
                request.EndAt),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Danh sách phiên đấu giá cho chợ, lọc và phân trang cursor (UC32).</summary>
    [HttpGet]
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
            new DeleteAuctionCommand(CurrentUserId, auctionId, reason),
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

        var (success, data, errors) = await Mediator.Send(
            new PlaceBidCommand(CurrentUserId, auctionId, request.Amount, request.IsAuto, request.MaxAutoBid),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>Lịch sử đặt giá của phiên, cursor theo placed_at (UC32).</summary>
    [HttpGet("{auctionId:guid}/bids")]
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
