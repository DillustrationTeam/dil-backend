using ArtCommission.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(u => u.IsVerified)
            .HasDefaultValue(false);

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.Property(u => u.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(u => u.LoginUsername)
            .HasMaxLength(30);

        builder.Property(u => u.NormalizedLoginUsername)
            .HasMaxLength(30);

        // Filtered unique index: SQL Server coi NULL là duy nhất trong unique index thường,
        // nhưng filter rõ ràng để tránh phụ thuộc vào hành vi ngầm định đó.
        builder.HasIndex(u => u.NormalizedLoginUsername)
            .IsUnique()
            .HasFilter("[NormalizedLoginUsername] IS NOT NULL");

        builder.Property(u => u.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(u => u.CoverUrl)
            .HasMaxLength(500);

        builder.Property(u => u.Bio)
            .HasMaxLength(1000);

        builder.Property(u => u.SocialLinks)
            .HasConversion(
                 v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                 v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
             )
            .Metadata.SetValueComparer(StringListValueComparer.Instance);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
