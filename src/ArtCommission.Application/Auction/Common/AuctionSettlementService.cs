using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Common;

/// <summary>Kết quả chốt một phiên đấu giá.</summary>
public sealed record AuctionFinalizeResult(
    Guid AuctionId,
    Guid? WinnerId,
    decimal FinalPrice,
    decimal FeeAmount,
    decimal NetToSeller,
    DateTimeOffset SettledAt,
    Guid? ArtworkOwnershipId,
    Guid? EscrowTransactionId)
{
    public static AuctionFinalizeResult None(Guid auctionId) =>
        new(auctionId, null, 0m, 0m, 0m, DateTimeOffset.UtcNow, null, null);

    /// <summary>Không chốt được vì lý do nghiệp vụ (không có bid, chưa đạt giá sàn...).</summary>
    public bool HasWinner => WinnerId.HasValue;
}

/// <summary>
/// Nghiệp vụ chốt phiên đấu giá — phần nhạy cảm nhất của module.
///
/// BỐN BẤT BIẾN:
///   1. IDEMPOTENT: gọi settle hai lần không được chuyển tiền và chuyển quyền hai lần.
///      Chốt chặn: nếu <c>SettledAt</c> đã có giá trị thì trả lại kết quả cũ, không làm gì.
///   2. Sổ chủ sở hữu chỉ có MỘT dòng <c>IsCurrent</c> cho mỗi tranh ⇒ phải tắt dòng cũ
///      rồi mới thêm dòng mới, cùng một transaction.
///   3. Tiền: bidder thắng trả <c>FinalPrice</c>, phí sàn trừ trên phần người bán nhận.
///   4. Các bid thua phải được nhả cọc — lượt thắng chuyển HoldStatus sang Released
///      (tiền rời ví đi trả người bán), lượt thua Refunded.
/// </summary>
public interface IAuctionSettlementService
{
    /// <summary>
    /// Chốt phiên cho bidder đã GIỮ cọc (luồng đấu giá thường hết hạn).
    /// </summary>
    Task<AuctionFinalizeResult> SettleWithHeldDepositAsync(
        Domain.Entities.Auction.Auction auction,
        Bid winningBid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chốt phiên cho người mua ngay: trừ TRỰC TIẾP số dư khả dụng của người mua
    /// (họ chưa giữ cọc) rồi trả cho người bán.
    /// </summary>
    Task<(bool Success, AuctionFinalizeResult? Result, string[] Errors)> SettleBuyNowAsync(
        Domain.Entities.Auction.Auction auction,
        Guid buyerId,
        decimal price,
        CancellationToken cancellationToken = default);
}

public class AuctionSettlementService : IAuctionSettlementService
{
    /// <summary>Phí sàn mặc định khi Admin chưa cấu hình PlatformConfig.</summary>
    private const decimal DefaultPlatformFeePercent = 5m;

    private readonly IApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly IAuctionMoneyService _moneyService;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger<AuctionSettlementService> _logger;

    public AuctionSettlementService(
        IApplicationDbContext db,
        IWalletService walletService,
        IAuctionMoneyService moneyService,
        INotificationPublisher notifications,
        ILogger<AuctionSettlementService> logger)
    {
        _db = db;
        _walletService = walletService;
        _moneyService = moneyService;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AuctionFinalizeResult> SettleWithHeldDepositAsync(
        Domain.Entities.Auction.Auction auction,
        Bid winningBid,
        CancellationToken cancellationToken = default)
    {
        // BẤT BIẾN 1 — đã chốt rồi thì trả lại kết quả cũ, tuyệt đối không chạy lại nghiệp vụ tiền.
        if (auction.SettledAt.HasValue && auction.WinnerId.HasValue)
        {
            _logger.LogInformation(
                "Phiên {AuctionId} đã chốt trước đó, bỏ qua lần gọi lặp.", auction.Id);

            return await BuildExistingResultAsync(auction, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var finalPrice = winningBid.Amount;

        // Nhả cọc của người thắng = tiền RỜI ví họ để trả cho người bán.
        var heldAmount = winningBid.HoldAmount;

        await _moneyService.ReleaseDepositAsync(
            winningBid,
            "Chốt phiên đấu giá — cọc được dùng để thanh toán.",
            cancellationToken);

        // ------------------------------------------------------------------
        // CÂN BẰNG TIỀN: tiền giữ được nhả ra PHẢI bằng số sẽ trả cho người bán.
        //
        // Hiện tại HoldAmount luôn bằng Amount nên phần chênh luôn bằng 0. Nhưng nếu
        // sau này Admin cấu hình cọc theo tỉ lệ (PlatformConfigKeys.AuctionHoldPercent),
        // hai con số sẽ lệch nhau. Không xử lý phần chênh thì hệ thống TẠO RA hoặc
        // ĐỐT MẤT tiền thật:
        //   - cọc < giá chốt ⇒ thiếu tiền trả người bán  → phải trừ thêm số dư khả dụng
        //   - cọc > giá chốt ⇒ thừa tiền của người mua    → phải hoàn lại phần thừa
        // Xử lý tường minh ở đây để công thức luôn đúng, bất kể cấu hình cọc.
        // ------------------------------------------------------------------
        var shortfall = finalPrice - heldAmount;

        if (shortfall > 0m)
        {
            var buyerWallet = await _walletService.GetOrCreateWalletAsync(
                winningBid.BidderId, cancellationToken);

            if (buyerWallet.Balance < shortfall)
            {
                // Không đủ tiền để bù phần thiếu ⇒ KHÔNG chốt phiên, tránh ghi nhận
                // một giao dịch mà người mua không trả đủ. Caller thấy HasWinner = false
                // và sẽ rollback transaction.
                _logger.LogWarning(
                    "Không chốt được phiên {AuctionId}: người mua thiếu {Shortfall} VND so với giá chốt.",
                    auction.Id, shortfall);

                return AuctionFinalizeResult.None(auction.Id);
            }

            // Trả phần thiếu bằng CẶP hold + release: tiền rời số dư khả dụng mà không
            // để lại dấu vết trong LockedBalance. Dùng thẳng EscrowRelease sẽ khiến
            // công thức đối soát trừ LockedBalance trong khi ví không hề giữ khoản này.
            await _walletService.HoldFundsAsync(
                buyerWallet,
                WalletTransactionType.EscrowHold,
                shortfall,
                AuctionRefTypes.BidShortfall,
                winningBid.Id,
                $"Tạm khoá phần cọc còn thiếu cho phiên {auction.Id}",
                cancellationToken);

            await _walletService.ReleaseFundsAsync(
                buyerWallet,
                WalletTransactionType.EscrowRelease,
                shortfall,
                AuctionRefTypes.BidShortfall,
                winningBid.Id,
                $"Bù phần cọc còn thiếu cho phiên {auction.Id}",
                cancellationToken);
        }
        else if (shortfall < 0m)
        {
            var buyerWallet = await _walletService.FindWalletAsync(
                winningBid.BidderId, cancellationToken);

            if (buyerWallet is not null)
            {
                // Phần cọc vượt giá chốt vẫn đang nằm trong LockedBalance ⇒ hoàn về
                // số dư khả dụng đúng bằng RefundFromHold.
                await _walletService.RefundHeldFundsAsync(
                    buyerWallet,
                    WalletTransactionType.RefundFromHold,
                    -shortfall,
                    AuctionRefTypes.BidShortfall,
                    winningBid.Id,
                    $"Hoàn phần cọc vượt giá chốt phiên {auction.Id}",
                    cancellationToken);
            }
        }

        winningBid.Status = BidStatus.Won;
        winningBid.UpdatedAt = now;

        var feePercent = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.PlatformFeePercent, DefaultPlatformFeePercent, cancellationToken);

        var feeAmount = Math.Round(finalPrice * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var netToSeller = finalPrice - feeAmount;

        var sellerWallet = await _walletService.GetOrCreateWalletAsync(auction.SellerId, cancellationToken);

        // Tiền đến người bán: ghi 1 dòng cho khoản thu gộp, 1 dòng cho phí sàn đã trừ.
        // Tách hai dòng để sổ cái đối soát được "tổng bán" và "phí", không phải suy ra từ hiệu số.
        //
        // Dùng EscrowReceive (KHÔNG phải EscrowRelease): đây là tiền VÀO số dư khả dụng
        // của người nhận. EscrowRelease mang nghĩa "rời khỏi phần đang giữ của người trả"
        // nên nếu dùng ở đây, VerifyLedgerConsistencyAsync sẽ trừ nhầm LockedBalance
        // của người bán và báo lệch số dù tiền đúng.
        await _walletService.CreditAsync(
            sellerWallet,
            WalletTransactionType.EscrowReceive,
            finalPrice,
            AuctionRefTypes.Settlement,
            auction.Id,
            $"Thu tiền bán đấu giá phiên {auction.Id}",
            cancellationToken);

        if (feeAmount > 0m)
        {
            await _walletService.DebitAsync(
                sellerWallet,
                WalletTransactionType.PlatformFee,
                feeAmount,
                AuctionRefTypes.Settlement,
                auction.Id,
                $"Phí nền tảng {feePercent}% trên phiên {auction.Id}",
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ownershipId = await AssignOwnershipAsync(auction, winningBid.BidderId, now, cancellationToken);

        var escrow = await UpsertEscrowAsync(
            auction, winningBid.BidderId, auction.SellerId, finalPrice, feeAmount, now, cancellationToken);

        // Đặt winner TRƯỚC khi tạo Deliverable — RecipientId lấy từ auction.WinnerId.
        auction.WinnerId = winningBid.BidderId;
        auction.FinalPrice = finalPrice;
        auction.SettledAt = now;

        // Hạn thanh toán: cửa sổ để winner hoàn tất nghĩa vụ thanh toán.
        // BẮT BUỘC phải ghi — không có nó thì endpoint xử lý quá hạn
        // (/settlement/expire) luôn từ chối và trở thành endpoint chết, còn trường
        // `paymentDeadline` trong mọi response luôn null.
        auction.PaymentDeadline = now.AddHours(
            (double)await GetPaymentWindowHoursAsync(cancellationToken));

        auction.Status = AuctionStatus.Settled;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await CreateDeliverableIfMissingAsync(auction, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Chốt phiên {AuctionId}: winner={WinnerId}, final={FinalPrice}, fee={Fee}, net={Net}.",
            auction.Id, winningBid.BidderId, finalPrice, feeAmount, netToSeller);

        await NotifyWinnerAsync(auction, winningBid.BidderId, finalPrice, cancellationToken);

        return new AuctionFinalizeResult(
            auction.Id,
            winningBid.BidderId,
            finalPrice,
            feeAmount,
            netToSeller,
            now,
            ownershipId,
            escrow.Id);
    }

    public async Task<(bool Success, AuctionFinalizeResult? Result, string[] Errors)> SettleBuyNowAsync(
        Domain.Entities.Auction.Auction auction,
        Guid buyerId,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        if (auction.SettledAt.HasValue && auction.WinnerId.HasValue)
        {
            return (true, await BuildExistingResultAsync(auction, cancellationToken), []);
        }

        var now = DateTimeOffset.UtcNow;

        var buyerWallet = await _walletService.GetOrCreateWalletAsync(buyerId, cancellationToken);

        if (buyerWallet.Status != WalletStatus.Active)
        {
            return (false, null, ["Ví của bạn đang bị khoá nên không thể mua ngay."]);
        }

        if (buyerWallet.Balance < price)
        {
            return (false, null,
                [$"Số dư khả dụng không đủ để mua ngay. Cần {price:N0} VND, hiện có {buyerWallet.Balance:N0} VND."]);
        }

        // Tiền rời ví người mua TRỰC TIẾP (họ chưa giữ cọc như luồng đặt giá).
        // RefType riêng cho luồng mua ngay: nếu dùng chung với RefType của khoản tiền
        // về người bán thì cả hai cùng khoá (RefType, auctionId, EscrowRelease) và
        // unique index UX_WalletTransaction_Ref sẽ chặn — chốt phiên không chạy được.
        //
        // Trả bằng CẶP hold + release để số dư khả dụng giảm mà LockedBalance không đổi:
        // dùng thẳng một dòng EscrowRelease sẽ khiến công thức đối soát trừ LockedBalance
        // trong khi ví người mua không hề giữ khoản tiền này.
        await _walletService.HoldFundsAsync(
            buyerWallet,
            WalletTransactionType.EscrowHold,
            price,
            AuctionRefTypes.BuyNowPayment,
            auction.Id,
            $"Tạm khoá tiền mua ngay phiên {auction.Id}",
            cancellationToken);

        await _walletService.ReleaseFundsAsync(
            buyerWallet,
            WalletTransactionType.EscrowRelease,
            price,
            AuctionRefTypes.BuyNowPayment,
            auction.Id,
            $"Thanh toán mua ngay phiên {auction.Id}",
            cancellationToken);

        var feePercent = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.PlatformFeePercent, DefaultPlatformFeePercent, cancellationToken);

        var feeAmount = Math.Round(price * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var netToSeller = price - feeAmount;

        var sellerWallet = await _walletService.GetOrCreateWalletAsync(auction.SellerId, cancellationToken);

        // Người bán nhận khoản gộp rồi bị trừ phí — ledger ghi đủ cả hai dòng
        // để công thức đối soát (Deposit + EscrowReceive − PlatformFee ...) vẫn khớp.
        await _walletService.CreditAsync(
            sellerWallet,
            WalletTransactionType.EscrowReceive,
            price,
            AuctionRefTypes.Settlement,
            auction.Id,
            $"Thu tiền bán đấu giá (mua ngay) phiên {auction.Id}",
            cancellationToken);

        if (feeAmount > 0m)
        {
            await _walletService.DebitAsync(
                sellerWallet,
                WalletTransactionType.PlatformFee,
                feeAmount,
                AuctionRefTypes.Settlement,
                auction.Id,
                $"Phí nền tảng {feePercent}% trên phiên {auction.Id}",
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Huỷ mọi bid đang dẫn đầu: mua ngay kết thúc phiên, các cọc phải được nhả.
        var leadingBids = await _db.Bids
            .Where(b => b.AuctionId == auction.Id
                        && b.Status == BidStatus.Leading
                        && !b.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var bid in leadingBids)
        {
            bid.Status = BidStatus.Lost;
            bid.UpdatedAt = now;
            await _moneyService.RefundDepositAsync(
                bid, "Phiên được mua ngay bởi người khác.", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ownershipId = await AssignOwnershipAsync(auction, buyerId, now, cancellationToken);

        var escrow = await UpsertEscrowAsync(
            auction, buyerId, auction.SellerId, price, feeAmount, now, cancellationToken);

        // Đặt winner TRƯỚC khi tạo Deliverable — RecipientId lấy từ auction.WinnerId.
        auction.WinnerId = buyerId;
        auction.FinalPrice = price;
        auction.CurrentPrice = price;
        auction.SettledAt = now;

        // Hạn thanh toán — xem ghi chú ở SettleWithHeldDepositAsync.
        auction.PaymentDeadline = now.AddHours(
            (double)await GetPaymentWindowHoursAsync(cancellationToken));

        auction.Status = AuctionStatus.Settled;
        auction.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        await CreateDeliverableIfMissingAsync(auction, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Mua ngay phiên {AuctionId}: buyer={BuyerId}, price={Price}, fee={Fee}.",
            auction.Id, buyerId, price, feeAmount);
        await NotifyWinnerAsync(auction, buyerId, price, cancellationToken);

        return (true, new AuctionFinalizeResult(
            auction.Id, buyerId, price, feeAmount, netToSeller, now, ownershipId, escrow.Id), []);
    }

    // ------------------------------------------------------------------
    // Phần dùng chung
    // ------------------------------------------------------------------

    /// <summary>
    /// Số giờ winner phải hoàn tất thanh toán, đọc từ cấu hình sàn.
    /// Mặc định 24 giờ; chặn biên để cấu hình rác (0/âm) không tạo ra hạn đã ở quá khứ.
    /// </summary>
    private async Task<decimal> GetPaymentWindowHoursAsync(CancellationToken cancellationToken)
    {
        const decimal defaultHours = 24m;

        var hours = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.AuctionPaymentWindowHours, defaultHours, cancellationToken);

        return hours <= 0m ? defaultHours : hours;
    }

    /// <summary>
    /// Chuyển quyền sở hữu tranh. Tắt dòng hiện tại TRƯỚC khi thêm dòng mới —
    /// nếu đảo thứ tự, unique filtered index <c>UX_ArtworkOwnerships_CurrentOwner</c> sẽ chặn.
    /// </summary>
    private async Task<Guid> AssignOwnershipAsync(
        Domain.Entities.Auction.Auction auction,
        Guid newOwnerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var currentRows = await _db.ArtworkOwnerships
            .Where(o => o.ArtworkId == auction.ArtworkId && o.IsCurrent && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var row in currentRows)
        {
            if (row.OwnerId == newOwnerId)
            {
                // Người mua đã là chủ sở hữu (trường hợp hy hữu) — giữ nguyên, không tạo dòng mới.
                return row.Id;
            }

            row.IsCurrent = false;
            row.ReleasedAt = now;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ownership = new ArtworkOwnership
        {
            ArtworkId = auction.ArtworkId,
            OwnerId = newOwnerId,
            AcquiredAt = now,
            TransferReason = auction.AuctionType == AuctionType.BuyNow
                ? OwnershipTransferReason.BuyNow
                : OwnershipTransferReason.AuctionWin,
            AuctionId = auction.Id,
            IsCurrent = true
        };

        _db.ArtworkOwnerships.Add(ownership);
        await _db.SaveChangesAsync(cancellationToken);

        return ownership.Id;
    }

    /// <summary>
    /// Ghi nhận escrow của giao dịch. Dùng upsert theo AuctionId vì đã có unique index —
    /// settle gọi lặp không được tạo bản ghi tiền thứ hai.
    /// </summary>
    private async Task<EscrowTransaction> UpsertEscrowAsync(
        Domain.Entities.Auction.Auction auction,
        Guid payerId,
        Guid payeeId,
        decimal amount,
        decimal feeAmount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var escrow = await _db.EscrowTransactions.FirstOrDefaultAsync(
            e => e.AuctionId == auction.Id && !e.IsDeleted,
            cancellationToken);

        if (escrow is null)
        {
            escrow = new EscrowTransaction
            {
                AuctionId = auction.Id,
                PayerId = payerId,
                PayeeId = payeeId,
                Amount = amount,
                ReleasedAmount = amount - feeAmount,
                RefundedAmount = 0m,
                FeeAmount = feeAmount,
                Status = EscrowStatus.Released,
                ReleasedAt = now,
                Note = $"Chốt phiên đấu giá {auction.Id}"
            };

            _db.EscrowTransactions.Add(escrow);
        }
        else
        {
            escrow.PayerId = payerId;
            escrow.PayeeId = payeeId;
            escrow.Amount = amount;
            escrow.ReleasedAmount = amount - feeAmount;
            escrow.FeeAmount = feeAmount;
            escrow.Status = EscrowStatus.Released;
            escrow.ReleasedAt ??= now;
            escrow.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return escrow;
    }

    /// <summary>
    /// Tạo bản ghi bàn giao file gốc nếu chưa có.
    ///
    /// LỖI ĐÃ SỬA: bản đầu chỉ tìm tranh trong <c>_db.Artworks.Local</c>. Nhưng handler
    /// chốt phiên nạp Auction KHÔNG kèm Include(Artwork), nên Local luôn rỗng ⇒ hàm
    /// thoát sớm và KHÔNG bao giờ tạo Deliverable. Phải truy vấn DB thật.
    /// </summary>
    private async Task CreateDeliverableIfMissingAsync(
        Domain.Entities.Auction.Auction auction,
        CancellationToken cancellationToken)
    {
        var alreadyExists = await _db.Deliverables
            .AnyAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        if (alreadyExists)
        {
            return;
        }

        // WinnerId phải được gán TRƯỚC khi gọi hàm này.
        if (!auction.WinnerId.HasValue)
        {
            return;
        }

        var artwork = await _db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == auction.ArtworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null || string.IsNullOrWhiteSpace(artwork.ImageUrl))
        {
            // Không có file gốc thì bỏ qua: endpoint download-url sẽ tự dựng lại từ tranh
            // khi cần, và ghi một bản ghi với URL rỗng sẽ hỏng link tải.
            return;
        }

        var isPng = artwork.ImageUrl.Contains(".png", StringComparison.OrdinalIgnoreCase);

        _db.Deliverables.Add(new Deliverable
        {
            AuctionId = auction.Id,
            RecipientId = auction.WinnerId.Value,
            FileName = $"{artwork.Title}.{(isPng ? "png" : "jpg")}",
            FileUrl = artwork.ImageUrl,
            MimeType = isPng ? "image/png" : "image/jpeg",
            FileSizeBytes = artwork.FileSizeBytes,
            IsDelivered = false
        });
    }

    /// <summary>Đọc lại kết quả của phiên đã chốt — nhánh idempotent.</summary>
    private async Task<AuctionFinalizeResult> BuildExistingResultAsync(
        Domain.Entities.Auction.Auction auction,
        CancellationToken cancellationToken)
    {
        var ownership = await _db.ArtworkOwnerships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.AuctionId == auction.Id && o.IsCurrent && !o.IsDeleted,
                cancellationToken);

        var escrow = await _db.EscrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        var finalPrice = auction.FinalPrice ?? 0m;
        var feeAmount = escrow?.FeeAmount ?? 0m;

        return new AuctionFinalizeResult(
            auction.Id,
            auction.WinnerId,
            finalPrice,
            feeAmount,
            finalPrice - feeAmount,
            auction.SettledAt ?? DateTimeOffset.UtcNow,
            ownership?.Id,
            escrow?.Id);
    }

    private async Task NotifyWinnerAsync(
        Domain.Entities.Auction.Auction auction,
        Guid winnerId,
        decimal finalPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.PublishAsync(
                winnerId,
                NotificationType.AuctionWon,
                "Bạn đã thắng phiên đấu giá",
                $"Chúc mừng! Bạn thắng phiên với giá {finalPrice:N0} VND. " +
                "Tải file gốc trong mục kết quả phiên đấu giá.",
                nameof(Domain.Entities.Auction.Auction),
                auction.Id,
                NotificationChannel.InApp,
                $"AuctionWon:{auction.Id}:{winnerId}",
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Thông báo lỗi không được làm hỏng nghiệp vụ đã commit.
            _logger.LogError(ex,
                "Không phát được thông báo thắng phiên cho userId={UserId}, auctionId={AuctionId}.",
                winnerId, auction.Id);
        }
    }
}
