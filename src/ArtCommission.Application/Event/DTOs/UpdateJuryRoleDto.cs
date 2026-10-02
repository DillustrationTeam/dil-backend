using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record UpdateJuryRoleDto
{
    public JuryRole Role { get; set; } = JuryRole.Jury;
}
