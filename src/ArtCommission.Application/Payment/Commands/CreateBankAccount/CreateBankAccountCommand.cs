using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.CreateBankAccount;

/// <summary>
/// UC49 — POST /api/v1/bank-accounts
/// Creator thêm tài khoản ngân hàng nhận tiền.
/// </summary>
public record CreateBankAccountCommand(
    Guid UserId,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault = false,
    string? BankBin = null,
    string? BankCode = null
) : IRequest<(bool Success, BankAccountDto? Data, string[] Errors)>;

public class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator()
    {
        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("Vui lòng nhập tên ngân hàng.")
            .MaximumLength(200).WithMessage("Tên ngân hàng tối đa 200 ký tự.");

        RuleFor(x => x.AccountNumber)
            .NotEmpty().WithMessage("Vui lòng nhập số tài khoản.")
            .MaximumLength(50).WithMessage("Số tài khoản tối đa 50 ký tự.")
            .Matches(@"^[0-9A-Za-z\-]+$")
            .WithMessage("Số tài khoản chỉ được chứa chữ, số và dấu gạch ngang.");

        RuleFor(x => x.AccountHolder)
            .NotEmpty().WithMessage("Vui lòng nhập tên chủ tài khoản.")
            .MaximumLength(200).WithMessage("Tên chủ tài khoản tối đa 200 ký tự.");
    }
}

public class CreateBankAccountCommandHandler
    : IRequestHandler<CreateBankAccountCommand, (bool, BankAccountDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public CreateBankAccountCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, BankAccountDto?, string[])> Handle(
        CreateBankAccountCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreateBankAccountCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var accountNumber = request.AccountNumber.Trim();
        var accountHolder = request.AccountHolder.Trim().ToUpperInvariant();

        var duplicated = await _db.BankAccounts.AnyAsync(
            b => b.UserId == request.UserId
                 && b.AccountNumber == accountNumber
                 && b.BankName == request.BankName.Trim()
                 && !b.IsDeleted,
            cancellationToken);

        if (duplicated)
        {
            return (false, null, ["Tài khoản ngân hàng này đã được lưu trước đó."]);
        }

        // Chỉ được có 1 tài khoản mặc định: nếu đang có TK mặc định khác thì hạ cờ trước.
        // Toàn bộ nằm chung 1 SaveChanges để không có khoảng thời gian 2 TK cùng mặc định.
        var existingAccounts = await _db.BankAccounts
            .Where(b => b.UserId == request.UserId && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        var isFirstAccount = existingAccounts.Count == 0;
        var shouldBeDefault = request.IsDefault || isFirstAccount;

        if (shouldBeDefault)
        {
            foreach (var account in existingAccounts.Where(a => a.IsDefault))
            {
                account.IsDefault = false;
                account.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var entity = new BankAccount
        {
            UserId = request.UserId,
            BankName = request.BankName.Trim(),
            BankBin = request.BankBin?.Trim(),
            BankCode = request.BankCode?.Trim(),
            AccountNumber = accountNumber,
            AccountHolder = accountHolder,
            IsDefault = shouldBeDefault,
            IsVerified = false
        };

        _db.BankAccounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, BankAccountMasker.ToDto(entity), []);
    }
}
