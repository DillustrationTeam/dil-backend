using ArtCommission.Domain.Entities.ArtistStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CreatorProfileConfiguration : IEntityTypeConfiguration<CreatorProfile>
{
    public void Configure(EntityTypeBuilder<CreatorProfile> builder)
    {
        builder.ToTable("CreatorProfile");
        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Bio).HasMaxLength(1000);
        builder.Property(profile => profile.RatingAvg).HasPrecision(3, 2).HasDefaultValue(0m);
        builder.Property(profile => profile.IsAiVerified).HasDefaultValue(false);
        builder.HasIndex(profile => profile.UserId).IsUnique().HasDatabaseName("UQ_CreatorProfile_UserId");
        builder.HasOne(profile => profile.User)
            .WithOne()
            .HasForeignKey<CreatorProfile>(profile => profile.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ArtworkConfiguration : IEntityTypeConfiguration<Artwork>
{
    public void Configure(EntityTypeBuilder<Artwork> builder)
    {
        builder.ToTable("Artwork");
        builder.HasKey(artwork => artwork.Id);
        builder.Property(artwork => artwork.Title).HasMaxLength(200).IsRequired();
        builder.Property(artwork => artwork.Description).HasMaxLength(2000);
        builder.Property(artwork => artwork.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(artwork => artwork.Style).HasMaxLength(100);
        builder.Property(artwork => artwork.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.HasIndex(artwork => new { artwork.CreatorId, artwork.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Artwork_CreatorId_CreatedAt");
        builder.HasOne(artwork => artwork.Creator)
            .WithMany(profile => profile.Artworks)
            .HasForeignKey(artwork => artwork.CreatorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tag");
        builder.HasKey(tag => tag.Id);
        builder.Property(tag => tag.Name).HasMaxLength(100).IsRequired();
        builder.Property(tag => tag.IsAiGenerated).HasDefaultValue(false);
        builder.HasIndex(tag => tag.Name).IsUnique().HasDatabaseName("UQ_Tag_Name");
    }
}

public class ArtworkTagConfiguration : IEntityTypeConfiguration<ArtworkTag>
{
    public void Configure(EntityTypeBuilder<ArtworkTag> builder)
    {
        builder.ToTable("ArtworkTag");
        builder.HasKey(artworkTag => new { artworkTag.ArtworkId, artworkTag.TagId });
        builder.Property(artworkTag => artworkTag.CreatedAt).IsRequired();
        builder.HasIndex(artworkTag => artworkTag.TagId).HasDatabaseName("IX_ArtworkTag_TagId");
        builder.HasOne(artworkTag => artworkTag.Artwork)
            .WithMany(artwork => artwork.ArtworkTags)
            .HasForeignKey(artworkTag => artworkTag.ArtworkId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(artworkTag => artworkTag.Tag)
            .WithMany(tag => tag.ArtworkTags)
            .HasForeignKey(artworkTag => artworkTag.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
