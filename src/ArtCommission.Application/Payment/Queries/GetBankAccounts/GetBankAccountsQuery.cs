using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Queries.GetBankAccounts;

/// <summary>
/// UC49 — GET /api/v1/bank-accounts
/// Creator lấy danh sách tài khoản ngân hàng đã lưu để chọn khi rút tiền.
/// </summary>
public record GetBankAccountsQuery(Guid UserId)
    : IRequest<(bool Success, IReadOnlyList<BankAccountDto>? Data, string[] Errors)>;

public class GetBankAccountsQueryHandler
    : IRequestHandler<GetBankAccountsQuery, (bool, IReadOnlyList<BankAccountDto>?, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetBankAccountsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<BankAccountDto>?, string[])> Handle(
        GetBankAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var accounts = await _db.BankAccounts
            .AsNoTracking()
            .Where(b => b.UserId == request.UserId && !b.IsDeleted)
            // Tài khoản mặc định lên đầu, sau đó mới nhất trước
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var dtos = accounts.Select(BankAccountMasker.ToDto).ToList();

        return (true, dtos, []);
    }
}
