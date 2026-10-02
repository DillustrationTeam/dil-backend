namespace ArtCommission.Application.Event.DTOs;

public sealed record EventSubmissionDto
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public string? EventTitle { get; set; }

    public Guid SubmitterId { get; set; }

    public string? SubmitterName { get; set; }

    public string? SubmitterAvatarUrl { get; set; }

    public Guid ArtworkId { get; set; }

    public string? ArtworkTitle { get; set; }

    public string? ArtworkImageUrl { get; set; }

    public string? ArtworkThumbnailUrl { get; set; }

    public bool AiScanPassed { get; set; }

    public int VoteCount { get; set; }

    public decimal? Score { get; set; }

    public string? AdminNote { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
