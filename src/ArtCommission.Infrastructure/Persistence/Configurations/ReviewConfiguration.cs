using ArtCommission.Domain.Entities.Commission;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews");

        builder.HasKey(r => r.Id);

        builder.HasIndex(r => r.CommissionId).IsUnique();

        builder.Property(r => r.Comment)
            .HasMaxLength(1000);

        builder.Property(r => r.ReviewerReply)
            .HasMaxLength(1000);
    }
}
