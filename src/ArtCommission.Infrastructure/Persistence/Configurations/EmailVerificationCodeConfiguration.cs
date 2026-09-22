using ArtCommission.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class EmailVerificationCodeConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("EmailVerificationCodes");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(e => e.Email)
            .HasDatabaseName("IX_EmailVerificationCodes_Email");

        builder.Property(e => e.CodeHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Purpose)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
    }
}
