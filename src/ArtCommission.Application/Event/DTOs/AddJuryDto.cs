namespace ArtCommission.Application.Event.DTOs;

public sealed record AddJuryDto
{
    public Guid CreatorId { get; set; }

    public bool IsHeadJury { get; set; } = false;
}
