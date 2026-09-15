using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.DeleteBankAccount;

/// <summary>
/// UC49 — DELETE /api/v1/bank-accounts/{bankAccountId}
/// Creator xoá tài khoản ngân hàng không còn dùng (xoá mềm qua IsDeleted).
/// Trả lỗi 409 nếu đang có PayoutRequest ở trạng thái Pending dùng tài khoản này.
/// </summary>
public record DeleteBankAccountCommand(Guid UserId, Guid BankAccountId)
    : IRequest<(bool Success, bool Conflict, string[] Errors)>;

public class DeleteBankAccountCommandHandler
    : IRequestHandler<DeleteBankAccountCommand, (bool, bool, string[])>
{
    private readonly IApplicationDbContext _db;

    public DeleteBankAccountCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, bool, string[])> Handle(
        DeleteBankAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _db.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == request.BankAccountId && b.UserId == request.UserId && !b.IsDeleted,
            cancellationToken);

        if (entity is null)
        {
            return (false, false, ["Không tìm thấy tài khoản ngân hàng."]);
        }

        // Đang có yêu cầu rút tiền chờ duyệt dùng tài khoản này => không cho xoá (409)
        var hasPendingPayout = await _db.PayoutRequests.AnyAsync(
            p => p.BankAccountId == entity.Id
                 && p.Status == PayoutStatus.Pending
                 && !p.IsDeleted,
            cancellationToken);

        if (hasPendingPayout)
        {
            return (false, true,
                ["Không thể xoá: đang có yêu cầu rút tiền chờ duyệt dùng tài khoản này."]);
        }

        entity.IsDeleted = true;
        entity.IsDefault = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, false, []);
    }
}
