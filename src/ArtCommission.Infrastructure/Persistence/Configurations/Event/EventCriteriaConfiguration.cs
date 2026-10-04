using ArtCommission.Domain.Entities.Event;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class EventCriteriaConfiguration : IEntityTypeConfiguration<EventCriteria>
{
    public void Configure(EntityTypeBuilder<EventCriteria> builder)
    {
        builder.ToTable("EventCriteria");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.MaxScore)
            .HasPrecision(5, 2)
            .HasDefaultValue(10.00m)
            .IsRequired();

        builder.Property(c => c.Weight)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(c => c.DisplayOrder)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.HasOne(c => c.Event)
            .WithMany(e => e.Criteria)
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.EventId);
    }
}
