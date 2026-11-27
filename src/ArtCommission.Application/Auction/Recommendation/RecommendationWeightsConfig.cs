using System.Globalization;
using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Recommendation;

/// <summary>
/// Ranking weights of the v1 recommender, readable from <c>PlatformConfigs</c> so an operator can
/// retune the rail without a deploy.
///
/// Defaults live in code: a fresh database (no config row) must still rank sensibly, and a malformed
/// JSON must never take the rail down - the fallback is always the shipped v1 set.
/// </summary>
public sealed record RecommendationWeightsConfig(
    double Content,
    double Urgency,
    double Popularity,
    double PriceFit,
    double Creator,
    double RiskPenalty,
    int WindowDays,
    int MaxPerCreator,
    int MaxPerCategory)
{
    public static readonly RecommendationWeightsConfig Default = new(
        Content: 0.45,
        Urgency: 0.25,
        Popularity: 0.15,
        PriceFit: 0.10,
        Creator: 0.05,
        RiskPenalty: 0.30,
        WindowDays: 7,
        MaxPerCreator: 2,
        MaxPerCategory: 3);

    public static async Task<RecommendationWeightsConfig> LoadAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var json = await db.PlatformConfigs.AsNoTracking()
            .Where(c => c.Key == PlatformConfigKeys.AuctionRecommendationWeights)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return Default;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RecommendationWeightsConfig>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return parsed is null ? Default : Sanitize(parsed);
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>Clamp every knob into a range that cannot produce a broken ordering.</summary>
    private static RecommendationWeightsConfig Sanitize(RecommendationWeightsConfig raw) => new(
        Content: Math.Clamp(raw.Content, 0d, 1d),
        Urgency: Math.Clamp(raw.Urgency, 0d, 1d),
        Popularity: Math.Clamp(raw.Popularity, 0d, 1d),
        PriceFit: Math.Clamp(raw.PriceFit, 0d, 1d),
        Creator: Math.Clamp(raw.Creator, 0d, 1d),
        RiskPenalty: Math.Clamp(raw.RiskPenalty, 0d, 1d),
        WindowDays: Math.Clamp(raw.WindowDays <= 0 ? Default.WindowDays : raw.WindowDays, 1, 30),
        MaxPerCreator: Math.Clamp(raw.MaxPerCreator <= 0 ? Default.MaxPerCreator : raw.MaxPerCreator, 1, 10),
        MaxPerCategory: Math.Clamp(raw.MaxPerCategory <= 0 ? Default.MaxPerCategory : raw.MaxPerCategory, 1, 10));

    public string Serialize() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = false });

    /// <summary>Vietnamese summary used by the admin config screen / diagnostics.</summary>
    public string Describe() => string.Format(
        CultureInfo.InvariantCulture,
        "content {0:0.00} · urgency {1:0.00} · popularity {2:0.00} · priceFit {3:0.00} · creator {4:0.00} · riskPenalty {5:0.00} · cửa sổ {6} ngày · tối đa {7}/creator, {8}/danh mục",
        Content, Urgency, Popularity, PriceFit, Creator, RiskPenalty, WindowDays, MaxPerCreator, MaxPerCategory);
}
