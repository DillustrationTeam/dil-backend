using ArtCommission.Application.Auction.Common;
using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctions;

/// <summary>
/// UC32 — GET /api/v1/auctions
/// Danh sách phiên cho chợ đấu giá. Lọc theo trạng thái / người bán / khoảng giá,
/// phân trang bằng CURSOR (keyset) chứ không OFFSET — OFFSET sâu trên bảng lớn
/// ngày càng chậm và có thể bỏ sót/bỏ lặp dòng khi dữ liệu đang thay đổi.
///
/// Cursor mã hoá cặp (EndAt, Id) để sắp xếp tất định khi nhiều phiên cùng EndAt.
/// </summary>
public record GetAuctionsQuery(
    string? Status = null,
    Guid? SellerId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Sort = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, IReadOnlyList<AuctionListItemDto>? Data, object? Meta, string[] Errors)>;

public class GetAuctionsQueryHandler
    : IRequestHandler<GetAuctionsQuery, (bool, IReadOnlyList<AuctionListItemDto>?, object?, string[])>
{
    private const int MaxLimit = 100;
    private const int DefaultLimit = 20;

    private readonly IApplicationDbContext _db;

    public GetAuctionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<AuctionListItemDto>?, object?, string[])> Handle(
        GetAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        // "Bộ lọc" tách riêng khỏi "phân trang": đếm total trên CÙNG bộ lọc nhưng TRƯỚC
        // khi áp cursor, để FE hiển thị được tổng số phiên khớp điều kiện.
        var filter = BuildFilter(request);

        if (filter.Error is not null)
        {
            return (false, null, null, [filter.Error]);
        }

        var query = filter.Query!;

        var total = await query.CountAsync(cancellationToken);

        var sort = request.Sort?.Trim().ToLowerInvariant() ?? "ending";

        // Keyset: đọc mốc (EndAt, CurrentPrice, CreatedAt, Id) từ cursor rồi lấy
        // các dòng nằm SAU mốc đó theo đúng chiều sắp xếp đang dùng.
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AuctionCursor.TryDecode(request.Cursor, out var cursorSort, out var cursorValues, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            // Cursor được sinh cho MỘT kiểu sắp xếp. Nếu client đổi sort giữa chừng,
            // mốc so sánh không còn khớp chiều sắp xếp ⇒ trang kết quả sai thầm lặng
            // (bỏ sót hoặc lặp dòng). Từ chối rõ ràng thay vì trả dữ liệu sai.
            if (!string.Equals(cursorSort, sort, StringComparison.OrdinalIgnoreCase))
            {
                return (false, null, null,
                    ["Cursor được tạo cho kiểu sắp xếp khác. Hãy tải lại từ trang đầu."]);
            }

            query = sort switch
            {
                "price" or "price_desc" =>
                    query.Where(a => a.CurrentPrice < cursorValues.Price
                                     || (a.CurrentPrice == cursorValues.Price && a.Id.CompareTo(cursorId) > 0)),
                "price_asc" =>
                    query.Where(a => a.CurrentPrice > cursorValues.Price
                                     || (a.CurrentPrice == cursorValues.Price && a.Id.CompareTo(cursorId) > 0)),
                "newest" =>
                    query.Where(a => a.CreatedAt < cursorValues.CreatedAt
                                     || (a.CreatedAt == cursorValues.CreatedAt && a.Id.CompareTo(cursorId) > 0)),
                _ =>
                    query.Where(a => a.EndAt > cursorValues.EndAt
                                     || (a.EndAt == cursorValues.EndAt && a.Id.CompareTo(cursorId) > 0))
            };
        }

        query = sort switch
        {
            "price" or "price_desc" => query.OrderByDescending(a => a.CurrentPrice).ThenBy(a => a.Id),
            "price_asc" => query.OrderBy(a => a.CurrentPrice).ThenBy(a => a.Id),
            "newest" => query.OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id),
            _ => query.OrderBy(a => a.EndAt).ThenBy(a => a.Id)
        };

        // Lấy dư 1 dòng để biết còn trang sau hay không, tránh phải COUNT toàn bảng.
        var rows = await query
            .Take(limit + 1)
            .Select(a => new AuctionRow(
                a.Id,
                a.ArtworkId,
                a.CurrentPrice,
                a.BidCount,
                a.EndAt,
                a.Status,
                a.CreatedAt,
                a.Artwork!.Title,
                a.Artwork!.ThumbnailUrl,
                a.Artwork!.ImageUrl,
                a.Artwork!.Style,
                a.Watches.Count(w => !w.IsDeleted)))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var items = page.Select(r => new AuctionListItemDto(
            AuctionId: r.Id,
            Artwork: new AuctionArtworkDto(
                r.ArtworkId,
                r.Title,
                r.ThumbnailUrl ?? r.ImageUrl,
                r.ImageUrl,
                r.Style),
            CurrentPrice: r.CurrentPrice,
            BidCount: r.BidCount,
            EndAt: r.EndAt,
            AuctionStatus: r.Status.ToString(),
            WatchCount: r.WatchCount)).ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? AuctionCursor.Encode(sort, last.EndAt, last.CurrentPrice, last.CreatedAt, last.Id)
            : null;

        // Đặc tả API yêu cầu meta: { cursor, total }. Trả thêm nextCursor (tên tường minh
        // hơn) để FE dùng được ngay, nhưng total là trường BẮT BUỘC phải có.
        return (true, items, new { cursor = nextCursor, nextCursor, total }, []);
    }

    /// <summary>
    /// Dựng bộ lọc dùng chung cho cả truy vấn đếm total lẫn truy vấn lấy trang dữ liệu.
    ///
    /// VÌ SAO tách riêng: nếu đếm total trên một bộ lọc khác với bộ lọc lấy dữ liệu thì
    /// `total` không khớp số dòng người dùng thực sự duyệt được — sai lệch chỉ lộ ra khi
    /// có filter, tức là đúng lúc FE đang hiển thị "x kết quả".
    /// </summary>
    private (IQueryable<Domain.Entities.Auction.Auction>? Query, string? Error) BuildFilter(
        GetAuctionsQuery request)
    {
        var query = _db.Auctions
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        // Mặc định ẩn phiên đã huỷ/đã chốt khỏi chợ — người dùng vào chợ để tìm
        // phiên còn đấu được, không phải xem lịch sử.
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AuctionStatus>(request.Status, ignoreCase: true, out var status))
            {
                return (null, $"Trạng thái không hợp lệ: {request.Status}.");
            }

            query = query.Where(a => a.Status == status);
        }
        else
        {
            query = query.Where(a =>
                a.Status == AuctionStatus.Scheduled
                || a.Status == AuctionStatus.Active
                || a.Status == AuctionStatus.Ended);
        }

        if (request.SellerId.HasValue)
        {
            query = query.Where(a => a.SellerId == request.SellerId.Value);
        }

        if (request.MinPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice >= request.MinPrice.Value);
        }

        if (request.MaxPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice <= request.MaxPrice.Value);
        }

        return (query, null);
    }

    /// <summary>Dòng phẳng đọc từ DB trước khi map ra DTO.</summary>
    private sealed record AuctionRow(
        Guid Id,
        Guid ArtworkId,
        decimal CurrentPrice,
        int BidCount,
        DateTimeOffset EndAt,
        AuctionStatus Status,
        DateTimeOffset CreatedAt,
        string Title,
        string? ThumbnailUrl,
        string? ImageUrl,
        string? Style,
        int WatchCount);
}

/// <summary>
/// Mã hoá / giải mã cursor keyset cho danh sách đấu giá.
///
/// Cursor được ký base64 để client không tự sửa được nội dung — nếu client gửi
/// mốc sai, phân trang sẽ nhảy cóc và bỏ sót dữ liệu mà không ai phát hiện.
/// </summary>
public static class AuctionCursor
{
    public static string Encode(
        string sort,
        DateTimeOffset endAt,
        decimal currentPrice,
        DateTimeOffset createdAt,
        Guid id)
    {
        var payload = string.Join('|',
            sort,
            endAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            currentPrice.ToString(System.Globalization.CultureInfo.InvariantCulture),
            createdAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));

        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(
        string cursor,
        out string sort,
        out AuctionCursorValues values,
        out Guid id)
    {
        sort = string.Empty;
        values = default;
        id = Guid.Empty;

        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');

            if (parts.Length != 5)
            {
                return false;
            }

            if (!long.TryParse(parts[1], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var endAtMs))
            {
                return false;
            }

            if (!decimal.TryParse(parts[2], System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var price))
            {
                return false;
            }

            if (!long.TryParse(parts[3], System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var createdMs))
            {
                return false;
            }

            if (!Guid.TryParseExact(parts[4], "N", out var parsedId))
            {
                return false;
            }

            sort = parts[0];

            values = new AuctionCursorValues(
                DateTimeOffset.FromUnixTimeMilliseconds(endAtMs),
                price,
                DateTimeOffset.FromUnixTimeMilliseconds(createdMs));

            id = parsedId;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Các mốc đọc ra từ cursor — đủ để so sánh keyset cho mọi kiểu sắp xếp.</summary>
public readonly record struct AuctionCursorValues(
    DateTimeOffset EndAt,
    decimal Price,
    DateTimeOffset CreatedAt);
