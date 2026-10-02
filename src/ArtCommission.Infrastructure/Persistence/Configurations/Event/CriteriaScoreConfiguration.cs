using ArtCommission.Domain.Entities.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class CriteriaScoreConfiguration : IEntityTypeConfiguration<CriteriaScore>
{
    public void Configure(EntityTypeBuilder<CriteriaScore> builder)
    {
        builder.ToTable("CriteriaScores");

        builder.HasKey(cs => new { cs.EventCriteriaId, cs.SubmissionId, cs.GradedByJuryId });

        builder.Property(cs => cs.Score)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(cs => cs.UpdatedAt)
            .IsRequired();

        builder.HasOne(cs => cs.EventCriteria)
            .WithMany(c => c.CriteriaScores)
            .HasForeignKey(cs => cs.EventCriteriaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cs => cs.Submission)
            .WithMany()
            .HasForeignKey(cs => cs.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cs => cs.GradedByJury)
            .WithMany(j => j.CriteriaScores)
            .HasForeignKey(cs => cs.GradedByJuryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(cs => cs.SubmissionId);
        builder.HasIndex(cs => cs.GradedByJuryId);
    }
}
