using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class PaymentOrderConfiguration : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(EntityTypeBuilder<PaymentOrder> builder)
    {
        builder.ToTable("PaymentOrder");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OrderRef)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.Gateway)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ArtCommission.Domain.Enums.PaymentOrderStatus.Pending)
            .IsRequired();

        builder.Property(o => o.PaymentLinkId)
            .HasMaxLength(100);

        builder.Property(o => o.CheckoutUrl)
            .HasMaxLength(500);

        builder.Property(o => o.QrCode)
            .HasMaxLength(1000);

        builder.Property(o => o.TransactionRef)
            .HasMaxLength(100);

        builder.Property(o => o.FailureReason)
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .IsRequired();

        builder.Property(o => o.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(o => o.OrderRef)
            .IsUnique()
            .HasDatabaseName("UX_PaymentOrder_OrderRef");

        // Mã đơn gửi sang cổng phải unique trong phạm vi 1 cổng
        builder.HasIndex(o => new { o.Gateway, o.PayOsOrderCode })
            .IsUnique()
            .HasDatabaseName("UX_PaymentOrder_Gateway_PayOsOrderCode");

        // IDEMPOTENCY KEY: 1 mã giao dịch của cổng chỉ được ghi nhận 1 lần
        builder.HasIndex(o => new { o.Gateway, o.TransactionRef })
            .IsUnique()
            .HasFilter("[TransactionRef] IS NOT NULL")
            .HasDatabaseName("UX_PaymentOrder_Gateway_TransactionRef");

        builder.HasIndex(o => new { o.UserId, o.CreatedAt })
            .HasDatabaseName("IX_PaymentOrder_UserId_CreatedAt")
            .IsDescending(false, true);

        builder.HasIndex(o => o.Status)
            .HasDatabaseName("IX_PaymentOrder_Status");

        builder.HasOne(o => o.Wallet)
            .WithMany()
            .HasForeignKey(o => o.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
