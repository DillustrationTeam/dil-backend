using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Sổ cái biến động số dư — APPEND ONLY.
/// Mỗi lần <see cref="Wallet.Balance"/> hoặc <see cref="Wallet.LockedBalance"/> đổi
/// PHẢI ghi đúng 1 dòng ở đây, kèm <see cref="BalanceAfter"/> để đối soát.
/// Không sửa, không xoá dòng đã ghi.
/// </summary>
public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }

    public WalletTransactionType Type { get; set; }

    public WalletTransactionDirection Direction { get; set; }

    /// <summary>Số tiền biến động — LUÔN dương. Chiều do <see cref="Direction"/> quyết định.</summary>
    public decimal Amount { get; set; }

    /// <summary>Số dư khả dụng sau khi áp dụng giao dịch này — phục vụ đối soát.</summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>Loại chứng từ gốc: "PaymentOrder", "PayoutRequest", "Auction", "Commission"...</summary>
    public string? RefType { get; set; }

    /// <summary>Id của chứng từ gốc tương ứng <see cref="RefType"/>.</summary>
    public Guid? RefId { get; set; }

    public string? Note { get; set; }

    // Navigation
    public Wallet? Wallet { get; set; }
}
