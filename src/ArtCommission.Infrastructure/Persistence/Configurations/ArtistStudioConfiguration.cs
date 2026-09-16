using ArtCommission.Domain.Entities.ArtistStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CreatorProfileConfiguration : IEntityTypeConfiguration<CreatorProfile>
{
    public void Configure(EntityTypeBuilder<CreatorProfile> builder)
    {
        builder.ToTable("CreatorProfiles");
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Headline).HasMaxLength(200);
        builder.Property(x => x.Specialties).HasMaxLength(500);
        builder.Property(x => x.WebsiteUrl).HasMaxLength(500);
        builder.Property(x => x.BannerUrl).HasMaxLength(500);
        builder.Property(x => x.Location).HasMaxLength(200);
    }
}

public class ArtworkConfiguration : IEntityTypeConfiguration<Artwork>
{
    public void Configure(EntityTypeBuilder<Artwork> builder)
    {
        builder.ToTable("Artworks");
        builder.HasIndex(x => x.CreatorProfileId);
        builder.HasIndex(x => x.ModerationStatus);
        builder.HasOne(x => x.CreatorProfile)
            .WithMany(x => x.Artworks)
            .HasForeignKey(x => x.CreatorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(500);
        builder.Property(x => x.ModerationStatus).HasMaxLength(20).HasDefaultValue("Pending");
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
    }
}

public class ArtworkTagConfiguration : IEntityTypeConfiguration<ArtworkTag>
{
    public void Configure(EntityTypeBuilder<ArtworkTag> builder)
    {
        builder.ToTable("ArtworkTags");
        builder.HasKey(x => new { x.ArtworkId, x.TagId });
        builder.HasOne(x => x.Artwork)
            .WithMany(x => x.ArtworkTags)
            .HasForeignKey(x => x.ArtworkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Tag)
            .WithMany(x => x.ArtworkTags)
            .HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("Follows");
        builder.HasKey(x => new { x.FollowerUserId, x.CreatorProfileId });
        builder.HasOne(x => x.CreatorProfile)
            .WithMany()
            .HasForeignKey(x => x.CreatorProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Follower)
            .WithMany()
            .HasForeignKey(x => x.FollowerUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
