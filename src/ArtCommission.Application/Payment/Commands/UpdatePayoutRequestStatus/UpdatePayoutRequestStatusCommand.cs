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

    public UpdatePayoutRequestStatusCommandHandler(IApplicationDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
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
            // Processed — Admin đã chuyển khoản thật, tiền đã bị trừ từ lúc tạo yêu cầu
            payout.TransactionRef = request.TransactionRef;
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
