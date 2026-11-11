namespace ArtCommission.Application.ArtistStudio.Moderation.DTOs;

public record ArtworkDuplicateMatchDto(
    Guid ArtworkId,
    string Title,
    string? ThumbnailUrl,
    string ImageUrl,
    Guid CreatorProfileId,
    string CreatorName,
    string ModerationStatus,
    DateTimeOffset CreatedAt,
    string MatchType,
    int SimilarityPercent,
    int HammingDistance,
    bool SameCreator,
    string? ReceiptToken
);

public record ArtworkDuplicateCheckResultDto(
    Guid ArtworkId,
    bool HasDuplicates,
    int ComparedArtworksCount,
    string? SourceSha256,
    long? SourceVisualHash,
    IReadOnlyList<ArtworkDuplicateMatchDto> Matches
);
