using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record EventRequestDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? BannerUrl { get; set; }

    public string? Rules { get; set; }

    public string? Prize { get; set; }

    public int MaxVote { get; set; } = 1;

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public bool IsFeatured { get; set; } = false;

    public DateTimeOffset SubmissionStartAt { get; set; }

    public DateTimeOffset SubmissionEndAt { get; set; }

    public DateTimeOffset JudgingStartAt { get; set; }

    public DateTimeOffset JudgingEndAt { get; set; }

    public DateTimeOffset VotingStartAt { get; set; }

    public DateTimeOffset VotingEndAt { get; set; }

    public DateTimeOffset ResultAnnouncementAt { get; set; }
}
