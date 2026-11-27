using System.Text.Json;

namespace ArtCommission.Application.Auction.Recommendation;

/// <summary>
/// Aggregated interest signal of one user: how much they care about each tag, style, category and
/// creator, plus the price band they actually bid in.
///
/// Weights are already time-decayed when the profile is built, so scoring can stay a plain sum.
/// </summary>
public sealed record InterestProfile(
    Dictionary<string, decimal> Tags,
    Dictionary<string, decimal> Styles,
    Dictionary<string, decimal> Categories,
    Dictionary<Guid, decimal> Creators,
    decimal MedianBidAmount,
    int SourceEventCount,
    DateTimeOffset BuiltAt)
{
    public static InterestProfile Empty { get; } = new(
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<Guid, decimal>(),
        0m,
        0,
        DateTimeOffset.UtcNow);

    public bool IsEmpty => Tags.Count == 0 && Styles.Count == 0 && Categories.Count == 0 && Creators.Count == 0;

    /// <summary>Raw content affinity of one auction, before normalisation.</summary>
    public decimal ContentScore(IEnumerable<string> tags, string? style, string? category)
    {
        decimal score = 0m;

        foreach (var tag in tags)
        {
            if (Tags.TryGetValue(tag, out var weight))
            {
                // Cap a single tag so one obsessive week cannot dominate the whole rail.
                score += Math.Min(weight, 12m);
            }
        }

        if (!string.IsNullOrWhiteSpace(style) && Styles.TryGetValue(style, out var styleWeight))
        {
            score += Math.Min(styleWeight, 8m) * 1.5m;
        }

        if (!string.IsNullOrWhiteSpace(category) && Categories.TryGetValue(category, out var categoryWeight))
        {
            score += Math.Min(categoryWeight, 10m);
        }

        return score;
    }

    /// <summary>Best matching tag, used to build the Vietnamese reason line.</summary>
    public string? BestTag(IEnumerable<string> tags)
        => tags
            .Select(tag => new { Tag = tag, Weight = Tags.GetValueOrDefault(tag) })
            .Where(x => x.Weight > 0)
            .OrderByDescending(x => x.Weight)
            .Select(x => x.Tag)
            .FirstOrDefault();

    public decimal CreatorAffinity(Guid creatorProfileId)
        => Creators.GetValueOrDefault(creatorProfileId);

    public string Serialize() => JsonSerializer.Serialize(this);

    public static InterestProfile Deserialize(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<InterestProfile>(json);
            return parsed ?? Empty;
        }
        catch (JsonException)
        {
            // A corrupted cache row must never break the rail: rebuild happens on the next call.
            return Empty;
        }
    }
}
