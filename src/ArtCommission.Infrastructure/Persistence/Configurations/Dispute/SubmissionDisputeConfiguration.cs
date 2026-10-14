using ArtCommission.Domain.Entities.Dispute;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Dispute;

public class SubmissionDisputeConfiguration : IEntityTypeConfiguration<SubmissionDispute>
{
    public void Configure(EntityTypeBuilder<SubmissionDispute> builder)
    {
        builder.ToTable("SubmissionDisputes");

        builder.HasKey(d => d.Id);

        builder.HasIndex(d => d.SubmissionId);

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Status)
            .HasMaxLength(50);

        builder.Property(d => d.Resolution)
            .HasMaxLength(50);


        builder.Property(d => d.Priority)
            .HasMaxLength(50);

        builder.Property(d => d.Note);

        builder.HasOne(d => d.Event)
            .WithMany()
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Submission)
            .WithMany(s => s.Disputes)
            .HasForeignKey(d => d.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
