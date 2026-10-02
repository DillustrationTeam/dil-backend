using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record InvitationDto
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string? EventTitle { get; set; }

    public string? EventBannerUrl { get; set; }

    public Guid SentFromAdminId { get; set; }

    public string? SentFromAdminName { get; set; }

    public Guid SentToCreatorId { get; set; }

    public string? SentToCreatorName { get; set; }

    public string? SentToCreatorEmail { get; set; }

    public string? SentToCreatorAvatarUrl { get; set; }

    public JuryRole Role { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }
}
