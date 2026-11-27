using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Application.Auction.Risk.Commands;

// =====================================================================
// 1. Re-run the detector for one auction (Administrator only)
// =====================================================================
public sealed record RescanAuctionRiskCommand(Guid AuctionId)
    : IRequest<(bool Success, decimal Score, string RiskLevel, int SignalCount, string[] Errors)>;

public sealed class RescanAuctionRiskCommandHandler(IRiskEngine riskEngine)
    : IRequestHandler<RescanAuctionRiskCommand, (bool Success, decimal Score, string RiskLevel, int SignalCount, string[] Errors)>
{
    public async Task<(bool, decimal, string, int, string[])> Handle(
        RescanAuctionRiskCommand request,
        CancellationToken cancellationToken)
    {
        var outcome = await riskEngine.EvaluateAsync(request.AuctionId, hotPathOnly: false, cancellationToken);

        if (outcome.Skipped)
        {
            return (false, 0m, AuctionRiskLevel.Low.ToString(), 0,
                ["KhÃ´ng quÃ©t Ä‘Æ°á»£c phiÃªn nÃ y: phiÃªn khÃ´ng tá»“n táº¡i hoáº·c bá»™ phÃ¡t hiá»‡n Ä‘ang táº¯t."]);
        }

        return (true, outcome.Score, outcome.Level.ToString(), outcome.SignalCount, []);
    }
}

// =====================================================================
// 2. Resolve a sheet: dismiss or confirm (Administrator / Moderator)
// =====================================================================
public sealed record ReviewAuctionRiskCommand(Guid AuctionId, Guid ReviewerId, string Status, string? Note)
    : IRequest<(bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>;

public sealed class ReviewAuctionRiskCommandHandler(
    IApplicationDbContext db,
    ArtCommission.Application.Notifications.Common.INotificationPublisher notifications,
    IIdentityService identity,
    IAuctionMoneyService money,
    IAuctionRealtimePublisher realtime,
    ILogger<ReviewAuctionRiskCommandHandler> logger)
    : IRequestHandler<ReviewAuctionRiskCommand, (bool Success, AuctionRiskAssessmentDto? Data, string[] Errors)>
{
    private const int MinNoteLength = 10;

    private const string ConflictMessage = "Phiếu vừa được người khác xử lý. Tải lại để xem trạng thái mới nhất.";

    /// <summary>
    /// Ghi thay đổi có bắt lỗi concurrency: Assessment mang RowVersion, nên hai kiểm duyệt viên bấm
    /// cùng lúc sẽ ném DbUpdateConcurrencyException - phải trả lỗi đọc được thay vì 500.
    /// </summary>
    private static async Task<bool> TrySaveAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    /// <summary>Sau 30 phÃºt khÃ´ng quyáº¿t Ä‘á»‹nh, phiáº¿u tá»± nháº£ vá» Open Ä‘á»ƒ ngÆ°á»i khÃ¡c xá»­ lÃ½.</summary>
    public static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(30);

    public async Task<(bool, AuctionRiskAssessmentDto?, string[])> Handle(
        ReviewAuctionRiskCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ReviewerId == Guid.Empty)
        {
            return (false, null, ["KhÃ´ng xÃ¡c Ä‘á»‹nh Ä‘Æ°á»£c ngÆ°á»i kiá»ƒm duyá»‡t."]);
        }

        // UnderReview = "tÃ´i Ä‘ang xem phiáº¿u nÃ y" (claim), Open = nháº£ phiáº¿u, Dismissed/Confirmed = quyáº¿t Ä‘á»‹nh.
        if (!Enum.TryParse<AuctionRiskAssessmentStatus>(request.Status, ignoreCase: true, out var target))
        {
            return (false, null, ["Tráº¡ng thÃ¡i khÃ´ng há»£p lá»‡ (UnderReview, Open, Dismissed hoáº·c Confirmed)."]);
        }

        var note = request.Note?.Trim();
        if (target == AuctionRiskAssessmentStatus.Confirmed
            && (note is null || note.Length < MinNoteLength))
        {
            return (false, null, [$"Khi xÃ¡c nháº­n gian láº­n pháº£i ghi chÃº Ã­t nháº¥t {MinNoteLength} kÃ½ tá»±."]);
        }

        var assessment = await db.AuctionRiskAssessments
            .FirstOrDefaultAsync(
                a => a.AuctionId == request.AuctionId
                     && !a.IsDeleted
                     && (a.Status == AuctionRiskAssessmentStatus.Open
                         || a.Status == AuctionRiskAssessmentStatus.UnderReview),
                cancellationToken);

        if (assessment is null)
        {
            return (false, null, ["PhiÃªn nÃ y khÃ´ng cÃ³ phiáº¿u rá»§i ro nÃ o Ä‘ang má»Ÿ."]);
        }

        var now = DateTimeOffset.UtcNow;

        if (target is AuctionRiskAssessmentStatus.UnderReview or AuctionRiskAssessmentStatus.Open)
        {
            // Claim / release: no decision is written, the sheet stays resolvable.
            if (target == AuctionRiskAssessmentStatus.UnderReview
                && assessment.ClaimedByUserId is { } holder
                && holder != request.ReviewerId
                && assessment.ClaimedAt is { } claimedAt
                && now - claimedAt < ClaimLifetime)
            {
                return (false, null, ["Phiáº¿u nÃ y Ä‘ang Ä‘Æ°á»£c má»™t kiá»ƒm duyá»‡t viÃªn khÃ¡c xem."]);
            }

            assessment.Status = target;
            assessment.ClaimedByUserId = target == AuctionRiskAssessmentStatus.UnderReview ? request.ReviewerId : null;
            assessment.ClaimedAt = target == AuctionRiskAssessmentStatus.UnderReview ? now : null;
            assessment.UpdatedAt = now;

            if (!await TrySaveAsync(db, cancellationToken))
            {
                return (false, null, [ConflictMessage]);
            }

            return (true, await BuildSheetAsync(db, request.AuctionId, assessment, cancellationToken), []);
        }

        if (target == AuctionRiskAssessmentStatus.Confirmed
            && (note is null || note.Length < MinNoteLength))
        {
            return (false, null, [$"Khi xÃ¡c nháº­n gian láº­n pháº£i ghi chÃº Ã­t nháº¥t {MinNoteLength} kÃ½ tá»±."]);
        }

        assessment.Status = target;
        assessment.ClaimedByUserId = null;
        assessment.ClaimedAt = null;
        assessment.ReviewedByUserId = request.ReviewerId;
        assessment.ReviewedAt = now;
        assessment.DecisionNote = note;
        assessment.UpdatedAt = now;

        if (!await TrySaveAsync(db, cancellationToken))
        {
            return (false, null, [ConflictMessage]);
        }

        if (target == AuctionRiskAssessmentStatus.Confirmed)
        {
            // Xác nhận gian lận = kết thúc phiên: huỷ phiên + hoàn toàn bộ cọc đang giữ.
            // Trước đây chỉ đặt cờ "tạm dừng nhận giá": phiên treo tới EndAt, tiền cọc của
            // những người đặt giá nằm chết trong LockedBalance, và người thắng (nếu phiên
            // kết thúc sau đó) vẫn bị tính là thắng một phiên đã xác nhận gian lận.
            var outcome = await CancelAuctionAfterFraudAsync(
                db, money, realtime, logger, request.AuctionId, note, cancellationToken);

            await NotifyFraudConfirmedAsync(
                db, notifications, identity, request.AuctionId, assessment, note, outcome, cancellationToken);
        }
        else
        {
            // Bỏ qua phiếu: người bán phải biết phiên đã được xem xét và tiếp tục bình thường.
            await NotifyRiskDismissedAsync(db, notifications, request.AuctionId, assessment, note, cancellationToken);
        }

        return (true, await BuildSheetAsync(db, request.AuctionId, assessment, cancellationToken), []);
    }

    /// <summary>Kết quả một lần huỷ phiên do gian lận — dùng để viết đúng nội dung thông báo.</summary>
    /// <param name="RefundedBids">Số cọc thực sự được chuyển từ LockedBalance về số dư khả dụng.</param>
    /// <param name="ReconciledBids">
    /// Số cọc mang cờ <see cref="HoldStatus.Held"/> nhưng ví KHÔNG còn giữ tiền (dữ liệu cũ đã nhả tiền
    /// mà quên đổi cờ). Không có gì để hoàn, chỉ đóng cờ lại cho sổ cái khỏi còn dòng "đang giữ" ảo.
    /// </param>
    private sealed record FraudCancellationOutcome(
        bool Cancelled,
        int RefundedBids,
        decimal RefundedAmount,
        int ReconciledBids,
        string AuctionStatus);

    /// <summary>
    /// Huỷ phiên và hoàn cọc sau khi kiểm duyệt xác nhận gian lận.
    ///
    /// Bất biến: mọi lượt bid còn <see cref="HoldStatus.Held"/> phải được hoàn về số dư khả dụng
    /// (người đặt giá không được mất tiền vì hành vi của người bán / kẻ gian), và lượt bid đó
    /// chuyển sang <see cref="BidStatus.Cancelled"/> để không còn ai "đang dẫn đầu" một phiên đã huỷ.
    ///
    /// Chỉ huỷ phiên còn sống (Active/Scheduled). Phiên đã chốt hoặc đã sang bước giao hàng thì
    /// tiền đã đi theo đường khác — hoàn cọc ở đây sẽ tạo sổ cái âm, nên bỏ qua và để quy trình
    /// khiếu nại xử lý.
    /// </summary>
    private static async Task<FraudCancellationOutcome> CancelAuctionAfterFraudAsync(
        IApplicationDbContext db,
        IAuctionMoneyService money,
        IAuctionRealtimePublisher realtime,
        ILogger logger,
        Guid auctionId,
        string? note,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions
            .FirstOrDefaultAsync(a => a.Id == auctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return new FraudCancellationOutcome(false, 0, 0m, 0, "Unknown");
        }

        var alreadyClosed = auction.Status is AuctionStatus.Cancelled
            or AuctionStatus.Settled
            or AuctionStatus.Expired
            or AuctionStatus.Completed
            or AuctionStatus.AwaitingPhysical
            or AuctionStatus.InTransit
            or AuctionStatus.Disputed;

        if (alreadyClosed)
        {
            return new FraudCancellationOutcome(false, 0, 0m, 0, auction.Status.ToString());
        }

        var now = DateTimeOffset.UtcNow;

        var heldBids = await db.Bids
            .Where(b => b.AuctionId == auctionId && !b.IsDeleted && b.HoldStatus == HoldStatus.Held)
            .ToListAsync(cancellationToken);

        var refunded = 0;
        var refundedAmount = 0m;
        var reconciled = 0;

        foreach (var bid in heldBids)
        {
            try
            {
                var ledger = await money.RefundDepositAsync(
                    bid,
                    "Huỷ phiên do kiểm duyệt xác nhận gian lận — hoàn cọc.",
                    cancellationToken);

                if (ledger is not null)
                {
                    refunded++;
                    refundedAmount += ledger.Amount;
                }
            }
            catch (InvalidOperationException ex)
            {
                // Ví không còn giữ khoản này (tiền đã nhả ở đường khác nhưng cờ HoldStatus chưa đổi).
                // Không có gì để hoàn — đóng cờ để sổ cái không còn dòng "đang giữ" ảo trong một
                // phiên đã huỷ, và ghi log để đối soát tay.
                reconciled++;
                bid.HoldStatus = HoldStatus.Refunded;
                logger.LogWarning(ex,
                    "Cọc bidId={BidId} auctionId={AuctionId} mang cờ Held nhưng ví không giữ tiền; đóng cờ, không hoàn.",
                    bid.Id, auctionId);
            }
            catch (Exception ex)
            {
                // Lỗi khác (DB, ví không tồn tại): giữ nguyên cờ Held để đối soát biết đường xử lý,
                // nhưng vẫn phải đóng phiên để người tham gia không tiếp tục đặt giá.
                logger.LogError(ex,
                    "Không hoàn được cọc bidId={BidId} auctionId={AuctionId} khi huỷ do gian lận.",
                    bid.Id, auctionId);
            }

            bid.Status = BidStatus.Cancelled;
            bid.UpdatedAt = now;
        }

        auction.Status = AuctionStatus.Cancelled;
        auction.CancelReason = string.IsNullOrWhiteSpace(note)
            ? "Huỷ phiên do kiểm duyệt xác nhận gian lận."
            : $"Huỷ phiên do kiểm duyệt xác nhận gian lận: {Truncate(note.Trim(), 200)}";
        auction.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await realtime.PublishAuctionStatusChangedAsync(
                auction.Id, auction.Status.ToString(), auction.CurrentPrice, auction.EndAt, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không broadcast được trạng thái huỷ auctionId={AuctionId}.", auction.Id);
        }

        logger.LogInformation(
            "Huỷ phiên {AuctionId} do gian lận: hoàn {Refunded}/{Total} cọc, tổng {Amount}, đóng cờ {Reconciled}.",
            auctionId, refunded, heldBids.Count, refundedAmount, reconciled);

        return new FraudCancellationOutcome(true, refunded, refundedAmount, reconciled, auction.Status.ToString());
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    /// <summary>
    /// Sau khi xác nhận gian lận: phiên đã bị HUỶ và cọc đã hoàn. Ba nhóm người liên quan đều được
    /// báo — người bán, những người đã đặt giá, và admin/moderator để lưu vết. Nội dung không nêu
    /// điểm hay tên dấu hiệu; nói rõ số tiền được hoàn để người đặt giá không phải đi hỏi.
    /// </summary>
    private static async Task NotifyFraudConfirmedAsync(
        IApplicationDbContext db,
        ArtCommission.Application.Notifications.Common.INotificationPublisher notifications,
        IIdentityService identity,
        Guid auctionId,
        ArtCommission.Domain.Entities.Auction.AuctionRiskAssessment assessment,
        string? note,
        FraudCancellationOutcome outcome,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction is null)
        {
            return;
        }

        var title = auction.Artwork?.Title ?? "phiên đấu giá";
        var dedupSuffix = $"{assessment.Id}";

        var sellerBody = outcome.Cancelled
            ? $"Phiên \"{title}\" đã bị huỷ sau khi kiểm duyệt xác nhận có dấu hiệu gian lận. Toàn bộ tiền cọc của người tham gia đã được hoàn về ví của họ."
            : $"Phiên \"{title}\" đã bị tạm dừng nhận giá do kiểm duyệt xác nhận có dấu hiệu gian lận. Ban quản trị sẽ liên hệ với bạn để xử lý tiếp.";

        await notifications.PublishAsync(
            auction.SellerId,
            NotificationType.AuctionFraudConfirmed,
            outcome.Cancelled ? "Phiên đấu giá đã bị huỷ" : "Phiên đấu giá bị tạm dừng",
            sellerBody,
            refType: "Auction",
            refId: auctionId,
            dedupKey: $"AuctionFraudConfirmed:seller:{auctionId}:{dedupSuffix}",
            cancellationToken: cancellationToken);

        var bidderIds = await db.Bids.AsNoTracking()
            .Where(b => b.AuctionId == auctionId && !b.IsDeleted)
            .Select(b => b.BidderId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var bidderBody = outcome.Cancelled
            ? $"Phiên \"{title}\" đã bị huỷ do kiểm duyệt xác nhận vi phạm. Tiền cọc bạn đã giữ chỗ đã được hoàn về số dư khả dụng trong ví."
            : $"Phiên \"{title}\" tạm dừng nhận giá do kiểm duyệt xác nhận vi phạm. Khoản tiền đã giữ chỗ sẽ được xử lý theo quy định của sàn.";

        foreach (var bidderId in bidderIds.Where(id => id != auction.SellerId))
        {
            await notifications.PublishAsync(
                bidderId,
                NotificationType.AuctionFraudConfirmed,
                outcome.Cancelled ? "Phiên đấu giá đã bị huỷ, cọc đã hoàn" : "Phiên đấu giá bạn tham gia bị tạm dừng",
                bidderBody,
                refType: "Auction",
                refId: auctionId,
                dedupKey: $"AuctionFraudConfirmed:bidder:{auctionId}:{bidderId}:{dedupSuffix}",
                cancellationToken: cancellationToken);
        }

        var operators = (await identity.GetUserIdsInRoleAsync(UserRoleNames.Administrator, cancellationToken))
            .Concat(await identity.GetUserIdsInRoleAsync(UserRoleNames.Moderator, cancellationToken))
            .Distinct()
            .Where(id => id != auction.SellerId);

        foreach (var operatorId in operators)
        {
            await notifications.PublishAsync(
                operatorId,
                NotificationType.AuctionFraudConfirmed,
                outcome.Cancelled ? "Đã huỷ phiên do xác nhận gian lận" : "Đã xác nhận gian lận một phiên đấu giá",
                $"Phiên \"{title}\" đã được xác nhận gian lận. Kết quả: huỷ phiên, hoàn {outcome.RefundedBids} cọc ({outcome.RefundedAmount:N0} ₫)"
                + (outcome.ReconciledBids > 0 ? $", đóng cờ {outcome.ReconciledBids} cọc không còn tiền giữ." : ".")
                + $" Ghi chú: {note}",
                refType: "Auction",
                refId: auctionId,
                dedupKey: $"AuctionFraudConfirmed:ops:{auctionId}:{operatorId}:{dedupSuffix}",
                cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// Bỏ qua phiếu: báo cho NGƯỜI BÁN rằng phiên đã được xem xét và không có vi phạm.
    ///
    /// Trước đây bỏ qua là im lặng: người bán thấy cờ "đang xem xét" trên phiên của mình (qua
    /// <see cref="NotificationType.AuctionRiskFlagged"/>) rồi cờ đó biến mất mà không có lời giải
    /// thích nào — đúng kiểu "bị nghi oan mà không ai nói gì". Không báo cho người đặt giá: họ
    /// không bị ảnh hưởng gì, báo chỉ gây lo.
    /// </summary>
    private static async Task NotifyRiskDismissedAsync(
        IApplicationDbContext db,
        ArtCommission.Application.Notifications.Common.INotificationPublisher notifications,
        Guid auctionId,
        ArtCommission.Domain.Entities.Auction.AuctionRiskAssessment assessment,
        string? note,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        if (auction is null)
        {
            return;
        }

        var title = auction.Artwork?.Title ?? "phiên đấu giá";

        await notifications.PublishAsync(
            auction.SellerId,
            NotificationType.AuctionRiskDismissed,
            "Phiên đấu giá đã được xem xét, không có vi phạm",
            $"Phiên \"{title}\" đã được kiểm duyệt xem xét và không phát hiện vi phạm. Phiên tiếp tục nhận đặt giá bình thường.",
            refType: "Auction",
            refId: auctionId,
            dedupKey: $"AuctionRiskDismissed:seller:{auctionId}:{assessment.Id}",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Builds the sheet payload for a fresh decision or a claim. Shared so every transition returns
    /// exactly the same shape the GET endpoint does.
    /// </summary>
    private static async Task<AuctionRiskAssessmentDto> BuildSheetAsync(
        IApplicationDbContext db,
        Guid auctionId,
        ArtCommission.Domain.Entities.Auction.AuctionRiskAssessment assessment,
        CancellationToken cancellationToken)
    {
        var auction = await db.Auctions.AsNoTracking()
            .Include(a => a.Artwork)
            .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);

        // Lấy JSON thô rồi bóc câu bằng chứng ở bộ nhớ: dùng CHUNG hàm với GET để hai đường trả về
        // không bao giờ lệch nhau (trước đây POST trả nguyên JSON nên phiếu hiện cả chuỗi \uXXXX).
        var rawSignals = await db.AuctionRiskSignals.AsNoTracking()
            .Where(s => s.AuctionId == auctionId && !s.IsDeleted && s.DetectorVersion == assessment.ModelVersion)
            .OrderByDescending(s => s.DetectedAt)
            .Select(s => new { s.Id, s.SignalCode, s.Weight, s.EvidenceJson, s.BidId, s.UserId, s.DetectedAt })
            .ToListAsync(cancellationToken);

        var signals = rawSignals
            .Select(s => new RiskSignalDto(
                s.Id,
                s.SignalCode.ToString(),
                AuctionRiskScorer.LabelOf(s.SignalCode),
                s.Weight,
                RiskEvidence.Extract(s.EvidenceJson),
                s.BidId,
                s.UserId,
                s.DetectedAt))
            .ToList();

        var claimedByName = assessment.ClaimedByUserId is { } claimerId
            ? await db.Users.AsNoTracking().Where(u => u.Id == claimerId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        var reviewerName = assessment.ReviewedByUserId is { } reviewerId
            ? await db.Users.AsNoTracking().Where(u => u.Id == reviewerId).Select(u => u.FullName).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new AuctionRiskAssessmentDto(
            assessment.Id,
            auctionId,
            auction?.Artwork?.Title ?? "Phiên đấu giá",
            auction?.Artwork?.ThumbnailUrl ?? auction?.Artwork?.WatermarkedUrl,
            auction?.CurrentPrice ?? 0m,
            assessment.Score,
            assessment.RiskLevel.ToString(),
            RiskLabels.LevelLabel(assessment.RiskLevel),
            assessment.Status.ToString(),
            assessment.SignalCount,
            assessment.Summary,
            assessment.DetectedAt,
            auction?.LastRiskScanAt,
            assessment.ClaimedByUserId,
            claimedByName,
            assessment.ClaimedAt,
            assessment.ReviewedByUserId,
            reviewerName,
            assessment.ReviewedAt,
            assessment.DecisionNote,
            auction?.Status.ToString(),
            signals);
    }
}