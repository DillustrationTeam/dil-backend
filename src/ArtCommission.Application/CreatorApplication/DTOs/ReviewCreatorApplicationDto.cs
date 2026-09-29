using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.CreatorApplication.DTOs;
public sealed record ReviewCreatorApplicationDto 
{
    public ApplicationStatus Status { get; init; }
    public string? ReviewNote { get; init; }
    public bool GrantAiVerifiedBadge { get; init; } = true;
}