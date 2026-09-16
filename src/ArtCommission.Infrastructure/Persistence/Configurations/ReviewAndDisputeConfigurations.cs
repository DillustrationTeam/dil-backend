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

        builder.Property(r => r.Comment)
            .HasMaxLength(1000);

        builder.Property(r => r.ReviewerReply)
            .HasMaxLength(1000);
    }
}

public class DisputeConfiguration : IEntityTypeConfiguration<Dispute>
{
    public void Configure(EntityTypeBuilder<Dispute> builder)
    {
        builder.ToTable("Disputes");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Reason)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(d => d.Status)
            .HasMaxLength(50);

        builder.Property(d => d.Resolution)
            .HasMaxLength(50);

        builder.Property(d => d.ClientRefundAmount)
            .HasPrecision(18, 2);

        builder.Property(d => d.ArtistPayAmount)
            .HasPrecision(18, 2);
    }
}
