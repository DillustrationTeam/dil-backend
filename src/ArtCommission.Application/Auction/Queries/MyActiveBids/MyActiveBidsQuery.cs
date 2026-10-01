using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.MyActiveBids;

public sealed record MyActiveBidsQuery(Guid UserId, string? Cursor, int Limit)
    : IRequest<(bool Success, IReadOnlyList<MyActiveBidDto>? Data, object? Meta, string[] Errors)>;

public sealed record MyActiveBidDto(
    Guid AuctionId,
    Guid ArtworkId,
    string ArtworkTitle,
    string ParticipationStatus,
    decimal CurrentPrice,
    decimal MinimumNextBid,
    DateTimeOffset EndAt,
    decimal LockedAmount,
    DateTimeOffset LastBidAt,
    Guid LastBidId);

public sealed class MyActiveBidsQueryHandler
    : IRequestHandler<MyActiveBidsQuery, (bool, IReadOnlyList<MyActiveBidDto>?, object?, string[])>
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;
    private readonly IApplicationDbContext _db;

    public MyActiveBidsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool, IReadOnlyList<MyActiveBidDto>?, object?, string[])> Handle(
        MyActiveBidsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, null, ["Cần đăng nhập để xem các phiên đang tham gia."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);
        var query = _db.Bids.AsNoTracking().Where(b =>
            b.BidderId == request.UserId && !b.IsDeleted
            && (b.Status == BidStatus.Leading || b.Status == BidStatus.Outbid)
            && b.Auction != null && !b.Auction.IsDeleted
            && (b.Auction.Status == AuctionStatus.Active || b.Auction.Status == AuctionStatus.Ended)
            && !_db.Bids.Any(other => other.BidderId == b.BidderId
                && other.AuctionId == b.AuctionId && !other.IsDeleted
                && (other.PlacedAt > b.PlacedAt || (other.PlacedAt == b.PlacedAt && other.Id.CompareTo(b.Id) < 0))));

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!AuctionListCursor.TryDecode(request.Cursor, out var placedAt, out var bidId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(b => b.PlacedAt < placedAt
                || (b.PlacedAt == placedAt && b.Id.CompareTo(bidId) > 0));
        }

        var rows = await query
            .OrderByDescending(b => b.PlacedAt)
            .ThenBy(b => b.Id)
            .Take(limit + 1)
            .Select(b => new
            {
                b.Id,
                b.PlacedAt,
                AuctionId = b.AuctionId,
                ArtworkId = b.Auction!.ArtworkId,
                ArtworkTitle = b.Auction.Artwork!.Title,
                Status = _db.Bids.Any(leading => leading.AuctionId == b.AuctionId
                    && leading.BidderId == request.UserId && leading.Status == BidStatus.Leading && !leading.IsDeleted)
                    ? "Leading" : "Outbid",
                CurrentPrice = b.Auction.CurrentPrice,
                MinimumNextBid = b.Auction.BidCount == 0 ? b.Auction.StartPrice : b.Auction.CurrentPrice + b.Auction.BidStep,
                EndAt = b.Auction.EndAt,
                LockedAmount = _db.Bids.Where(held => held.AuctionId == b.AuctionId
                    && held.BidderId == request.UserId && held.HoldStatus == HoldStatus.Held && !held.IsDeleted)
                    .Sum(held => (decimal?)held.HoldAmount) ?? 0m
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var data = page.Select(x => new MyActiveBidDto(
            x.AuctionId, x.ArtworkId, x.ArtworkTitle, x.Status, x.CurrentPrice,
            x.MinimumNextBid, x.EndAt, x.LockedAmount, x.PlacedAt, x.Id)).ToList();
        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? AuctionListCursor.Encode(last.PlacedAt, last.Id)
            : null;
        var totalLockedAmount = await _db.Bids.AsNoTracking()
            .Where(b => b.BidderId == request.UserId && !b.IsDeleted && b.HoldStatus == HoldStatus.Held
                && b.Auction != null && !b.Auction.IsDeleted
                && (b.Auction.Status == AuctionStatus.Active || b.Auction.Status == AuctionStatus.Ended))
            .SumAsync(b => (decimal?)b.HoldAmount, cancellationToken) ?? 0m;

        return (true, data, new { nextCursor, count = data.Count, totalLockedAmount }, []);
    }
}

internal static class AuctionListCursor
{
    public static string Encode(DateTimeOffset timestamp, Guid id) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{timestamp.UtcTicks}|{id:N}"));

    public static bool TryDecode(string value, out DateTimeOffset timestamp, out Guid id)
    {
        timestamp = default;
        id = default;
        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            return parts.Length == 2
                && long.TryParse(parts[0], out var ticks)
                && Guid.TryParseExact(parts[1], "N", out id)
                && TryCreate(ticks, out timestamp);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryCreate(long ticks, out DateTimeOffset timestamp)
    {
        try
        {
            timestamp = new DateTimeOffset(ticks, TimeSpan.Zero);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            timestamp = default;
            return false;
        }
    }
}
