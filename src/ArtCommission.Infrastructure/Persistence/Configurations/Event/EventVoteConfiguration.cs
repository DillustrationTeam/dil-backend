using ArtCommission.Domain.Entities.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class EventVoteConfiguration : IEntityTypeConfiguration<EventVote>
{
    public void Configure(EntityTypeBuilder<EventVote> builder)
    {
        builder.ToTable("EventVotes");

        builder.HasKey(v => new { v.SubmissionId, v.VoterId });

        builder.Property(v => v.VotedAt)
            .IsRequired();

        builder.HasOne(v => v.Submission)
            .WithMany(s => s.Votes)
            .HasForeignKey(v => v.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Voter)
            .WithMany()
            .HasForeignKey(v => v.VoterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.VoterId);
    }
}
