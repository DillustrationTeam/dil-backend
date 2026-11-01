using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetCommercialRightsDeed;

public sealed record GetCommercialRightsDeedQuery(
    Guid AuctionId
) : IRequest<(bool Success, CommercialRightsDeedDto? Data, int StatusCode, string[] Errors)>;

public sealed class GetCommercialRightsDeedQueryHandler
    : IRequestHandler<GetCommercialRightsDeedQuery, (bool, CommercialRightsDeedDto?, int, string[])>
{
    private readonly IApplicationDbContext _db;

    public GetCommercialRightsDeedQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, CommercialRightsDeedDto?, int, string[])> Handle(
        GetCommercialRightsDeedQuery request,
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

        if (auction.Status != AuctionStatus.Settled || !auction.WinnerId.HasValue || !auction.SettledAt.HasValue)
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

        var seller = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == auction.SellerId, cancellationToken);

        var winner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == auction.WinnerId.Value, cancellationToken);

        var issuedAt = ownership?.AcquiredAt ?? auction.SettledAt.Value;
        var certNumber = $"DIL-CERT-{issuedAt:yyyyMMdd}-{auction.Id.ToString()[..8].ToUpperInvariant()}";
        var verificationCode = $"VERIFY-{(ownership?.Id ?? auction.Id).ToString()[..12].ToUpperInvariant()}";

        var signatureRaw = $"COMMERCIAL-DEED|{auction.Id}|{artwork.Id}|{seller?.Id}|{winner?.Id}|{auction.FinalPrice}|{issuedAt.ToUnixTimeSeconds()}";
        var signatureHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureRaw)));

        var terms = "VĂN BẢN CHUYỂN GIAO TOÀN DIỆN QUYỀN KHAI THÁC THƯƠNG MẠI (COMMERCIAL RIGHTS TRANSFER DEED)\n\n"
            + "1. Chuyển giao toàn quyền thương mại: Người sáng tác (Artist) cam kết chuyển giao vĩnh viễn và độc quyền toàn bộ quyền sử dụng, quyền phái sinh và quyền khai thác thương mại đối với thiết kế/tác phẩm này cho Người thắng đấu giá (Collector/Owner).\n"
            + "2. Ứng dụng truyền thông & phát triển: Chủ sở hữu có toàn quyền ứng dụng vào dự án Game, thiết kế mô hình VTuber (Live2D/3D), sản xuất ấn phẩm phái sinh (Merchandise, Artbook, Poster), và phát hành đa nền tảng.\n"
            + "3. Cam kết tính độc bản (1-of-1 Exclusivity): Người sáng tác cam kết không tái bán, không chuyển nhượng hoặc cấp phép trùng lặp thiết kế này cho bất kỳ bên thứ ba nào khác.\n"
            + "4. Quyền tài sản & thị trường thứ cấp: Chủ sở hữu có toàn quyền chuyển nhượng hoặc niêm yết bán lại (Resell) trên thị trường thứ cấp của Dillustration.\n"
            + "5. Quyền nhân thân bất khả xâm phạm (Inalienable Moral Rights): Quyền tác giả và quyền đứng tên tác giả vĩnh viễn và tuyệt đối thuộc về Họa sĩ sáng tác gốc. Dù người mua nắm giữ 100% quyền sở hữu độc bản và quyền khai thác thương mại, người mua cam kết KHÔNG được mạo danh là tác giả, đồng thời bảo đảm ghi nhận danh tính Họa sĩ sáng tác trong hồ sơ lai lịch (Provenance) và mọi sản phẩm phái sinh.";

        var dto = new CommercialRightsDeedDto(
            AuctionId: auction.Id,
            ArtworkId: artwork.Id,
            ArtworkTitle: artwork.Title,
            ArtworkImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
            CertificateNumber: certNumber,
            VerificationCode: verificationCode,
            DigitalSignature: signatureHash,
            CreatorId: auction.SellerId,
            CreatorName: seller?.FullName ?? "Dillustration Artist",
            OwnerId: auction.WinnerId.Value,
            OwnerName: winner?.FullName ?? "Exclusive Collector",
            FinalPrice: auction.FinalPrice ?? 0m,
            SettledAt: issuedAt,
            ScopeOfRights: "1-of-1 Exclusive Commercial Exploitation & Derivative Rights",
            CommercialExploitationAllowed: true,
            VtuberAndGameDevAllowed: true,
            ResellAllowed: true,
            MoralRightsRetainedByArtist: true,
            TermsAndConditions: terms
        );

        return (true, dto, 200, []);
    }
}
