using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record JuryDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? EventTitle { get; set; }
    public Guid CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public string? CreatorDisplayName { get; set; }
    public string? CreatorAvatarUrl { get; set; }
    public string? CreatorBio { get; set; }
    public JuryRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
