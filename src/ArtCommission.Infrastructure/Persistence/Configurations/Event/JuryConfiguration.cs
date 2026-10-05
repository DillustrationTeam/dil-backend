using ArtCommission.Domain.Entities.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class JuryConfiguration : IEntityTypeConfiguration<Jury>
{
    public void Configure(EntityTypeBuilder<Jury> builder)
    {
        builder.ToTable("Juries");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.IsHeadJury)
            .HasDefaultValue(false);

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.HasOne(j => j.Event)
            .WithMany(e => e.Juries)
            .HasForeignKey(j => j.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.Creator)
            .WithMany()
            .HasForeignKey(j => j.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(j => new { j.EventId, j.CreatorId })
            .IsUnique();

        builder.HasIndex(j => j.CreatorId);

        builder.HasIndex(j => j.EventId)
            .IsUnique()
            .HasFilter("[IsHeadJury] = 1");
    }
}
