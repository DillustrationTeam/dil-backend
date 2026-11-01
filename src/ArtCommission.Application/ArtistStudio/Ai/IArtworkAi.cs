using ArtCommission.Application.ArtistStudio.DTOs;
namespace ArtCommission.Application.ArtistStudio.Ai;

public record ArtworkMetadataSuggestion(string Title, string Description, IReadOnlyList<string> Tags, string Style);
public interface IArtworkMetadataService
{
    Task<ArtworkMetadataSuggestion> SuggestAsync(Stream image, CancellationToken ct);
}
public interface IArtworkAiDetector
{
    bool IsConfigured { get; }
    Task<decimal> DetectAsync(string imageUrl, CancellationToken ct);
}
public record ArtworkRecommendation(ArtworkDto Artwork, string Reason);
public record ArtworkScanStatus(string Status, decimal? Score, string? Provider, string? Model, DateTimeOffset? CheckedAt, string? Error);
