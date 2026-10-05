using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Commands.UpdatePayoutRequestStatus;

/// <summary>
/// UC49 — PUT /api/v1/payout-requests/{payoutRequestId}/status
/// Admin duyệt (Processed) hoặc từ chối (Rejected). Từ chối thì HOÀN TIỀN về ví Creator.
/// Yêu cầu quyền Administrator.
/// </summary>
public record UpdatePayoutRequestStatusCommand(
    Guid AdminId,
    Guid PayoutRequestId,
    string PayoutStatus,
    string? PayoutNote = null,
    string? TransactionRef = null
) : IRequest<(bool Success, bool NotFound, PayoutRequestDto? Data, string[] Errors)>;

public class UpdatePayoutRequestStatusCommandValidator : AbstractValidator<UpdatePayoutRequestStatusCommand>
{
    public UpdatePayoutRequestStatusCommandValidator()
    {
        RuleFor(x => x.PayoutRequestId)
            .NotEmpty().WithMessage("Thiếu id yêu cầu rút tiền.");

        RuleFor(x => x.PayoutStatus)
            .NotEmpty().WithMessage("Thiếu trạng thái mới.")
            .Must(s => s.Equals(PayoutStatusNames.Processed, StringComparison.OrdinalIgnoreCase)
                       || s.Equals(PayoutStatusNames.Rejected, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ được chuyển sang Processed hoặc Rejected.");

        RuleFor(x => x.PayoutNote)
            .NotEmpty()
            .When(x => string.Equals(x.PayoutStatus, PayoutStatusNames.Rejected, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vui lòng nhập lý do từ chối.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        RuleFor(x => x.TransactionRef)
            .MaximumLength(100).WithMessage("Mã giao dịch tối đa 100 ký tự.");
    }
}

public class UpdatePayoutRequestStatusCommandHandler
    : IRequestHandler<UpdatePayoutRequestStatusCommand, (bool, bool, PayoutRequestDto?, string[])>
{
    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly IPayOsPayoutService _payOsPayoutService;

    public UpdatePayoutRequestStatusCommandHandler(
        IApplicationDbContext db,
        IWalletService walletService,
        IPayOsPayoutService payOsPayoutService)
    {
        _db = db;
        _walletService = walletService;
        _payOsPayoutService = payOsPayoutService;
    }

    public async Task<(bool, bool, PayoutRequestDto?, string[])> Handle(
        UpdatePayoutRequestStatusCommand request,
        CancellationToken cancellationToken)
    {
        var validation = new UpdatePayoutRequestStatusCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var targetStatus = Enum.Parse<PayoutStatus>(request.PayoutStatus, ignoreCase: true);

        // Toàn bộ nằm trong 1 transaction: đổi trạng thái + (nếu từ chối) hoàn tiền + ghi sổ cái
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        // Đọc lại trong transaction để 2 Admin bấm cùng lúc không hoàn tiền 2 lần
        var payout = await _db.PayoutRequests.FirstOrDefaultAsync(
            p => p.Id == request.PayoutRequestId && !p.IsDeleted,
            cancellationToken);

        if (payout is null)
        {
            return (false, true, null, ["Không tìm thấy yêu cầu rút tiền."]);
        }

        // IDEMPOTENT: đã xử lý rồi thì không trừ/hoàn thêm lần nữa
        if (payout.Status != PayoutStatus.Pending)
        {
            var existing = await LoadDtoAsync(payout, cancellationToken);
            return (false, false, existing,
                [$"Yêu cầu này đã ở trạng thái {payout.Status}, không thể xử lý lại."]);
        }

        BankAccount? bankAccount = null;

        if (targetStatus == PayoutStatus.Rejected)
        {
            var wallet = await _walletService.GetOrCreateWalletAsync(payout.UserId, cancellationToken);

            // HOÀN TIỀN về ví Creator — CreditAsync cộng Balance + ghi sổ cái
            await _walletService.CreditAsync(
                wallet,
                WalletTransactionType.Refund,
                payout.Amount,
                nameof(Domain.Entities.Payment.PayoutRequest),
                payout.Id,
                $"Hoàn tiền do từ chối yêu cầu rút: {request.PayoutNote}",
                cancellationToken);

            payout.RejectReason = request.PayoutNote;
            payout.PayoutNote = request.PayoutNote;
        }
        else
        {
            // Processed — Thực hiện chi tiền qua payOS hoặc ghi nhận mã giao dịch của Admin
            bankAccount = await _db.BankAccounts
                .FirstOrDefaultAsync(b => b.Id == payout.BankAccountId, cancellationToken);

            if (bankAccount is null)
            {
                return (false, false, null, ["Không tìm thấy tài khoản ngân hàng nhận tiền."]);
            }

            if (!string.IsNullOrWhiteSpace(request.TransactionRef))
            {
                // Admin đã chuyển tay ngoài đời hoặc cung cấp mã giao dịch thủ công
                payout.TransactionRef = request.TransactionRef;
            }
            else
            {
                // Tự động phát lệnh chi qua Kênh chi payOS
                var refId = $"PO{payout.Id.ToString()[..8].ToUpperInvariant()}";
                var desc = string.IsNullOrWhiteSpace(request.PayoutNote)
                    ? $"Payout {payout.Amount:N0}VND"
                    : request.PayoutNote.Trim();
                if (desc.Length > 25) desc = desc[..25];

                var payoutResult = await _payOsPayoutService.CreatePayoutAsync(
                    new PayOsPayoutRequest(
                        ReferenceId: refId,
                        Amount: (long)payout.Amount,
                        Description: desc,
                        ToBin: bankAccount.BankBin ?? "970418",
                        ToAccountNumber: bankAccount.AccountNumber
                    ),
                    cancellationToken);

                if (!payoutResult.Success)
                {
                    return (false, false, null,
                        [$"Cổng payOS từ chối lệnh chi tiền: {payoutResult.Message} (Mã lỗi: {payoutResult.ErrorCode ?? "N/A"})"]);
                }

                payout.TransactionRef = payoutResult.TransactionRef ?? refId;
            }

            if (!string.IsNullOrWhiteSpace(request.PayoutNote))
            {
                payout.PayoutNote = request.PayoutNote;
            }
        }

        payout.Status = targetStatus;
        payout.ProcessedBy = request.AdminId;
        payout.ProcessedAt = DateTimeOffset.UtcNow;
        payout.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        bankAccount = await _db.BankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == payout.BankAccountId, cancellationToken);

        return (true, false,
            ArtCommission.Application.Payment.Commands.CreatePayoutRequest
                .CreatePayoutRequestCommandHandler.MapToDto(payout, bankAccount),
            []);
    }

    private async Task<PayoutRequestDto?> LoadDtoAsync(
        Domain.Entities.Payment.PayoutRequest payout,
        CancellationToken cancellationToken)
    {
        var bankAccount = await _db.BankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == payout.BankAccountId, cancellationToken);

        return ArtCommission.Application.Payment.Commands.CreatePayoutRequest
            .CreatePayoutRequestCommandHandler.MapToDto(payout, bankAccount);
    }
}
