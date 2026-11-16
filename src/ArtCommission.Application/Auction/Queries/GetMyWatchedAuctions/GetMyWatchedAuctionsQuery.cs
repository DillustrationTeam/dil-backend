using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetMyWatchedAuctions;

/// <summary>
/// Danh sách phiên người dùng đang theo dõi ("Phiên tôi đang theo dõi").
///
/// Trước đây chỉ có nút Watch/Unwatch trong trang chi tiết và không có màn nào liệt kê lại,
/// nên người dùng bấm theo dõi xong là mất dấu — không biết mình đang chờ phiên nào.
///
/// Chỉ trả phiên còn sống (Scheduled/Active/Ended chờ chốt); phiên đã huỷ hoặc đã xoá mềm
/// bị loại, để danh sách không đầy rác sau nhiều tháng.
/// </summary>
public sealed record GetMyWatchedAuctionsQuery(
    Guid UserId,
    string? Cursor = null,
    int Limit = 12
) : IRequest<(bool Success, IReadOnlyList<AuctionListItemDto>? Data, object? Meta, string[] Errors)>;

public sealed class GetMyWatchedAuctionsQueryHandler
    : IRequestHandler<GetMyWatchedAuctionsQuery, (bool, IReadOnlyList<AuctionListItemDto>?, object?, string[])>
{
    private const int MaxLimit = 50;
    private const int DefaultLimit = 12;

    private readonly IApplicationDbContext _db;

    public GetMyWatchedAuctionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<AuctionListItemDto>?, object?, string[])> Handle(
        GetMyWatchedAuctionsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, null, ["Cần đăng nhập để xem phiên đang theo dõi."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);

        var query = _db.AuctionWatches
            .AsNoTracking()
            .Where(w => w.UserId == request.UserId && !w.IsDeleted)
            .Join(
                _db.Auctions.AsNoTracking().Where(a => !a.IsDeleted
                                                       && a.Status != AuctionStatus.Cancelled),
                w => w.AuctionId,
                a => a.Id,
                (w, a) => a);

        var total = await query.CountAsync(cancellationToken);

        // Sắp theo giờ kết thúc tăng dần: phiên sắp đóng là việc cần xử lý trước.
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!WatchCursor.TryDecode(request.Cursor, out var cursorEndAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(a => a.EndAt > cursorEndAt
                                     || (a.EndAt == cursorEndAt && a.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderBy(a => a.EndAt)
            .ThenBy(a => a.Id)
            .Take(limit + 1)
            .Select(a => new
            {
                a.Id,
                a.ArtworkId,
                a.CurrentPrice,
                a.BidCount,
                a.StartAt,
                a.EndAt,
                a.Status,
                a.HasPhysicalCopy,
                a.PhysicalDescription,
                a.FinalPrice,
                Title = a.Artwork!.Title,
                ThumbnailUrl = a.Artwork!.ThumbnailUrl,
                ImageUrl = a.Artwork!.ImageUrl,
                WatermarkedUrl = a.Artwork!.WatermarkedUrl,
                Style = a.Artwork!.Style,
                WatchCount = a.Watches.Count(w => !w.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var items = page.Select(a => new AuctionListItemDto(
            AuctionId: a.Id,
            Artwork: new AuctionArtworkDto(
                a.ArtworkId,
                a.Title,
                a.ThumbnailUrl ?? a.WatermarkedUrl ?? a.ImageUrl,
                a.WatermarkedUrl ?? a.ImageUrl,
                a.Style),
            CurrentPrice: a.CurrentPrice,
            BidCount: a.BidCount,
            StartAt: a.StartAt,
            EndAt: a.EndAt,
            AuctionStatus: a.Status.ToString(),
            WatchCount: a.WatchCount,
            HasPhysicalCopy: a.HasPhysicalCopy,
            PhysicalDescription: a.PhysicalDescription,
            FinalPrice: a.FinalPrice)).ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? WatchCursor.Encode(last.EndAt, last.Id)
            : null;

        return (true, items, new { cursor = nextCursor, nextCursor, total, count = items.Count }, []);
    }
}

/// <summary>Cursor keyset cho danh sách phiên đang theo dõi (EndAt, Id).</summary>
public static class WatchCursor
{
    public static string Encode(DateTimeOffset endAt, Guid id)
    {
        var payload = string.Join('|',
            endAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset endAt, out Guid id)
    {
        endAt = default;
        id = Guid.Empty;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(parts[0], "O",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out endAt))
            {
                return false;
            }

            return Guid.TryParseExact(parts[1], "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
