using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
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
    DbSet<ArtCommission.Domain.Entities.Payment.PlatformConfig> PlatformConfigs { get; }

    // Module Voucher (UC50)
    DbSet<Voucher> Vouchers { get; }
    DbSet<VoucherRedemption> VoucherRedemptions { get; }

    // Module Notification (UC45)
    DbSet<Notification> Notifications { get; }
    
    DbSet<ArtCommission.Domain.Entities.CreatorApplication.CreatorApplication> CreatorApplications { get; }

    // Module Commission & Dispute (UC10/UC14/UC27/UC30)
    DbSet<ArtCommission.Domain.Entities.Commission.Commission> Commissions { get; }
    DbSet<ArtCommission.Domain.Entities.Commission.Dispute> Disputes { get; }
    DbSet<ArtCommission.Domain.Entities.Commission.Milestone> Milestones { get; }
    DbSet<ArtCommission.Domain.Entities.Commission.Review> Reviews { get; }

    // Module Chat (UC26)
    DbSet<ArtCommission.Domain.Entities.Chat.Message> Messages { get; }

    // Module Identity & User Sanctions (SCR-23 / UC31)
    DbSet<ApplicationUser> Users { get; }
    DbSet<UserSanction> UserSanctions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<EmailVerificationCode> EmailVerificationCodes { get; }

    DbSet<T> Set<T>() where T : class;

    /// <summary>
    /// Truy cập DatabaseFacade để mở transaction ACID
    /// (bắt buộc với mọi nghiệp vụ đụng tiền).
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
