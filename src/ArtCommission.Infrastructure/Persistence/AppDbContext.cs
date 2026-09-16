using System.Reflection;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
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
