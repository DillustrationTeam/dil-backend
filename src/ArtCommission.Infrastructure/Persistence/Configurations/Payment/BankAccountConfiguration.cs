using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Payment;

public class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("BankAccount");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BankName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.BankBin)
            .HasMaxLength(20);

        builder.Property(b => b.BankCode)
            .HasMaxLength(20);

        builder.Property(b => b.AccountNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(b => b.AccountHolder)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(b => b.IsDefault)
            .HasDefaultValue(false);

        builder.Property(b => b.IsVerified)
            .HasDefaultValue(false);

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.IsDeleted)
            .HasDefaultValue(false);

        builder.HasIndex(b => new { b.UserId, b.IsDeleted })
            .HasDatabaseName("IX_BankAccount_UserId_IsDeleted");

        // Mỗi user chỉ được có ĐÚNG 1 tài khoản mặc định (ràng buộc ở tầng DB).
        // Filtered unique index chỉ tính các dòng còn sống.
        builder.HasIndex(b => b.UserId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0")
            .HasDatabaseName("UX_BankAccount_DefaultPerUser");

        builder.HasOne<ArtCommission.Domain.Entities.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
