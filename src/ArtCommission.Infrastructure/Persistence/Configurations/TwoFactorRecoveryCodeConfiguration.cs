using ArtCommission.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class TwoFactorRecoveryCodeConfiguration : IEntityTypeConfiguration<TwoFactorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<TwoFactorRecoveryCode> builder)
    {
        builder.ToTable("TwoFactorRecoveryCodes");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.CodeHash)
            .HasMaxLength(450)
            .IsRequired();

        builder.HasIndex(c => new { c.UserId, c.CodeHash })
            .IsUnique()
            .HasDatabaseName("UX_TwoFactorRecoveryCodes_UserId_CodeHash");
    }
}
