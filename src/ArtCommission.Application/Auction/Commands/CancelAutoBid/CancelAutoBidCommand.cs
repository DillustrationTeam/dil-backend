using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Commands.CancelAutoBid;

/// <summary>
/// Huỷ đăng ký đặt giá tự động (auto-bid) của chính người dùng trong một phiên.
///
/// VÌ SAO CẦN: trước đây một khi đã đặt trần auto-bid thì KHÔNG có cách nào tắt — chỉ có
/// thể ghi đè bằng một lần đặt giá mới, hoặc chờ phiên chốt (lúc đó hệ thống mới xoá mềm).
/// Người dùng đổi ý là bị khoá ở trạng thái "tự động nâng giá tới trần", rủi ro mua ngoài
/// ý muốn.
///
/// LUẬT:
///   - Chỉ huỷ được khi phiên còn Active; phiên đã chốt thì auto-bid cũng hết hiệu lực.
///   - Không đụng tới lượt đặt giá đang dẫn đầu: huỷ auto-bid KHÔNG rút lượt bid hiện tại,
///     cũng không nhả tiền cọc — người dùng vẫn là người dẫn đầu với mức giá đã đặt.
///   - Xoá mềm để giữ vết (ai đã từng đăng ký trần bao nhiêu).
/// </summary>
public sealed record CancelAutoBidCommand(
    Guid AuctionId,
    Guid UserId
) : IRequest<(bool Success, string? Message, string[] Errors)>;

public sealed class CancelAutoBidCommandHandler
    : IRequestHandler<CancelAutoBidCommand, (bool, string?, string[])>
{
    private readonly IApplicationDbContext _db;

    public CancelAutoBidCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, string?, string[])> Handle(
        CancelAutoBidCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty || request.UserId == Guid.Empty)
        {
            return (false, null, ["Thiếu mã phiên hoặc người dùng."]);
        }

        var auction = await _db.Auctions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (auction is null)
        {
            return (false, null, ["Không tìm thấy phiên đấu giá."]);
        }

        if (auction.Status != AuctionStatus.Active)
        {
            return (false, null,
                ["Phiên không còn diễn ra nên đăng ký đặt giá tự động đã hết hiệu lực."]);
        }

        var autoBid = await _db.AuctionAutoBids
            .FirstOrDefaultAsync(
                x => x.AuctionId == request.AuctionId && x.BidderId == request.UserId && !x.IsDeleted,
                cancellationToken);

        if (autoBid is null)
        {
            return (false, null, ["Bạn chưa đăng ký đặt giá tự động cho phiên này."]);
        }

        autoBid.IsDeleted = true;
        autoBid.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (true, "Đã huỷ đặt giá tự động. Lượt đặt giá hiện tại của bạn vẫn được giữ nguyên.", []);
    }
}
