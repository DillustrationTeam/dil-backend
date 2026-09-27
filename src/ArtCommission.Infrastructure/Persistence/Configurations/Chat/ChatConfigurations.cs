using ArtCommission.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Chat;

/// <summary>
/// Cấu hình EF Core cho module Workroom Chat (UC43).
///
/// LƯU Ý QUAN TRỌNG VỀ <see cref="Message.MessageType"/>:
/// cột này đã tồn tại trong DB với kiểu số (enum lưu dạng int) do cấu hình cũ.
/// Cố tình GIỮ int thay vì đổi sang string: đổi kiểu cột sẽ làm migration
/// fail trên DB đang có dữ liệu (giá trị 0/1/2 không ép được sang 'Text'/'Image').
/// Muốn đổi sang string thì phải có script chuyển dữ liệu riêng — không tự làm.
/// </summary>
public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.ToTable("ChatRooms");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.RoomType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Domain.Enums.ChatRoomType.Commission)
            .IsRequired();

        builder.Property(r => r.Title).HasMaxLength(200).IsRequired();
        builder.Property(r => r.IsLocked).HasDefaultValue(false);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Commission.Commission>()
            .WithMany()
            .HasForeignKey(r => r.CommissionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Domain.Entities.Auction.Auction>()
            .WithMany()
            .HasForeignKey(r => r.AuctionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Danh sách phòng của tôi, sắp theo tin nhắn mới nhất.
        builder.HasIndex(r => r.LastMessageAt)
            .HasDatabaseName("IX_ChatRooms_LastMessageAt");

        builder.HasIndex(r => r.CommissionId)
            .HasDatabaseName("IX_ChatRooms_CommissionId");

        builder.HasIndex(r => r.AuctionId)
            .HasDatabaseName("IX_ChatRooms_AuctionId");
    }
}

public class ChatRoomMemberConfiguration : IEntityTypeConfiguration<ChatRoomMember>
{
    public void Configure(EntityTypeBuilder<ChatRoomMember> builder)
    {
        builder.ToTable("ChatRoomMembers");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MemberRole).HasMaxLength(30).IsRequired();
        builder.Property(m => m.HasLeft).HasDefaultValue(false);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.IsDeleted).HasDefaultValue(false);

        builder.HasOne(m => m.Room)
            .WithMany(r => r.Members)
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Một user chỉ là thành viên một lần trong một phòng.
        builder.HasIndex(m => new { m.RoomId, m.UserId })
            .IsUnique()
            .HasDatabaseName("UX_ChatRoomMembers_Room_User");

        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("IX_ChatRoomMembers_UserId");
    }
}

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType)
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.MessageTypes.Text)
            .IsRequired();

        builder.Property(m => m.Body).IsRequired();
        builder.Property(m => m.AttachmentUrl).HasMaxLength(500);

        // Cụm trường dịch — YÊU CẦU SCHEMA #4.
        builder.Property(m => m.SourceLang).HasMaxLength(10);
        builder.Property(m => m.TargetLang).HasMaxLength(10);
        builder.Property(m => m.TranslationError).HasMaxLength(500);

        builder.Property(m => m.TranslationStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Domain.Enums.TranslationStatus.NotRequested)
            .IsRequired();

        builder.Property(m => m.SentAt).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.IsDeleted).HasDefaultValue(false);

        builder.HasOne(m => m.Room)
            .WithMany(r => r.Messages)
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Đường đi chính của UC43: lịch sử tin nhắn theo phòng, cursor theo sent_at.
        builder.HasIndex(m => new { m.RoomId, m.SentAt })
            .HasDatabaseName("IX_Messages_Room_SentAt");

        // Giữ index cũ để không phá truy vấn theo commission đang có.
        builder.HasIndex(m => new { m.CommissionId, m.SentAt })
            .HasDatabaseName("IX_Messages_Commission_SentAt");
    }
}

public class MessageAttachmentConfiguration : IEntityTypeConfiguration<MessageAttachment>
{
    public void Configure(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileUrl).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(300);
        builder.Property(a => a.MimeType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.IsDeleted).HasDefaultValue(false);

        builder.HasOne(a => a.Message)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UploadedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.MessageId)
            .HasDatabaseName("IX_MessageAttachments_MessageId");
    }
}
