using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionBids;

/// <summary>
/// UC32 — GET /api/v1/auctions/{auctionId}/bids
/// Lịch sử đặt giá, phân trang bằng cursor theo <c>placed_at</c>.
///
/// Sắp xếp giảm dần theo thời gian: lượt mới nhất là thông tin người dùng cần trước.
/// Cursor mã hoá (PlacedAt, Id) để không bỏ sót/lặp khi có bid mới xen vào giữa lúc đọc.
/// </summary>
public record GetAuctionBidsQuery(
    Guid AuctionId,
    string? Cursor = null,
    int Limit = 20,
    /// <summary>
    /// Người gọi (null khi là khách chưa đăng nhập). Dùng để chỉ trả "số tiền đang giữ"
    /// cho chính chủ lượt đặt giá — số tiền khoá trong ví người khác không phải dữ liệu công khai.
    /// </summary>
    Guid? CurrentUserId = null
) : IRequest<(bool Success, IReadOnlyList<BidDto>? Data, object? Meta, string[] Errors)>;

public class GetAuctionBidsQueryHandler
    : IRequestHandler<GetAuctionBidsQuery, (bool, IReadOnlyList<BidDto>?, object?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetAuctionBidsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<BidDto>?, object?, string[])> Handle(
        GetAuctionBidsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.AuctionId == Guid.Empty)
        {
            return (false, null, null, ["Thiếu mã phiên đấu giá."]);
        }

        var auctionExists = await _db.Auctions
            .AsNoTracking()
            .AnyAsync(a => a.Id == request.AuctionId && !a.IsDeleted, cancellationToken);

        if (!auctionExists)
        {
            return (false, null, null, ["Không tìm thấy phiên đấu giá."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.Bids
            .AsNoTracking()
            .Where(b => b.AuctionId == request.AuctionId && !b.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!BidCursor.TryDecode(request.Cursor, out var cursorPlacedAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(b => b.PlacedAt < cursorPlacedAt
                                     || (b.PlacedAt == cursorPlacedAt && b.Id.CompareTo(cursorId) > 0));
        }

        // Lấy dư 1 dòng để biết còn trang sau hay không.
        var rows = await query
            .OrderByDescending(b => b.PlacedAt)
            .ThenBy(b => b.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var bidderIds = page.Select(b => b.BidderId).Distinct().ToList();

        var bidderNames = await _db.Users
            .AsNoTracking()
            .Where(u => bidderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, cancellationToken);

        var items = page
            .Select(b => new BidDto(
                BidId: b.Id,
                AuctionId: b.AuctionId,
                BidderId: b.BidderId,
                BidderFullName: bidderNames.TryGetValue(b.BidderId, out var name) ? name : null,
                Bidder: new BidderDto(
                    b.BidderId,
                    bidderNames.TryGetValue(b.BidderId, out var bidderName) ? bidderName : null),
                Amount: b.Amount,
                // Số tiền đang giữ chỉ trả cho chính chủ lượt đặt giá. Người ngoài (kể cả
                // khách chưa đăng nhập) nhận 0 để không suy ra được quy mô ví của người khác.
                HoldAmount: request.CurrentUserId == b.BidderId ? b.HoldAmount : 0m,
                BidStatus: b.Status.ToString(),
                HoldStatus: b.HoldStatus.ToString(),
                PlacedAt: b.PlacedAt))
            .ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? BidCursor.Encode(last.PlacedAt, last.Id)
            : null;

        return (true, items, new { nextCursor, count = items.Count }, []);
    }
}

/// <summary>Mã hoá cursor phân trang lịch sử bid theo (PlacedAt, Id).</summary>
public static class BidCursor
{
    public static string Encode(DateTimeOffset placedAt, Guid id)
    {
        var payload = string.Join('|',
            // Giữ đủ 7 chữ số phần lẻ như datetimeoffset(7) của SQL Server. Cắt xuống mili
            // giây làm mốc cursor sớm hơn dòng thật ⇒ bid nằm giữa hai mốc bị bỏ sót khi
            // sang trang (Chính AuctionCursor đã tránh đúng lỗi này).
            placedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset placedAt, out Guid id)
    {
        placedAt = default;
        id = Guid.Empty;

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');

            if (parts.Length != 2)
            {
                return false;
            }

            // Cursor cũ (bản encode bằng mili giây) vẫn giải mã được để không làm gãy
            // phiên đang phân trang dở; cursor mới dùng ISO "O" nên nhánh này chỉ là đường lùi.
            if (long.TryParse(parts[0], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var legacyMs))
            {
                placedAt = DateTimeOffset.FromUnixTimeMilliseconds(legacyMs);
            }
            else if (!DateTimeOffset.TryParseExact(parts[0], "O",
                         System.Globalization.CultureInfo.InvariantCulture,
                         System.Globalization.DateTimeStyles.None, out placedAt))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[1], "N", out var parsedId))
            {
                return false;
            }

            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
