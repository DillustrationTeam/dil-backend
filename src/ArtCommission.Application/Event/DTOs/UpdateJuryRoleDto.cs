namespace ArtCommission.Application.Event.DTOs;

public sealed record UpdateJuryRoleDto
{
    public bool IsHeadJury { get; set; }
}
