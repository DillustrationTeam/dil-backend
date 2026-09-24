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
        builder.Property(x => x.Style).HasMaxLength(100);
        builder.Property(x => x.LicenseType).HasMaxLength(30).HasDefaultValue("Personal");
        builder.Property(x => x.StartingPrice).HasPrecision(18, 2);
    }
}

public class CreatorWorkstationConfiguration : IEntityTypeConfiguration<CreatorTerms>, IEntityTypeConfiguration<CreatorAutoReplySetting>, IEntityTypeConfiguration<CreatorFaq>, IEntityTypeConfiguration<CreatorWorkItem>, IEntityTypeConfiguration<CreatorAsset>
{
    public void Configure(EntityTypeBuilder<CreatorTerms> builder)
    {
        builder.ToTable("CreatorTerms"); builder.HasIndex(x => x.CreatorProfileId).IsUnique(); builder.Property(x => x.CommercialLicenseMultiplier).HasPrecision(4, 2);
        builder.Property(x => x.RevisionPolicy).HasMaxLength(2000); builder.Property(x => x.CancellationPolicy).HasMaxLength(2000);
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<CreatorAutoReplySetting> builder)
    {
        builder.ToTable("CreatorAutoReplySettings"); builder.HasIndex(x => x.CreatorProfileId).IsUnique(); builder.Property(x => x.BriefTemplate).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<CreatorFaq> builder)
    {
        builder.ToTable("CreatorFaqs"); builder.HasIndex(x => new { x.CreatorProfileId, x.DisplayOrder }); builder.Property(x => x.Question).HasMaxLength(500).IsRequired(); builder.Property(x => x.Answer).HasMaxLength(2000).IsRequired();
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<CreatorWorkItem> builder)
    {
        builder.ToTable("CreatorWorkItems"); builder.HasIndex(x => new { x.CreatorProfileId, x.Stage }); builder.Property(x => x.ClientName).HasMaxLength(150).IsRequired(); builder.Property(x => x.Title).HasMaxLength(200).IsRequired(); builder.Property(x => x.Stage).HasMaxLength(30).IsRequired();
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
    public void Configure(EntityTypeBuilder<CreatorAsset> builder)
    {
        builder.ToTable("CreatorAssets"); builder.HasIndex(x => new { x.CreatorProfileId, x.AssetType }); builder.Property(x => x.AssetType).HasMaxLength(30).IsRequired(); builder.Property(x => x.Name).HasMaxLength(150).IsRequired(); builder.Property(x => x.AssetUrl).HasMaxLength(500);
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class MarketplaceInteractionConfiguration : IEntityTypeConfiguration<ArtworkFavorite>, IEntityTypeConfiguration<ArtworkComment>, IEntityTypeConfiguration<PersonalCollection>, IEntityTypeConfiguration<CollectionArtwork>, IEntityTypeConfiguration<CommissionService>, IEntityTypeConfiguration<CreatorReview>
{
    public void Configure(EntityTypeBuilder<ArtworkFavorite> builder)
    {
        builder.ToTable("ArtworkFavorites");
        builder.HasKey(x => new { x.UserId, x.ArtworkId });
        builder.HasOne(x => x.Artwork).WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<ArtworkComment> builder)
    {
        builder.ToTable("ArtworkComments"); builder.HasIndex(x => new { x.ArtworkId, x.CreatedAt });
        builder.Property(x => x.Body).HasMaxLength(1000).IsRequired();
        builder.HasOne(x => x.Artwork).WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<PersonalCollection> builder)
    {
        builder.ToTable("PersonalCollections"); builder.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
    }

    public void Configure(EntityTypeBuilder<CollectionArtwork> builder)
    {
        builder.ToTable("CollectionArtworks"); builder.HasKey(x => new { x.CollectionId, x.ArtworkId });
        builder.HasOne(x => x.Collection).WithMany(x => x.CollectionArtworks).HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Artwork).WithMany().HasForeignKey(x => x.ArtworkId).OnDelete(DeleteBehavior.NoAction);
    }

    public void Configure(EntityTypeBuilder<CommissionService> builder)
    {
        builder.ToTable("CommissionServices"); builder.HasIndex(x => new { x.CreatorProfileId, x.IsActive });
        builder.Property(x => x.Title).HasMaxLength(150).IsRequired(); builder.Property(x => x.StartingPrice).HasPrecision(18, 2);
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<CreatorReview> builder)
    {
        builder.ToTable("CreatorReviews"); builder.HasIndex(x => new { x.CreatorProfileId, x.CreatedAt });
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.HasOne(x => x.CreatorProfile).WithMany().HasForeignKey(x => x.CreatorProfileId).OnDelete(DeleteBehavior.Cascade);
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
            .OnDelete(DeleteBehavior.NoAction);
    }
}
