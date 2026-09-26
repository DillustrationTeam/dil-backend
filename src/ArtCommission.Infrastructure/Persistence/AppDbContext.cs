using System.Reflection;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Chat;
using ArtCommission.Domain.Entities.CreatorApplication;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Entities.Revenue;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Module Artist Studio
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<Artwork> Artworks => Set<Artwork>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArtworkTag> ArtworkTags => Set<ArtworkTag>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<ArtworkFavorite> ArtworkFavorites => Set<ArtworkFavorite>();
    public DbSet<ArtworkComment> ArtworkComments => Set<ArtworkComment>();
    public DbSet<PersonalCollection> PersonalCollections => Set<PersonalCollection>();
    public DbSet<CollectionArtwork> CollectionArtworks => Set<CollectionArtwork>();
    public DbSet<CommissionService> CommissionServices => Set<CommissionService>();
    public DbSet<CreatorReview> CreatorReviews => Set<CreatorReview>();
    public DbSet<CreatorTerms> CreatorTerms => Set<CreatorTerms>();
    public DbSet<CreatorAutoReplySetting> CreatorAutoReplySettings => Set<CreatorAutoReplySetting>();
    public DbSet<CreatorFaq> CreatorFaqs => Set<CreatorFaq>();
    public DbSet<CreatorWorkItem> CreatorWorkItems => Set<CreatorWorkItem>();
    public DbSet<CreatorAsset> CreatorAssets => Set<CreatorAsset>();

    // Module Commission & Dispute
    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Dispute> Disputes => Set<Dispute>();

    // Module Payment & Wallet
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();
    public DbSet<PlatformConfig> PlatformConfigs => Set<PlatformConfig>();

    // Module Voucher
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<CreatorApplication> CreatorApplications => Set<CreatorApplication>();



    // Module Chat (UC26)
    public DbSet<ArtCommission.Domain.Entities.Chat.Message> Messages => Set<ArtCommission.Domain.Entities.Chat.Message>();

    // ---------------------------------------------------------------------
    // Module Auction & Art Trade (UC32–UC35)
    // ---------------------------------------------------------------------
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<AuctionWatch> AuctionWatches => Set<AuctionWatch>();
    public DbSet<ArtworkOwnership> ArtworkOwnerships => Set<ArtworkOwnership>();
    public DbSet<EscrowTransaction> EscrowTransactions => Set<EscrowTransaction>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();

    // ---------------------------------------------------------------------
    // Module Workroom Chat (UC43)
    // ---------------------------------------------------------------------
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<ChatRoomMember> ChatRoomMembers => Set<ChatRoomMember>();
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    // ---------------------------------------------------------------------
    // Module AI Assistant (UC44 chatbot, UC46 deadline risk)
    // ---------------------------------------------------------------------
    public DbSet<AiConversation> AiConversations => Set<AiConversation>();
    public DbSet<AiMessage> AiMessages => Set<AiMessage>();
    public DbSet<DeadlineReminder> DeadlineReminders => Set<DeadlineReminder>();
    public DbSet<UserReminderSetting> UserReminderSettings => Set<UserReminderSetting>();

    // ---------------------------------------------------------------------
    // Module Creator Revenue Analytics (UC51)
    // ---------------------------------------------------------------------
    public DbSet<RevenueSnapshot> RevenueSnapshots => Set<RevenueSnapshot>();

    // Module Identity & User Sanctions (SCR-23 / UC31)
    public DbSet<UserSanction> UserSanctions => Set<UserSanction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Database-First Table Name Mappings
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        // Apply Entity Configurations
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}