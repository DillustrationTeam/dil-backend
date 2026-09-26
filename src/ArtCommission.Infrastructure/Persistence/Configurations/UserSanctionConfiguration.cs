using ArtCommission.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class UserSanctionConfiguration : IEntityTypeConfiguration<UserSanction>
{
    public void Configure(EntityTypeBuilder<UserSanction> builder)
    {
        builder.ToTable("UserSanctions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ActionType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.ActionByAdmin)
            .WithMany()
            .HasForeignKey(s => s.ActionByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.UserId, s.CreatedAt });
    }
}
