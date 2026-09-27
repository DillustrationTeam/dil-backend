using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Commission.Disputes.Commands;

/// <summary>
/// Command thực thi phán quyết trọng tài và phân bổ tiền ký quỹ Escrow nguyên tử (SCR-22 / UC30).
/// </summary>
public record ResolveDisputeArbitrationCommand(
    Guid DisputeId,
    decimal ClientRefundPercent,
    string AdminNote,
    Guid? ModeratorId = null
) : IRequest<(bool Success, string Message, string[] Errors)>;

public class ResolveDisputeArbitrationCommandValidator : AbstractValidator<ResolveDisputeArbitrationCommand>
{
    public ResolveDisputeArbitrationCommandValidator()
    {
        RuleFor(x => x.DisputeId)
            .NotEmpty().WithMessage("Mã vụ tranh chấp (DisputeId) không được để trống.");

        RuleFor(x => x.ClientRefundPercent)
            .InclusiveBetween(0m, 100m)
            .WithMessage("Tỷ lệ hoàn tiền cho Client phải nằm trong khoảng từ 0% đến 100%.");

        RuleFor(x => x.AdminNote)
            .NotEmpty().WithMessage("Vui lòng nhập lý do/căn cứ phán quyết tranh chấp của trọng tài.")
            .MaximumLength(2000).WithMessage("Ghi chú phán quyết không được vượt quá 2000 ký tự.");
    }
}

public class ResolveDisputeArbitrationCommandHandler
    : IRequestHandler<ResolveDisputeArbitrationCommand, (bool Success, string Message, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;

    public ResolveDisputeArbitrationCommandHandler(IApplicationDbContext db, IWalletService walletService)
    {
        _db = db;
        _walletService = walletService;
    }

    public async Task<(bool Success, string Message, string[] Errors)> Handle(
        ResolveDisputeArbitrationCommand request,
        CancellationToken cancellationToken)
    {
        var validator = new ResolveDisputeArbitrationCommandValidator();
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return (false, string.Empty, validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        // Chạy trong 1 EF Core DB Transaction (IApplicationDbContext.Database.BeginTransactionAsync)
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var dispute = await _db.Disputes
                .Include(d => d.Commission)
                .FirstOrDefaultAsync(d => d.Id == request.DisputeId && !d.IsDeleted, cancellationToken);

            if (dispute == null)
            {
                return (false, string.Empty, new[] { "Không tìm thấy vụ tranh chấp tương ứng." });
            }

            if (dispute.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase))
            {
                return (false, string.Empty, new[] { "Vụ tranh chấp này đã được phân xử trước đó và đã đóng hồ sơ." });
            }

            var commission = dispute.Commission;
            var totalLockedEscrow = commission.EscrowHeldAmount;

            // Tính toán số tiền phân bổ Escrow
            var clientRefundAmount = Math.Round(totalLockedEscrow * (request.ClientRefundPercent / 100m), 2);
            var creatorGrossAmount = totalLockedEscrow - clientRefundAmount;

            decimal feePercent = await _walletService.GetDecimalConfigAsync(
                PlatformConfigKeys.PlatformFeePercent, 10.0m, cancellationToken);
            decimal platformFee = Math.Round(creatorGrossAmount * (feePercent / 100m), 2);
            decimal creatorPayAmount = Math.Max(0m, creatorGrossAmount - platformFee);

            if (totalLockedEscrow > 0)
            {
                var clientWallet = await _walletService.GetOrCreateWalletAsync(commission.ClientId, cancellationToken);
                var creatorWallet = await _walletService.GetOrCreateWalletAsync(commission.CreatorId, cancellationToken);

                // ------------------------------------------------------------------
                // SỬA 3 LỖI SỔ CÁI Ở KHỐI NÀY (trước đây làm ví Creator luôn lệch khi đối soát):
                //
                // 1. LockedBalance của Client bị trừ thẳng vào entity, KHÔNG ghi dòng sổ cái.
                //    VerifyLedgerConsistencyAsync tính LockedBalance từ sổ cái
                //    (EscrowHold − EscrowRelease − RefundFromHold) nên phần bị trừ tay này
                //    không bao giờ khớp. Nay dùng RefundHeldFundsAsync / ReleaseFundsAsync —
                //    mỗi hàm cập nhật LockedBalance VÀ ghi một dòng sổ cái trong cùng bước.
                //
                // 2. Tiền trả Creator ghi bằng EscrowRelease — sai chiều. EscrowRelease mang
                //    nghĩa "rời khỏi phần đang giữ của NGƯỜI TRẢ", nên công thức đối soát
                //    trừ nhầm LockedBalance của Creator (ví Creator vốn không có tiền đang giữ).
                //    Phía người nhận phải là EscrowReceive (chiều VÀO số dư khả dụng).
                //
                // 3. Phí sàn chỉ được trừ bằng lời trong ghi chú, không có dòng PlatformFee
                //    nào được ghi. Hệ quả: Creator nhận tiền gộp mà sổ cái không có phí,
                //    nên báo cáo doanh thu/phí của sàn thiếu hẳn phần này. Nay ghi đủ 2 dòng
                //    (EscrowReceive + PlatformFee) để mọi báo cáo cộng ra đúng.
                // ------------------------------------------------------------------

                // Escrow THỰC CÓ trong LockedBalance có thể nhỏ hơn EscrowHeldAmount trên đơn
                // (dữ liệu lệch từ lần xử lý trước, hoặc Admin sửa tay). Phân bổ theo số trên
                // đơn trong khi ví không đủ tiền sẽ TẠO RA tiền từ hư không, nên phải phân bổ
                // theo số thực có. RefundHeldFundsAsync cũng sẽ ném lỗi nếu vượt LockedBalance.
                var effectiveEscrow = Math.Min(clientWallet.LockedBalance, totalLockedEscrow);

                // Tính lại phân bổ theo số thực có, giữ nguyên tỉ lệ phán quyết.
                clientRefundAmount = Math.Round(effectiveEscrow * (request.ClientRefundPercent / 100m), 2);
                creatorGrossAmount = effectiveEscrow - clientRefundAmount;
                platformFee = Math.Round(creatorGrossAmount * (feePercent / 100m), 2);
                creatorPayAmount = Math.Max(0m, creatorGrossAmount - platformFee);

                // (1) Phần hoàn về Client: LockedBalance → Balance.
                if (clientRefundAmount > 0)
                {
                    await _walletService.RefundHeldFundsAsync(
                        clientWallet,
                        WalletTransactionType.RefundFromHold,
                        clientRefundAmount,
                        "Commission",
                        commission.Id,
                        $"Hoàn {request.ClientRefundPercent}% tiền cọc Escrow do giải quyết tranh chấp (Mã vụ: {dispute.Id}): {request.AdminNote}",
                        cancellationToken);
                }

                // (2) Phần trả Creator: rời hẳn khỏi LockedBalance của Client.
                if (creatorGrossAmount > 0)
                {
                    await _walletService.ReleaseFundsAsync(
                        clientWallet,
                        WalletTransactionType.EscrowRelease,
                        creatorGrossAmount,
                        "Commission",
                        commission.Id,
                        $"Giải ngân tiền cọc Escrow theo phán quyết tranh chấp (Mã vụ: {dispute.Id}): {request.AdminNote}",
                        cancellationToken);
                }

                // (3) Creator nhận khoản gộp — chiều VÀO số dư khả dụng.
                if (creatorGrossAmount > 0)
                {
                    await _walletService.CreditAsync(
                        creatorWallet,
                        WalletTransactionType.EscrowReceive,
                        creatorGrossAmount,
                        "Commission",
                        commission.Id,
                        $"Nhận tiền cọc Escrow theo phán quyết tranh chấp (Mã vụ: {dispute.Id}): {request.AdminNote}",
                        cancellationToken);
                }

                // (4) Phí sàn — chiều RA, ghi thành dòng riêng để báo cáo phí của sàn đủ số.
                if (platformFee > 0)
                {
                    await _walletService.DebitAsync(
                        creatorWallet,
                        WalletTransactionType.PlatformFee,
                        platformFee,
                        "Commission",
                        commission.Id,
                        $"Phí nền tảng {feePercent}% trên tiền giải ngân tranh chấp (Mã vụ: {dispute.Id})",
                        cancellationToken);
                }
            }

            // Cập nhật Commission.EscrowStatus sang Refunded / Released / Split
            if (request.ClientRefundPercent >= 100m)
            {
                commission.EscrowStatus = EscrowStatus.Refunded;
            }
            else if (request.ClientRefundPercent <= 0m)
            {
                commission.EscrowStatus = EscrowStatus.Released;
            }
            else
            {
                commission.EscrowStatus = EscrowStatus.Split;
            }

            // Cập nhật Commission.Status sang Completed hoặc Cancelled
            if (request.ClientRefundPercent <= 0m)
            {
                commission.Status = CommissionStatus.Completed;
            }
            else
            {
                commission.Status = CommissionStatus.Cancelled;
            }

            commission.EscrowHeldAmount = 0m;
            commission.DisbursedAmount += creatorPayAmount;
            commission.UpdatedAt = DateTimeOffset.UtcNow;

            // Cập nhật Dispute.Status = "Resolved", Resolution, ResolvedAt = DateTimeOffset.UtcNow
            string resolution = request.ClientRefundPercent >= 100m ? "ClientWin100"
                : request.ClientRefundPercent <= 0m ? "ArtistWin100"
                : $"Split_{request.ClientRefundPercent}_{100 - request.ClientRefundPercent}";

            dispute.Status = "Resolved";
            dispute.Resolution = resolution;
            dispute.ClientRefundAmount = clientRefundAmount;
            dispute.ArtistPayAmount = creatorPayAmount;
            dispute.AdminNote = request.AdminNote;
            dispute.ResolvedAt = DateTimeOffset.UtcNow;
            dispute.UpdatedAt = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            string summary = request.ClientRefundPercent >= 100m
                ? $"Phán quyết Client thắng 100%. Đã hoàn trả {clientRefundAmount:N0} VND vào ví của Client."
                : request.ClientRefundPercent <= 0m
                    ? $"Phán quyết Creator thắng 100%. Đã giải ngân {creatorPayAmount:N0} VND vào ví Creator (sau khi trừ {feePercent}% phí sàn {platformFee:N0} VND)."
                    : $"Phán quyết phân chia ({request.ClientRefundPercent}% - {100 - request.ClientRefundPercent}%). Đã hoàn {clientRefundAmount:N0} VND vào ví Client, giải ngân {creatorPayAmount:N0} VND vào ví Creator.";

            return (true, summary, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, string.Empty, new[] { $"Lỗi xử lý giao dịch phân xử tranh chấp: {ex.Message}" });
        }
    }
}
