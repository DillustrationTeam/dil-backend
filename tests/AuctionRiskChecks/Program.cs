using ArtCommission.Application.Auction.Risk;
using ArtCommission.Application.Auction.Risk.Rules;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using AuctionEntity = ArtCommission.Domain.Entities.Auction.Auction;
using BidEntity = ArtCommission.Domain.Entities.Auction.Bid;

// Runnable checks for the auction fraud detector (no xUnit in this repo).
// Run: dotnet run --project tests/AuctionRiskChecks
//
// The rules are pure functions over RiskContext, so every scenario builds a context by hand and
// asserts on the produced signals - no database, no clock, no network.

var settings = new RiskSettings();
var passed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAIL: {name}{(detail is null ? string.Empty : $" - {detail}")}");
        Environment.Exit(1);
    }

    passed++;
    Console.WriteLine($"  ok  {name}");
}

RiskContext BuildContext(
    AuctionEntity auction,
    IReadOnlyList<BidEntity> bids,
    Dictionary<Guid, DeviceFingerprint>? devices = null,
    Dictionary<Guid, BidderHistory>? history = null,
    Dictionary<Guid, DateTimeOffset>? createdAt = null,
    DeviceFingerprint? sellerDevices = null,
    DateTimeOffset? now = null)
    => new()
    {
        Auction = auction,
        Bids = bids,
        BidderDevices = devices ?? new Dictionary<Guid, DeviceFingerprint>(),
        SellerDevices = sellerDevices ?? DeviceFingerprint.Empty,
        BidderHistory = history ?? new Dictionary<Guid, BidderHistory>(),
        BidderCreatedAt = createdAt ?? new Dictionary<Guid, DateTimeOffset>(),
        Now = now ?? DateTimeOffset.UtcNow
    };

var t0 = new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
var seller = Guid.NewGuid();
var bidderA = Guid.NewGuid();
var bidderB = Guid.NewGuid();

AuctionEntity NewAuction(decimal startPrice = 1_000_000m, decimal step = 50_000m) => new()
{
    Id = Guid.NewGuid(),
    ArtworkId = Guid.NewGuid(),
    SellerId = seller,
    StartPrice = startPrice,
    BidStep = step,
    CurrentPrice = startPrice,
    StartAt = t0.AddDays(-1),
    EndAt = t0.AddDays(1),
    Status = AuctionStatus.Active
};

BidEntity NewBid(AuctionEntity auction, Guid bidder, decimal amount, DateTimeOffset placedAt, bool isAuto = false, decimal? maxAuto = null)
    => new()
    {
        Id = Guid.NewGuid(),
        AuctionId = auction.Id,
        BidderId = bidder,
        Amount = amount,
        PlacedAt = placedAt,
        IsAuto = isAuto,
        MaxAutoBid = maxAuto,
        Status = BidStatus.Leading
    };

Console.WriteLine("1. Bid pattern rules");

{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, 7)
        .Select(i => NewBid(auction, i % 2 == 0 ? bidderA : bidderB, 1_000_000m + (i * 50_000m), t0.AddSeconds(i * 10)))
        .ToList();

    var signals = new BidVelocityRule().Evaluate(BuildContext(auction, bids), settings);
    Check("BidVelocity fires for 7 bids in 60s", signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, 7)
        .Select(i => NewBid(auction, bidderA, 1_000_000m + (i * 50_000m), t0.AddMinutes(i * 2)))
        .ToList();

    var signals = new BidVelocityRule().Evaluate(BuildContext(auction, bids), settings);
    Check("BidVelocity stays quiet for 7 bids over 12 minutes", signals.Count == 0, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_600_000m, t0.AddMinutes(1))
    };

    var signals = new PriceJumpRule().Evaluate(BuildContext(auction, bids), settings);
    Check("PriceJump fires on a +52% jump", signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_100_000m, t0.AddMinutes(1))
    };

    var signals = new PriceJumpRule().Evaluate(BuildContext(auction, bids), settings);
    Check("PriceJump stays quiet on one normal step", signals.Count == 0, $"signals={signals.Count}");
}

// Lệch 1 đơn vị cũ: phải 7 lượt mới xét, nên đợt 6 lượt/18 giây lọt hoàn toàn.
{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, settings.VelocityThreshold)
        .Select(i => NewBid(auction, i % 2 == 0 ? bidderA : bidderB, 1_000_000m + (i * 50_000m), t0.AddSeconds(i * 5)))
        .ToList();

    var signals = new BidVelocityRule().Evaluate(BuildContext(auction, bids), settings);
    Check($"BidVelocity fires for exactly {settings.VelocityThreshold} bids in 20s", signals.Count == 1, $"signals={signals.Count}");
}

// Ngưỡng cũ dùng Math.Max nên cú nhảy 4 bước giá vẫn không tính (mốc thực tế là 40% giá trước).
{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_250_000m, t0.AddMinutes(1))
    };

    var signals = new PriceJumpRule().Evaluate(BuildContext(auction, bids), settings);
    Check("PriceJump fires on a 4-step jump below 40%", signals.Count == 1, $"signals={signals.Count}");
}

// Khoảng trống lớn nhất: nhiều tài khoản thay nhau, nhịp máy. Đo trên API thật trước khi sửa: 0 điểm.
{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, settings.BurstBidCount)
        .Select(i => NewBid(auction, i % 2 == 0 ? bidderA : bidderB, 1_000_000m + (i * 50_000m), t0.AddSeconds(i * 3)))
        .ToList();

    var signals = new BidBurstClusterRule().Evaluate(BuildContext(auction, bids), settings);
    Check(
        $"BidBurstCluster fires for {settings.BurstBidCount} bids / 2 accounts / 3s apart",
        signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, settings.BurstBidCount)
        .Select(i => NewBid(auction, bidderA, 1_000_000m + (i * 50_000m), t0.AddSeconds(i * 3)))
        .ToList();

    var signals = new BidBurstClusterRule().Evaluate(BuildContext(auction, bids), settings);
    Check("BidBurstCluster stays quiet for a single account", signals.Count == 0, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, settings.BurstBidCount)
        .Select(i => NewBid(auction, i % 2 == 0 ? bidderA : bidderB, 1_000_000m + (i * 50_000m), t0.AddMinutes(i * 3)))
        .ToList();

    var signals = new BidBurstClusterRule().Evaluate(BuildContext(auction, bids), settings);
    Check("BidBurstCluster stays quiet at one bid every 3 minutes", signals.Count == 0, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderA, 1_100_000m, t0.AddMinutes(1)),
        NewBid(auction, bidderA, 1_150_000m, t0.AddMinutes(2))
    };

    var signals = new PriceLadderRule().Evaluate(BuildContext(auction, bids), settings);
    Check("PriceLadder fires on 3 self-bids of exactly one step", signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0, isAuto: true, maxAuto: 1_500_000m),
        NewBid(auction, bidderB, 1_100_000m, t0.AddMinutes(1), isAuto: true, maxAuto: 1_520_000m),
        NewBid(auction, bidderA, 1_150_000m, t0.AddMinutes(2), isAuto: true, maxAuto: 1_540_000m)
    };

    var signals = new AutoBidWallRule().Evaluate(BuildContext(auction, bids), settings);
    Check("AutoBidWall fires on 3 ceilings within 5%", signals.Count == 1, $"signals={signals.Count}");
}

Console.WriteLine("2. Device-link rules");

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_100_000m, t0.AddMinutes(1))
    };

    var devices = new Dictionary<Guid, DeviceFingerprint>
    {
        [bidderA] = new(new HashSet<string> { "113.161.32.7" }, new HashSet<string> { "chrome-win" }),
        [bidderB] = new(new HashSet<string> { "113.161.32.7" }, new HashSet<string> { "chrome-win" }),
        [seller] = new(new HashSet<string> { "27.72.10.4" }, new HashSet<string> { "safari-mac" })
    };

    var shared = new SharedDeviceRule().Evaluate(BuildContext(auction, bids, devices), settings);
    Check("SharedDevice fires when two bidders share an IP", shared.Count == 1, $"signals={shared.Count}");
    Check("SharedDevice masks the IP in evidence", shared[0].Evidence.Contains("113.161.32.x"), shared[0].Evidence);

    var sellerLinked = new SellerLinkedBidderRule().Evaluate(
        BuildContext(auction, bids, devices, sellerDevices: devices[seller]), settings);
    Check("SellerLinkedBidder stays quiet when seller IP differs", sellerLinked.Count == 0, $"signals={sellerLinked.Count}");

    devices[bidderB] = new(new HashSet<string> { "27.72.10.4" }, new HashSet<string> { "safari-mac" });
    var linked = new SellerLinkedBidderRule().Evaluate(
        BuildContext(auction, bids, devices, sellerDevices: devices[seller]), settings);
    Check("SellerLinkedBidder fires when a bidder shares the seller IP", linked.Count == 1, $"signals={linked.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_100_000m, t0.AddMinutes(1))
    };

    var devices = new Dictionary<Guid, DeviceFingerprint>
    {
        [bidderA] = new(new HashSet<string> { "113.161.32.7" }, new HashSet<string>()),
        [bidderB] = new(new HashSet<string> { "203.113.5.9" }, new HashSet<string>()),
        [seller] = new(new HashSet<string> { "27.72.10.4" }, new HashSet<string>())
    };

    var shared = new SharedDeviceRule().Evaluate(BuildContext(auction, bids, devices), settings);
    Check("SharedDevice stays quiet on distinct IPs", shared.Count == 0, $"signals={shared.Count}");
}

Console.WriteLine("3. History rules");

{
    var auction = NewAuction();
    var bids = new List<BidEntity> { NewBid(auction, bidderA, 1_050_000m, t0) };
    var history = new Dictionary<Guid, BidderHistory> { [bidderA] = new(0, 2) };

    var signals = new NonPaymentHistoryRule().Evaluate(BuildContext(auction, bids, history: history), settings);
    Check("NonPaymentHistory fires at 2 unpaid wins", signals.Count == 1, $"signals={signals.Count}");

    var quiet = new NonPaymentHistoryRule().Evaluate(BuildContext(auction, bids, history: new Dictionary<Guid, BidderHistory> { [bidderA] = new(0, 1) }), settings);
    Check("NonPaymentHistory stays quiet at 1 unpaid win", quiet.Count == 0, $"signals={quiet.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity> { NewBid(auction, bidderA, 1_050_000m, t0) };
    var history = new Dictionary<Guid, BidderHistory> { [bidderA] = new(4, 2) };

    var signals = new WinNoPaymentRateRule().Evaluate(BuildContext(auction, bids, history: history), settings);
    Check("WinNoPaymentRate fires at 2/4 unpaid", signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = new List<BidEntity>
    {
        NewBid(auction, bidderA, 1_050_000m, t0),
        NewBid(auction, bidderB, 1_100_000m, t0.AddSeconds(20)),
        NewBid(auction, bidderA, 1_150_000m, t0.AddSeconds(40)),
        NewBid(auction, bidderB, 1_200_000m, t0.AddSeconds(60))
    };

    var signals = new SelfOutbidLoopRule().Evaluate(BuildContext(auction, bids), settings);
    Check("SelfOutbidLoop fires on A,B,A,B", signals.Count == 1, $"signals={signals.Count}");
}

{
    var auction = NewAuction();
    var bids = Enumerable.Range(0, 5)
        .Select(i => NewBid(auction, bidderA, 1_050_000m + (i * 50_000m), t0.AddMinutes(i)))
        .ToList();
    var createdAt = new Dictionary<Guid, DateTimeOffset> { [bidderA] = t0.AddHours(-2) };

    var signals = new NewAccountBurstRule().Evaluate(BuildContext(auction, bids, createdAt: createdAt), settings);
    Check("NewAccountBurst fires for a 2h-old account with 5 bids", signals.Count == 1, $"signals={signals.Count}");

    createdAt[bidderA] = t0.AddDays(-30);
    var quiet = new NewAccountBurstRule().Evaluate(BuildContext(auction, bids, createdAt: createdAt), settings);
    Check("NewAccountBurst stays quiet for an old account", quiet.Count == 0, $"signals={quiet.Count}");
}

Console.WriteLine("4. Scorer");

{
    var scorer = new AuctionRiskScorer();
    var empty = scorer.Score([], settings);
    Check("Empty evidence scores 0/Low", empty.Score == 0m && empty.Level == AuctionRiskLevel.Low);

    var mediumDrafts = new List<RiskSignalDraft>
    {
        RiskSignalDraft.ForAuction(RiskSignalCode.AutoBidWall, "3 ceilings"),
        RiskSignalDraft.ForBid(RiskSignalCode.BidVelocity, Guid.NewGuid(), bidderA, "7 bids")
    };
    var medium = scorer.Score(mediumDrafts, settings);
    Check("AutoBidWall + BidVelocity = 30 points -> Medium", medium.Score == 30m && medium.Level == AuctionRiskLevel.Medium, $"{medium.Score}/{medium.Level}");

    // Leo thang: Velocity (15) + BurstCluster (35) cùng cửa sổ ngắn -> +10 = 60 -> High.
    var burstDrafts = new List<RiskSignalDraft>
    {
        RiskSignalDraft.ForBid(RiskSignalCode.BidVelocity, Guid.NewGuid(), bidderA, "5 bids / 20s"),
        RiskSignalDraft.ForBid(RiskSignalCode.BidBurstCluster, Guid.NewGuid(), bidderB, "5 bids / 2 accounts")
    };
    var burst = scorer.Score(burstDrafts, settings);
    Check("Velocity + BurstCluster escalates to 60 -> High", burst.Score == 60m && burst.Level == AuctionRiskLevel.High, $"{burst.Score}/{burst.Level}");
    Check("Escalation is explained in the summary", burst.Summary.Contains("cộng thêm 10", StringComparison.Ordinal), burst.Summary);

    var highDrafts = new List<RiskSignalDraft>
    {
        RiskSignalDraft.ForUser(RiskSignalCode.SharedDevice, bidderB, "shared ip"),
        RiskSignalDraft.ForUser(RiskSignalCode.PriceLadder, bidderA, "ladder"),
        RiskSignalDraft.ForUser(RiskSignalCode.NonPaymentHistory, bidderA, "2 unpaid")
    };
    var high = scorer.Score(highDrafts, settings);
    Check("SharedDevice 30 + Ladder 15 + NonPayment 20 = 65 -> High", high.Score == 65m && high.Level == AuctionRiskLevel.High, $"{high.Score}/{high.Level}");
    Check("Summary reuses the scorer label of the strongest signal", high.Summary.Contains(AuctionRiskScorer.Labels[RiskSignalCode.SharedDevice], StringComparison.Ordinal) && high.Summary.Contains(AuctionRiskScorer.Labels[RiskSignalCode.NonPaymentHistory], StringComparison.Ordinal), high.Summary);

    var criticalDrafts = highDrafts
        .Append(RiskSignalDraft.ForUser(RiskSignalCode.SellerLinkedBidder, bidderA, "same ip as seller"))
        .ToList();
    var critical = scorer.Score(criticalDrafts, settings);
    Check("100 raw points clamp at 100 -> Critical", critical.Score == 100m && critical.Level == AuctionRiskLevel.Critical, $"{critical.Score}/{critical.Level}");
}

Console.WriteLine($"\nAll {passed} checks passed.");
