using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record InviteJuryDto
{
    public string Email { get; set; } = string.Empty;

    public JuryRole Role { get; set; } = JuryRole.Jury;
}
