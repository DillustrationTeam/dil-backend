using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.CreatePayoutRequest;

/// <summary>
/// UC49 — POST /api/v1/payout-requests
/// Creator gửi yêu cầu rút tiền từ số dư khả dụng về ngân hàng.
/// Hệ thống TRỪ VÍ NGAY trong transaction, hoàn lại nếu Admin từ chối.
/// </summary>
public record CreatePayoutRequestCommand(
    Guid UserId,
    decimal Amount,
    Guid BankAccountId,
    string? PayoutNote = null
) : IRequest<(bool Success, PayoutRequestDto? Data, decimal WalletBalance, string[] Errors)>;

public class CreatePayoutRequestCommandValidator : AbstractValidator<CreatePayoutRequestCommand>
{
    public CreatePayoutRequestCommandValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Số tiền rút phải lớn hơn 0.");

        RuleFor(x => x.BankAccountId)
            .NotEmpty().WithMessage("Vui lòng chọn tài khoản ngân hàng nhận tiền.");

        RuleFor(x => x.PayoutNote)
            .MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public class CreatePayoutRequestCommandHandler
    : IRequestHandler<CreatePayoutRequestCommand, (bool, PayoutRequestDto?, decimal, string[])>
{
    /// <summary>Số tiền rút tối thiểu mặc định khi Admin chưa cấu hình PlatformConfig.</summary>
    private const decimal DefaultMinPayoutAmount = 50_000m;

    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;

    public CreatePayoutRequestCommandHandler(IApplicationDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
    }

    public async Task<(bool, PayoutRequestDto?, decimal, string[])> Handle(
        CreatePayoutRequestCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new CreatePayoutRequestCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, 0m, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Một transaction ACID cho toàn bộ: đọc số dư -> trừ ví -> ghi sổ cái -> tạo yêu cầu
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        var bankAccount = await _db.BankAccounts.FirstOrDefaultAsync(
            b => b.Id == request.BankAccountId && b.UserId == request.UserId && !b.IsDeleted,
            cancellationToken);

        if (bankAccount is null)
        {
            return (false, null, 0m, ["Không tìm thấy tài khoản ngân hàng."]);
        }

        var wallet = await _walletService.GetOrCreateWalletAsync(request.UserId, cancellationToken);

        if (wallet.Status != WalletStatus.Active)
        {
            return (false, null, wallet.Balance, ["Ví đang bị khoá, không thể rút tiền."]);
        }

        var minPayout = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.MinPayoutAmount, DefaultMinPayoutAmount, cancellationToken);

        if (request.Amount < minPayout)
        {
            return (false, null, wallet.Balance,
                [$"Số tiền rút tối thiểu là {minPayout:N0} VND."]);
        }

        if (request.Amount > wallet.Balance)
        {
            return (false, null, wallet.Balance,
                ["Số dư khả dụng không đủ để thực hiện yêu cầu rút tiền."]);
        }

        // TRỪ VÍ NGAY (theo đúng spec) — không đợi Admin duyệt
        wallet.Balance -= request.Amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        var payoutRequest = new PayoutRequest
        {
            UserId = request.UserId,
            WalletId = wallet.Id,
            BankAccountId = bankAccount.Id,
            Amount = request.Amount,
            Status = PayoutStatus.Pending,
            PayoutNote = request.PayoutNote,
            BankAccountSnapshot = BankAccountMasker.BuildSnapshot(bankAccount)
        };

        _db.PayoutRequests.Add(payoutRequest);

        // Ghi sổ cái (BalanceAfter lấy sau khi đã trừ)
        await _walletService.RecordTransactionAsync(
            wallet,
            WalletTransactionType.Payout,
            WalletTransactionDirection.Out,
            request.Amount,
            nameof(PayoutRequest),
            payoutRequest.Id,
            $"Yêu cầu rút tiền về {bankAccount.BankName} {BankAccountMasker.Mask(bankAccount.AccountNumber)}",
            cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (true, MapToDto(payoutRequest, bankAccount), wallet.Balance, []);
    }

    internal static PayoutRequestDto MapToDto(PayoutRequest entity, BankAccount? bankAccount) => new(
        PayoutRequestId: entity.Id,
        Amount: entity.Amount,
        PayoutStatus: entity.Status.ToString(),
        PayoutNote: entity.PayoutNote,
        ProcessedBy: entity.ProcessedBy,
        CreatedAt: entity.CreatedAt,
        ProcessedAt: entity.ProcessedAt,
        TransactionRef: entity.TransactionRef,
        RejectReason: entity.RejectReason,
        BankAccount: bankAccount is null ? null : BankAccountMasker.ToDto(bankAccount)
    );
}
