using ArtCommission.Application.Payment.Commands.CreateVoucher;
using ArtCommission.Application.Payment.Commands.DeleteVoucher;
using ArtCommission.Application.Payment.Commands.RedeemVoucher;
using ArtCommission.Application.Payment.Commands.UpdateVoucher;
using ArtCommission.Application.Payment.Queries.GetVoucherById;
using ArtCommission.Application.Payment.Queries.GetVouchers;
using ArtCommission.Application.Payment.Queries.ValidateVoucher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArtCommission.API.Controllers;

/// <summary>
/// UC50 — Voucher (mã giảm giá).
///
/// Admin quản lý voucher toàn sàn; Creator quản lý voucher do mình tạo.
/// Người dùng thường chỉ gọi 2 endpoint <c>validate</c> (kiểm tra) và <c>redeem</c> (ghi nhận).
/// </summary>
[Authorize]
[Route("api/v1/vouchers")]
public class VouchersController : ApiControllerBase
{
    private bool IsAdmin => User.IsInRole("Administrator");

    /// <summary>
    /// Danh sách voucher (UC50).
    /// </summary>
    /// <remarks>
    /// Administrator thấy toàn bộ; các role khác chỉ thấy voucher do mình tạo.
    /// Lọc theo <c>voucherStatus</c> (Scheduled/Active/Expired/Disabled/UsedUp) và <c>discountType</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? voucherStatus,
        [FromQuery] string? discountType,
        [FromQuery] string? cursor,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new GetVouchersQuery(CurrentUserId, IsAdmin, voucherStatus, discountType, cursor, limit),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Chi tiết một voucher kèm thống kê đã dùng (UC50).
    /// </summary>
    [HttpGet("{voucherId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid voucherId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, notFound, data, errors) = await Mediator.Send(
            new GetVoucherByIdQuery(CurrentUserId, IsAdmin, voucherId), cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return notFound ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Tạo mã giảm giá (UC50).
    /// </summary>
    /// <remarks>
    /// Quyền: Administrator hoặc Creator.
    /// <c>scope</c> nhận mảng chuỗi: <c>["Commission","Auction","Deposit"]</c> — bỏ trống = áp dụng cho cả ba.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        if (!TryParseDate(request.StartDate, out var startDate))
        {
            return BadRequestEnvelope("Ngày bắt đầu không hợp lệ — cần định dạng yyyy-MM-dd.");
        }

        if (!TryParseDate(request.EndDate, out var endDate))
        {
            return BadRequestEnvelope("Ngày kết thúc không hợp lệ — cần định dạng yyyy-MM-dd.");
        }

        var (success, data, errors) = await Mediator.Send(
            new CreateVoucherCommand(
                CurrentUserId,
                IsAdmin,
                request.VoucherCode,
                request.Name,
                request.DiscountType,
                request.DiscountValue,
                request.MinOrderAmount,
                request.MaxDiscountAmount,
                request.Scope,
                startDate,
                endDate,
                request.UsageLimit),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Sửa cấu hình voucher (UC50).
    /// </summary>
    /// <remarks>Không sửa được <c>voucherCode</c> — mã đã phát ra cho người dùng.</remarks>
    [HttpPut("{voucherId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid voucherId,
        [FromBody] UpdateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        DateOnly? startDate = null;
        if (!string.IsNullOrWhiteSpace(request.StartDate))
        {
            if (!TryParseDate(request.StartDate, out var parsed))
            {
                return BadRequestEnvelope("Ngày bắt đầu không hợp lệ — cần định dạng yyyy-MM-dd.");
            }

            startDate = parsed;
        }

        DateOnly? endDate = null;
        if (!string.IsNullOrWhiteSpace(request.EndDate))
        {
            if (!TryParseDate(request.EndDate, out var parsed))
            {
                return BadRequestEnvelope("Ngày kết thúc không hợp lệ — cần định dạng yyyy-MM-dd.");
            }

            endDate = parsed;
        }

        var (success, notFound, data, errors) = await Mediator.Send(
            new UpdateVoucherCommand(
                CurrentUserId,
                IsAdmin,
                voucherId,
                request.Name,
                request.DiscountType,
                request.DiscountValue,
                request.MinOrderAmount,
                request.MaxDiscountAmount,
                request.Scope,
                startDate,
                endDate,
                request.UsageLimit,
                request.IsActive),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
        }

        return notFound ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Vô hiệu hoá voucher (UC50) — xoá mềm, giữ lại vết đã dùng.
    /// </summary>
    [HttpDelete("{voucherId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid voucherId, CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, notFound, errors) = await Mediator.Send(
            new DeleteVoucherCommand(CurrentUserId, IsAdmin, voucherId), cancellationToken);

        if (success)
        {
            return OkEnvelope(new { message = "Đã vô hiệu hoá mã giảm giá." });
        }

        return notFound ? NotFoundEnvelope(errors) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Kiểm tra mã giảm giá ở bước checkout (UC50).
    /// </summary>
    /// <remarks>
    /// CHỈ ĐỌC — không tiêu lượt. Mã không dùng được vẫn trả HTTP 200 với <c>isValid = false</c>
    /// kèm <c>reason</c> để hiển thị ngay dưới ô nhập.
    /// </remarks>
    [HttpPost("validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Validate(
        [FromBody] ValidateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, data, errors) = await Mediator.Send(
            new ValidateVoucherQuery(
                CurrentUserId,
                request.VoucherCode ?? string.Empty,
                request.OrderAmount,
                request.RefType ?? string.Empty,
                request.RefId),
            cancellationToken);

        return success ? OkEnvelope(data) : BadRequestEnvelope(errors);
    }

    /// <summary>
    /// Ghi nhận đã dùng voucher khi giao dịch được xác nhận (UC50).
    /// </summary>
    /// <remarks>
    /// Idempotent theo <c>(refType, refId)</c>: gọi lại cho cùng giao dịch sẽ trả về
    /// đúng bản ghi cũ, KHÔNG tiêu thêm lượt. Trả 409 khi giao dịch đã áp mã khác
    /// hoặc mã vừa hết lượt do request song song.
    /// </remarks>
    [HttpPost("redeem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentUserId == Guid.Empty)
        {
            return UnauthorizedEnvelope();
        }

        var (success, conflict, data, errors) = await Mediator.Send(
            new RedeemVoucherCommand(
                CurrentUserId,
                request.VoucherCode ?? string.Empty,
                request.RefType ?? string.Empty,
                request.RefId,
                request.OrderAmount),
            cancellationToken);

        if (success)
        {
            return OkEnvelope(data);
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

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out date);
}

// ---------------------------------------------------------------------------
// Request body — tách khỏi command để không cho client tự gửi userId / isAdmin.
// ---------------------------------------------------------------------------

/// <summary>Body của <c>POST /api/v1/vouchers</c>.</summary>
public record CreateVoucherRequest(
    string VoucherCode,
    string? Name,
    /// <summary><c>Percent</c> hoặc <c>Fixed</c>.</summary>
    string DiscountType,
    decimal DiscountValue,
    decimal MinOrderAmount,
    decimal? MaxDiscountAmount,
    /// <summary><c>["Commission","Auction","Deposit"]</c> — bỏ trống = cả ba.</summary>
    string[]? Scope,
    /// <summary>Định dạng <c>yyyy-MM-dd</c>.</summary>
    string StartDate,
    /// <summary>Định dạng <c>yyyy-MM-dd</c>.</summary>
    string EndDate,
    int? UsageLimit
);

/// <summary>Body của <c>PUT /api/v1/vouchers/{voucherId}</c> — field nào bỏ trống thì giữ nguyên.</summary>
public record UpdateVoucherRequest(
    string? Name,
    string? DiscountType,
    decimal? DiscountValue,
    decimal? MinOrderAmount,
    decimal? MaxDiscountAmount,
    string[]? Scope,
    string? StartDate,
    string? EndDate,
    int? UsageLimit,
    bool? IsActive
);

/// <summary>Body của <c>POST /api/v1/vouchers/validate</c>.</summary>
public record ValidateVoucherRequest(
    string? VoucherCode,
    decimal OrderAmount,
    /// <summary><c>Commission</c> / <c>Auction</c> / <c>Deposit</c>.</summary>
    string? RefType,
    Guid? RefId
);

/// <summary>Body của <c>POST /api/v1/vouchers/redeem</c>.</summary>
public record RedeemVoucherRequest(
    string? VoucherCode,
    /// <summary><c>Commission</c> / <c>Auction</c> / <c>Deposit</c>.</summary>
    string? RefType,
    Guid RefId,
    decimal OrderAmount
);
