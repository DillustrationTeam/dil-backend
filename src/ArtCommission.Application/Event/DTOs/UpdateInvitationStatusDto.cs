using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record UpdateInvitationStatusDto
{
    public InvitationStatus Status { get; set; }
}
