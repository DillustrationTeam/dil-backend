namespace ArtCommission.Application.Event.DTOs;

public sealed record EventLeaderboardItemDto
{
    public int Rank { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SubmitterId { get; set; }
    public string? SubmitterName { get; set; }
    public string? SubmitterAvatarUrl { get; set; }
    public Guid ArtworkId { get; set; }
    public string? ArtworkTitle { get; set; }
    public string? ArtworkThumbnailUrl { get; set; }
    public decimal? Score { get; set; }
    public int VoteCount { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
}
