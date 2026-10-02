namespace ArtCommission.Application.Event.DTOs;

public sealed record EventDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? BannerUrl { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Prize { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public string? CreatedByAdminName { get; set; }

    public int SubmissionCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsActive { get; set; }
}
