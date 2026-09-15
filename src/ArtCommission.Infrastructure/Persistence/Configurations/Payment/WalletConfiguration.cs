using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallet");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Balance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(w => w.LockedBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(w => w.Currency)
            .HasMaxLength(3)
            .HasDefaultValue("VND")
            .IsRequired();

        builder.Property(w => w.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ArtCommission.Domain.Enums.WalletStatus.Active)
            .IsRequired();

        // Concurrency token: SQL Server rowversion — chống race condition khi nạp/rút tiền
        builder.Property(w => w.RowVersion)
            .IsRowVersion();

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        builder.Property(w => w.IsDeleted)
            .HasDefaultValue(false);

        // 1-1 với Users
        builder.HasIndex(w => w.UserId)
            .IsUnique()
            .HasDatabaseName("UX_Wallet_UserId");

        builder.HasOne(w => w.User)
            .WithOne()
            .HasForeignKey<Wallet>(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
