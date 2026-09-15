using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("WalletTransaction");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.Direction)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.BalanceAfter)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.RefType)
            .HasMaxLength(30);

        builder.Property(t => t.Note)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.IsDeleted)
            .HasDefaultValue(false);

        // Truy vấn lịch sử giao dịch: mới nhất trước
        builder.HasIndex(t => new { t.WalletId, t.CreatedAt })
            .HasDatabaseName("IX_WalletTransaction_WalletId_CreatedAt")
            .IsDescending(false, true);

        // Chống ghi trùng cùng một chứng từ cho cùng một ví (idempotency ở tầng DB)
        builder.HasIndex(t => new { t.RefType, t.RefId, t.Type })
            .IsUnique()
            .HasFilter("[RefId] IS NOT NULL")
            .HasDatabaseName("UX_WalletTransaction_Ref");

        builder.HasOne(t => t.Wallet)
            .WithMany(w => w.Transactions)
            .HasForeignKey(t => t.WalletId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
