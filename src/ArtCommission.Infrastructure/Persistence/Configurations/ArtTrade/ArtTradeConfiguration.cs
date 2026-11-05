using ArtTradeEntity = ArtCommission.Domain.Entities.Trade.ArtTrade;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ArtCommission.Infrastructure.Persistence.Configurations.ArtTrade;

public class ArtTradeConfiguration : IEntityTypeConfiguration<ArtTradeEntity>
{
    public void Configure(EntityTypeBuilder<ArtTradeEntity> builder)
    {
        builder.ToTable("ArtTrades");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.UpdatedAt).IsConcurrencyToken();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.InitiatorRequestNote)
            .HasMaxLength(2000);

        builder.Property(t => t.ReceiverRequestNote)
            .HasMaxLength(2000);

        builder.Property(t => t.InitiatorOriginalFileUrl)
            .HasMaxLength(500);

        builder.Property(t => t.InitiatorPreviewUrl)
            .HasMaxLength(500);

        builder.Property(t => t.ReceiverOriginalFileUrl)
            .HasMaxLength(500);

        builder.Property(t => t.ReceiverPreviewUrl)
            .HasMaxLength(500);

        builder.Property(t => t.CancellationReason)
            .HasMaxLength(500);

        builder.Property(t => t.RejectionReason)
            .HasMaxLength(500);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsConcurrencyToken();

        // Foreign keys & Delete behaviors
        builder.HasOne(t => t.Initiator)
            .WithMany()
            .HasForeignKey(t => t.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Receiver)
            .WithMany()
            .HasForeignKey(t => t.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CancelledBy)
            .WithMany()
            .HasForeignKey(t => t.CancelledById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.InitiatorArtwork)
            .WithMany()
            .HasForeignKey(t => t.InitiatorArtworkId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.ReceiverArtwork)
            .WithMany()
            .HasForeignKey(t => t.ReceiverArtworkId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.HasIndex(t => new { t.InitiatorId, t.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ArtTrades_InitiatorId_CreatedAt");

        builder.HasIndex(t => new { t.ReceiverId, t.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ArtTrades_ReceiverId_CreatedAt");

        builder.HasIndex(t => new { t.Status, t.UpdatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("IX_ArtTrades_Status_UpdatedAt");
    }
}

