using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionCertificate;

public record GetAuctionCertificateQuery(
    Guid AuctionId,
    Guid? CurrentUserId = null,
    bool IsAdmin = false
) : IRequest<(bool Success, AuctionCertificateDto? Data, int StatusCode, string[] Errors)>;

public class GetAuctionCertificateQueryHandler
    : IRequestHandler<GetAuctionCertificateQuery, (bool, AuctionCertificateDto?, int, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetAuctionCertificateQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, AuctionCertificateDto?, int, string[])> Handle(
        GetAuctionCertificateQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, 400, ["Thiếu mã phiên đấu giá."]);
        }

        var auction = await _db.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, 404, ["Không tìm thấy phiên đấu giá."]);
        }

        // Chứng nhận sở hữu hình thành ngay khi phiên chốt có người thắng, KHÔNG phụ thuộc
        // vào việc tiền ký quỹ đã giải ngân hay bưu phẩm đã tới. Người thắng phiên có bản vật lý
        // vẫn phải xem/tải được chứng nhận trong lúc chờ bưu cục giao tranh.
        if (!auction.WinnerId.HasValue || auction.Status is not (
                AuctionStatus.Settled
                or AuctionStatus.AwaitingPhysical
                or AuctionStatus.InTransit
                or AuctionStatus.Completed))
        {
            return (false, null, 400, ["Phiên đấu giá chưa được chốt hoặc chưa có người thắng hợp lệ."]);
        }

        var artwork = await _db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == auction.ArtworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (false, null, 404, ["Không tìm thấy thông tin tác phẩm."]);
        }

        // Chứng nhận chứa danh tính người mua (họ tên, userId), giá chốt và chữ ký số.
        // Chỉ người thắng, người bán hoặc Ban quản trị được đọc â€” không để lộ cho người lạ.
        var isParticipant = request.CurrentUserId.HasValue
                            && (request.CurrentUserId.Value == auction.WinnerId
                                || request.CurrentUserId.Value == auction.SellerId);
        if (!request.IsAdmin && !isParticipant)
        {
            return (false, null, 403,
                ["Chỉ người thắng, người bán hoặc Ban quản trị mới xem được chứng nhận này."]);
        }

        var ownership = await _db.ArtworkOwnerships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.AuctionId == auction.Id && o.OwnerId == auction.WinnerId.Value && !o.IsDeleted,
                cancellationToken);

        if (ownership is null)
        {
            return (false, null, 404, ["Không tìm thấy chứng nhận quyền sở hữu cho phiên này."]);
        }

        var seller = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == auction.SellerId, cancellationToken);

        var winner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == auction.WinnerId.Value, cancellationToken);

        // TÁC GIẢ của chứng nhận là hoạ sĩ sáng tác, KHÔNG phải người bán. Khi tranh được bán
        // lại (resale), người bán chỉ là chủ sở hữu trước đó — trước đây ghi nhầm người bán
        // thành tác giả nên chứng nhận của tranh bán lại in sai tên hoạ sĩ.
        var creatorUserId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.Id == artwork.CreatorProfileId && !p.IsDeleted)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        var creator = creatorUserId.HasValue
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId.Value, cancellationToken)
            : null;

        var issuedAt = ownership.AcquiredAt;
        var certNumber = $"DIL-CERT-{issuedAt:yyyyMMdd}-{auction.Id.ToString()[..8].ToUpperInvariant()}";
        var verificationCode = $"VERIFY-{ownership.Id.ToString()[..12].ToUpperInvariant()}";

        // Tạo Digital Signature (SHA-256 Hash) bảo mật chứng thực tính toàn vẹn
        var signatureRaw = $"{auction.Id}|{artwork.Id}|{seller?.Id}|{winner?.Id}|{auction.FinalPrice}|{issuedAt.ToUnixTimeSeconds()}";
        var signatureHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureRaw)));

        var certificate = new AuctionCertificateDto(
            CertificateNumber: certNumber,
            AuctionId: auction.Id,
            ArtworkId: artwork.Id,
            ArtworkTitle: artwork.Title,
            ArtworkImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
            Style: artwork.Style,
            CreatorId: creatorUserId ?? auction.SellerId,
            CreatorName: creator?.FullName ?? seller?.FullName ?? "Unknown Artist",
            OwnerId: auction.WinnerId.Value,
            OwnerName: winner?.FullName ?? "Anonymous Collector",
            FinalPrice: auction.FinalPrice ?? 0m,
            IssuedAt: issuedAt,
            VerificationCode: verificationCode,
            DigitalSignature: signatureHash,
            LicenseType: "Exclusive Ownership & Commercial Rights"
        );

        return (true, certificate, 200, []);
    }
}
