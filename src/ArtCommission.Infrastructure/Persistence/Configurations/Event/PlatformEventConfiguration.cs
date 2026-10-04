using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;
public class PlatformEventConfiguration : IEntityTypeConfiguration<PlatformEvent>
{
    public void Configure(EntityTypeBuilder<PlatformEvent> builder)
    {
        builder.ToTable("PlatformEvents", t =>
        {
            t.HasCheckConstraint("CK_PlatformEvents_Timeline",
                "[SubmissionStartAt] < [SubmissionEndAt] AND [SubmissionEndAt] < [JudgingStartAt] AND [JudgingStartAt] < [JudgingEndAt] AND [JudgingEndAt] < [VotingStartAt] AND [VotingStartAt] < [VotingEndAt] AND [VotingEndAt] < [ResultAnnouncementAt]");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.BannerUrl)
            .HasMaxLength(500);

        builder.Property(e => e.Description)
            .IsRequired();

        builder.Property(e => e.Rules);

        builder.Property(e => e.Prize)
            .HasMaxLength(1000);

        builder.Property(e => e.MaxVote)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(EventStatus.Draft)
            .IsRequired();

        builder.Property(e => e.SubmissionStartAt)
            .IsRequired();

        builder.Property(e => e.SubmissionEndAt)
            .IsRequired();

        builder.Property(e => e.JudgingStartAt)
            .IsRequired();

        builder.Property(e => e.JudgingEndAt)
            .IsRequired();

        builder.Property(e => e.VotingStartAt)
            .IsRequired();

        builder.Property(e => e.VotingEndAt)
            .IsRequired();

        builder.Property(e => e.ResultAnnouncementAt)
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .IsRequired();

        builder.Property(e => e.UpdatedAt);

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.HasOne(e => e.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.CreatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Submissions)
            .WithOne(s => s.Event)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.Status, e.SubmissionStartAt, e.SubmissionEndAt });

        builder.HasIndex(e => e.CreatedByAdminId);

        builder.HasIndex(e => e.CreatedAt);
    }
}