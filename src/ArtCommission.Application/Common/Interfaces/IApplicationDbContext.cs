using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Chat;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Entities.Revenue;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

// Alias bắt buộc: namespace ArtCommission.Application.Auction trùng tên với entity
// ArtCommission.Domain.Entities.Auction.Auction, nên tên trần "Auction" sẽ bind vào
// namespace chứ không phải kiểu entity.
using AuctionEntity = ArtCommission.Domain.Entities.Auction.Auction;
using BidEntity = ArtCommission.Domain.Entities.Auction.Bid;
using AuctionWatchEntity = ArtCommission.Domain.Entities.Auction.AuctionWatch;
using ArtworkOwnershipEntity = ArtCommission.Domain.Entities.Auction.ArtworkOwnership;
using EscrowTransactionEntity = ArtCommission.Domain.Entities.Auction.EscrowTransaction;
using DeliverableEntity = ArtCommission.Domain.Entities.Auction.Deliverable;

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
    DbSet<ArtworkFavorite> ArtworkFavorites { get; }
    DbSet<ArtworkComment> ArtworkComments { get; }
    DbSet<PersonalCollection> PersonalCollections { get; }
    DbSet<CollectionArtwork> CollectionArtworks { get; }
    DbSet<CommissionService> CommissionServices { get; }
    DbSet<CreatorReview> CreatorReviews { get; }
    DbSet<CreatorTerms> CreatorTerms { get; }
    DbSet<CreatorAutoReplySetting> CreatorAutoReplySettings { get; }
    DbSet<CreatorFaq> CreatorFaqs { get; }
    DbSet<CreatorWorkItem> CreatorWorkItems { get; }
    DbSet<CreatorAsset> CreatorAssets { get; }

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

    // Module Auction & Art Trade (UC32–UC35)
    DbSet<AuctionEntity> Auctions { get; }
    DbSet<BidEntity> Bids { get; }
    DbSet<AuctionWatchEntity> AuctionWatches { get; }
    DbSet<ArtworkOwnershipEntity> ArtworkOwnerships { get; }
    DbSet<EscrowTransactionEntity> EscrowTransactions { get; }
    DbSet<DeliverableEntity> Deliverables { get; }

    // Module Workroom Chat (UC43)
    DbSet<ChatRoom> ChatRooms { get; }
    DbSet<ChatRoomMember> ChatRoomMembers { get; }
    DbSet<MessageAttachment> MessageAttachments { get; }

    // Module AI Assistant (UC44 / UC46)
    DbSet<AiConversation> AiConversations { get; }
    DbSet<AiMessage> AiMessages { get; }
    DbSet<DeadlineReminder> DeadlineReminders { get; }
    DbSet<UserReminderSetting> UserReminderSettings { get; }

    // Module Creator Revenue Analytics (UC51)
    DbSet<RevenueSnapshot> RevenueSnapshots { get; }

    // Module Identity & User Sanctions (SCR-23 / UC31)
    DbSet<ApplicationUser> Users { get; }
    DbSet<UserSanction> UserSanctions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<T> Set<T>() where T : class;

    /// <summary>
    /// Truy cập change tracker cho một entity — cần khi phải bỏ theo dõi một entity
    /// sau lỗi ghi (ví dụ thua cuộc đua tạo bản ghi trùng unique index).
    /// AppDbContext đã có sẵn phương thức này nên tự thoả interface.
    /// </summary>
    Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Truy cập DatabaseFacade để mở transaction ACID
    /// (bắt buộc với mọi nghiệp vụ đụng tiền).
    /// </summary>
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
