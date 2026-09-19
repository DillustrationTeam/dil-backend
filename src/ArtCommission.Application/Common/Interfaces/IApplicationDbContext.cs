using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArtCommission.Application.Common.Interfaces;

/// <summary>
/// Cổng truy cập dữ liệu cho tầng Application.
/// Lý do tồn tại: project Application KHÔNG tham chiếu Infrastructure
/// (xem docs/01-architecture.md — chiều phụ thuộc chỉ đi vào trong),
/// nên handler không thể inject thẳng AppDbContext.
/// AppDbContext implement interface này.
/// </summary>
public interface IApplicationDbContext
{
    // Module Artist Studio
    DbSet<CreatorProfile> CreatorProfiles { get; }
    DbSet<Artwork> Artworks { get; }
    DbSet<Tag> Tags { get; }
    DbSet<ArtworkTag> ArtworkTags { get; }
    DbSet<Follow> Follows { get; }

    // Module Payment & Wallet
    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }
    DbSet<PaymentOrder> PaymentOrders { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<PayoutRequest> PayoutRequests { get; }
    DbSet<PlatformConfig> PlatformConfigs { get; }

    // Module Voucher (UC50)
    DbSet<Voucher> Vouchers { get; }
    DbSet<VoucherRedemption> VoucherRedemptions { get; }

    // Module Notification (UC45)
    DbSet<Notification> Notifications { get; }

    DbSet<T> Set<T>() where T : class;

    /// <summary>
    /// Truy cập DatabaseFacade để mở transaction ACID
    /// (bắt buộc với mọi nghiệp vụ đụng tiền).
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
