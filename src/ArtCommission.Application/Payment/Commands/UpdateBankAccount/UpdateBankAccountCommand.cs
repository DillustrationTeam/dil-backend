using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.UpdateBankAccount;

/// <summary>
/// UC49 — PUT /api/v1/bank-accounts/{bankAccountId}
/// Creator sửa hoặc đặt mặc định một tài khoản ngân hàng.
/// </summary>
public record UpdateBankAccountCommand(
    Guid UserId,
    Guid BankAccountId,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    bool IsDefault = false,
    string? BankBin = null,
    string? BankCode = null
) : IRequest<(bool Success, BankAccountDto? Data, string[] Errors)>;

public class UpdateBankAccountCommandValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountCommandValidator()
    {
        RuleFor(x => x.BankAccountId)
            .NotEmpty().WithMessage("Thiếu id tài khoản ngân hàng.");

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

public class UpdateBankAccountCommandHandler
    : IRequestHandler<UpdateBankAccountCommand, (bool, BankAccountDto?, string[])>
{
    private readonly IApplicationDbContext _db;

    public UpdateBankAccountCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, BankAccountDto?, string[])> Handle(
        UpdateBankAccountCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new UpdateBankAccountCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var entity = await _db.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == request.BankAccountId && b.UserId == request.UserId && !b.IsDeleted,
            cancellationToken);

        if (entity is null)
        {
            return (false, null, ["Không tìm thấy tài khoản ngân hàng."]);
        }

        var accountNumber = request.AccountNumber.Trim();

        var duplicated = await _db.BankAccounts.AnyAsync(
            b => b.Id != entity.Id
                 && b.UserId == request.UserId
                 && b.AccountNumber == accountNumber
                 && b.BankName == request.BankName.Trim()
                 && !b.IsDeleted,
            cancellationToken);

        if (duplicated)
        {
            return (false, null, ["Đã có tài khoản ngân hàng khác trùng thông tin này."]);
        }

        if (request.IsDefault && !entity.IsDefault)
        {
            var currentDefaults = await _db.BankAccounts
                .Where(b => b.UserId == request.UserId && b.IsDefault && !b.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var account in currentDefaults)
            {
                account.IsDefault = false;
                account.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        entity.BankName = request.BankName.Trim();
        entity.AccountNumber = accountNumber;
        entity.AccountHolder = request.AccountHolder.Trim().ToUpperInvariant();
        entity.BankBin = request.BankBin?.Trim() ?? entity.BankBin;
        entity.BankCode = request.BankCode?.Trim() ?? entity.BankCode;
        entity.IsDefault = request.IsDefault || entity.IsDefault;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, BankAccountMasker.ToDto(entity), []);
    }
}
