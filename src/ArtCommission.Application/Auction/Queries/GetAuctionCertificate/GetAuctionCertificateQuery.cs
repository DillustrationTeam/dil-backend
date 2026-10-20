using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionCertificate;

public record GetAuctionCertificateQuery(
    Guid AuctionId
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

        if (auction.Status != AuctionStatus.Settled || !auction.WinnerId.HasValue)
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
            CreatorId: auction.SellerId,
            CreatorName: seller?.FullName ?? "Unknown Artist",
            OwnerId: auction.WinnerId.Value,
            OwnerName: winner?.FullName ?? "Anonymous Collector",
            FinalPrice: auction.FinalPrice ?? 0m,
            IssuedAt: issuedAt,
            VerificationCode: verificationCode,
            DigitalSignature: signatureHash,
            LicenseType: "1-of-1 Exclusive Ownership & Commercial Rights"
        );

        return (true, certificate, 200, []);
    }
}
