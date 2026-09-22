using System.Reflection;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.CreatorApplication;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
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
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();

    // Module Artist Studio
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<Artwork> Artworks => Set<Artwork>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArtworkTag> ArtworkTags => Set<ArtworkTag>();
    public DbSet<Follow> Follows => Set<Follow>();

    // Module Payment & Wallet (UC47/UC48/UC49)
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<PayoutRequest> PayoutRequests => Set<PayoutRequest>();
    public DbSet<PlatformConfig> PlatformConfigs => Set<PlatformConfig>();

    // Module Voucher (UC50)
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<CreatorApplication> CreatorApplications => Set<CreatorApplication>();

    // Module Commission & Dispute (UC10/UC14/UC27/UC30)
    public DbSet<ArtCommission.Domain.Entities.Commission.Commission> Commissions => Set<ArtCommission.Domain.Entities.Commission.Commission>();
    public DbSet<ArtCommission.Domain.Entities.Commission.Dispute> Disputes => Set<ArtCommission.Domain.Entities.Commission.Dispute>();
    public DbSet<ArtCommission.Domain.Entities.Commission.Milestone> Milestones => Set<ArtCommission.Domain.Entities.Commission.Milestone>();
    public DbSet<ArtCommission.Domain.Entities.Commission.Review> Reviews => Set<ArtCommission.Domain.Entities.Commission.Review>();

    // Module Chat (UC26)
    public DbSet<ArtCommission.Domain.Entities.Chat.Message> Messages => Set<ArtCommission.Domain.Entities.Chat.Message>();

    // Module Identity & User Sanctions (SCR-23 / UC31)
    public DbSet<UserSanction> UserSanctions => Set<UserSanction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Database-First Table Name Mappings (Matching 01_auth_identity.sql)
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
