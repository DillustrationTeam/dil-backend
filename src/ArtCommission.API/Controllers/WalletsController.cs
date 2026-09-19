using ArtCommission.Application.Wallets.Queries.GetMyWallet;
using ArtCommission.Application.Wallets.Queries.GetMyWalletTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC47 — Ví sàn và lịch sử biến động số dư.
/// </summary>
[Authorize]
[Route("api/v1/wallets")]
public class WalletsController : ApiControllerBase
{
    /// <summary>
    /// Xem ví của mình: số dư khả dụng, tiền đang giữ escrow, tổng đã rút (UC47).
    /// </summary>
    /// <remarks>
    /// Người dùng chưa từng giao dịch sẽ nhận ví rỗng (số 0) thay vì lỗi 404,
    /// để trang /wallet hiển thị được ngay.
    /// </remarks>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetMyWalletQuery(CurrentUserId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Lịch sử biến động số dư của mình, có lọc và phân trang cursor (UC47).
    /// </summary>
    /// <remarks>
    /// Lọc được theo loại giao dịch, chiều tiền (In/Out), khoảng thời gian.
    /// Trả kèm `meta.total` là tổng số dòng khớp bộ lọc (trước khi phân trang).
    /// </remarks>
    [HttpGet("me/transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyTransactions(
        [FromQuery] string? walletTxType,
        [FromQuery] string? direction,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, page, total, errors) = await Mediator.Send(
            new GetMyWalletTransactionsQuery(
                UserId: CurrentUserId,
                WalletTxType: walletTxType,
                Direction: direction,
                From: from,
                To: to,
                Cursor: cursor,
                Limit: limit <= 0 ? 20 : limit),
            cancellationToken);

        if (!success || page is null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(page.Items, new { nextCursor = page.NextCursor, total });
    }
}
