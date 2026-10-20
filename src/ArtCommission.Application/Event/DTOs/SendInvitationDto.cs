namespace ArtCommission.Application.Event.DTOs;

public sealed record SendInvitationDto
{
    public Guid EventId { get; set; }

    public string Email { get; set; } = string.Empty;

    public bool IsHeadJury { get; set; }
}
