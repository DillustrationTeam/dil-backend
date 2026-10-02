namespace ArtCommission.Application.Event.DTOs;

public sealed record EventDetailDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? BannerUrl { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Rules { get; set; }

    public string? Prize { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public string? CreatedByAdminName { get; set; }

    public string? CreatedByAdminAvatarUrl { get; set; }

    public int SubmissionCount { get; set; }

    public int TotalVoteCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsOpenForSubmission { get; set; }
}
