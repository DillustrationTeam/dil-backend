using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Common;

/// <summary>
/// Nghiệp vụ ví dùng chung cho MỌI module đụng tiền.
/// Mọi thao tác đổi số dư đều đi qua đây để KHÔNG quên ghi sổ cái,
/// và để mọi thay đổi nằm trong 1 transaction ACID của caller.
///
/// Có 2 loại tiền trong ví:
///   - Balance       : số dư khả dụng — rút được, dùng để nạp escrow được
///   - LockedBalance : tiền đang bị giữ (escrow đơn vẽ, cọc đấu giá) — KHÔNG rút được
///
/// Bất biến: Balance >= 0 và LockedBalance >= 0 luôn luôn đúng.
/// </summary>
public interface IWalletService
{
    /// <summary>Lấy ví của user, tạo mới nếu chưa có.</summary>
    Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lấy ví để đọc số dư; trả null nếu user chưa có ví.</summary>
    Task<Wallet?> FindWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi 1 dòng sổ cái — gọi trong cùng transaction với thay đổi số dư.
    /// <paramref name="wallet"/> phải đã được cập nhật TRƯỚC khi gọi,
    /// vì hàm chụp lại <c>BalanceAfter</c> và <c>LockedBalanceAfter</c> tại thời điểm gọi.
    /// </summary>
    Task<WalletTransaction> RecordTransactionAsync(
        Wallet wallet,
        WalletTransactionType type,
        WalletTransactionDirection direction,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cộng tiền vào số dư khả dụng (nạp tiền, hoàn tiền về ví).
    /// </summary>
    Task<WalletTransaction> CreditAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trừ tiền khỏi số dư khả dụng (rút tiền, trừ phí).
    /// Ném <see cref="InvalidOperationException"/> nếu không đủ số dư.
    /// </summary>
    Task<WalletTransaction> DebitAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GIỮ TIỀN: chuyển <paramref name="amount"/> từ số dư khả dụng sang tiền đang giữ.
    /// Dùng cho: khoá escrow đơn vẽ, khoá tiền cọc khi đặt giá đấu giá.
    /// Ném <see cref="InvalidOperationException"/> nếu không đủ số dư khả dụng.
    /// </summary>
    Task<WalletTransaction> HoldFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GIẢI NGÂN: trừ tiền khỏi phần đang giữ và trả RA KHỎI ví
    /// (chuyển cho Creator, hoặc tiền rời hệ thống).
    /// Dùng cho: giải ngân mốc hoàn thành.
    /// </summary>
    Task<WalletTransaction> ReleaseFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// HOÀN TIỀN ĐANG GIỮ: trả tiền từ phần đang giữ về lại số dư khả dụng
    /// (huỷ đơn, bidder thua cuộc, winner quá hạn thanh toán...).
    ///
    /// KHÁC <see cref="CreditAsync"/>: tiền KHÔNG đến từ bên ngoài mà chuyển từ
    /// <c>LockedBalance</c> sang <c>Balance</c>. Sổ cái ghi loại
    /// <see cref="WalletTransactionType.RefundFromHold"/> để công thức đối soát
    /// vẫn đúng — nếu ghi nhầm thành <c>Refund</c> thì đối soát sẽ lệch.
    /// </summary>
    Task<WalletTransaction> RefundHeldFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>Đọc 1 giá trị cấu hình sàn, trả về mặc định nếu chưa cấu hình.</summary>
    Task<decimal> GetDecimalConfigAsync(string key, decimal defaultValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Đối soát: kiểm tra <c>Balance</c> và <c>LockedBalance</c> của ví có khớp sổ cái không.
    /// Trả về danh sách sai lệch — RỖNG nghĩa là khớp.
    /// Dùng cho job kiểm tra định kỳ hoặc test.
    /// </summary>
    Task<IReadOnlyList<string>> VerifyLedgerConsistencyAsync(
        Guid walletId,
        CancellationToken cancellationToken = default);
}

public class WalletService : IWalletService
{
    private readonly IApplicationDbContext _db;

    public WalletService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await _db.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new Wallet
        {
            UserId = userId,
            Balance = 0m,
            LockedBalance = 0m,
            Currency = "VND",
            Status = WalletStatus.Active
        };

        _db.Wallets.Add(wallet);
        await _db.SaveChangesAsync(cancellationToken);

        return wallet;
    }

    public Task<Wallet?> FindWalletAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Wallets.FirstOrDefaultAsync(w => w.UserId == userId && !w.IsDeleted, cancellationToken);

    // ---------------------------------------------------------------------
    // Ghi sổ cái
    // ---------------------------------------------------------------------

    public async Task<WalletTransaction> RecordTransactionAsync(
        Wallet wallet,
        WalletTransactionType type,
        WalletTransactionDirection direction,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Type = type,
            Direction = direction,
            Amount = amount,
            // Chụp lại cả 2 loại số dư SAU khi áp dụng giao dịch — phục vụ đối soát
            BalanceAfter = wallet.Balance,
            LockedBalanceAfter = wallet.LockedBalance,
            RefType = refType,
            RefId = refId,
            Note = note
        };

        _db.WalletTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return transaction;
    }

    // ---------------------------------------------------------------------
    // Tiền khả dụng
    // ---------------------------------------------------------------------

    public async Task<WalletTransaction> CreditAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);

        wallet.Balance += amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        return await RecordTransactionAsync(
            wallet, type, WalletTransactionDirection.In, amount, refType, refId, note, cancellationToken);
    }

    public async Task<WalletTransaction> DebitAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);
        EnsureAvailable(wallet, amount, "Số dư khả dụng không đủ.");

        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        return await RecordTransactionAsync(
            wallet, type, WalletTransactionDirection.Out, amount, refType, refId, note, cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Tiền đang giữ (escrow / cọc đấu giá)
    // ---------------------------------------------------------------------

    public async Task<WalletTransaction> HoldFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);
        EnsureAvailable(wallet, amount, "Số dư khả dụng không đủ để giữ tiền.");

        // Tiền rời số dư khả dụng, chuyển sang trạng thái đang giữ
        wallet.Balance -= amount;
        wallet.LockedBalance += amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        return await RecordTransactionAsync(
            wallet, type, WalletTransactionDirection.Out, amount, refType, refId, note, cancellationToken);
    }

    public async Task<WalletTransaction> ReleaseFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);
        EnsureLocked(wallet, amount, "Số tiền đang giữ không đủ để giải ngân.");

        // Tiền rời khỏi ví (trả cho người nhận) — không quay lại số dư khả dụng
        wallet.LockedBalance -= amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        return await RecordTransactionAsync(
            wallet, type, WalletTransactionDirection.Out, amount, refType, refId, note, cancellationToken);
    }

    public async Task<WalletTransaction> RefundHeldFundsAsync(
        Wallet wallet,
        WalletTransactionType type,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        EnsurePositive(amount);
        EnsureLocked(wallet, amount, "Số tiền đang giữ không đủ để hoàn lại.");

        // Tiền từ trạng thái giữ quay về số dư khả dụng
        wallet.LockedBalance -= amount;
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTimeOffset.UtcNow;

        return await RecordTransactionAsync(
            wallet, type, WalletTransactionDirection.In, amount, refType, refId, note, cancellationToken);
    }

    /// <summary>
    /// Chốt lại: kiểm tra 2 loại số dư của ví khớp với sổ cái.
    /// Dùng để đối soát định kỳ — trả về danh sách sai lệch (rỗng = khớp).
    /// </summary>
    public async Task<IReadOnlyList<string>> VerifyLedgerConsistencyAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        var wallet = await _db.Wallets
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == walletId, cancellationToken);

        if (wallet is null)
        {
            return [$"Không tìm thấy ví {walletId}."];
        }

        var sums = await _db.WalletTransactions
            .AsNoTracking()
            .Where(t => t.WalletId == walletId && !t.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Deposit = g.Where(t => t.Type == WalletTransactionType.Deposit)
                           .Sum(t => (decimal?)t.Amount) ?? 0m,
                CommissionEarning = g.Where(t => t.Type == WalletTransactionType.CommissionEarning)
                                     .Sum(t => (decimal?)t.Amount) ?? 0m,
                Refund = g.Where(t => t.Type == WalletTransactionType.Refund)
                          .Sum(t => (decimal?)t.Amount) ?? 0m,
                RefundFromHold = g.Where(t => t.Type == WalletTransactionType.RefundFromHold)
                                  .Sum(t => (decimal?)t.Amount) ?? 0m,
                Hold = g.Where(t => t.Type == WalletTransactionType.EscrowHold)
                        .Sum(t => (decimal?)t.Amount) ?? 0m,
                Release = g.Where(t => t.Type == WalletTransactionType.EscrowRelease)
                           .Sum(t => (decimal?)t.Amount) ?? 0m,
                Payout = g.Where(t => t.Type == WalletTransactionType.Payout)
                          .Sum(t => (decimal?)t.Amount) ?? 0m,
                Fee = g.Where(t => t.Type == WalletTransactionType.PlatformFee)
                       .Sum(t => (decimal?)t.Amount) ?? 0m,
                Adjustment = g.Where(t => t.Type == WalletTransactionType.Adjustment)
                              .Sum(t => (decimal?)t.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (sums is null)
        {
            return [];
        }

        var problems = new List<string>();

        // Tiền đang giữ: Hold làm tăng; EscrowRelease và RefundFromHold làm giảm.
        // Refund (từ ngoài) và Deposit KHÔNG đụng LockedBalance.
        var expectedLocked = sums.Hold - sums.Release - sums.RefundFromHold;
        if (expectedLocked != wallet.LockedBalance)
        {
            problems.Add(
                $"LockedBalance lệch: ví có {wallet.LockedBalance:N0}, sổ cái tính ra {expectedLocked:N0} " +
                $"(Hold {sums.Hold:N0} − Release {sums.Release:N0} − RefundFromHold {sums.RefundFromHold:N0}).");
        }

        // Số dư khả dụng: cộng tiền vào, trừ tiền ra.
        // Payout ở đây là tiền đã trừ lúc tạo yêu cầu; nếu Admin từ chối thì có thêm dòng Refund.
        var expectedBalance = sums.Deposit
                              + sums.CommissionEarning
                              + sums.Refund
                              + sums.RefundFromHold
                              + sums.Adjustment
                              - sums.Hold
                              - sums.Payout
                              - sums.Fee;

        if (expectedBalance != wallet.Balance)
        {
            problems.Add(
                $"Balance lệch: ví có {wallet.Balance:N0}, sổ cái tính ra {expectedBalance:N0}.");
        }

        return problems;
    }

    // ---------------------------------------------------------------------
    // Cấu hình sàn
    // ---------------------------------------------------------------------

    public async Task<decimal> GetDecimalConfigAsync(
        string key,
        decimal defaultValue,
        CancellationToken cancellationToken = default)
    {
        var config = await _db.PlatformConfigs
            .FirstOrDefaultAsync(c => c.Key == key && !c.IsDeleted, cancellationToken);

        if (config is null || string.IsNullOrWhiteSpace(config.Value))
        {
            return defaultValue;
        }

        return decimal.TryParse(
            config.Value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : defaultValue;
    }

    // ---------------------------------------------------------------------
    // Kiểm tra bất biến
    // ---------------------------------------------------------------------

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền giao dịch phải lớn hơn 0.");
        }
    }

    private static void EnsureAvailable(Wallet wallet, decimal amount, string message)
    {
        if (wallet.Balance < amount)
        {
            throw new InvalidOperationException(
                $"{message} Hiện có {wallet.Balance:N0}, cần {amount:N0} VND.");
        }
    }

    private static void EnsureLocked(Wallet wallet, decimal amount, string message)
    {
        if (wallet.LockedBalance < amount)
        {
            throw new InvalidOperationException(
                $"{message} Đang giữ {wallet.LockedBalance:N0}, cần {amount:N0} VND.");
        }
    }
}
