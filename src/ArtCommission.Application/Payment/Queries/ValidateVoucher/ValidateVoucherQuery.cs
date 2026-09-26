using ArtCommission.Application.Payment.Common;
using MediatR;

namespace ArtCommission.Application.Payment.Queries.ValidateVoucher;

/// <summary>
/// UC50 — POST /api/v1/vouchers/validate
/// Người dùng nhập mã ở bước checkout: hệ thống kiểm tra mã có áp được cho đơn này không.
///
/// CHỈ ĐỌC — không tiêu lượt, không ghi gì. Lượt chỉ bị tiêu ở <c>/vouchers/redeem</c>
/// khi giao dịch đã được xác nhận.
/// </summary>
public record ValidateVoucherQuery(
    Guid UserId,
    string VoucherCode,
    decimal OrderAmount,
    /// <summary>Commission / Auction / Deposit.</summary>
    string RefType,
    /// <summary>Id giao dịch — bỏ trống khi mới kiểm tra ở bước nhập mã.</summary>
    Guid? RefId = null
) : IRequest<(bool Success, VoucherValidationDto? Data, string[] Errors)>;

public class ValidateVoucherQueryHandler
    : IRequestHandler<ValidateVoucherQuery, (bool, VoucherValidationDto?, string[])>
{
    private readonly IVoucherCheckService _voucherCheck;

    public ValidateVoucherQueryHandler(IVoucherCheckService voucherCheck)
    {
        _voucherCheck = voucherCheck;
    }

    public async Task<(bool, VoucherValidationDto?, string[])> Handle(
        ValidateVoucherQuery request,
        CancellationToken cancellationToken)
    {
        var check = await _voucherCheck.CheckAsync(
            request.UserId,
            request.VoucherCode,
            request.OrderAmount,
            request.RefType,
            request.RefId,
            cancellationToken);

        // Không tra được mã / mã không dùng được vẫn là HTTP 200 với isValid = false:
        // đây là bước kiểm tra, không phải lỗi hệ thống — FE cần hiện lý do ngay dưới ô nhập.
        if (!check.Success)
        {
            return (true, new VoucherValidationDto(
                check.Voucher?.Id,
                false,
                0m,
                request.OrderAmount,
                check.Reason), []);
        }

        return (true, new VoucherValidationDto(
            check.Voucher!.Id,
            true,
            check.DiscountAmount,
            check.FinalAmount,
            null), []);
    }
}
