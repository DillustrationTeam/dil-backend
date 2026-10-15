using ArtCommission.Domain.Entities.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class EventSubmissionConfiguration : IEntityTypeConfiguration<EventSubmission>
{
    public void Configure(EntityTypeBuilder<EventSubmission> builder)
    {
        builder.ToTable("EventSubmissions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Description)
            .HasMaxLength(2000);

        builder.Property(s => s.AiScanPassed)
            .HasDefaultValue(false);

        builder.Property(s => s.VoteCount)
            .HasDefaultValue(0);

        builder.Property(s => s.Score)
            .HasPrecision(5, 2);

        builder.Property(s => s.AdminNote)
            .HasMaxLength(1000);

        builder.Property(s => s.SubmittedAt)
            .IsRequired();

        builder.HasOne(s => s.Event)
            .WithMany(e => e.Submissions)
            .HasForeignKey(s => s.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Submitter)
            .WithMany()
            .HasForeignKey(s => s.SubmitterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Artwork)
            .WithMany()
            .HasForeignKey(s => s.ArtworkId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.EventId, s.Score, s.VoteCount });

        builder.HasIndex(s => new { s.EventId, s.SubmitterId });

        builder.HasIndex(s => s.ArtworkId);
    }
}
