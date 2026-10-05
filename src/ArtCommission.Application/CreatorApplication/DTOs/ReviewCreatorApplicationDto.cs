using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.CreatorApplication.DTOs;

public sealed record ReviewCreatorApplicationDto
{
    public ApplicationStatus Status { get; set; }
    public string? ReviewNote { get; set; }
    public bool GrantAiVerifiedBadge { get; set; } = true;
}