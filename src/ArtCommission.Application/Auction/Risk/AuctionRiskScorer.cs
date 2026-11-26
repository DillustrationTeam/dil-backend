using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// Turns per-signal points into a clamped 0-100 score plus the Vietnamese one-liner shown on the
/// moderation sheet. Template-based on purpose: the moderator must be able to explain the flag to
/// the seller without an LLM in the loop.
/// </summary>
public sealed class AuctionRiskScorer
{
    /// <summary>Vietnamese label per code - the API never exposes the enum name.</summary>
    public static readonly IReadOnlyDictionary<RiskSignalCode, string> Labels =
        new Dictionary<RiskSignalCode, string>
        {
            [RiskSignalCode.SharedDevice] = "Nhiều tài khoản dùng chung thiết bị",
            [RiskSignalCode.SellerLinkedBidder] = "Tài khoản trùng thiết bị với người bán",
            [RiskSignalCode.BidVelocity] = "Tăng giá quá nhanh",
            [RiskSignalCode.PriceJump] = "Nhảy giá bất thường",
            [RiskSignalCode.PriceLadder] = "Tự đẩy giá theo bước",
            [RiskSignalCode.NonPaymentHistory] = "Từng thắng mà không thanh toán",
            [RiskSignalCode.WinNoPaymentRate] = "Tỉ lệ bỏ thanh toán cao",
            [RiskSignalCode.SelfOutbidLoop] = "Hai tài khoản qua lại đẩy giá",
            [RiskSignalCode.AutoBidWall] = "Tường giá tự động",
            [RiskSignalCode.NewAccountBurst] = "Tài khoản mới đấu dồn dập"
        };

    public RiskScoreResult Score(IReadOnlyList<RiskSignalDraft> drafts, RiskSettings settings)
    {
        if (drafts.Count == 0)
        {
            return new RiskScoreResult(0m, AuctionRiskLevel.Low, "Không phát hiện dấu hiệu bất thường.");
        }

        var raw = drafts.Sum(d => settings.WeightOf(d.Code));
        var score = Math.Clamp(decimal.Round(raw, 2), 0m, 100m);
        var level = settings.LevelOf(score);

        var top = drafts
            .GroupBy(d => d.Code)
            .Select(g => new { Label = LabelOf(g.Key), Points = settings.WeightOf(g.Key) })
            .OrderByDescending(x => x.Points)
            .Take(3)
            .Select(x => x.Label)
            .ToList();

        var summary = $"{drafts.Count} dấu hiệu: {string.Join(", ", top)}.";
        return new RiskScoreResult(score, level, summary);
    }

    public static string LabelOf(RiskSignalCode code)
        => Labels.TryGetValue(code, out var label) ? label : code.ToString();
}

public sealed record RiskScoreResult(decimal Score, AuctionRiskLevel Level, string Summary);
