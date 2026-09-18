using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.DeleteVoucher;

/// <summary>
/// UC50 — DELETE /api/v1/vouchers/{voucherId}
/// Vô hiệu hoá voucher bằng xoá MỀM. KHÔNG xoá cứng vì còn
/// <c>VoucherRedemption</c> tham chiếu tới — xoá cứng sẽ mất vết đối soát.
/// </summary>
public record DeleteVoucherCommand(Guid UserId, bool IsAdmin, Guid VoucherId)
    : IRequest<(bool Success, bool NotFound, string[] Errors)>;

public class DeleteVoucherCommandHandler
    : IRequestHandler<DeleteVoucherCommand, (bool, bool, string[])>
{
    private readonly IApplicationDbContext _db;

    public DeleteVoucherCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, bool, string[])> Handle(
        DeleteVoucherCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Vouchers.FirstOrDefaultAsync(
            v => v.Id == request.VoucherId && !v.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return (false, true, ["Không tìm thấy mã giảm giá."]);
        }

        var isOwner = entity.CreatedByUserId.HasValue && entity.CreatedByUserId == request.UserId;
        if (!request.IsAdmin && !isOwner)
        {
            return (false, true, ["Bạn không có quyền xoá mã giảm giá này."]);
        }

        entity.IsDeleted = true;
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, false, []);
    }
}
