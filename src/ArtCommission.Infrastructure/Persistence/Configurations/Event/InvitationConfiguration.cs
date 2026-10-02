using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.Event;

public class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(InvitationStatus.Pending)
            .IsRequired();

        builder.Property(i => i.IsHeadJury)
            .HasDefaultValue(false);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.RespondedAt);

        builder.HasOne(i => i.Event)
            .WithMany(e => e.Invitations)
            .HasForeignKey(i => i.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.SentFromAdmin)
            .WithMany()
            .HasForeignKey(i => i.SentFromAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.SentToCreator)
            .WithMany()
            .HasForeignKey(i => i.SentToCreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.EventId, i.Status });
        builder.HasIndex(i => new { i.SentToCreatorId, i.Status });
        builder.HasIndex(i => i.SentFromAdminId);
        builder.HasIndex(i => new { i.EventId, i.SentToCreatorId })
            .IsUnique()
            .HasFilter("[Status] = 'Pending'");
    }
}
