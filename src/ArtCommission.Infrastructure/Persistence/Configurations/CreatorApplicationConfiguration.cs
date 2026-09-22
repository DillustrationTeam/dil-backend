using ArtCommission.Domain.Entities.CreatorApplication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class CreatorApplicationConfiguration : IEntityTypeConfiguration<CreatorApplication>
{
    public void Configure(EntityTypeBuilder<CreatorApplication> builder)
    {
        builder.ToTable("CreatorApplications");

        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Applicant)
               .WithMany()
               .HasForeignKey(c => c.ApplicantId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PortfolioLinks)
               .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                );

       builder.Property(x => x.SocialLinks)
               .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                );

        builder.HasOne(c => c.ReviewedByMod)
               .WithMany()
               .HasForeignKey(c => c.ReviewedByModId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Status)
               .HasConversion<string>()
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(c => c.IdProofUrl)
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(c => c.PrimaryStyle)
               .HasMaxLength(100);

        builder.Property(c => c.SpeedpaintVideoUrl)
               .HasMaxLength(500);

        builder.Property(c => c.IsNationalIdVerified)
               .HasDefaultValue(false);

        builder.HasIndex(c => new {c.ApplicantId, c.Status});
    }
}