using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Auction.Risk.Rules;

/// <summary>
/// N bids of the same auction inside a short window. Manual bidders do not click that fast, and a
/// sniper war still alternates between accounts instead of repeating one.
/// </summary>
public sealed class BidVelocityRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.BidVelocity;

    public bool IsHotPathSafe => true;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var bids = context.Bids;
        if (bids.Count <= settings.VelocityThreshold)
        {
            return [];
        }

        var window = settings.VelocityWindow;
        for (var end = settings.VelocityThreshold; end < bids.Count; end++)
        {
            var current = bids[end];
            var start = end - settings.VelocityThreshold;
            if (current.PlacedAt - bids[start].PlacedAt > window)
            {
                continue;
            }

            var count = end - start + 1;
            return
            [
                RiskSignalDraft.ForBid(
                    Code,
                    current.Id,
                    current.BidderId,
                    $"{count} lượt đặt giá trong {window.TotalSeconds:0} giây bởi {context.AliasOf(current.BidderId)}")
            ];
        }

        return [];
    }
}

/// <summary>
/// A single bid that leaps far past both the configured step and a share of the current price -
/// the classic "set a fake ceiling" move in a shill setup.
/// </summary>
public sealed class PriceJumpRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.PriceJump;

    public bool IsHotPathSafe => true;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var auction = context.Auction;
        var drafts = new List<RiskSignalDraft>();

        var previous = auction.StartPrice;
        foreach (var bid in context.Bids)
        {
            var delta = bid.Amount - previous;
            var stepFloor = auction.BidStep > 0 ? auction.BidStep * settings.StepJumpMultiplier : 0m;
            var relativeFloor = previous > 0 ? previous * settings.RelativeJumpRatio : 0m;
            var floor = Math.Max(stepFloor, relativeFloor);

            if (floor > 0 && delta >= floor)
            {
                drafts.Add(RiskSignalDraft.ForBid(
                    Code,
                    bid.Id,
                    bid.BidderId,
                    $"Nhảy giá {delta:0.##} so với mức trước {previous:0.##} bởi {context.AliasOf(bid.BidderId)}"));
            }

            previous = bid.Amount;
        }

        return drafts;
    }
}

/// <summary>
/// The same account raising its own price one minimum step at a time - bidding against nobody to
/// inflate the displayed price.
/// </summary>
public sealed class PriceLadderRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.PriceLadder;

    public bool IsHotPathSafe => true;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var bids = context.Bids;
        if (bids.Count < 2)
        {
            return [];
        }

        var drafts = new List<RiskSignalDraft>();
        var runLength = 1;

        for (var i = 1; i < bids.Count; i++)
        {
            var previous = bids[i - 1];
            var current = bids[i];

            var sameBidder = current.BidderId == previous.BidderId;
            var oneStep = Math.Abs(current.Amount - previous.Amount - context.Auction.BidStep) < 0.01m;

            if (sameBidder && oneStep)
            {
                runLength++;

                if (runLength == settings.LadderLength)
                {
                    drafts.Add(RiskSignalDraft.ForBid(
                        Code,
                        current.Id,
                        current.BidderId,
                        $"{runLength} lượt liên tiếp, mỗi lượt đúng một bước giá, bởi {context.AliasOf(current.BidderId)}"));
                }

                continue;
            }

            runLength = 1;
        }

        return drafts;
    }
}

/// <summary>
/// Several auto-bid ceilings stacked almost on top of each other: three accounts programmed to keep
/// winning by a hair usually belong to one person.
/// </summary>
public sealed class AutoBidWallRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.AutoBidWall;

    public bool IsHotPathSafe => true;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var autoBids = context.Bids.Where(b => b.IsAuto && b.MaxAutoBid is > 0).ToList();
        if (autoBids.Count < settings.AutoBidWallCount)
        {
            return [];
        }

        var ceilings = autoBids.Select(b => b.MaxAutoBid!.Value).OrderBy(v => v).ToList();
        var max = ceilings[^1];
        var min = ceilings[^settings.AutoBidWallCount];

        if (max <= 0 || (max - min) / max > settings.AutoBidWallSpread)
        {
            return [];
        }

        var last = autoBids[^1];
        return
        [
            RiskSignalDraft.ForAuction(
                Code,
                $"{settings.AutoBidWallCount} trần đặt giá tự động nằm trong {settings.AutoBidWallSpread * 100:0}% ({min:0.##}-{max:0.##}), lượt cuối bởi {context.AliasOf(last.BidderId)}")
        ];
    }
}
