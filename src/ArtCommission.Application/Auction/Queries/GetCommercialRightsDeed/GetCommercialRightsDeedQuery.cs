using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetCommercialRightsDeed;

public sealed record GetCommercialRightsDeedQuery(
    Guid AuctionId,
    Guid? CurrentUserId = null,
    bool IsAdmin = false
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

        // Văn bản bàn giao quyền thương mại có hiệu lực ngay khi phiên chốt có người thắng,
        // độc lập với tiến độ bưu cục — người thắng phiên vật lý vẫn xem được khi đang chờ giao tranh.
        if (!auction.WinnerId.HasValue || auction.Status is not (
                AuctionStatus.Settled
                or AuctionStatus.AwaitingPhysical
                or AuctionStatus.InTransit
                or AuctionStatus.Completed))
        {
            return (false, null, 400, ["Phiên đấu giá chưa được chốt hoặc chưa có người thắng hợp lệ."]);
        }

        // Văn bản bàn giao quyền thương mại chứa tên, giá chốt và chữ ký số của hai bên.
        // Chỉ người trong cuộc (người thắng / người bán) hoặc Admin được đọc.
        var isParticipant = request.CurrentUserId.HasValue
                            && (request.CurrentUserId.Value == auction.WinnerId
                                || request.CurrentUserId.Value == auction.SellerId);
        if (!request.IsAdmin && !isParticipant)
        {
            return (false, null, 403,
                ["Chỉ người thắng, người bán hoặc Ban quản trị mới xem được văn bản này."]);
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

        // Văn bản ghi TÁC GIẢ là hoạ sĩ sáng tác, không phải người bán: khi tranh được bán lại,
        // người bán chỉ là chủ sở hữu trước đó. Trước đây dùng auction.SellerId nên văn bản của
        // tranh resale ghi sai tác giả.
        var creatorUserId = await _db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.Id == artwork.CreatorProfileId && !p.IsDeleted)
            .Select(p => (Guid?)p.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        var creator = creatorUserId.HasValue
            ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == creatorUserId.Value, cancellationToken)
            : null;

        // SettledAt có thể null khi phiên đang chờ giao tranh vật lý — lấy mốc chuyển quyền sở hữu
        // hoặc thời điểm cập nhật cuối cùng làm ngày cấp văn bản.
        var issuedAt = ownership?.AcquiredAt
                       ?? auction.SettledAt
                       ?? auction.UpdatedAt
                       ?? auction.CreatedAt;
        var certNumber = $"DIL-CERT-{issuedAt:yyyyMMdd}-{auction.Id.ToString()[..8].ToUpperInvariant()}";
        var verificationCode = $"VERIFY-{(ownership?.Id ?? auction.Id).ToString()[..12].ToUpperInvariant()}";

        var signatureRaw = $"COMMERCIAL-DEED|{auction.Id}|{artwork.Id}|{seller?.Id}|{winner?.Id}|{auction.FinalPrice}|{issuedAt.ToUnixTimeSeconds()}";
        var signatureHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureRaw)));

        var terms = "VĂN BẢN CHUYỂN GIAO TOÀN DIỆN QUYỀN KHAI THÁC THƯƠNG MẠI (COMMERCIAL RIGHTS TRANSFER DEED)\n\n"
            + "1. Chuyển giao quyền tài sản đối với bức tranh: Người sáng tác (Artist) cam kết chuyển giao vĩnh viễn và độc quyền quyền sở hữu bức tranh — gồm bản số độc bản và bản tranh gốc vật lý nếu phiên đấu giá có kèm bản vật lý — cho Người thắng đấu giá (Collector/Owner).\n"
            + "2. Quyền khai thác thương mại hình ảnh bức tranh: Chủ sở hữu có toàn quyền sử dụng hình ảnh bức tranh để sinh lời, gồm in ấn tranh để bán, đưa hình ảnh tranh vào ấn phẩm quảng cáo, bao bì và nội dung truyền thông, không giới hạn doanh thu và không phải trả thêm phí tác quyền định kỳ.\n"
            + "3. Quyền trưng bày và cho mượn tranh: Chủ sở hữu được trưng bày bức tranh tại không gian riêng, phòng tranh hoặc triển lãm, và được cho mượn tranh để giới thiệu với công chúng, miễn ghi rõ tên Hoạ sĩ sáng tác.\n"
            + "4. Cam kết tính độc bản (Exclusivity): Người sáng tác cam kết không vẽ lại, không tái bán, không chuyển nhượng hoặc cấp phép trùng lặp bức tranh này cho bất kỳ bên thứ ba nào khác.\n"
            + "5. Quyền tài sản & thị trường thứ cấp: Chủ sở hữu có toàn quyền chuyển nhượng hoặc niêm yết bán lại (Resell) bức tranh trên thị trường thứ cấp của Dillustration.\n"
            + "6. Quyền nhân thân bất khả xâm phạm (Inalienable Moral Rights): Quyền tác giả và quyền đứng tên tác giả vĩnh viễn và tuyệt đối thuộc về Họa sĩ sáng tác gốc. Dù người mua nắm giữ 100% quyền sở hữu độc bản và quyền khai thác thương mại đối với bức tranh, người mua cam kết KHÔNG được mạo danh là tác giả, đồng thời bảo đảm ghi nhận danh tính Họa sĩ sáng tác trong hồ sơ lai lịch (Provenance) và mọi ấn phẩm có sử dụng hình ảnh tranh.";

        var dto = new CommercialRightsDeedDto(
            AuctionId: auction.Id,
            ArtworkId: artwork.Id,
            ArtworkTitle: artwork.Title,
            ArtworkImageUrl: artwork.WatermarkedUrl ?? artwork.ImageUrl,
            CertificateNumber: certNumber,
            VerificationCode: verificationCode,
            DigitalSignature: signatureHash,
            CreatorId: creatorUserId ?? auction.SellerId,
            CreatorName: creator?.FullName ?? seller?.FullName ?? "Dillustration Artist",
            OwnerId: auction.WinnerId.Value,
            OwnerName: winner?.FullName ?? "Exclusive Collector",
            FinalPrice: auction.FinalPrice ?? 0m,
            SettledAt: issuedAt,
            ScopeOfRights: "Exclusive Commercial Exploitation & Derivative Rights",
            CommercialExploitationAllowed: true,
            VtuberAndGameDevAllowed: true,
            ResellAllowed: true,
            MoralRightsRetainedByArtist: true,
            TermsAndConditions: terms
        );

        return (true, dto, 200, []);
    }
}
