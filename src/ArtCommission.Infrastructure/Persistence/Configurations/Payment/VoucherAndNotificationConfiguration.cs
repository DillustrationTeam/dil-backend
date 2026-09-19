using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Voucher");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VoucherCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(v => v.Name)
            .HasMaxLength(200);

        // Lưu enum dạng chuỗi để đọc DB bằng mắt vẫn hiểu, giống các bảng Payment khác.
        builder.Property(v => v.DiscountType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.Scope)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(v => v.DiscountValue)
            .HasPrecision(18, 2);

        builder.Property(v => v.MinOrderAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.Property(v => v.MaxDiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(v => v.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(v => v.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(v => v.UsedCount)
            .HasDefaultValue(0);

        builder.Property(v => v.IsActive)
            .HasDefaultValue(true);

        builder.Property(v => v.CreatedAt)
            .IsRequired();

        builder.Property(v => v.IsDeleted)
            .HasDefaultValue(false);

        // Mã voucher không được trùng — kể cả voucher đã xoá mềm, vì người dùng
        // vẫn có thể đang gõ lại mã cũ và cần nhận lỗi rõ ràng thay vì trúng voucher khác.
        builder.HasIndex(v => v.VoucherCode)
            .IsUnique()
            .HasDatabaseName("UX_Voucher_VoucherCode");

        builder.HasIndex(v => new { v.IsActive, v.StartDate, v.EndDate })
            .HasDatabaseName("IX_Voucher_Active_Window");

        builder.HasIndex(v => v.IsDeleted)
            .HasDatabaseName("IX_Voucher_IsDeleted");

        builder.HasIndex(v => v.CreatedByUserId)
            .HasDatabaseName("IX_Voucher_CreatedByUserId");

        // Voucher do Creator tạo: xoá user thì bỏ liên kết, KHÔNG xoá voucher.
        builder.HasOne<ArtCommission.Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.Redemptions)
            .WithOne(r => r.Voucher!)
            .HasForeignKey(r => r.VoucherId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("VoucherRedemption");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RefType)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(r => r.DiscountAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.OrderAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.FinalAmount)
            .HasPrecision(18, 2);

        builder.Property(r => r.RedeemedAt)
            .IsRequired();

        // Một giao dịch chỉ được áp ĐÚNG 1 voucher — chặn gọi redeem 2 lần cho cùng refId.
        builder.HasIndex(r => new { r.RefType, r.RefId })
            .IsUnique()
            .HasDatabaseName("UX_VoucherRedemption_Ref");

        builder.HasIndex(r => new { r.VoucherId, r.UserId })
            .HasDatabaseName("IX_VoucherRedemption_Voucher_User");

        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_VoucherRedemption_UserId");

        builder.HasOne<ArtCommission.Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.NotificationType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.NotificationTitle)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(n => n.Body)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(n => n.RefType)
            .HasMaxLength(30);

        builder.Property(n => n.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(NotificationChannel.InApp)
            .IsRequired();

        builder.Property(n => n.IsRead)
            .HasDefaultValue(false);

        builder.Property(n => n.DedupKey)
            .HasMaxLength(200);

        builder.Property(n => n.FailedReason)
            .HasMaxLength(500);

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.Property(n => n.IsDeleted)
            .HasDefaultValue(false);

        // Truy vấn nóng nhất: "thông báo của tôi, mới nhất trước" + đếm chưa đọc.
        builder.HasIndex(n => new { n.UserId, n.CreatedAt })
            .HasDatabaseName("IX_Notification_UserId_CreatedAt");

        builder.HasIndex(n => new { n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notification_UserId_IsRead");

        // Chống gửi trùng: chỉ unique khi DedupKey có giá trị.
        builder.HasIndex(n => new { n.UserId, n.DedupKey })
            .IsUnique()
            .HasFilter("[DedupKey] IS NOT NULL")
            .HasDatabaseName("UX_Notification_UserId_DedupKey");

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
