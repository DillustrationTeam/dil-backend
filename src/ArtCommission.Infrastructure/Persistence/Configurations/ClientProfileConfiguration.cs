using ArtCommission.Domain.Entities.ArtistStudio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("ClientProfiles");

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Username).HasMaxLength(30);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.Timezone).HasMaxLength(100);

        builder.Property(x => x.InterestTags)
            .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
             );

        builder.Property(x => x.PreferredLanguages)
            .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
             );
    }
}

public class ClientReviewConfiguration : IEntityTypeConfiguration<ClientReview>
{
    public void Configure(EntityTypeBuilder<ClientReview> builder)
    {
        builder.ToTable("ClientReviews");

        builder.Property(x => x.Comment).HasMaxLength(1000);

        builder.HasOne(x => x.Client)
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction để tránh nhiều đường cascade cùng trỏ về Users (giống lý do ở FollowConfiguration).
        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.CommissionId);
    }
}
