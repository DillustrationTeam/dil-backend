using System.Globalization;
using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// Tunable knobs of the fraud detector, read from <c>PlatformConfigs</c> on every sweep so an
/// administrator can retune weights/thresholds without a deploy.
///
/// Defaults live here (not in the database) so a fresh database still detects fraud before the
/// seeder has run, and a deleted config row never turns the detector off.
/// </summary>
public sealed class RiskSettings
{
    /// <summary>Bid count inside <see cref="VelocityWindow"/> that starts looking like a bot.</summary>
    public int VelocityThreshold { get; init; } = 6;

    public TimeSpan VelocityWindow { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>Multiplier of <c>BidStep</c> that counts as a suspicious single jump.</summary>
    public decimal StepJumpMultiplier { get; init; } = 3m;

    /// <summary>Relative jump (share of the previous price) that counts as suspicious.</summary>
    public decimal RelativeJumpRatio { get; init; } = 0.4m;

    /// <summary>Ladder length (same bidder, one step each) before it becomes evidence.</summary>
    public int LadderLength { get; init; } = 3;

    public int AutoBidWallCount { get; init; } = 3;
    public decimal AutoBidWallSpread { get; init; } = 0.05m;
    public int OutbidLoopLength { get; init; } = 4;

    public int NonPaymentThreshold { get; init; } = 2;
    public int WinCountThreshold { get; init; } = 3;
    public decimal NonPaymentRateThreshold { get; init; } = 0.3m;

    public TimeSpan NewAccountWindow { get; init; } = TimeSpan.FromHours(72);
    public int NewAccountBidThreshold { get; init; } = 5;

    /// <summary>How far back refresh-token evidence is collected.</summary>
    public TimeSpan DeviceLookback { get; init; } = TimeSpan.FromDays(30);

    public bool Enabled { get; init; } = true;
    public decimal HighThreshold { get; init; } = 55m;
    public decimal CriticalThreshold { get; init; } = 80m;
    public int SweepBatchSize { get; init; } = 200;

    /// <summary>Per-signal points. An empty config keeps these defaults.</summary>
    public IReadOnlyDictionary<RiskSignalCode, decimal> Weights { get; init; } = DefaultWeights;

    public static readonly IReadOnlyDictionary<RiskSignalCode, decimal> DefaultWeights =
        new Dictionary<RiskSignalCode, decimal>
        {
            [RiskSignalCode.SharedDevice] = 30m,
            [RiskSignalCode.SellerLinkedBidder] = 35m,
            [RiskSignalCode.SelfOutbidLoop] = 30m,
            [RiskSignalCode.NonPaymentHistory] = 20m,
            [RiskSignalCode.PriceLadder] = 15m,
            [RiskSignalCode.WinNoPaymentRate] = 15m,
            [RiskSignalCode.AutoBidWall] = 15m,
            [RiskSignalCode.BidVelocity] = 10m,
            [RiskSignalCode.PriceJump] = 10m,
            [RiskSignalCode.NewAccountBurst] = 10m
        };

    public decimal WeightOf(RiskSignalCode code)
        => Weights.TryGetValue(code, out var weight) ? weight : 0m;

    public AuctionRiskLevel LevelOf(decimal score)
    {
        if (score >= CriticalThreshold)
        {
            return AuctionRiskLevel.Critical;
        }

        if (score >= HighThreshold)
        {
            return AuctionRiskLevel.High;
        }

        // Buckets below High are advisory only: the moderator queue ignores them, so an exact
        // Medium/Low split is not worth another configurable threshold.
        return score >= HighThreshold / 2m ? AuctionRiskLevel.Medium : AuctionRiskLevel.Low;
    }

    /// <summary>
    /// Reads the five risk keys. Missing or unparsable values fall back to the defaults above -
    /// a typo in the admin panel must never disable detection silently, so failures are ignored
    /// on purpose here and surfaced by the sweep log instead.
    /// </summary>
    public static async Task<RiskSettings> LoadAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var keys = new[]
        {
            PlatformConfigKeys.AuctionRiskDetectionEnabled,
            PlatformConfigKeys.AuctionRiskThresholdHigh,
            PlatformConfigKeys.AuctionRiskThresholdCritical,
            PlatformConfigKeys.AuctionRiskSweepBatchSize,
            PlatformConfigKeys.AuctionRiskWeightsJson
        };

        var rows = await db.PlatformConfigs.AsNoTracking()
            .Where(c => keys.Contains(c.Key))
            .Select(c => new { c.Key, c.Value })
            .ToListAsync(ct);

        var map = rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        var defaults = new RiskSettings();

        return new RiskSettings
        {
            Enabled = ReadBool(map, PlatformConfigKeys.AuctionRiskDetectionEnabled, defaults.Enabled),
            HighThreshold = ReadDecimal(map, PlatformConfigKeys.AuctionRiskThresholdHigh, defaults.HighThreshold),
            CriticalThreshold = ReadDecimal(map, PlatformConfigKeys.AuctionRiskThresholdCritical, defaults.CriticalThreshold),
            SweepBatchSize = ReadInt(map, PlatformConfigKeys.AuctionRiskSweepBatchSize, defaults.SweepBatchSize),
            Weights = ReadWeights(map)
        };
    }

    private static bool ReadBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
        => map.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed) ? parsed : fallback;

    private static decimal ReadDecimal(IReadOnlyDictionary<string, string> map, string key, decimal fallback)
        => map.TryGetValue(key, out var raw)
           && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed) ? parsed : fallback;

    private static IReadOnlyDictionary<RiskSignalCode, decimal> ReadWeights(IReadOnlyDictionary<string, string> map)
    {
        if (!map.TryGetValue(PlatformConfigKeys.AuctionRiskWeightsJson, out var json)
            || string.IsNullOrWhiteSpace(json))
        {
            return DefaultWeights;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, decimal>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null || parsed.Count == 0)
            {
                return DefaultWeights;
            }

            var weights = new Dictionary<RiskSignalCode, decimal>(DefaultWeights);
            foreach (var (name, points) in parsed)
            {
                if (Enum.TryParse<RiskSignalCode>(name, ignoreCase: true, out var code))
                {
                    weights[code] = Math.Clamp(points, 0m, 100m);
                }
            }

            return weights;
        }
        catch (JsonException)
        {
            // Invalid JSON must not disable detection - fall back to v1 weights.
            return DefaultWeights;
        }
    }
}
