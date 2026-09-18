using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Queries.GetVoucherById;

/// <summary>Chi tiết voucher kèm thống kê đã dùng (UC50).</summary>
public record VoucherDetailDto(
    VoucherDto Voucher,
    int UsedCount,
    int? RemainingCount,
    IReadOnlyList<VoucherRedemptionDto> Redemptions
);

/// <summary>
/// UC50 — GET /api/v1/vouchers/{voucherId}
/// Xem chi tiết một voucher kèm danh sách lượt đã dùng.
/// Ai cũng xem được chi tiết (người mua cần xem mã mình sắp dùng);
/// riêng voucher của Creator khác thì chỉ Admin xem được.
/// </summary>
public record GetVoucherByIdQuery(Guid UserId, bool IsAdmin, Guid VoucherId)
    : IRequest<(bool Success, bool NotFound, VoucherDetailDto? Data, string[] Errors)>;

public class GetVoucherByIdQueryHandler
    : IRequestHandler<GetVoucherByIdQuery, (bool, bool, VoucherDetailDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetVoucherByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, bool, VoucherDetailDto?, string[])> Handle(
        GetVoucherByIdQuery request,
        CancellationToken cancellationToken)
    {
        var voucher = await _db.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == request.VoucherId && !v.IsDeleted, cancellationToken);

        if (voucher is null)
        {
            return (false, true, null, ["Không tìm thấy mã giảm giá."]);
        }

        // Voucher toàn sàn (Admin tạo) ai cũng xem được; voucher của Creator chỉ chủ sở hữu + Admin.
        var isPublicVoucher = !voucher.CreatedByUserId.HasValue;
        var isOwner = voucher.CreatedByUserId == request.UserId;

        if (!isPublicVoucher && !isOwner && !request.IsAdmin)
        {
            return (false, true, null, ["Bạn không có quyền xem mã giảm giá này."]);
        }

        var redemptions = await _db.VoucherRedemptions
            .AsNoTracking()
            .Where(r => r.VoucherId == voucher.Id)
            .OrderByDescending(r => r.RedeemedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var today = VoucherCheckService.TodayInVietnam();
        var dto = VoucherResponseMapper.ToDto(voucher, today, request.UserId);

        var detail = new VoucherDetailDto(
            dto,
            voucher.UsedCount,
            voucher.UsageLimit.HasValue ? Math.Max(0, voucher.UsageLimit.Value - voucher.UsedCount) : null,
            redemptions.Select(VoucherResponseMapper.ToDto).ToList());

        return (true, false, detail, []);
    }
}
