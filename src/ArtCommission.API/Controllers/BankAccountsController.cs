using ArtCommission.Application.Payment.Commands.CreateBankAccount;
using ArtCommission.Application.Payment.Commands.DeleteBankAccount;
using ArtCommission.Application.Payment.Commands.UpdateBankAccount;
using ArtCommission.Application.Payment.Queries.GetBankAccounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC49 — Tài khoản ngân hàng nhận tiền của Creator (dùng khi rút tiền).
/// </summary>
[Authorize]
[Route("api/v1/bank-accounts")]
public class BankAccountsController : ApiControllerBase
{
    /// <summary>
    /// Danh sách tài khoản ngân hàng đã lưu (UC49).
    /// </summary>
    /// <remarks>Số tài khoản được che, chỉ lộ 4 ký tự cuối.</remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetBankAccountsQuery(CurrentUserId), cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Thêm tài khoản ngân hàng nhận tiền (UC49).
    /// </summary>
    /// <remarks>Tài khoản đầu tiên tự động trở thành mặc định.</remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            command with { UserId = CurrentUserId }, cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Sửa hoặc đặt mặc định một tài khoản ngân hàng (UC49).
    /// </summary>
    [HttpPut("{bankAccountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Update(
        Guid bankAccountId,
        [FromBody] UpdateBankAccountCommand command,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            command with { UserId = CurrentUserId, BankAccountId = bankAccountId },
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Xoá tài khoản ngân hàng không còn dùng (UC49) — xoá mềm.
    /// </summary>
    /// <remarks>Trả 409 nếu đang có yêu cầu rút tiền chờ duyệt dùng tài khoản này.</remarks>
    [HttpDelete("{bankAccountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid bankAccountId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, conflict, errors) = await Mediator.Send(
            new DeleteBankAccountCommand(CurrentUserId, bankAccountId), cancellationToken);

        if (success)
        {
            return OkEnvelope(new { message = "Đã xoá tài khoản ngân hàng." });
        }

        if (conflict)
        {
            return Conflict(new
            {
                data = (object?)null,
                meta = (object?)null,
                error = new { title = "Conflict", details = errors }
            });
        }

        return BadRequestEnvelope(errors);
    }
}
