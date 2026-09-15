using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("PayoutRequest");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ArtCommission.Domain.Enums.PayoutStatus.Pending)
            .IsRequired();

        builder.Property(p => p.PayoutNote)
            .HasMaxLength(500);

        builder.Property(p => p.TransactionRef)
            .HasMaxLength(100);

        builder.Property(p => p.RejectReason)
            .HasMaxLength(500);

        builder.Property(p => p.BankAccountSnapshot)
            .HasMaxLength(1000);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(p => new { p.UserId, p.CreatedAt })
            .HasDatabaseName("IX_PayoutRequest_UserId_CreatedAt")
            .IsDescending(false, true);

        builder.HasIndex(p => p.Status)
            .HasDatabaseName("IX_PayoutRequest_Status");

        // Phục vụ check 409 khi xoá BankAccount đang có payout Pending
        builder.HasIndex(p => p.BankAccountId)
            .HasDatabaseName("IX_PayoutRequest_BankAccountId");

        builder.HasOne(p => p.BankAccount)
            .WithMany()
            .HasForeignKey(p => p.BankAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(p => p.WalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ArtCommission.Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
