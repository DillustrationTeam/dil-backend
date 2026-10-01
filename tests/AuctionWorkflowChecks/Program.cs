using ArtCommission.Application.Auction.Commands.PlaceBid;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Auction.Queries.EligibleArtworks;
using ArtCommission.Application.Auction.Queries.MyActiveBids;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorStatistics;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Notifications;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("auction-workflow-" + Guid.NewGuid())
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .Options;
await using var db = new AppDbContext(options);
var sellerId = Guid.NewGuid();
var creatorProfile = new CreatorProfile
{
    Id = Guid.NewGuid(),
    UserId = sellerId,
    DisplayName = "Auction workflow seller",
    RatingAverage = 4.5m,
    RatingCount = 3
};
var auctionArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Auction workflow artwork",
    ImageUrl = "https://example.test/auction.png"
};
var auction = new Auction
{
    Id = Guid.NewGuid(),
    SellerId = sellerId,
    ArtworkId = auctionArtwork.Id,
    StartPrice = 1_000m,
    CurrentPrice = 1_000m,
    BidStep = 100m,
    BidCount = 0,
    StartAt = DateTimeOffset.UtcNow.AddMinutes(-1),
    EndAt = DateTimeOffset.UtcNow.AddMinutes(10),
    Status = AuctionStatus.Active
};
db.CreatorProfiles.Add(creatorProfile);
db.Artworks.Add(auctionArtwork);
db.Auctions.Add(auction);
await db.SaveChangesAsync();

var money = new TestAuctionMoneyService();
var handler = new PlaceBidCommandHandler(
    db,
    money,
    new TestNotificationPublisher(),
    new TestAuctionRealtimePublisher(),
    new TestAuctionLifecycleNotifier(),
    NullLogger<PlaceBidCommandHandler>.Instance);

var firstBidder = Guid.NewGuid();
var first = await handler.Handle(new PlaceBidCommand(firstBidder, auction.Id, 1_000m), default);
Ensure(first.Success && auction.CurrentPrice == 1_000m, "Bid đầu tiên không được ghi đúng giá khởi điểm.");

var earlyAutoBidder = Guid.NewGuid();
var earlyAuto = await handler.Handle(new PlaceBidCommand(earlyAutoBidder, auction.Id, 0m, true, 1_500m), default);
Ensure(earlyAuto.Success && auction.CurrentPrice == 1_100m, "Proxy-bid không nâng giá đúng một bước.");
Ensure(earlyAuto.Data!.Bid.HoldAmount <= 1_500m, "Tiền giữ vượt trần proxy-bid.");

var tiedAutoBidder = Guid.NewGuid();
var tiedAuto = await handler.Handle(new PlaceBidCommand(tiedAutoBidder, auction.Id, 0m, true, 1_500m), default);
Ensure(tiedAuto.Success && tiedAuto.Data!.Bid.BidStatus == BidStatus.Outbid.ToString(), "Hòa trần không ưu tiên người đăng ký trước.");
Ensure(tiedAuto.Data!.Bid.Amount != 1_500m, "API đã để lộ trần auto-bid trong Amount.");
Ensure((await db.Bids.SingleAsync(b => b.AuctionId == auction.Id && b.Status == BidStatus.Leading)).BidderId == earlyAutoBidder,
    "Người đăng ký auto-bid trước không giữ vị trí dẫn đầu khi hòa trần.");

var underMinimum = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, 0m, true, 1_599m), default);
Ensure(!underMinimum.Success && auction.CurrentPrice == 1_500m, "Trần dưới giá tối thiểu đã làm thay đổi giá phiên.");

var newLeader = Guid.NewGuid();
var strongerAuto = await handler.Handle(new PlaceBidCommand(newLeader, auction.Id, 0m, true, 2_000m), default);
Ensure(strongerAuto.Success && strongerAuto.Data!.Bid.Amount <= 2_000m, "Trần cao hơn không thắng hoặc giá vượt trần.");

auction.EndAt = DateTimeOffset.UtcNow.AddMinutes(2);
await db.SaveChangesAsync();
var bidderInSnipeWindow = Guid.NewGuid();
var oldEndAt = auction.EndAt;
var extended = await handler.Handle(
    new PlaceBidCommand(bidderInSnipeWindow, auction.Id, auction.CurrentPrice + auction.BidStep), default);
Ensure(extended.Success && auction.EndAt == oldEndAt.AddMinutes(3), "Bid trong 3 phút cuối không gia hạn đúng 3 phút.");

var endAfterExtension = auction.EndAt;
var failed = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, auction.CurrentPrice, false), default);
Ensure(!failed.Success && auction.EndAt == endAfterExtension, "Bid lỗi đã làm thay đổi EndAt.");

auction.EndAt = DateTimeOffset.UtcNow.AddMinutes(4);
await db.SaveChangesAsync();
var outsideEndAt = auction.EndAt;
var outsideWindow = await handler.Handle(
    new PlaceBidCommand(Guid.NewGuid(), auction.Id, auction.CurrentPrice + auction.BidStep), default);
Ensure(outsideWindow.Success && auction.EndAt == outsideEndAt, "Bid ngoài ngưỡng đã kích hoạt anti-snipe.");

var (myBidsSuccess, myBidsData, myBidsMeta, _) = await new MyActiveBidsQueryHandler(db).Handle(
    new MyActiveBidsQuery(newLeader, null, 20), default);
var totalLockedAmount = Convert.ToDecimal(
    myBidsMeta!.GetType().GetProperty("totalLockedAmount")!.GetValue(myBidsMeta));
Ensure(myBidsSuccess && myBidsData!.Count == 1
       && myBidsData[0].ParticipationStatus == "Leading"
       && myBidsData[0].LockedAmount == 2_000m
       && totalLockedAmount == myBidsData[0].LockedAmount,
    "My Active Bids không trả đúng trạng thái dẫn đầu hoặc tổng tiền đang khóa.");

var eligibleArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Eligible artwork",
    ImageUrl = "https://example.test/eligible.png"
};
db.Artworks.Add(eligibleArtwork);
await db.SaveChangesAsync();
var (eligibleSuccess, eligibleData, _, _) = await new EligibleArtworksQueryHandler(db).Handle(
    new EligibleArtworksQuery(sellerId, null, 20), default);
Ensure(eligibleSuccess && eligibleData!.Count == 1 && eligibleData[0].ArtworkId == eligibleArtwork.Id,
    "Eligible Artworks không loại tranh đang thuộc phiên đấu giá còn hiệu lực.");

var (statisticsSuccess, statisticsData, _) = await new GetCreatorStatisticsQueryHandler(db).Handle(
    new GetCreatorStatisticsQuery(creatorProfile.Id), default);
Ensure(statisticsSuccess && statisticsData!.CommissionCompletionRate == 0m
       && statisticsData.AverageRating == 4.5m && statisticsData.ReviewCount == 3,
    "Seller statistics xử lý sai trường hợp chưa có commission hoặc dữ liệu rating.");

Console.WriteLine("Auction workflow checks passed (proxy caps, tie order, anti-snipe, active bids, eligible artworks, seller statistics).");

var sqlConnectionString = Environment.GetEnvironmentVariable("AUCTION_TEST_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(sqlConnectionString))
{
    Console.WriteLine("SQL Server race check skipped; set AUCTION_TEST_SQL_CONNECTION_STRING to a dedicated test database.");
}
else
{
    await RunSqlServerRaceCheckAsync(sqlConnectionString);
}

static void Ensure(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task RunSqlServerRaceCheckAsync(string connectionString)
{
    var parsedConnection = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
    var databaseName = parsedConnection.TryGetValue("Initial Catalog", out var initialCatalog)
        ? initialCatalog?.ToString()
        : parsedConnection.TryGetValue("Database", out var database) ? database?.ToString() : null;
    if (string.IsNullOrWhiteSpace(databaseName) || !databaseName.Contains("test", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("SQL Server race check chỉ chạy với database có tên chứa 'test'.");
    }

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(connectionString)
        .Options;
    await using var setup = new AppDbContext(options);
    await setup.Database.MigrateAsync();

    var sellerId = Guid.NewGuid();
    var bidderAId = Guid.NewGuid();
    var bidderBId = Guid.NewGuid();
    var profileId = Guid.NewGuid();
    var artworkId = Guid.NewGuid();
    var auctionId = Guid.NewGuid();
    setup.Users.AddRange(
        new ApplicationUser { Id = sellerId, UserName = $"auction-seller-{sellerId:N}", NormalizedUserName = $"AUCTION-SELLER-{sellerId:N}", Email = $"{sellerId:N}@example.test", NormalizedEmail = $"{sellerId:N}@EXAMPLE.TEST", FullName = "Seller" },
        new ApplicationUser { Id = bidderAId, UserName = $"auction-bidder-{bidderAId:N}", NormalizedUserName = $"AUCTION-BIDDER-{bidderAId:N}", Email = $"{bidderAId:N}@example.test", NormalizedEmail = $"{bidderAId:N}@EXAMPLE.TEST", FullName = "Bidder A" },
        new ApplicationUser { Id = bidderBId, UserName = $"auction-bidder-{bidderBId:N}", NormalizedUserName = $"AUCTION-BIDDER-{bidderBId:N}", Email = $"{bidderBId:N}@example.test", NormalizedEmail = $"{bidderBId:N}@EXAMPLE.TEST", FullName = "Bidder B" });
    setup.CreatorProfiles.Add(new CreatorProfile
    {
        Id = profileId, UserId = sellerId, DisplayName = "Concurrency test seller"
    });
    setup.Artworks.Add(new Artwork
    {
        Id = artworkId, CreatorProfileId = profileId, Title = "Concurrency test artwork", ImageUrl = "https://example.test/art.png"
    });
    setup.Auctions.Add(new Auction
    {
        Id = auctionId,
        SellerId = sellerId,
        ArtworkId = artworkId,
        StartPrice = 1_000m,
        CurrentPrice = 1_000m,
        BidStep = 100m,
        BidCount = 0,
        StartAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        EndAt = DateTimeOffset.UtcNow.AddMinutes(10),
        Status = AuctionStatus.Active
    });
    await setup.SaveChangesAsync();

    var barrierMoney = new BarrierAuctionMoneyService();
    await using var contextA = new AppDbContext(options);
    await using var contextB = new AppDbContext(options);
    var handlerA = new PlaceBidCommandHandler(contextA, barrierMoney, new TestNotificationPublisher(), new TestAuctionRealtimePublisher(), new TestAuctionLifecycleNotifier(), NullLogger<PlaceBidCommandHandler>.Instance);
    var handlerB = new PlaceBidCommandHandler(contextB, barrierMoney, new TestNotificationPublisher(), new TestAuctionRealtimePublisher(), new TestAuctionLifecycleNotifier(), NullLogger<PlaceBidCommandHandler>.Instance);
    var results = await Task.WhenAll(
        handlerA.Handle(new PlaceBidCommand(bidderAId, auctionId, 1_000m), default),
        handlerB.Handle(new PlaceBidCommand(bidderBId, auctionId, 1_000m), default));

    Ensure(results.Count(x => x.Success) == 1, "Hai bid SQL Server đồng thời không được phân xử thành đúng một người thắng.");
    Ensure(results.Count(x => x.Conflict is not null) == 1, "Request thua cuộc đua SQL Server chưa trả 409 kèm giá mới.");
    await using var verification = new AppDbContext(options);
    var leadingBids = await verification.Bids.AsNoTracking()
        .Where(b => b.AuctionId == auctionId && b.Status == BidStatus.Leading && !b.IsDeleted)
        .ToListAsync();
    Ensure(leadingBids.Count == 1, "Filtered unique index không giữ đúng một bid Leading.");
    var latestAuction = await verification.Auctions.AsNoTracking().SingleAsync(a => a.Id == auctionId);
    Ensure(results.Single(x => !x.Success).Conflict!.CurrentPrice == latestAuction.CurrentPrice,
        "409 không trả CurrentPrice mới từ SQL Server.");

    Console.WriteLine("SQL Server race check passed (one leading bid, loser receives 409/current price).");
}

sealed class TestAuctionMoneyService : IAuctionMoneyService
{
    private readonly Dictionary<Guid, (decimal Balance, decimal Locked)> _wallets = [];

    public Task<(AuctionHoldResult Result, WalletTransaction? Ledger)> HoldDepositAsync(
        Guid userId, Guid bidId, decimal amount, CancellationToken cancellationToken = default)
    {
        var wallet = _wallets.TryGetValue(userId, out var current)
            ? current
            : (Balance: 100_000m, Locked: 0m);
        if (wallet.Balance < amount)
        {
            return Task.FromResult((AuctionHoldResult.Insufficient(wallet.Balance, "Không đủ số dư."), (WalletTransaction?)null));
        }

        wallet = (wallet.Balance - amount, wallet.Locked + amount);
        _wallets[userId] = wallet;
        return Task.FromResult((AuctionHoldResult.Ok(wallet.Balance, wallet.Locked), (WalletTransaction?)null));
    }

    public Task<WalletTransaction?> RefundDepositAsync(Bid bid, string reason, CancellationToken cancellationToken = default)
    {
        if (bid.HoldStatus == HoldStatus.Held && _wallets.TryGetValue(bid.BidderId, out var wallet))
        {
            _wallets[bid.BidderId] = (wallet.Balance + bid.HoldAmount, wallet.Locked - bid.HoldAmount);
            bid.HoldStatus = HoldStatus.Refunded;
        }
        return Task.FromResult<WalletTransaction?>(null);
    }

    public Task<WalletTransaction?> ReleaseDepositAsync(Bid bid, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult<WalletTransaction?>(null);
}

sealed class BarrierAuctionMoneyService : IAuctionMoneyService
{
    private readonly TaskCompletionSource<bool> _bothBidsReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    public async Task<(AuctionHoldResult Result, WalletTransaction? Ledger)> HoldDepositAsync(
        Guid userId, Guid bidId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _arrivals) == 2)
        {
            _bothBidsReady.TrySetResult(true);
        }

        await _bothBidsReady.Task.WaitAsync(cancellationToken);
        return (AuctionHoldResult.Ok(100_000m - amount, amount), null);
    }

    public Task<WalletTransaction?> RefundDepositAsync(Bid bid, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult<WalletTransaction?>(null);

    public Task<WalletTransaction?> ReleaseDepositAsync(Bid bid, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult<WalletTransaction?>(null);
}

sealed class TestNotificationPublisher : INotificationPublisher
{
    public Task<NotificationDto?> PublishAsync(Guid userId, NotificationType notificationType, string title, string body,
        string? refType = null, Guid? refId = null, NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null, CancellationToken cancellationToken = default) => Task.FromResult<NotificationDto?>(null);
}

sealed class TestAuctionRealtimePublisher : IAuctionRealtimePublisher
{
    public Task PublishBidPlacedAsync(Guid auctionId, PlaceBidResultDto bid, decimal currentPrice, decimal minimumNextBid, DateTimeOffset endAt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task PublishAuctionExtendedAsync(Guid auctionId, Guid bidId, DateTimeOffset oldEndAt, DateTimeOffset newEndAt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task PublishAuctionStatusChangedAsync(Guid auctionId, string status, decimal currentPrice, DateTimeOffset endAt, CancellationToken cancellationToken) => Task.CompletedTask;
}

sealed class TestAuctionLifecycleNotifier : IAuctionLifecycleNotifier
{
    public Task NotifyParticipantsAsync(Guid auctionId, AuctionLifecycleEvent lifecycleEvent, Guid? operationId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
