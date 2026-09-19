using System.Security.Claims;
using ArtCommission.Application.Payment.Commands.CreatePayoutRequest;
using ArtCommission.Application.Payment.Commands.UpdatePayoutRequestStatus;
using ArtCommission.Application.Payment.Queries.GetPayoutRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC49 — Yêu cầu rút tiền từ ví sàn về tài khoản ngân hàng.
/// </summary>
[Authorize]
[Route("api/v1/payout-requests")]
public class PayoutRequestsController : ApiControllerBase
{
    private const string AdministratorRole = "Administrator";

    /// <summary>Người gọi API có phải Administrator không.</summary>
    private bool IsAdmin => User.IsInRole(AdministratorRole);

    /// <summary>
    /// Tạo yêu cầu rút tiền (UC49).
    /// </summary>
    /// <remarks>
    /// Tiền bị TRỪ khỏi ví ngay khi tạo yêu cầu.
    /// Số tiền phải &gt;= MinPayoutAmount (cấu hình PlatformConfig) và &lt;= số dư khả dụng.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePayoutRequestCommand command,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, walletBalance, errors) = await Mediator.Send(
            command with { UserId = CurrentUserId }, cancellationToken);

        if (!success)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(new
        {
            payoutRequest = data,
            wallet = new { balance = walletBalance }
        });
    }

    /// <summary>
    /// Lịch sử yêu cầu rút tiền (UC49).
    /// </summary>
    /// <remarks>
    /// Creator chỉ thấy yêu cầu của mình.
    /// Administrator thấy toàn bộ hàng chờ, hoặc lọc theo <c>userId</c> của một Creator.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] Guid? userId,
        [FromQuery] string? cursor,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, page, errors) = await Mediator.Send(
            new GetPayoutRequestsQuery(
                RequesterId: CurrentUserId,
                IsAdmin: IsAdmin,
                Status: status,
                UserId: userId,
                Cursor: cursor,
                Limit: limit <= 0 ? 20 : limit),
            cancellationToken);

        if (!success || page is null)
        {
            return BadRequestEnvelope(errors);
        }

        return OkEnvelope(page.Items, new { nextCursor = page.NextCursor });
    }

    /// <summary>
    /// Admin duyệt hoặc từ chối yêu cầu rút tiền (UC49).
    /// </summary>
    /// <remarks>
    /// Chỉ Administrator. Truyền <c>payoutStatus</c> = Processed hoặc Rejected.
    /// Từ chối thì tiền được hoàn lại ví Creator trong cùng một transaction.
    /// </remarks>
    [HttpPut("{payoutRequestId:guid}/status")]
    [Authorize(Roles = AdministratorRole)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateStatus(
        Guid payoutRequestId,
        [FromBody] UpdatePayoutRequestStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, notFound, data, errors) = await Mediator.Send(
            command with { AdminId = CurrentUserId, PayoutRequestId = payoutRequestId },
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        if (notFound)
        {
            return NotFoundEnvelope(errors);
        }

        return BadRequestEnvelope(errors);
    }
}
