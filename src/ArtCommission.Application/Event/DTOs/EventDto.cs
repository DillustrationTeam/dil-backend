using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record EventDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? BannerUrl { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Rules { get; set; }

    public string? Prize { get; set; }

    public int MaxVote { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public bool IsFeatured { get; set; }

    public DateTimeOffset SubmissionStartAt { get; set; }

    public DateTimeOffset SubmissionEndAt { get; set; }

    public DateTimeOffset JudgingStartAt { get; set; }

    public DateTimeOffset JudgingEndAt { get; set; }

    public DateTimeOffset VotingStartAt { get; set; }

    public DateTimeOffset VotingEndAt { get; set; }

    public DateTimeOffset ResultAnnouncementAt { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public string? CreatedByAdminName { get; set; }

    public int SubmissionCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsActive { get; set; }
}
