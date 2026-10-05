using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Event;

public class Invitation : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid SentFromAdminId { get; set; }

    public Guid SentToCreatorId { get; set; }

    public bool IsHeadJury { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    
    public PlatformEvent? Event { get; set; }
    
    public ApplicationUser? SentFromAdmin { get; set; }

    public CreatorProfile? SentToCreator { get; set; }
}
