namespace ArtCommission.Application.ArtistStudio.DTOs;

public record ArtworkDto(
    Guid Id,
    Guid CreatorProfileId,
    string Title,
    string? Description,
    string ImageUrl,
    string? ThumbnailUrl,
    bool IsAiGenerated,
    decimal? AiDetectionScore,
    string ModerationStatus,
    int ViewCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> Tags
);

public record CreateArtworkRequest(
    string Title,
    string? Description,
    string ImageUrl,
    string? ThumbnailUrl,
    bool IsAiGenerated = false,
    decimal? AiDetectionScore = null,
    IReadOnlyList<string>? Tags = null
);

public record UpdateArtworkRequest(
    string? Title,
    string? Description,
    string? ImageUrl,
    string? ThumbnailUrl,
    bool? IsAiGenerated,
    decimal? AiDetectionScore,
    string? ModerationStatus,
    IReadOnlyList<string>? Tags
);
