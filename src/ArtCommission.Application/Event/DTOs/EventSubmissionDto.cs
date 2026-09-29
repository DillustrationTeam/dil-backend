namespace ArtCommission.Application.Event.DTOs;
public sealed record EventSubmissionDto (
    Guid Id,
    Guid EventId,
    string? EventTitle,
    Guid SubmitterId,
    string? SubmitterName,
    string? SubmitterAvatarUrl,
    Guid ArtworkId,
    string? ArtworkTitle,
    string? ArtworkImageUrl,
    string? ArtworkThumbnailUrl,
    bool AiScanPassed,
    int VoteCount,
    decimal? Score,
    string? AdminNote,
    DateTimeOffset SubmittedAt,
    bool HasUserVoted = false
);
