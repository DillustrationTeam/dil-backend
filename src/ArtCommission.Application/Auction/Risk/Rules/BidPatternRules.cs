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

        // Điều kiện cũ `bids.Count <= VelocityThreshold` làm ngưỡng thực tế là 7 lượt chứ không phải
        // 6: một đợt 6 lượt trong 18 giây đi qua mà không bị chấm điểm nào. Nay là "đủ ngưỡng là xét".
        if (bids.Count < settings.VelocityThreshold)
        {
            return [];
        }

        var window = settings.VelocityWindow;
        for (var end = settings.VelocityThreshold - 1; end < bids.Count; end++)
        {
            var current = bids[end];
            var start = end - settings.VelocityThreshold + 1;
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
/// Một cụm lượt đặt giá dồn dập do NHIỀU tài khoản khác nhau đẩy, nhịp gần như không có khoảng nghỉ.
///
/// VÌ SAO cần rule riêng (đo được, xem mục 7.17 của kế hoạch): 6 lượt trong 18 giây bởi 2 tài khoản,
/// có một cú nhảy 4 bước giá, mà bộ cũ chấm **0 điểm** — vì <see cref="BidVelocityRule"/> chỉ nhìn
/// tốc độ, còn <see cref="PriceLadderRule"/> chỉ bắt trường hợp CÙNG một người. Kiểu đẩy giá phổ
/// biến nhất (hai tài khoản thay nhau) rơi đúng vào khoảng trống giữa hai rule đó.
/// </summary>
public sealed class BidBurstClusterRule : IRiskRule
{
    public RiskSignalCode Code => RiskSignalCode.BidBurstCluster;

    public bool IsHotPathSafe => true;

    public IReadOnlyList<RiskSignalDraft> Evaluate(RiskContext context, RiskSettings settings)
    {
        var bids = context.Bids;
        if (bids.Count < settings.BurstBidCount)
        {
            return [];
        }

        var window = settings.BurstWindow;

        // Cửa sổ trượt trên chuỗi bid đã sắp theo thời gian: tìm cụm ĐẦU TIÊN đủ điều kiện.
        for (var end = settings.BurstBidCount - 1; end < bids.Count; end++)
        {
            var start = end - settings.BurstBidCount + 1;
            var span = bids[end].PlacedAt - bids[start].PlacedAt;

            if (span > window)
            {
                continue;
            }

            var cluster = bids.Skip(start).Take(settings.BurstBidCount).ToList();
            var accounts = cluster.Select(b => b.BidderId).Distinct().Count();
            if (accounts < 2)
            {
                // Một tài khoản duy nhất đã có PriceLadder lo; ở đây chỉ bắt kiểu dàn xếp nhiều tài khoản.
                continue;
            }

            var averageGap = span.TotalSeconds / Math.Max(1, settings.BurstBidCount - 1);
            if (averageGap > settings.BurstMaxAverageGapSeconds)
            {
                continue;
            }

            return
            [
                RiskSignalDraft.ForBid(
                    Code,
                    cluster[^1].Id,
                    cluster[^1].BidderId,
                    $"{settings.BurstBidCount} lượt trong {span.TotalSeconds:0} giây bởi {accounts} tài khoản, "
                    + $"cách nhau trung bình {averageGap:0.#} giây")
            ];
        }

        return [];
    }
}

/// <summary>
/// A single bid that leaps far past the configured step - the classic "set a fake ceiling" move in a
/// shill setup.
///
/// Ngưỡng dùng <c>Math.Min</c> của hai mốc (bội số bước giá và tỉ lệ giá trước), tức là vượt MỘT
/// trong hai mốc đã tính. Trước đây là <c>Math.Max</c>: phải vượt CẢ HAI, nên với phiên giá 340.000
/// và bước 20.000 thì cú nhảy 80.000 (4 bước) vẫn không tính, còn mốc thực tế là 40% giá = 136.000 —
/// rule gần như không bao giờ bắn.
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

            // Mốc thấp hơn quyết định: một trong hai điều kiện đủ để coi là nhảy bất thường.
            var floor = stepFloor <= 0m ? relativeFloor
                : relativeFloor <= 0m ? stepFloor
                : Math.Min(stepFloor, relativeFloor);

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
