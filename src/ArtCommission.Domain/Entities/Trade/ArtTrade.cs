using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Trade;

/// <summary>
/// Thực thể đại diện cho một giao dịch trao đổi tác phẩm (Art Trade) giữa 2 họa sĩ.
/// </summary>
public class ArtTrade : BaseEntity
{
    public Guid InitiatorId { get; set; }
    public Guid ReceiverId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string? InitiatorRequestNote { get; set; }
    public string? ReceiverRequestNote { get; set; }
    public string? ReceiverResponseNote { get; set; }

    public Guid? InitiatorArtworkId { get; set; }
    public Guid? ReceiverArtworkId { get; set; }

    public string? InitiatorOriginalFileUrl { get; set; }
    public string? InitiatorPreviewUrl { get; set; }
    public DateTimeOffset? InitiatorSubmittedAt { get; set; }

    public string? ReceiverOriginalFileUrl { get; set; }
    public string? ReceiverPreviewUrl { get; set; }
    public DateTimeOffset? ReceiverSubmittedAt { get; set; }

    public bool IsInitiatorConfirmed { get; set; }
    public DateTimeOffset? InitiatorConfirmedAt { get; set; }

    public bool IsReceiverConfirmed { get; set; }
    public DateTimeOffset? ReceiverConfirmedAt { get; set; }

    /// <summary>Thời điểm Side B chấp nhận đề nghị. Trade chỉ chuyển Accepted khi Side A cũng xác nhận.</summary>
    public DateTimeOffset? ReceiverAcceptedAt { get; set; }

    public TradeStatus Status { get; set; } = TradeStatus.Proposed;

    public DateTimeOffset? SubmissionDeadlineAt { get; set; }
    public DateTimeOffset? ConfirmationDeadlineAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public string? RejectionReason { get; set; }

    // Navigation Properties
    public ApplicationUser? Initiator { get; set; }
    public ApplicationUser? Receiver { get; set; }
    public ApplicationUser? CancelledBy { get; set; }

    public Artwork? InitiatorArtwork { get; set; }
    public Artwork? ReceiverArtwork { get; set; }

    public ICollection<Dispute.TradeDispute> Disputes { get; set; } = new List<Dispute.TradeDispute>();
}

