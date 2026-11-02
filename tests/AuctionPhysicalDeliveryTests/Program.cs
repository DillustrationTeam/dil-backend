using ArtCommission.Application.Auction.Commands.CreateAuction;
using ArtCommission.Application.Auction.Commands.Delivery;
using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuctionPhysicalDeliveryTests;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("================================================================");
        Console.WriteLine("  BẮT ĐẦU CHẠY TOÀN BỘ TEST SUITE: CƠ CHẾ GIAO NHẬN TRANH VẬT LÝ");
        Console.WriteLine("================================================================\n");

        int passed = 0;
        int failed = 0;

        void AssertTest(string name, bool condition, string detail = "")
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  [PASS] ");
                Console.ResetColor();
                Console.WriteLine($"{name} {(string.IsNullOrEmpty(detail) ? "" : $"({detail})")}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  [FAIL] ");
                Console.ResetColor();
                Console.WriteLine($"{name} FAILED! {detail}");
                failed++;
            }
        }

        try
        {
            // -------------------------------------------------------------
            // TC-01: Validator CreateAuctionCommand
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-01: Kiểm tra Validate CreateAuctionCommand (Bản vật lý) ---");
            var validator = new CreateAuctionCommandValidator();

            var validCmd = new CreateAuctionCommand(
                UserId: Guid.NewGuid(),
                ArtworkId: Guid.NewGuid(),
                StartPrice: 100_000m,
                ReservePrice: 150_000m,
                BidStep: 10_000m,
                BuyNowPrice: 500_000m,
                StartAt: DateTimeOffset.UtcNow.AddMinutes(5),
                EndAt: DateTimeOffset.UtcNow.AddHours(25),
                HasPhysicalCopy: true,
                PhysicalDescription: "Tranh toan sơn dầu khổ 60x80cm đóng khung composite cao cấp.");
            var resValid = validator.Validate(validCmd);
            AssertTest("TC-01a: Mô tả tranh vật lý <= 500 ký tự hợp lệ", resValid.Errors.All(e => e.PropertyName != nameof(CreateAuctionCommand.PhysicalDescription)));

            var longDescCmd = validCmd with { PhysicalDescription = new string('A', 501) };
            var resLong = validator.Validate(longDescCmd);
            AssertTest("TC-01b: Mô tả tranh vật lý > 500 ký tự bị chặn", resLong.Errors.Any(e => e.PropertyName == nameof(CreateAuctionCommand.PhysicalDescription)));

            // -------------------------------------------------------------
            // TC-02: Chốt phiên - Phân nhánh Tiền số vs Tranh vật lý
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-02: Phân nhánh Tiền số vs Tranh vật lý khi Chốt phiên ---");
            var db = CreateInMemoryDbContext("TestDb_TC02");
            var notifications = new FakeNotificationPublisher();
            var walletService = new WalletService(db);
            var moneyService = new AuctionMoneyService(walletService);
            var settlementService = new AuctionSettlementService(
                db, walletService, moneyService, notifications, NullLogger<AuctionSettlementService>.Instance);

            var sellerId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();
            var artworkId = Guid.NewGuid();

            // Seed Artwork
            db.Artworks.Add(new Artwork { Id = artworkId, CreatorProfileId = sellerId, Title = "Đêm Trăng Sơn Mài" });

            // Seed Wallets
            var sellerWallet = await walletService.GetOrCreateWalletAsync(sellerId);
            var winnerWallet = await walletService.GetOrCreateWalletAsync(winnerId);
            await db.SaveChangesAsync();

            // 2.1 Phiên kỹ thuật số thuần túy (HasPhysicalCopy = false)
            var digitalAuction = new Auction
            {
                Id = Guid.NewGuid(),
                ArtworkId = artworkId,
                SellerId = sellerId,
                StartPrice = 100_000m,
                CurrentPrice = 300_000m,
                BidStep = 10_000m,
                Status = AuctionStatus.Active,
                HasPhysicalCopy = false,
                StartAt = DateTimeOffset.UtcNow.AddHours(-10),
                EndAt = DateTimeOffset.UtcNow.AddHours(-1)
            };
            db.Auctions.Add(digitalAuction);

            var digitalWinningBid = new Bid
            {
                Id = Guid.NewGuid(),
                AuctionId = digitalAuction.Id,
                BidderId = winnerId,
                Amount = 300_000m,
                HoldAmount = 300_000m,
                Status = BidStatus.Leading,
                PlacedAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            db.Bids.Add(digitalWinningBid);
            await db.SaveChangesAsync();

            await settlementService.SettleWithHeldDepositAsync(digitalAuction, digitalWinningBid);

            AssertTest("TC-02a: Phiên số -> AuctionStatus = Settled", digitalAuction.Status == AuctionStatus.Settled);
            var digitalEscrow = await db.EscrowTransactions.FirstOrDefaultAsync(e => e.AuctionId == digitalAuction.Id);
            AssertTest("TC-02b: Phiên số -> EscrowStatus = Released ngay lập tức", digitalEscrow?.Status == EscrowStatus.Released);
            sellerWallet = await walletService.GetOrCreateWalletAsync(sellerId);
            AssertTest("TC-02c: Phiên số -> Người bán được giải ngân ngay", sellerWallet.Balance == 285_000m, "300k - 5% fee = 285k");

            // 2.2 Phiên CÓ bản tranh vật lý (HasPhysicalCopy = true)
            var seller2Id = Guid.NewGuid();
            var seller2Wallet = await walletService.GetOrCreateWalletAsync(seller2Id);
            var physicalAuction = new Auction
            {
                Id = Guid.NewGuid(),
                ArtworkId = artworkId,
                SellerId = seller2Id,
                StartPrice = 500_000m,
                CurrentPrice = 1_000_000m,
                BidStep = 50_000m,
                Status = AuctionStatus.Active,
                HasPhysicalCopy = true,
                PhysicalDescription = "Tranh sơn dầu bản gốc đóng khung gỗ",
                StartAt = DateTimeOffset.UtcNow.AddHours(-10),
                EndAt = DateTimeOffset.UtcNow.AddHours(-1)
            };
            db.Auctions.Add(physicalAuction);

            var physicalWinningBid = new Bid
            {
                Id = Guid.NewGuid(),
                AuctionId = physicalAuction.Id,
                BidderId = winnerId,
                Amount = 1_000_000m,
                HoldAmount = 1_000_000m,
                Status = BidStatus.Leading,
                PlacedAt = DateTimeOffset.UtcNow.AddHours(-2)
            };
            db.Bids.Add(physicalWinningBid);
            await db.SaveChangesAsync();

            await settlementService.SettleWithHeldDepositAsync(physicalAuction, physicalWinningBid);

            AssertTest("TC-02d: Phiên vật lý -> Status = AwaitingPhysical", physicalAuction.Status == AuctionStatus.AwaitingPhysical);
            AssertTest("TC-02e: Phiên vật lý -> Hạn chốt 48h được thiết lập", physicalAuction.PhysicalChoiceDeadline.HasValue && physicalAuction.PhysicalChoiceDeadline.Value > DateTimeOffset.UtcNow.AddHours(47));
            var physicalEscrow = await db.EscrowTransactions.FirstOrDefaultAsync(e => e.AuctionId == physicalAuction.Id);
            AssertTest("TC-02f: Phiên vật lý -> Ký quỹ Deposited tại sàn (CHƯA giải ngân)", physicalEscrow?.Status == EscrowStatus.Deposited);
            seller2Wallet = await walletService.GetOrCreateWalletAsync(seller2Id);
            AssertTest("TC-02g: Phiên vật lý -> Người bán CHƯA nhận tiền trong ví", seller2Wallet.Balance == 0m);

            // -------------------------------------------------------------
            // TC-03: Người thắng yêu cầu nhận tranh vật lý
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-03: Người thắng yêu cầu nhận tranh vật lý (UC35) ---");
            var requestDeliveryHandler = new RequestPhysicalDeliveryCommandHandler(
                db, notifications, NullLogger<RequestPhysicalDeliveryCommandHandler>.Instance);

            // 3.1 Kẻ lạ cố yêu cầu
            var intruderId = Guid.NewGuid();
            var (failAuth, _, authErrors) = await requestDeliveryHandler.Handle(
                new RequestPhysicalDeliveryCommand(intruderId, physicalAuction.Id, "Hacker", "0900000000", "Quận 1"), default);
            AssertTest("TC-03a: Người ngoài không thể yêu cầu nhận tranh", !failAuth && authErrors.Any(e => e.Contains("Chỉ người thắng")));

            // 3.2 Winner yêu cầu hợp lệ
            var (reqSuccess, deliveryDto, reqErrors) = await requestDeliveryHandler.Handle(
                new RequestPhysicalDeliveryCommand(
                    winnerId,
                    physicalAuction.Id,
                    RecipientName: "Nguyễn Văn Người Thắng",
                    RecipientPhone: "0912345678",
                    ShippingAddress: "123 Đường Lê Lợi, Phường Bến Nghé, Quận 1, TP.HCM",
                    NoteToSeller: "Nhờ shop bọc kỹ chống sốc 4 góc khung tranh."), default);

            AssertTest("TC-03b: Winner yêu cầu nhận tranh thành công", reqSuccess && deliveryDto != null);
            var deliveryEntity = await db.Set<AuctionDelivery>().FirstOrDefaultAsync(d => d.AuctionId == physicalAuction.Id);
            AssertTest("TC-03c: Trạng thái delivery = AwaitingShipment", deliveryEntity?.Status == DeliveryStatus.AwaitingShipment);
            AssertTest("TC-03d: Auction.RequiresPhysicalDelivery = true", physicalAuction.RequiresPhysicalDelivery == true);
            AssertTest("TC-03e: Đã gửi thông báo cho Seller", notifications.Notifications.Any(n => n.UserId == seller2Id && n.Title.Contains("tranh vật lý")));

            // -------------------------------------------------------------
            // TC-04: Người bán cập nhật mã vận đơn & Phiếu gửi bưu cục
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-04: Người bán gửi hàng qua bưu cục (UC35) ---");
            var updateShippingHandler = new UpdateShippingInfoCommandHandler(
                db, notifications, NullLogger<UpdateShippingInfoCommandHandler>.Instance);

            // 4.1 Người ngoài không được update shipping
            var (failShipAuth, _, shipAuthErrors) = await updateShippingHandler.Handle(
                new UpdateShippingInfoCommand(winnerId, physicalAuction.Id, "VNPost", "VN123456789"), default);
            AssertTest("TC-04a: Chỉ Seller mới được cập nhật vận đơn", !failShipAuth && shipAuthErrors.Any(e => e.Contains("Chỉ người bán")));

            // 4.2 Seller cập nhật hợp lệ
            var (shipSuccess, shipDto, _) = await updateShippingHandler.Handle(
                new UpdateShippingInfoCommand(
                    seller2Id,
                    physicalAuction.Id,
                    CarrierName: "VNPost",
                    TrackingCode: "VN888999000VN",
                    TrackingUrl: "https://vnpost.vn/tra-cuu-dinh-vi?code=VN888999000VN",
                    ShippingReceiptUrl: "https://cloudinary.com/dillustration/receipts/ship_receipt_01.jpg",
                    PackageImageUrl: "https://cloudinary.com/dillustration/packages/box_01.jpg"), default);

            AssertTest("TC-04b: Cập nhật vận đơn bưu cục thành công", shipSuccess && shipDto != null);
            AssertTest("TC-04c: Delivery chuyển sang InTransit", deliveryEntity?.Status == DeliveryStatus.InTransit);
            AssertTest("TC-04d: Auction chuyển sang InTransit", physicalAuction.Status == AuctionStatus.InTransit);
            AssertTest("TC-04e: Đã gửi thông báo hàng đang giao cho Buyer", notifications.Notifications.Any(n => n.UserId == winnerId && n.Title.Contains("đang được giao")));

            // Kiểm tra biên lai bưu cục
            var receiptsMidway = AuctionMapper.BuildReceipts(physicalAuction, physicalEscrow, deliveryEntity);
            AssertTest("TC-04f: Biên lai bưu cục xuất hiện minh bạch", receiptsMidway.Any(r => r.ReceiptType == "CarrierShipping" && r.ProofImageUrl != null));

            // -------------------------------------------------------------
            // TC-05: Người mua xác nhận đã nhận hàng (Nhả tiền cho Seller)
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-05: Người mua nhận hàng & Sàn giải ngân cho Seller ---");
            var confirmHandler = new ConfirmPhysicalDeliveryCommandHandler(
                db, settlementService, notifications, NullLogger<ConfirmPhysicalDeliveryCommandHandler>.Instance);

            var (confirmSuccess, confirmDto, _) = await confirmHandler.Handle(
                new ConfirmPhysicalDeliveryCommand(
                    winnerId,
                    physicalAuction.Id,
                    DeliveryReceiptUrl: "https://cloudinary.com/dillustration/pod/pod_signature_01.jpg"), default);

            AssertTest("TC-05a: Xác nhận nhận tranh thành công", confirmSuccess && confirmDto != null);
            AssertTest("TC-05b: Delivery chuyển sang Delivered", deliveryEntity?.Status == DeliveryStatus.Delivered);
            AssertTest("TC-05c: Auction chuyển sang Completed", physicalAuction.Status == AuctionStatus.Completed);
            AssertTest("TC-05d: Escrow giải ngân -> Released", physicalEscrow?.Status == EscrowStatus.Released);
            seller2Wallet = await walletService.GetOrCreateWalletAsync(seller2Id);
            AssertTest("TC-05e: Người bán nhận đủ tiền trừ phí sàn 5%", seller2Wallet.Balance == 950_000m, "1.000.000đ - 5% (50.000đ) = 950.000đ");
            AssertTest("TC-05f: Thông báo tiền đã về ví cho Seller", notifications.Notifications.Any(n => n.UserId == seller2Id && n.Title.Contains("giải ngân")));

            // Kiểm tra toàn bộ 4 biên lai
            var finalReceipts = AuctionMapper.BuildReceipts(physicalAuction, physicalEscrow, deliveryEntity);
            AssertTest("TC-05g: Bộ 4 biên lai giao dịch đầy đủ",
                finalReceipts.Any(r => r.ReceiptType == "EscrowDeposit") &&
                finalReceipts.Any(r => r.ReceiptType == "CarrierShipping") &&
                finalReceipts.Any(r => r.ReceiptType == "ProofOfDelivery") &&
                finalReceipts.Any(r => r.ReceiptType == "SettlementPayout"));

            // -------------------------------------------------------------
            // TC-06: Nhánh Người thắng từ chối nhận tranh (Chỉ lấy file số)
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-06: Người thắng từ chối tranh vật lý -> Giải ngân ngay ---");
            var dbDecline = CreateInMemoryDbContext("TestDb_TC06");
            var walletServiceDecline = new WalletService(dbDecline);
            var moneyServiceDecline = new AuctionMoneyService(walletServiceDecline);
            var declineSettlement = new AuctionSettlementService(
                dbDecline, walletServiceDecline, moneyServiceDecline, notifications, NullLogger<AuctionSettlementService>.Instance);

            var seller3Id = Guid.NewGuid();
            var declineAuction = new Auction
            {
                Id = Guid.NewGuid(),
                ArtworkId = artworkId,
                SellerId = seller3Id,
                WinnerId = winnerId,
                StartPrice = 200_000m,
                CurrentPrice = 400_000m,
                FinalPrice = 400_000m,
                Status = AuctionStatus.AwaitingPhysical,
                HasPhysicalCopy = true,
                PhysicalChoiceDeadline = DateTimeOffset.UtcNow.AddHours(48),
                SettledAt = DateTimeOffset.UtcNow
            };
            dbDecline.Auctions.Add(declineAuction);
            dbDecline.EscrowTransactions.Add(new EscrowTransaction
            {
                AuctionId = declineAuction.Id,
                PayerId = winnerId,
                PayeeId = seller3Id,
                Amount = 400_000m,
                FeeAmount = 20_000m,
                Status = EscrowStatus.Deposited
            });
            await dbDecline.SaveChangesAsync();

            var declineHandler = new DeclinePhysicalDeliveryCommandHandler(
                dbDecline, declineSettlement, NullLogger<DeclinePhysicalDeliveryCommandHandler>.Instance);

            var (declineSuccess, _) = await declineHandler.Handle(new DeclinePhysicalDeliveryCommand(winnerId, declineAuction.Id), default);
            AssertTest("TC-06a: Từ chối nhận tranh thành công", declineSuccess);
            AssertTest("TC-06b: Auction.RequiresPhysicalDelivery = false", declineAuction.RequiresPhysicalDelivery == false);
            var seller3Wallet = await walletServiceDecline.GetOrCreateWalletAsync(seller3Id);
            AssertTest("TC-06c: Tiền lập tức giải ngân cho Seller", seller3Wallet.Balance == 380_000m, "400k - 20k fee = 380k");

            // -------------------------------------------------------------
            // TC-07: Nhánh Khiếu nại bưu kiện (Đóng băng ký quỹ)
            // -------------------------------------------------------------
            Console.WriteLine("\n--- TC-07: Khiếu nại giao nhận -> Đóng băng ký quỹ ---");
            var dbDispute = CreateInMemoryDbContext("TestDb_TC07");
            var disputeAuction = new Auction
            {
                Id = Guid.NewGuid(),
                ArtworkId = artworkId,
                SellerId = sellerId,
                WinnerId = winnerId,
                Status = AuctionStatus.InTransit,
                HasPhysicalCopy = true
            };
            dbDispute.Auctions.Add(disputeAuction);
            var disputeDelivery = new AuctionDelivery
            {
                Id = Guid.NewGuid(),
                AuctionId = disputeAuction.Id,
                RecipientId = winnerId,
                RecipientName = "Nguyễn Văn Winner",
                RecipientPhone = "0988888888",
                ShippingAddress = "Hà Nội",
                Status = DeliveryStatus.InTransit
            };
            dbDispute.Set<AuctionDelivery>().Add(disputeDelivery);
            var disputeEscrow = new EscrowTransaction
            {
                Id = Guid.NewGuid(),
                AuctionId = disputeAuction.Id,
                PayerId = winnerId,
                PayeeId = sellerId,
                Amount = 500_000m,
                Status = EscrowStatus.Deposited
            };
            dbDispute.EscrowTransactions.Add(disputeEscrow);
            await dbDispute.SaveChangesAsync();

            var disputeHandler = new DisputePhysicalDeliveryCommandHandler(
                dbDispute, notifications, NullLogger<DisputePhysicalDeliveryCommandHandler>.Instance);

            var (disputeSuccess, dispDto, _) = await disputeHandler.Handle(
                new DisputePhysicalDeliveryCommand(winnerId, disputeAuction.Id, "Khung tranh bị nứt vỡ khi bưu tá giao hàng", "https://img.com/broken.jpg"), default);

            AssertTest("TC-07a: Gửi khiếu nại thành công", disputeSuccess && dispDto != null);
            AssertTest("TC-07b: Delivery.Status = Disputed", disputeDelivery.Status == DeliveryStatus.Disputed);
            AssertTest("TC-07c: EscrowTransaction.Status = Disputed (Đóng băng tiền)", disputeEscrow.Status == EscrowStatus.Disputed);
            AssertTest("TC-07d: Đã gửi thông báo khiếu nại cho Seller", notifications.Notifications.Any(n => n.UserId == sellerId && n.Title.Contains("Khiếu nại")));

            // -------------------------------------------------------------
            // TỔNG KẾT
            // -------------------------------------------------------------
            Console.WriteLine("\n================================================================");
            Console.WriteLine($"  KẾT QUẢ KIỂM THỬ: {passed} PASSED / {failed} FAILED");
            Console.WriteLine("================================================================\n");

            return failed == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[EXCEPTION TRONG TEST RUNNER]: {ex}");
            Console.ResetColor();
            return 1;
        }
    }

    private static AppDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }
}

// ---------------------------------------------------------------------
// FAKE NOTIFICATION PUBLISHER FOR VERIFICATION
// ---------------------------------------------------------------------

class FakeNotificationPublisher : INotificationPublisher
{
    public class NotificationRecord
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
    }

    public List<NotificationRecord> Notifications { get; } = new();

    public Task<NotificationDto?> PublishAsync(
        Guid userId,
        NotificationType notificationType,
        string title,
        string body,
        string? refType = null,
        Guid? refId = null,
        NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        Notifications.Add(new NotificationRecord { UserId = userId, Title = title, Body = body });
        return Task.FromResult<NotificationDto?>(new NotificationDto(
            Guid.NewGuid(),
            notificationType.ToString(),
            title,
            body,
            refType,
            refId,
            channel.ToString(),
            false,
            null,
            DateTimeOffset.UtcNow));
    }
}
