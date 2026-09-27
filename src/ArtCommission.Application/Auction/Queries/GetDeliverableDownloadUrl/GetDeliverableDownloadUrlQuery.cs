using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetDeliverableDownloadUrl;

/// <summary>
/// UC35 — POST /api/v1/auctions/{auctionId}/settlement/download-url
/// Winner tải file gốc tranh sau khi đã thanh toán, qua link có hạn.
///
/// HAI ĐIỀU KIỆN PHẢI ĐÚNG (theo spec):
///   1. Người gọi PHẢI là winner — 403 nếu không.
///   2. Phiên PHẢI đã thanh toán xong (escrow Released, không ở trạng thái Expired) — 409 nếu chưa.
///
/// Mỗi lượt cấp link đều tăng <c>DownloadCount</c>: cần dấu vết để phát hiện việc
/// một tài khoản phát tán file gốc.
/// </summary>
public record GetDeliverableDownloadUrlQuery(
    Guid UserId,
    Guid AuctionId
) : IRequest<(bool Success, DeliverableDownloadDto? Data, int StatusCode, string[] Errors)>;

public class GetDeliverableDownloadUrlQueryHandler
    : IRequestHandler<GetDeliverableDownloadUrlQuery, (bool, DeliverableDownloadDto?, int, string[])>
{
    /// <summary>
    /// Thời hạn link tải dùng khi Admin CHƯA cấu hình `PresignedUrlExpirationMinutes`.
    /// 15 phút là con số đã chốt cho luồng tải file gốc (SCR-15 ghi rõ "15-min presigned URL").
    /// </summary>
    private const decimal DefaultLifetimeMinutes = 15m;

    private readonly IApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IWalletService _walletService;

    public GetDeliverableDownloadUrlQueryHandler(
        IApplicationDbContext db,
        IFileStorageService fileStorage,
        IWalletService walletService)
    {
        _db = db;
        _fileStorage = fileStorage;
        _walletService = walletService;
    }

    public async Task<(bool, DeliverableDownloadDto?, int, string[])> Handle(
        GetDeliverableDownloadUrlQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, 400, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, 404, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.WinnerId != request.UserId)
        {
            // 403 vì đây là vấn đề quyền, không phải thiếu dữ liệu.
            return (false, null, 403, ["Chỉ người thắng phiên mới được tải file gốc."]);
        }

        if (auction.Status == AuctionStatus.Expired)
        {
            return (false, null, 409,
                ["Kết quả phiên đã bị huỷ do quá hạn thanh toán nên không còn quyền tải file gốc."]);
        }

        if (!auction.SettledAt.HasValue)
        {
            return (false, null, 409, ["Phiên chưa được chốt nên chưa có file gốc để bàn giao."]);
        }

        var escrow = await _db.EscrowTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.AuctionId == auction.Id && !e.IsDeleted, cancellationToken);

        // Yêu cầu "đã thanh toán": escrow phải đã giải ngân (Released/PartialReleased).
        var isPaid = escrow is not null
                     && escrow.Status is EscrowStatus.Released or EscrowStatus.PartialReleased;

        if (!isPaid)
        {
            return (false, null, 409,
                ["Chưa ghi nhận thanh toán cho phiên này nên chưa thể tải file gốc."]);
        }

        var deliverable = await _db.Deliverables
            .FirstOrDefaultAsync(d => d.AuctionId == auction.Id && !d.IsDeleted, cancellationToken);

        // Nếu bản ghi bàn giao chưa tồn tại (dữ liệu cũ), dựng từ tranh thay vì báo lỗi.
        if (deliverable is null)
        {
            var artwork = await _db.Artworks
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == auction.ArtworkId && !a.IsDeleted, cancellationToken);

            if (artwork is null || string.IsNullOrWhiteSpace(artwork.ImageUrl))
            {
                return (false, null, 404, ["Không tìm thấy file gốc của tranh."]);
            }

            deliverable = new Domain.Entities.Auction.Deliverable
            {
                AuctionId = auction.Id,
                RecipientId = request.UserId,
                FileName = artwork.Title,
                FileUrl = artwork.ImageUrl,
                MimeType = "image/jpeg",
                FileSizeBytes = artwork.FileSizeBytes
            };

            _db.Deliverables.Add(deliverable);
        }

        // Thời hạn link lấy từ cấu hình sàn (SCR-18: Admin đặt được 5–120 phút) thay vì
        // hardcode. Nếu hardcode thì Admin đổi policy xong link vẫn hết hạn theo số cũ,
        // và không ai hiểu vì sao cấu hình "không có tác dụng".
        var lifetimeMinutes = await _walletService.GetDecimalConfigAsync(
            PlatformConfigKeys.PresignedUrlExpirationMinutes,
            DefaultLifetimeMinutes,
            cancellationToken);

        // Chặn biên: cấu hình rác (0 hoặc âm) sẽ tạo link hết hạn ngay lập tức.
        if (lifetimeMinutes <= 0m)
        {
            lifetimeMinutes = DefaultLifetimeMinutes;
        }

        PresignedDownload presigned;
        try
        {
            presigned = await _fileStorage.CreateDownloadUrlAsync(
                deliverable.FileUrl,
                deliverable.FileName,
                TimeSpan.FromMinutes((double)lifetimeMinutes),
                cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            // Host không nằm trong allowlist — đây là lỗi cấu hình/dữ liệu, không phải lỗi người dùng.
            return (false, null, 500, [ex.Message]);
        }

        deliverable.DownloadCount += 1;
        deliverable.IsDelivered = true;
        deliverable.DeliveredAt ??= DateTimeOffset.UtcNow;
        deliverable.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return (true, new DeliverableDownloadDto(
            presigned.Url,
            presigned.ExpiresAt,
            deliverable.FileName), 200, []);
    }
}
