using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// Alias bắt buộc: namespace chứa file này tên là ...Configurations.Auction,
// trùng tên với entity Auction nên tên trần sẽ bind vào namespace.
using AuctionEntity = ArtCommission.Domain.Entities.Auction.Auction;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Auction;

/// <summary>
/// Cấu hình EF Core cho module Auction &amp; Art Trade (UC32–UC35).
/// Đặt chung một file vì 6 bảng này chỉ có nghĩa khi đứng cạnh nhau —
/// đọc một chỗ là thấy hết ràng buộc toàn vẹn của module.
/// </summary>
public class AuctionConfiguration : IEntityTypeConfiguration<AuctionEntity>
{
    public void Configure(EntityTypeBuilder<AuctionEntity> builder)
    {
        builder.ToTable("Auctions");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AuctionType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AuctionType.Standard)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AuctionStatus.Scheduled)
            .IsRequired();

        // Tiền: luôn decimal, precision cố định để không mất phần lẻ khi làm tròn.
        builder.Property(a => a.StartPrice).HasPrecision(18, 2);
        builder.Property(a => a.ReservePrice).HasPrecision(18, 2);
        builder.Property(a => a.BidStep).HasPrecision(18, 2);
        builder.Property(a => a.BuyNowPrice).HasPrecision(18, 2);
        builder.Property(a => a.CurrentPrice).HasPrecision(18, 2);
        builder.Property(a => a.FinalPrice).HasPrecision(18, 2);

        builder.Property(a => a.BidCount).HasDefaultValue(0);

        builder.Property(a => a.CancelReason).HasMaxLength(500);

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).HasDefaultValue(false);

        builder.HasOne(a => a.Artwork)
            .WithMany()
            .HasForeignKey(a => a.ArtworkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Xoá user KHÔNG được xoá phiên đã thắng — chỉ bỏ liên kết người thắng.
        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.WinnerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Truy vấn nóng: chợ đấu giá lọc theo trạng thái rồi sắp theo giờ kết thúc.
        builder.HasIndex(a => new { a.Status, a.EndAt })
            .HasDatabaseName("IX_Auctions_Status_EndAt");

        builder.HasIndex(a => new { a.SellerId, a.Status })
            .HasDatabaseName("IX_Auctions_Seller_Status");

        builder.HasIndex(a => new { a.Status, a.CurrentPrice })
            .HasDatabaseName("IX_Auctions_Status_CurrentPrice");

        builder.HasIndex(a => a.ArtworkId)
            .HasDatabaseName("IX_Auctions_ArtworkId");
    }
}

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("Bids");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BidStatus.Leading)
            .IsRequired();

        builder.Property(b => b.HoldStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(HoldStatus.None)
            .IsRequired();

        builder.Property(b => b.Amount).HasPrecision(18, 2);
        builder.Property(b => b.HoldAmount).HasPrecision(18, 2);
        builder.Property(b => b.MaxAutoBid).HasPrecision(18, 2);

        builder.Property(b => b.PlacedAt).IsRequired();
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.IsDeleted).HasDefaultValue(false);

        builder.HasOne(b => b.Auction)
            .WithMany(a => a.Bids)
            .HasForeignKey(b => b.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Bidder)
            .WithMany()
            .HasForeignKey(b => b.BidderId)
            .OnDelete(DeleteBehavior.Restrict);

        // BẤT BIẾN TIỀN: mỗi phiên tối đa MỘT bid đang dẫn đầu.
        // Ép ở tầng DB vì nếu chỉ kiểm tra trong handler thì 2 request đồng thời
        // vẫn có thể cùng vượt qua kiểm tra và tạo 2 bid Leading ⇒ giữ tiền 2 người.
        builder.HasIndex(b => b.AuctionId)
            .IsUnique()
            .HasFilter("[Status] = 'Leading'")
            .HasDatabaseName("UX_Bids_OneLeadingPerAuction");

        // Cursor phân trang lịch sử bid theo placed_at.
        builder.HasIndex(b => new { b.AuctionId, b.PlacedAt })
            .HasDatabaseName("IX_Bids_Auction_PlacedAt");

        builder.HasIndex(b => b.BidderId)
            .HasDatabaseName("IX_Bids_BidderId");
    }
}

public class AuctionWatchConfiguration : IEntityTypeConfiguration<AuctionWatch>
{
    public void Configure(EntityTypeBuilder<AuctionWatch> builder)
    {
        builder.ToTable("AuctionWatches");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.NotifyOnOutbid).HasDefaultValue(true);
        builder.Property(w => w.NotifyEndingSoon).HasDefaultValue(true);
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.IsDeleted).HasDefaultValue(false);

        builder.HasOne(w => w.Auction)
            .WithMany(a => a.Watches)
            .HasForeignKey(w => w.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bấm theo dõi 2 lần không được sinh 2 dòng.
        builder.HasIndex(w => new { w.AuctionId, w.UserId })
            .IsUnique()
            .HasDatabaseName("UX_AuctionWatches_Auction_User");
    }
}

public class ArtworkOwnershipConfiguration : IEntityTypeConfiguration<ArtworkOwnership>
{
    public void Configure(EntityTypeBuilder<ArtworkOwnership> builder)
    {
        builder.ToTable("ArtworkOwnerships");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.TransferReason)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasDefaultValue(OwnershipTransferReason.AuctionWin)
            .IsRequired();

        builder.Property(o => o.AcquiredAt).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.ArtistStudio.Artwork>()
            .WithMany()
            .HasForeignKey(o => o.ArtworkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AuctionEntity>()
            .WithMany()
            .HasForeignKey(o => o.AuctionId)
            .OnDelete(DeleteBehavior.SetNull);

        // YÊU CẦU SCHEMA #3: mỗi tranh chỉ có MỘT chủ sở hữu hiện tại.
        // Index có filter — SQL Server không hỗ trợ unique một phần bằng check constraint.
        builder.HasIndex(o => o.ArtworkId)
            .IsUnique()
            .HasFilter("[IsCurrent] = 1")
            .HasDatabaseName("UX_ArtworkOwnerships_CurrentOwner");

        builder.HasIndex(o => new { o.OwnerId, o.IsCurrent })
            .HasDatabaseName("IX_ArtworkOwnerships_Owner_Current");
    }
}

public class EscrowTransactionConfiguration : IEntityTypeConfiguration<EscrowTransaction>
{
    public void Configure(EntityTypeBuilder<EscrowTransaction> builder)
    {
        builder.ToTable("EscrowTransactions");

        builder.HasKey(e => e.Id);

        // KHÔNG đặt HasDefaultValue cho Status: EscrowStatus bắt đầu từ 1 (Pending),
        // nên giá trị mặc định của CLR là 0 không phải hằng số hợp lệ. EF sẽ luôn dùng
        // default của DB cho mọi insert mang giá trị 0 và bỏ qua giá trị entity đặt —
        // đúng loại lỗi im lặng khó tìm. Handler luôn gán Status tường minh.
        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Amount).HasPrecision(18, 2);
        builder.Property(e => e.ReleasedAmount).HasPrecision(18, 2);
        builder.Property(e => e.RefundedAmount).HasPrecision(18, 2);
        builder.Property(e => e.FeeAmount).HasPrecision(18, 2);

        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasOne(e => e.Auction)
            .WithMany()
            .HasForeignKey(e => e.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.PayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.PayeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // YÊU CẦU SCHEMA #1: một phiên chỉ sinh một escrow ⇒ settle gọi lại
        // sẽ đụng unique index thay vì tạo bản ghi tiền thứ hai.
        builder.HasIndex(e => e.AuctionId)
            .IsUnique()
            .HasDatabaseName("UX_EscrowTransactions_Auction");
    }
}

public class DeliverableConfiguration : IEntityTypeConfiguration<Deliverable>
{
    public void Configure(EntityTypeBuilder<Deliverable> builder)
    {
        builder.ToTable("Deliverables");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();
        builder.Property(d => d.FileUrl).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.MimeType).HasMaxLength(120);
        builder.Property(d => d.IsDelivered).HasDefaultValue(false);
        builder.Property(d => d.DownloadCount).HasDefaultValue(0);
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.IsDeleted).HasDefaultValue(false);

        builder.HasOne(d => d.Auction)
            .WithMany()
            .HasForeignKey(d => d.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        // YÊU CẦU SCHEMA #2: mỗi phiên chỉ bàn giao file gốc một lần.
        builder.HasIndex(d => d.AuctionId)
            .IsUnique()
            .HasDatabaseName("UX_Deliverables_Auction");
    }
}
