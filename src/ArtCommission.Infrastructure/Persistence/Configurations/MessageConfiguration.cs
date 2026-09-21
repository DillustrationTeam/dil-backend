using ArtCommission.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(m => m.Body)
            .IsRequired();

        builder.Property(m => m.AttachmentUrl)
            .HasMaxLength(500);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CommissionId, m.SentAt })
            .HasDatabaseName("IX_Messages_Commission_SentAt");
    }
}
