using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Payment.Common;

/// <summary>
/// Nghiệp vụ ví dùng chung cho module Payment (nạp tiền, rút tiền, hoàn tiền).
/// Mọi thao tác đổi số dư đều đi qua đây để KHÔNG quên ghi sổ cái
/// và để mọi thay đổi nằm trong 1 transaction ACID.
/// </summary>
public interface IWalletService
{
    /// <summary>Lấy ví của user, tạo mới nếu chưa có.</summary>
    Task<Wallet> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Lấy ví để đọc số dư; trả null nếu user chưa có ví.</summary>
    Task<Wallet?> FindWalletAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Ghi 1 dòng sổ cái — gọi trong cùng transaction với thay đổi số dư.</summary>
    Task<WalletTransaction> RecordTransactionAsync(
        Wallet wallet,
        WalletTransactionType type,
        WalletTransactionDirection direction,
        decimal amount,
        string? refType,
        Guid? refId,
        string? note,
        CancellationToken cancellationToken = default);

    /// <summary>Đọc 1 giá trị cấu hình sàn, trả về mặc định nếu chưa cấu hình.</summary>
    Task<decimal> GetDecimalConfigAsync(string key, decimal defaultValue, CancellationToken cancellationToken = default);
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
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền giao dịch phải lớn hơn 0.");
        }

        var transaction = new WalletTransaction
        {
            WalletId = wallet.Id,
            Type = type,
            Direction = direction,
            Amount = amount,
            // Số dư khả dụng SAU khi áp dụng giao dịch — phục vụ đối soát
            BalanceAfter = wallet.Balance,
            RefType = refType,
            RefId = refId,
            Note = note
        };

        _db.WalletTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);

        return transaction;
    }

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
}
