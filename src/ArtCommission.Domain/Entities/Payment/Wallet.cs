using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Payment;

/// <summary>
/// Ví của người dùng trên sàn. Quan hệ 1-1 với <see cref="ApplicationUser"/>.
/// Mọi biến động số dư phải đi kèm 1 dòng <see cref="WalletTransaction"/>.
/// </summary>
public class Wallet : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Số dư khả dụng — rút được, dùng để nạp escrow được.</summary>
    public decimal Balance { get; set; }

    /// <summary>Tiền đang bị giữ (escrow đơn vẽ, cọc đấu giá). Không rút được.</summary>
    public decimal LockedBalance { get; set; }

    /// <summary>Đơn vị tiền tệ. Hệ thống chỉ hỗ trợ VND.</summary>
    public string Currency { get; set; } = "VND";

    public WalletStatus Status { get; set; } = WalletStatus.Active;

    /// <summary>
    /// Concurrency token (SQL rowversion) — chống race condition khi nạp/rút tiền
    /// theo docs/03-database.md mục 3.3.
    /// </summary>
    public byte[]? RowVersion { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
    public ICollection<WalletTransaction> Transactions { get; set; } = [];
}
