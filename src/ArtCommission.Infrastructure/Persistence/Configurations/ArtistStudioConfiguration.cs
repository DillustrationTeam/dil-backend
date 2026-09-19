using ArtCommission.Domain.Entities.ArtistStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CreatorProfileConfiguration : IEntityTypeConfiguration<CreatorProfile>
{
    public void Configure(EntityTypeBuilder<CreatorProfile> builder)
    {
        builder.ToTable("CreatorProfiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.DisplayName).HasMaxLength(150).IsRequired();
        builder.Property(profile => profile.Headline).HasMaxLength(200);
        builder.Property(profile => profile.Bio).HasMaxLength(1000);
        builder.Property(profile => profile.Specialties).HasMaxLength(500);
        builder.Property(profile => profile.Location).HasMaxLength(200);
        builder.Property(profile => profile.WebsiteUrl).HasMaxLength(500);
        builder.Property(profile => profile.BannerUrl).HasMaxLength(500);
        builder.Property(profile => profile.RateCard).HasMaxLength(1000);
        builder.Property(profile => profile.RatingAverage).HasPrecision(3, 2).HasDefaultValue(0m);
        builder.Property(profile => profile.IsAiVerified).HasDefaultValue(false);
        builder.Property(profile => profile.IsApproved).HasDefaultValue(false);
        builder.Property(profile => profile.IsAcceptingOrders).HasDefaultValue(true);

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
        builder.ToTable("Artworks");
        builder.HasKey(artwork => artwork.Id);

        builder.Property(artwork => artwork.Title).HasMaxLength(200).IsRequired();
        builder.Property(artwork => artwork.Description).HasMaxLength(2000);
        builder.Property(artwork => artwork.ImageUrl).HasMaxLength(500).IsRequired();
        builder.Property(artwork => artwork.ThumbnailUrl).HasMaxLength(500);
        builder.Property(artwork => artwork.Style).HasMaxLength(100);
        builder.Property(artwork => artwork.Status).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.HasIndex(artwork => new { artwork.CreatorProfileId, artwork.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_Artwork_CreatorProfileId_CreatedAt");
        builder.HasOne(artwork => artwork.CreatorProfile)
            .WithMany(profile => profile.Artworks)
            .HasForeignKey(artwork => artwork.CreatorProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(tag => tag.Id);
        // Chờ nội dung Tag.cs để chốt field (Name, IsAiGenerated...) và MaxLength (100 hay 50)
        builder.HasIndex(tag => tag.Name).IsUnique().HasDatabaseName("UQ_Tag_Name");
    }
}

public class ArtworkTagConfiguration : IEntityTypeConfiguration<ArtworkTag>
{
    public void Configure(EntityTypeBuilder<ArtworkTag> builder)
    {
        builder.ToTable("ArtworkTags");
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

        // PHẢI là NoAction, KHÔNG được Cascade:
        // Users → CreatorProfiles → Follows và Users → Follows là HAI đường cascade
        // cùng trỏ về bảng Follows ⇒ SQL Server chặn tạo FK
        // ("may cause cycles or multiple cascade paths", lỗi 1785).
        builder.HasOne(x => x.Follower)
            .WithMany()
            .HasForeignKey(x => x.FollowerUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}