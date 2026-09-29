namespace ArtCommission.Application.Event.DTOs;
public sealed record EventLeaderboardItemDto (
    int Rank,
    Guid SubmissionId,
    Guid SubmitterId,
    string? SubmitterName,
    string? SubmitterAvatarUrl,
    Guid ArtworkId,
    string? ArtworkTitle,
    string? ArtworkThumbnailUrl,
    decimal? Score,
    int VoteCount,
    DateTimeOffset SubmittedAt
);
