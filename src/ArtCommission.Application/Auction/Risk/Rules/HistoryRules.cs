using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk.Rules;

/// <summary>
/// The bidder already let auctions expire unpaid. Two such sessions make the next "win" look like
/// a price-inflation attempt rather than a purchase intent.
/// </summary>
public sealed class NonPaymentHistoryRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.NonPaymentHistory;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var drafts = new List<RiskSignalDraft>();
        foreach (var bidderId in context.Bids.Select(b => b.BidderId).Distinct())
        {
            var history = context.BidderHistory.GetValueOrDefault(bidderId);
            if (history is null || history.ExpiredUnpaidCount < settings.NonPaymentThreshold)
            {
                continue;
            }

            drafts.Add(RiskSignalDraft.ForUser(
                Code,
                bidderId,
                $"{context.AliasOf(bidderId)} đã để {history.ExpiredUnpaidCount} phiên thắng quá hạn thanh toán"));
        }

        return drafts;
    }
}

/// <summary>
/// Wins often, pays rarely. Requires enough wins so a single unlucky cancellation cannot trigger
/// it, then compares the unpaid share against the configured ratio.
/// </summary>
public sealed class WinNoPaymentRateRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.WinNoPaymentRate;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var drafts = new List<RiskSignalDraft>();
        foreach (var bidderId in context.Bids.Select(b => b.BidderId).Distinct())
        {
            var history = context.BidderHistory.GetValueOrDefault(bidderId);
            if (history is null || history.WonCount < settings.WinCountThreshold || history.WonCount == 0)
            {
                continue;
            }

            var rate = (decimal)history.ExpiredUnpaidCount / history.WonCount;
            if (rate <= settings.NonPaymentRateThreshold)
            {
                continue;
            }

            drafts.Add(RiskSignalDraft.ForUser(
                Code,
                bidderId,
                $"{context.AliasOf(bidderId)}: {history.ExpiredUnpaidCount}/{history.WonCount} phiên thắng không thanh toán ({rate:P0})"));
        }

        return drafts;
    }
}

/// <summary>
/// Two accounts outbidding each other in alternation - the usual way to walk a price up while
/// looking like genuine competition.
/// </summary>
public sealed class SelfOutbidLoopRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.SelfOutbidLoop;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var bids = context.Bids;
        if (bids.Count < settings.OutbidLoopLength)
        {
            return [];
        }

        for (var start = 0; start + settings.OutbidLoopLength <= bids.Count; start++)
        {
            var first = bids[start].BidderId;
            var second = bids[start + 1].BidderId;
            if (first == second)
            {
                continue;
            }

            var loop = true;
            for (var offset = 0; offset < settings.OutbidLoopLength; offset++)
            {
                var expected = offset % 2 == 0 ? first : second;
                if (bids[start + offset].BidderId != expected)
                {
                    loop = false;
                    break;
                }
            }

            if (!loop)
            {
                continue;
            }

            var tail = bids[start + settings.OutbidLoopLength - 1];
            return
            [
                RiskSignalDraft.ForBid(
                    Code,
                    tail.Id,
                    tail.BidderId,
                    $"{context.AliasOf(first)} và {context.AliasOf(second)} qua lại {settings.OutbidLoopLength} lượt đặt giá")
            ];
        }

        return [];
    }
}

/// <summary>
/// A brand-new account bidding aggressively is normal on a hype auction and suspicious on a quiet
/// one; the sweep weighs it as a single low-value signal and lets a moderator judge.
/// </summary>
public sealed class NewAccountBurstRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.NewAccountBurst;

    public bool IsHotPathSafe => false;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var drafts = new List<RiskSignalDraft>();
        var perBidder = context.Bids
            .GroupBy(b => b.BidderId)
            .Select(g => new
            {
                BidderId = g.Key,
                Count = g.Count(),
                LastBid = g.OrderBy(b => b.PlacedAt).Last()
            });

        foreach (var entry in perBidder)
        {
            if (entry.Count < settings.NewAccountBidThreshold)
            {
                continue;
            }

            if (!context.BidderCreatedAt.TryGetValue(entry.BidderId, out var createdAt))
            {
                continue;
            }

            if (entry.LastBid.PlacedAt - createdAt > settings.NewAccountWindow)
            {
                continue;
            }

            drafts.Add(RiskSignalDraft.ForBid(
                Code,
                entry.LastBid.Id,
                entry.BidderId,
                $"Tài khoản của {context.AliasOf(entry.BidderId)} mới {(entry.LastBid.PlacedAt - createdAt).TotalHours:0} giờ nhưng đã đặt {entry.Count} lượt"));
        }

        return drafts;
    }
}
