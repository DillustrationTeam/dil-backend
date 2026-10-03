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
)
{
    public string? CreatorName { get; init; }
    public string? CreatorHeadline { get; init; }
    public decimal CreatorRating { get; init; }
    public int AvailableSlots { get; init; }
    public string? Style { get; init; }
    public string LicenseType { get; init; } = "Personal";
    public decimal? StartingPrice { get; init; }
}

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

public record UpdateCreatorArtworkRequest(
    string Title,
    string? Description,
    IReadOnlyList<string>? Tags,
    string? ImageUrl,
    string? ThumbnailUrl);
