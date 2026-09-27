using ArtCommission.Domain.Entities.Revenue;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Revenue;

/// <summary>
/// Cấu hình EF Core cho module Creator Revenue Analytics (UC51).
/// </summary>
public class RevenueSnapshotConfiguration : IEntityTypeConfiguration<RevenueSnapshot>
{
    public void Configure(EntityTypeBuilder<RevenueSnapshot> builder)
    {
        builder.ToTable("RevenueSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Scope)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(RevenueSnapshotScope.Daily)
            .IsRequired();

        builder.Property(s => s.GrossAmount).HasPrecision(18, 2);
        builder.Property(s => s.FeeAmount).HasPrecision(18, 2);
        builder.Property(s => s.NetAmount).HasPrecision(18, 2);
        builder.Property(s => s.CommissionGrossAmount).HasPrecision(18, 2);
        builder.Property(s => s.AuctionGrossAmount).HasPrecision(18, 2);

        builder.Property(s => s.CompletedOrderCount).HasDefaultValue(0);
        builder.Property(s => s.RebuildCount).HasDefaultValue(0);

        // Bắt buộc: DateOnly cần khai kiểu cột tường minh, nếu không EF map thành nvarchar
        // và mọi so sánh khoảng ngày sẽ sai thứ tự.
        builder.Property(s => s.SnapshotDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mỗi Creator chỉ có MỘT bản chốt cho mỗi (kỳ, ngày) — chốt lại là UPDATE,
        // không phải thêm dòng. Thiếu unique index thì rebuild chạy 2 lần sẽ nhân đôi doanh thu.
        builder.HasIndex(s => new { s.CreatorId, s.Scope, s.SnapshotDate })
            .IsUnique()
            .HasDatabaseName("UX_RevenueSnapshots_Creator_Scope_Date");

        // Danh sách snapshot theo khoảng ngày, cursor theo SnapshotDate.
        builder.HasIndex(s => new { s.Scope, s.SnapshotDate })
            .HasDatabaseName("IX_RevenueSnapshots_Scope_Date");
    }
}
