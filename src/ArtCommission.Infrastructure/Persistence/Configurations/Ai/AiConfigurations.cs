using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Ai;

/// <summary>
/// Cấu hình EF Core cho module AI Assistant (UC44 chatbot, UC46 deadline risk).
/// </summary>
public class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Topic).HasMaxLength(300).IsRequired();

        builder.Property(c => c.ContextType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AiContextType.General)
            .IsRequired();

        builder.Property(c => c.MessageCount).HasDefaultValue(0);
        builder.Property(c => c.IsArchived).HasDefaultValue(false);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Danh sách phiên của tôi, mới nhất trước — cursor theo LastMessageAt.
        builder.HasIndex(c => new { c.UserId, c.LastMessageAt })
            .HasDatabaseName("IX_AiConversations_User_LastMessageAt");

        builder.HasIndex(c => new { c.ContextType, c.ContextId })
            .HasDatabaseName("IX_AiConversations_Context");
    }
}

public class AiMessageConfiguration : IEntityTypeConfiguration<AiMessage>
{
    public void Configure(EntityTypeBuilder<AiMessage> builder)
    {
        builder.ToTable("AiMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Content).IsRequired();

        // Lưu role dạng chuỗi để đọc DB thấy ngay "user"/"assistant", không phải đoán số.
        builder.Property(m => m.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AiMessageRole.User)
            .IsRequired();

        builder.Property(m => m.FeedbackNote).HasMaxLength(1000);
        builder.Property(m => m.ModelVersion).HasMaxLength(80);
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.IsDeleted).HasDefaultValue(false);

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tải lại hội thoại theo thứ tự thời gian.
        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt })
            .HasDatabaseName("IX_AiMessages_Conversation_CreatedAt");

        builder.HasIndex(m => m.FeedbackHelpful)
            .HasDatabaseName("IX_AiMessages_FeedbackHelpful");
    }
}

public class DeadlineReminderConfiguration : IEntityTypeConfiguration<DeadlineReminder>
{
    public void Configure(EntityTypeBuilder<DeadlineReminder> builder)
    {
        builder.ToTable("DeadlineReminders");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Channel)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(ReminderChannel.InApp)
            .IsRequired();

        builder.Property(r => r.RiskScore).HasPrecision(5, 2);
        builder.Property(r => r.ModelVersion).HasMaxLength(80);
        builder.Property(r => r.FailureReason).HasMaxLength(500);
        builder.Property(r => r.IsManual).HasDefaultValue(false);
        builder.Property(r => r.RemindAt).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Commission.Commission>()
            .WithMany()
            .HasForeignKey(r => r.CommissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction, KHÔNG phải SetNull: xoá một Commission đã cascade tới cả
        // DeadlineReminders (qua CommissionId) và tới Milestones (qua CommissionId).
        // Nếu FK MilestoneId cũng SetNull thì SQL Server báo lỗi 1785 (multiple cascade
        // paths) và cả migration bị chặn. MilestoneId là tham chiếu phụ nên NoAction
        // là lựa chọn đúng: xoá mốc không được tự ý sửa bản ghi nhắc nhở.
        builder.HasOne<Domain.Entities.Commission.Milestone>()
            .WithMany()
            .HasForeignKey(r => r.MilestoneId)
            .OnDelete(DeleteBehavior.NoAction);

        // Job gửi nhắc nhở quét theo (chưa gửi, tới giờ) — index phải phủ đúng truy vấn này.
        builder.HasIndex(r => new { r.SentAt, r.RemindAt })
            .HasDatabaseName("IX_DeadlineReminders_Pending_RemindAt");

        builder.HasIndex(r => r.CommissionId)
            .HasDatabaseName("IX_DeadlineReminders_CommissionId");
    }
}

public class UserReminderSettingConfiguration : IEntityTypeConfiguration<UserReminderSetting>
{
    public void Configure(EntityTypeBuilder<UserReminderSetting> builder)
    {
        builder.ToTable("UserReminderSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.EmailEnabled).HasDefaultValue(true);
        builder.Property(s => s.PushEnabled).HasDefaultValue(true);
        builder.Property(s => s.LeadHours).HasDefaultValue(24);
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.IsDeleted).HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-1 với user: thiếu unique index thì 2 request song song tạo 2 dòng cấu hình.
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasDatabaseName("UX_UserReminderSettings_UserId");
    }
}
