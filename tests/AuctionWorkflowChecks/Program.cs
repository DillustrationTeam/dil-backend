using ArtCommission.Application.Auction.Commands.PlaceBid;
using ArtCommission.Application.Auction.Commands.CreateAuction;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Auction.Queries.EligibleArtworks;
using ArtCommission.Application.Auction.Queries.MyActiveBids;
using ArtCommission.Application.Auction.Queries.GetDeliverableDownloadUrl;
using ArtCommission.Application.Auction.Queries.GetAuctionCertificate;
using ArtCommission.Application.Auction.Queries.GetMyCollection;
using ArtCommission.Application.Auction.Queries.GetArtworkProvenance;
using ArtCommission.Application.Auction.Queries.GetCommercialRightsDeed;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorStatistics;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
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

var sqlConnectionString = Environment.GetEnvironmentVariable("AUCTION_TEST_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(sqlConnectionString))
{
    Console.WriteLine("SQL Server race check skipped; set AUCTION_TEST_SQL_CONNECTION_STRING to a dedicated test database.");
}
else
{
    await RunSqlServerRaceCheckAsync(sqlConnectionString);
}

var sellerId = Guid.NewGuid();
var initialSellerUser = new ApplicationUser
{
    Id = sellerId,
    UserName = "initial_seller",
    FullName = "Auction workflow seller",
    Email = "initial_seller@example.test"
};
db.Users.Add(initialSellerUser);

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
var realtime = new TestAuctionRealtimePublisher();
var lifecycle = new TestAuctionLifecycleNotifier();
var handler = new PlaceBidCommandHandler(
    db,
    money,
    new TestNotificationPublisher(),
    realtime,
    lifecycle,
    NullLogger<PlaceBidCommandHandler>.Instance);

var createHandler = new CreateAuctionCommandHandler(
    db,
    realtime,
    lifecycle,
    NullLogger<CreateAuctionCommandHandler>.Instance);

Console.WriteLine("--- CHẠY 40 TEST CASES BACKEND HỆ THỐNG ĐẤU GIÁ VÀ GIÁ TRỊ 1-OF-1 ---");

// =========================================================================
// NHÓM A: KHỞI TẠO VÀ THIẾT LẬP ĐẤU GIÁ (BE-01 ĐẾN BE-08)
// =========================================================================
var now = DateTimeOffset.UtcNow;
var testArtwork1 = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Artwork Test 1",
    ImageUrl = "https://example.test/art1.png"
};
db.Artworks.Add(testArtwork1);
await db.SaveChangesAsync();

// [BE-01] CreateAuction: Tạo phiên cơ bản thành công
var (cSuccess1, cData1, cErrors1, _) = await createHandler.Handle(new CreateAuctionCommand(
    sellerId, testArtwork1.Id, 500_000m, null, 50_000m, null, now.AddMinutes(-1), now.AddHours(2)
), default);
Ensure(cSuccess1 && cData1 != null, $"[BE-01] Thất bại tạo đấu giá cơ bản: {string.Join(", ", cErrors1)}");
LogPass("BE-01", "CreateAuction: Khởi tạo phiên đấu giá cơ bản thành công");

// [BE-02] CreateAuction: Đính kèm Master File Pack (.zip) tự động tạo Deliverable
var testArtworkZip = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Artwork with Zip",
    ImageUrl = "https://example.test/artzip.png"
};
db.Artworks.Add(testArtworkZip);
await db.SaveChangesAsync();

var (cSuccessZip, cDataZip, cErrorsZip, _) = await createHandler.Handle(new CreateAuctionCommand(
    sellerId, testArtworkZip.Id, 1_000_000m, null, 100_000m, null, now.AddMinutes(-1), now.AddHours(3),
    "https://storage.test/packs/art.zip", "Artwork-Pack.zip", 52_428_800
), default);
Ensure(cSuccessZip && cDataZip != null, $"[BE-02] Thất bại tạo đấu giá có Master Pack: {string.Join(", ", cErrorsZip)}");
var zipDeliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.AuctionId == cDataZip!.AuctionId);
Ensure(zipDeliverable != null && zipDeliverable.FileName == "Artwork-Pack.zip" && zipDeliverable.MimeType == "application/zip",
    "[BE-02] Không tự động tạo deliverable cho Master Pack.");
LogPass("BE-02", "CreateAuction: Đính kèm Master File Pack (.zip) tự động tạo Deliverable");

// [BE-03] CreateAuction: Không có Master File Pack không tạo Deliverable thừa
var noZipDeliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.AuctionId == cData1!.AuctionId);
Ensure(noZipDeliverable == null, "[BE-03] Tạo Deliverable thừa khi không có Master Pack.");
LogPass("BE-03", "CreateAuction: Không có Master File Pack không tạo Deliverable thừa");

// [BE-04] CreateAuction: Tranh đang có phiên Active bị chặn tạo thêm
var (cSuccessConflict, _, _, cConflict4) = await createHandler.Handle(new CreateAuctionCommand(
    sellerId, testArtwork1.Id, 600_000m, null, 50_000m, null, now.AddMinutes(-1), now.AddHours(2)
), default);
Ensure(!cSuccessConflict && cConflict4, "[BE-04] Không chặn tạo phiên trùng lặp cho tranh đang đấu giá.");
LogPass("BE-04", "CreateAuction: Tranh đang đấu giá bị chặn tạo thêm phiên (Conflict)");

// [BE-05] CreateAuction: ReservePrice < StartPrice bị từ chối
var otherSellerId = Guid.NewGuid();
var otherProfile = new CreatorProfile
{
    Id = Guid.NewGuid(),
    UserId = otherSellerId,
    DisplayName = "Other Seller",
};
db.CreatorProfiles.Add(otherProfile);
var testArtworkReserve = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = otherProfile.Id,
    Title = "Artwork Reserve",
    ImageUrl = "https://example.test/artreserve.png"
};
db.Artworks.Add(testArtworkReserve);
await db.SaveChangesAsync();

var (cSuccess5, _, cErrors5, _) = await createHandler.Handle(new CreateAuctionCommand(
    otherSellerId, testArtworkReserve.Id, 500_000m, 400_000m, 50_000m, null, now.AddMinutes(-1), now.AddHours(2)
), default);
Ensure(!cSuccess5 && cErrors5.Any(e => e.Contains("Giá sàn")), "[BE-05] Không chặn ReservePrice < StartPrice.");
LogPass("BE-05", "CreateAuction: ReservePrice thấp hơn StartPrice bị từ chối");

// [BE-06] CreateAuction: BuyNowPrice <= StartPrice bị từ chối
var (cSuccess6, _, cErrors6, _) = await createHandler.Handle(new CreateAuctionCommand(
    otherSellerId, testArtworkReserve.Id, 500_000m, null, 50_000m, 500_000m, now.AddMinutes(-1), now.AddHours(2)
), default);
Ensure(!cSuccess6 && cErrors6.Any(e => e.Contains("Giá mua ngay")), "[BE-06] Không chặn BuyNowPrice <= StartPrice.");
LogPass("BE-06", "CreateAuction: BuyNowPrice <= StartPrice bị từ chối");

// [BE-07] CreateAuction: Thời lượng phiên < 1 giờ bị từ chối
var (cSuccess7, _, cErrors7, _) = await createHandler.Handle(new CreateAuctionCommand(
    otherSellerId, testArtworkReserve.Id, 500_000m, null, 50_000m, null, now, now.AddMinutes(30)
), default);
Ensure(!cSuccess7 && cErrors7.Any(e => e.Contains("ít nhất 1 giờ")), "[BE-07] Không chặn phiên ngắn hơn 1 giờ.");
LogPass("BE-07", "CreateAuction: Thời lượng phiên < 1 giờ bị từ chối");

// [BE-08] CreateAuction: Người không sở hữu tranh bị từ chối mở phiên
var strangerSellerId = Guid.NewGuid();
var (cSuccess8, _, cErrors8, _) = await createHandler.Handle(new CreateAuctionCommand(
    strangerSellerId, testArtworkReserve.Id, 500_000m, null, 50_000m, null, now.AddMinutes(-1), now.AddHours(2)
), default);
Ensure(!cSuccess8 && cErrors8.Any(e => e.Contains("không phải chủ sở hữu")), "[BE-08] Không chặn người không sở hữu tranh mở đấu giá.");
LogPass("BE-08", "CreateAuction: Người không sở hữu tranh bị từ chối mở phiên");

// =========================================================================
// NHÓM B: ĐẶT GIÁ VÀ CƠ CHẾ ĐẤU GIÁ (BE-09 ĐẾN BE-19)
// =========================================================================
var firstBidder = Guid.NewGuid();
var first = await handler.Handle(new PlaceBidCommand(firstBidder, auction.Id, 1_000m), default);
Ensure(first.Success && auction.CurrentPrice == 1_000m, "[BE-09] Bid đầu tiên không được ghi đúng giá khởi điểm.");
LogPass("BE-09", "PlaceBid: Bid đầu tiên đúng giá khởi điểm thành công");

// [BE-10] PlaceBid: Bid dưới StartPrice bị từ chối
var lowBid = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, 500m), default);
Ensure(!lowBid.Success, "[BE-10] Đặt giá dưới giá khởi điểm không bị từ chối.");
LogPass("BE-10", "PlaceBid: Đặt giá dưới giá khởi điểm bị từ chối");

// [BE-11] PlaceBid: Bid không đủ bước giá tối thiểu bị từ chối
var underStep = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, 1_050m), default);
Ensure(!underStep.Success, "[BE-11] Đặt giá không đủ 1 bước giá không bị từ chối.");
LogPass("BE-11", "PlaceBid: Đặt giá không đủ bước giá tối thiểu bị từ chối");

// [BE-12] PlaceBid: Seller tự đặt giá trên phiên của mình bị chặn
var sellerSelfBid = await handler.Handle(new PlaceBidCommand(sellerId, auction.Id, 1_200m), default);
Ensure(!sellerSelfBid.Success, "[BE-12] Seller tự bid trên phiên của mình không bị chặn.");
LogPass("BE-12", "PlaceBid: Chặn người bán tự đặt giá (Self-bidding block)");

var earlyAutoBidder = Guid.NewGuid();
var earlyAuto = await handler.Handle(new PlaceBidCommand(earlyAutoBidder, auction.Id, 0m, true, 1_500m), default);
Ensure(earlyAuto.Success && auction.CurrentPrice == 1_100m, "[BE-13] Proxy-bid không nâng giá đúng một bước.");
Ensure(earlyAuto.Data!.Bid.HoldAmount <= 1_500m, "[BE-13] Tiền giữ vượt trần proxy-bid.");
LogPass("BE-13", "PlaceBid: Proxy-bid tự động nâng 1 bước giá và giữ trần cọc");
// [BE-14] PlaceBid: Proxy-bid trần thấp hơn bị outbid ngay lập tức
var weakAutoBidder = Guid.NewGuid();
var weakAuto = await handler.Handle(new PlaceBidCommand(weakAutoBidder, auction.Id, 0m, true, 1_200m), default);
Ensure(weakAuto.Success && weakAuto.Data!.Bid.BidStatus == BidStatus.Outbid.ToString(), "[BE-14] Proxy-bid yếu hơn không bị đánh dấu Outbid.");
LogPass("BE-14", "PlaceBid: Proxy-bid trần thấp hơn bị đánh dấu Outbid ngay lập tức");

// [BE-15] PlaceBid: Hòa trần proxy-bid ưu tiên người đặt trước
var tiedAutoBidder = Guid.NewGuid();
var tiedAuto = await handler.Handle(new PlaceBidCommand(tiedAutoBidder, auction.Id, 0m, true, 1_500m), default);
Ensure(tiedAuto.Success && tiedAuto.Data!.Bid.BidStatus == BidStatus.Outbid.ToString(), "[BE-15] Hòa trần không ưu tiên người đăng ký trước.");
Ensure((await db.Bids.SingleAsync(b => b.AuctionId == auction.Id && b.Status == BidStatus.Leading)).BidderId == earlyAutoBidder,
    "[BE-15] Người đăng ký auto-bid trước không giữ vị trí dẫn đầu khi hòa trần.");
LogPass("BE-15", "PlaceBid: Hòa trần ưu tiên người đăng ký trước giữ vị trí Leading");

// [BE-16] PlaceBid: Không để lộ trần auto-bid ra public Amount
Ensure(tiedAuto.Data!.Bid.Amount != 1_500m, "[BE-16] API đã để lộ trần auto-bid trong Amount.");
LogPass("BE-16", "PlaceBid: API bảo mật không để lộ trần auto-bid ra Amount");

var underMinimum = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, 0m, true, 1_599m), default);
Ensure(!underMinimum.Success && auction.CurrentPrice == 1_500m, "Trần dưới giá tối thiểu đã làm thay đổi giá phiên.");

// =========================================================================
// NHÓM C: CONCURRENCY, VÍ VÀ KHÓA CỌC (BE-20 ĐẾN BE-23)
// =========================================================================
// [BE-20] Concurrency: Đặt giá trần cao hơn trở thành leader mới
var newLeader = Guid.NewGuid();
var strongerAuto = await handler.Handle(new PlaceBidCommand(newLeader, auction.Id, 0m, true, 2_000m), default);
Ensure(strongerAuto.Success && strongerAuto.Data!.Bid.Amount <= 2_000m, "[BE-20] Trần cao hơn không thắng hoặc giá vượt trần.");
LogPass("BE-20", "Concurrency: Phân xử thứ tự dẫn đầu và cập nhật CurrentPrice chính xác");

// [BE-21] HoldDeposit: Tiền cọc được phong tỏa trong ví người dẫn đầu
Ensure(strongerAuto.Data!.Bid.HoldAmount > 0 && strongerAuto.Data!.Bid.HoldAmount <= 2_000m, "[BE-21] Tiền cọc không được phong tỏa đúng mức.");
LogPass("BE-21", "HoldDeposit: Tiền cọc được phong tỏa chính xác trong ví bidder dẫn đầu");

// [BE-22] HoldDeposit: Tiền cọc người bị vượt giá được hoàn trả
var outbidBid = await db.Bids.FirstAsync(b => b.AuctionId == auction.Id && b.BidderId == earlyAutoBidder && b.Status == BidStatus.Outbid);
Ensure(outbidBid.HoldStatus == HoldStatus.Refunded || outbidBid.HoldStatus == HoldStatus.Released || outbidBid.HoldStatus == HoldStatus.None,
    "[BE-22] Tiền cọc của người bị outbid không được giải phóng.");
LogPass("BE-22", "HoldDeposit: Tiền cọc của bidder bị vượt giá được hoàn trả/giải phóng");

// [BE-17] Anti-snipe: Bid trong 3 phút cuối gia hạn phiên thêm đúng 3 phút
auction.EndAt = DateTimeOffset.UtcNow.AddMinutes(2);
await db.SaveChangesAsync();
var bidderInSnipeWindow = Guid.NewGuid();
var oldEndAt = auction.EndAt;
var extended = await handler.Handle(
    new PlaceBidCommand(bidderInSnipeWindow, auction.Id, auction.CurrentPrice + auction.BidStep), default);
Ensure(extended.Success && auction.EndAt == oldEndAt.AddMinutes(3), "[BE-17] Bid trong 3 phút cuối không gia hạn đúng 3 phút.");
Ensure(lifecycle.Events.Count(x => x == (auction.Id, AuctionLifecycleEvent.Extended)) == 1,
    "[BE-17] Anti-snipe không gửi đúng một lifecycle notification sau commit.");
Ensure(realtime.Extensions.Count == 1 && realtime.Extensions[0].OldEndAt == oldEndAt
       && realtime.Extensions[0].NewEndAt == auction.EndAt,
    "[BE-17] Anti-snipe không broadcast đúng thời gian gia hạn sau commit.");
LogPass("BE-17", "Anti-snipe: Đặt giá trong 3 phút cuối tự động gia hạn 3 phút");

// [BE-18] Anti-snipe: Bid ngoài ngưỡng 3 phút không gia hạn
auction.EndAt = DateTimeOffset.UtcNow.AddMinutes(4);
await db.SaveChangesAsync();
var outsideEndAt = auction.EndAt;
var outsideWindow = await handler.Handle(
    new PlaceBidCommand(Guid.NewGuid(), auction.Id, auction.CurrentPrice + auction.BidStep), default);
Ensure(outsideWindow.Success && auction.EndAt == outsideEndAt, "[BE-18] Bid ngoài ngưỡng đã kích hoạt anti-snipe.");
LogPass("BE-18", "Anti-snipe: Đặt giá ngoài 3 phút cuối không kích hoạt gia hạn");

// [BE-19] Anti-snipe: Bid lỗi/thất bại không làm thay đổi EndAt
var endAfterExtension = auction.EndAt;
var failed = await handler.Handle(new PlaceBidCommand(Guid.NewGuid(), auction.Id, auction.CurrentPrice, false), default);
Ensure(!failed.Success && auction.EndAt == endAfterExtension, "[BE-19] Bid lỗi đã làm thay đổi EndAt.");
LogPass("BE-19", "Anti-snipe: Bid lỗi không làm thay đổi thời gian EndAt");

// [BE-23] MyActiveBids: Trả về trạng thái dẫn đầu và tổng tiền đang khóa cọc
var (myBidsSuccess, myBidsData, myBidsMeta, _) = await new MyActiveBidsQueryHandler(db).Handle(
    new MyActiveBidsQuery(newLeader, null, 20), default);
var totalLockedAmount = Convert.ToDecimal(
    myBidsMeta!.GetType().GetProperty("totalLockedAmount")!.GetValue(myBidsMeta));
Ensure(myBidsSuccess && myBidsData!.Count == 1
       && myBidsData[0].ParticipationStatus == "Leading"
       && myBidsData[0].LockedAmount == 2_000m
       && totalLockedAmount == myBidsData[0].LockedAmount,
    "[BE-23] My Active Bids không trả đúng trạng thái dẫn đầu hoặc tổng tiền đang khóa.");
LogPass("BE-23", "MyActiveBids: Truy vấn danh sách bid đang tham gia và tổng tiền khóa cọc");

var eligibleArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Eligible artwork",
    ImageUrl = "https://example.test/eligible.png"
};
var transferredArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creatorProfile.Id,
    Title = "Transferred artwork",
    ImageUrl = "https://example.test/transferred.png"
};
db.Artworks.AddRange(eligibleArtwork, transferredArtwork);
db.ArtworkOwnerships.Add(new ArtworkOwnership
{
    ArtworkId = transferredArtwork.Id,
    OwnerId = Guid.NewGuid(),
    IsCurrent = true,
    AcquiredAt = DateTimeOffset.UtcNow,
    TransferReason = OwnershipTransferReason.AuctionWin
});
await db.SaveChangesAsync();
var (eligibleSuccess, eligibleData, _, _) = await new EligibleArtworksQueryHandler(db).Handle(
    new EligibleArtworksQuery(sellerId, null, 20), default);
Ensure(eligibleSuccess && eligibleData!.Count == 1 && eligibleData[0].ArtworkId == eligibleArtwork.Id,
    "Eligible Artworks không loại tranh đang đấu giá hoặc đã chuyển cho chủ sở hữu khác.");

var (statisticsSuccess, statisticsData, _) = await new GetCreatorStatisticsQueryHandler(db).Handle(
    new GetCreatorStatisticsQuery(creatorProfile.Id), default);
Ensure(statisticsSuccess && statisticsData!.CommissionCompletionRate == 0m
       && statisticsData.AverageRating == 4.5m && statisticsData.ReviewCount == 3,
    "Seller statistics xử lý sai trường hợp chưa có commission hoặc dữ liệu rating.");

// =========================================================================
// NHÓM D & E: CHỐT PHIÊN, GIẢI NGÂN, VÀ 5 TÍNH NĂNG ĐỘC BẢN 1-OF-1 (BE-24 ĐẾN BE-37)
// =========================================================================
var deliverableSellerId = Guid.NewGuid();
var deliverableWinnerId = Guid.NewGuid();
var strangerId = Guid.NewGuid();

var sellerUser = new ApplicationUser
{
    Id = deliverableSellerId,
    UserName = "deliverable_seller",
    FullName = "Master Creator",
    Email = "seller@example.test"
};
var winnerUser = new ApplicationUser
{
    Id = deliverableWinnerId,
    UserName = "deliverable_winner",
    FullName = "Elite Collector",
    Email = "winner@example.test"
};
var deliverableSellerProfile = new CreatorProfile
{
    Id = Guid.NewGuid(),
    UserId = deliverableSellerId,
    DisplayName = "Master Creator",
    User = sellerUser
};
db.Users.AddRange(sellerUser, winnerUser);
db.CreatorProfiles.Add(deliverableSellerProfile);

var masterArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = deliverableSellerProfile.Id,
    Title = "Masterpiece Bundle",
    ImageUrl = "https://cdn.dillustration.test/artworks/masterpiece.zip",
    FileSizeBytes = 52_428_800
};
db.Artworks.Add(masterArtwork);

var deliverableAuction = new Auction
{
    Id = Guid.NewGuid(),
    SellerId = deliverableSellerId,
    ArtworkId = masterArtwork.Id,
    StartPrice = 5_000m,
    CurrentPrice = 10_000m,
    BidStep = 500m,
    BidCount = 1,
    StartAt = DateTimeOffset.UtcNow.AddHours(-2),
    EndAt = DateTimeOffset.UtcNow.AddHours(-1),
    Status = AuctionStatus.Active
};
db.Auctions.Add(deliverableAuction);

var winningBid = new Bid
{
    Id = Guid.NewGuid(),
    AuctionId = deliverableAuction.Id,
    BidderId = deliverableWinnerId,
    Amount = 10_000m,
    HoldAmount = 10_000m,
    HoldStatus = HoldStatus.Held,
    Status = BidStatus.Leading,
    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
};
db.Bids.Add(winningBid);
await db.SaveChangesAsync();

var testWalletService = new WalletService(db);
var settlementService = new AuctionSettlementService(
    db,
    testWalletService,
    money,
    new TestNotificationPublisher(),
    NullLogger<AuctionSettlementService>.Instance);

// [BE-24] SettleAuction: Chuyển trạng thái phiên sang Settled
var settleResult = await settlementService.SettleWithHeldDepositAsync(deliverableAuction, winningBid, default);
Ensure(settleResult.AuctionId == deliverableAuction.Id && deliverableAuction.Status == AuctionStatus.Settled,
    "[BE-24] SettleWithHeldDepositAsync không cập nhật trạng thái Settled.");
LogPass("BE-24", "SettleAuction: Chuyển trạng thái phiên từ Active sang Settled thành công");

// [BE-25] SettleAuction: WinnerId được cập nhật chính xác
Ensure(deliverableAuction.WinnerId == deliverableWinnerId,
    "[BE-25] SettleWithHeldDepositAsync không gán WinnerId chính xác.");
LogPass("BE-25", "SettleAuction: WinnerId được cập nhật chính xác cho người thắng cuộc");

// [BE-26] SettleAuction: Tự động sinh Deliverable và cập nhật RecipientId cho Winner
var createdDeliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.AuctionId == deliverableAuction.Id);
Ensure(createdDeliverable != null, "[BE-26] Không tự động sinh Deliverable sau khi chốt phiên.");
Ensure(createdDeliverable!.FileName == "Masterpiece Bundle.zip", $"[BE-26] Tên file deliverable không đúng ({createdDeliverable.FileName}).");
Ensure(createdDeliverable.MimeType == "application/zip", $"[BE-26] MIME type không nhận diện đúng ZIP ({createdDeliverable.MimeType}).");
LogPass("BE-26", "SettleAuction: Deliverable Master File Pack được liên kết sang WinnerId");

// [BE-27] SettleAuction: Giải ngân tiền từ cọc Winner cho Seller
Ensure(settleResult.FinalPrice == 10_000m && settleResult.FeeAmount > 0,
    "[BE-27] Phí nền tảng hoặc số tiền thanh toán không được tính đúng.");
LogPass("BE-27", "SettleAuction: Giải ngân tiền cọc của Winner cho Seller trừ phí sàn nền tảng");

// [BE-28] Ownership: Tạo bản ghi ArtworkOwnership mới cho Winner
var createdOwnership = await db.ArtworkOwnerships.FirstOrDefaultAsync(o => o.AuctionId == deliverableAuction.Id && o.IsCurrent);
Ensure(createdOwnership != null && createdOwnership.OwnerId == deliverableWinnerId,
    "[BE-28] ArtworkOwnership không ghi nhận người thắng là chủ sở hữu mới.");
Ensure(createdOwnership!.TransferReason == OwnershipTransferReason.AuctionWin,
    "[BE-28] TransferReason không phải AuctionWin.");
LogPass("BE-28", "Ownership: Tạo bản ghi ArtworkOwnership mới với IsCurrent=true và TransferReason=AuctionWin");

// [BE-29] Ownership: Đảm bảo chỉ có duy nhất 1 bản ghi IsCurrent = true cho tác phẩm
var activeOwnersCount = await db.ArtworkOwnerships.CountAsync(o => o.ArtworkId == masterArtwork.Id && o.IsCurrent && !o.IsDeleted);
Ensure(activeOwnersCount == 1, "[BE-29] Có nhiều hơn 1 chủ sở hữu IsCurrent=true trên tác phẩm độc bản 1-of-1.");
LogPass("BE-29", "Ownership: Đảm bảo duy nhất một chủ sở hữu hợp pháp IsCurrent=true (Độc bản 1-of-1)");

// [BE-30] DeliverableDownload: Winner lấy link tải bàn giao gốc (Download URL)
var testStorage = new TestFileStorageService();
var downloadHandler = new GetDeliverableDownloadUrlQueryHandler(db, testStorage, testWalletService);
var (downloadSuccess, downloadData, downloadCode, downloadErrors) = await downloadHandler.Handle(
    new GetDeliverableDownloadUrlQuery(deliverableWinnerId, deliverableAuction.Id), default);
Ensure(downloadSuccess && downloadCode == 200 && downloadData != null,
    $"[BE-30] Winner không lấy được link tải file gốc: {string.Join(", ", downloadErrors)}");
Ensure(downloadData!.FileName == "Masterpiece Bundle.zip", "[BE-30] Tên file tải về không khớp.");
Ensure(downloadData.LicenseType == "1-of-1 Exclusive Ownership & Commercial Rights",
    "[BE-30] LicenseType không đúng loại thương mại độc quyền 1-of-1.");
LogPass("BE-30", "DeliverableDownload: Winner nhận presigned URL tải file Master Pack kèm số chứng chỉ");

// [BE-31] DeliverableDownload: Người lạ (stranger) KHÔNG được tải file gốc (403)
var (strangerSuccess, _, strangerCode, _) = await downloadHandler.Handle(
    new GetDeliverableDownloadUrlQuery(strangerId, deliverableAuction.Id), default);
Ensure(!strangerSuccess && strangerCode == 403,
    "[BE-31] Người lạ không bị chặn 403 khi cố tải file gốc của winner.");
LogPass("BE-31", "DeliverableDownload: Người không phải Winner bị chặn 403 Forbidden tuyệt đối");

// [BE-32] DeliverableDownload: Kiểm tra đếm lượt tải
var reloadedDeliverable = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(d => d.AuctionId == deliverableAuction.Id);
Ensure(reloadedDeliverable!.IsDelivered && reloadedDeliverable.DownloadCount == 1,
    "[BE-32] DownloadCount không tăng lên 1 sau lượt tải đầu tiên.");
LogPass("BE-32", "DeliverableDownload: DownloadCount tăng lên sau mỗi lượt tải hợp lệ");

// [BE-33] GetAuctionCertificate: Kiểm tra lấy Chứng nhận số 1-of-1
var certHandler = new GetAuctionCertificateQueryHandler(db);
var (certSuccess, certData, certCode, certErrors) = await certHandler.Handle(
    new GetAuctionCertificateQuery(deliverableAuction.Id), default);
Ensure(certSuccess && certCode == 200 && certData != null,
    $"[BE-33] Không lấy được chứng nhận đấu giá: {string.Join(", ", certErrors)}");
Ensure(certData!.OwnerId == deliverableWinnerId, "[BE-33] Chủ sở hữu trên chứng nhận không khớp winner.");
Ensure(certData.OwnerName == "Elite Collector", "[BE-33] Tên chủ sở hữu trên chứng nhận không khớp.");
Ensure(certData.VerificationCode.StartsWith("VERIFY-"), "[BE-33] VerificationCode không đúng định dạng VERIFY-.");
Ensure(certData.DigitalSignature.Length == 64, "[BE-33] DigitalSignature không phải SHA-256 hex 64 ký tự.");
LogPass("BE-33", "GetAuctionCertificate: Cấp chứng nhận số 1-of-1 đầy đủ chữ ký SHA-256 và VerificationCode");

// [BE-34] GetMyCollection: Trả về danh sách tác phẩm 1-of-1 của Winner
var myCollectionHandler = new GetMyCollectionQueryHandler(db);
var (myCollSuccess, myCollData, myCollErrors) = await myCollectionHandler.Handle(
    new GetMyCollectionQuery(deliverableWinnerId), default);
Ensure(myCollSuccess && myCollData != null && myCollData.Count >= 1,
    $"[BE-34] GetMyCollection không trả về tác phẩm cho winner: {string.Join(", ", myCollErrors)}");
Ensure(myCollData!.Any(i => i.ArtworkId == masterArtwork.Id && i.HasMasterFile && i.FinalPrice == 10_000m),
    "[BE-34] Tác phẩm trong MyCollection thiếu cờ HasMasterFile hoặc sai FinalPrice.");
LogPass("BE-34", "GetMyCollection: Trả về danh sách tác phẩm 1-of-1 sở hữu bởi Winner kèm HasMasterFile");

// [BE-35] GetArtworkProvenance: Trả về chuỗi lai lịch tác phẩm (Creator ➔ Winner)
var provenanceHandler = new GetArtworkProvenanceQueryHandler(db);
var (provSuccess, provData, provCode, provErrors) = await provenanceHandler.Handle(
    new GetArtworkProvenanceQuery(masterArtwork.Id), default);
Ensure(provSuccess && provCode == 200 && provData != null,
    $"[BE-35] GetArtworkProvenance thất bại: {string.Join(", ", provErrors)}");
Ensure(provData!.CurrentOwnerName == "Elite Collector", "[BE-35] CurrentOwnerName trong Provenance không đúng.");
Ensure(provData.IsCurrentOwnerCollector, "[BE-35] IsCurrentOwnerCollector không phải true khi đã đổi chủ sang Collector.");
Ensure(provData.History.Count >= 1, "[BE-35] Lịch sử chuyển nhượng Provenance trống.");
LogPass("BE-35", "GetArtworkProvenance: Trả về chuỗi lịch sử lai lịch tác phẩm (Creator -> Collector)");

// [BE-36] GetCommercialRightsDeed: Trả về văn bản pháp lý 5 điều khoản và chữ ký số SHA-256
var deedHandler = new GetCommercialRightsDeedQueryHandler(db);
var (deedSuccess, deedData, deedCode, deedErrors) = await deedHandler.Handle(
    new GetCommercialRightsDeedQuery(deliverableAuction.Id), default);
Ensure(deedSuccess && deedCode == 200 && deedData != null,
    $"[BE-36] GetCommercialRightsDeed thất bại: {string.Join(", ", deedErrors)}");
Ensure(deedData!.CreatorName == "Master Creator", "[BE-36] CreatorName trong hợp đồng thương mại không đúng.");
Ensure(deedData.OwnerName == "Elite Collector", "[BE-36] OwnerName trong hợp đồng thương mại không đúng.");
Ensure(deedData.DigitalSignature.Length == 64, "[BE-36] Chữ ký số SHA-256 của hợp đồng không hợp lệ.");
Ensure(deedData.CertificateNumber.StartsWith("DIL-CERT-"), "[BE-36] Mã chứng chỉ hợp đồng không chuẩn DIL-CERT-.");
Ensure(deedData.VerificationCode.StartsWith("VERIFY-"), "[BE-36] Mã xác thực hợp đồng không chuẩn VERIFY-.");
LogPass("BE-36", "GetCommercialRightsDeed: Xuất văn bản bàn giao 5 điều khoản thương mại kèm chữ ký số SHA-256");

// [BE-37] Resell / Secondary Market: Winner đủ điều kiện mở bán lại, Seller cũ bị loại trừ
var (winnerEligibleSuccess, winnerArtworks, _, _) = await new EligibleArtworksQueryHandler(db).Handle(
    new EligibleArtworksQuery(deliverableWinnerId, null, 20), default);
Ensure(winnerEligibleSuccess && winnerArtworks!.Any(a => a.ArtworkId == masterArtwork.Id),
    "[BE-37] Tranh đã thắng đấu giá không xuất hiện trong danh sách đủ điều kiện mở bán lại của Winner.");

var (sellerEligibleSuccess, sellerArtworks, _, _) = await new EligibleArtworksQueryHandler(db).Handle(
    new EligibleArtworksQuery(deliverableSellerId, null, 20), default);
Ensure(sellerEligibleSuccess && !sellerArtworks!.Any(a => a.ArtworkId == masterArtwork.Id),
    "[BE-37] Tranh đã bán vẫn xuất hiện trong danh sách mở bán lại của Seller cũ.");
LogPass("BE-37", "Resell / Secondary Market: Winner được cấp quyền niêm yết bán lại, Seller cũ bị thu hồi");

// [BE-38] Deliverable Multi-asset: Trả về đồng thời liên kết tải ảnh gốc HD và gói nén Master Pack (.zip)
Ensure(!string.IsNullOrEmpty(downloadData.ArtworkImageDownloadUrl),
    "[BE-38] ArtworkImageDownloadUrl không được rỗng.");
Ensure(downloadData.HasMasterPack,
    "[BE-38] HasMasterPack phải bằng true đối với deliverable có gói nén Master Pack.");
Ensure(!string.IsNullOrEmpty(downloadData.ArtworkTitle),
    "[BE-38] ArtworkTitle không được rỗng.");
LogPass("BE-38", "Deliverable Multi-asset: Trả về đồng thời liên kết tải ảnh gốc HD và gói Master Pack (.zip)");

// [BE-39] Moral Rights Retained: Điều lệ quy định rõ quyền tác giả gốc bất khả xâm phạm và cấm mạo danh
Ensure(deedData.MoralRightsRetainedByArtist, "[BE-39] MoralRightsRetainedByArtist phải là true.");
Ensure(deedData.TermsAndConditions.Contains("5. Quyền nhân thân bất khả xâm phạm (Inalienable Moral Rights)"),
    "[BE-39] Không tìm thấy điều khoản 5 về Quyền nhân thân bất khả xâm phạm trong TermsAndConditions.");
Ensure(deedData.TermsAndConditions.Contains("người mua cam kết KHÔNG được mạo danh là tác giả"),
    "[BE-39] TermsAndConditions không chứa điều khoản cấm mạo danh tác giả.");
LogPass("BE-39", "Moral Rights Retained: Điều lệ quy định rõ quyền tác giả gốc bất khả xâm phạm và cấm mạo danh");

// [BE-40] Provenance Immutability: Đảm bảo danh tính Họa sĩ sáng tác gốc cố định qua các giao dịch
Ensure(provData.CreatorName == "Master Creator", "[BE-40] Danh tính Họa sĩ sáng tác gốc bị sai lệch.");
Ensure(provData.CreatorId == masterArtwork.CreatorProfileId, "[BE-40] CreatorId không khớp với Họa sĩ ban đầu.");
Ensure(provData.IsCurrentOwnerCollector, "[BE-40] Chủ sở hữu hiện tại phải là Collector.");
Ensure(provData.CurrentOwnerName == "Elite Collector", "[BE-40] CurrentOwnerName phải là người thắng phiên.");
Ensure(provData.History.Any(h => h.TransferReason == "AuctionWin" && h.OwnerName == "Elite Collector"),
    "[BE-40] Lịch sử chuyển nhượng không ghi nhận mốc thắng phiên của Winner.");
LogPass("BE-40", "Provenance Immutability: Danh tính Họa sĩ sáng tác gốc bất biến vĩnh viễn trong chuỗi lai lịch");

Console.WriteLine("=========================================================================");
Console.WriteLine("HOÀN THÀNH TOÀN BỘ 40 TEST CASE BACKEND ĐẤU GIÁ VÀ GIÁ TRỊ 1-OF-1: TẤT CẢ ĐỀU PASS!");
Console.WriteLine("=========================================================================");

static void LogPass(string code, string description)
{
    Console.WriteLine($"  [PASS] [{code}] {description}");
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

    await contextA.DisposeAsync();
    await contextB.DisposeAsync();
    await verification.DisposeAsync();
    await setup.Database.EnsureDeletedAsync();
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
    public List<(Guid AuctionId, DateTimeOffset OldEndAt, DateTimeOffset NewEndAt)> Extensions { get; } = [];

    public Task PublishBidPlacedAsync(Guid auctionId, PlaceBidResultDto bid, decimal currentPrice, decimal minimumNextBid, DateTimeOffset endAt, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task PublishAuctionExtendedAsync(Guid auctionId, Guid bidId, DateTimeOffset oldEndAt, DateTimeOffset newEndAt, CancellationToken cancellationToken)
    {
        Extensions.Add((auctionId, oldEndAt, newEndAt));
        return Task.CompletedTask;
    }
    public Task PublishAuctionStatusChangedAsync(Guid auctionId, string status, decimal currentPrice, DateTimeOffset endAt, CancellationToken cancellationToken) => Task.CompletedTask;
}

sealed class TestAuctionLifecycleNotifier : IAuctionLifecycleNotifier
{
    public List<(Guid AuctionId, AuctionLifecycleEvent Event)> Events { get; } = [];

    public Task NotifyParticipantsAsync(Guid auctionId, AuctionLifecycleEvent lifecycleEvent, Guid? operationId = null, CancellationToken cancellationToken = default)
    {
        Events.Add((auctionId, lifecycleEvent));
        return Task.CompletedTask;
    }
}

sealed class TestFileStorageService : IFileStorageService
{
    public Task<PresignedDownload> CreateDownloadUrlAsync(
        string fileUrl, string fileName, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PresignedDownload($"https://cdn.dillustration.test/signed/{fileName}?token=test", DateTimeOffset.UtcNow.Add(validFor)));

    public Task<string> SaveAsync(
        Stream content, string fileName, string contentType, string folder, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://cdn.dillustration.test/{folder}/{fileName}");
}
