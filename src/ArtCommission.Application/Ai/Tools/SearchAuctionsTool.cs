using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchAuctionsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_auctions";

    public string Description =>
        "Searches or lists public artwork auctions by optional keyword, status, auctionType, physical copy availability, buy-now option, price range, end date, and sort order.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching artwork title or physical description"
            },
            status = new
            {
                type = "string",
                @enum = new[] { "scheduled", "active", "ended", "any" },
                description = "Optional auction status filter (defaults to any)"
            },
            auctionType = new
            {
                type = "string",
                @enum = new[] { "standard", "buynow", "any" },
                description = "Optional auction type filter"
            },
            hasPhysicalCopy = new
            {
                type = "boolean",
                description = "Optional filter for auctions offering a physical copy"
            },
            hasBuyNow = new
            {
                type = "boolean",
                description = "If true, only return auctions with a buy-now price"
            },
            minPrice = new
            {
                type = "number",
                description = "Optional minimum current price (VND)"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum current price (VND)"
            },
            endingBefore = new
            {
                type = "string",
                description = "Optional ISO 8601 date/time; returns auctions ending on or before this time"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "ending_soon", "price_asc", "price_desc", "most_bids" },
                description = "Optional sort order (defaults to ending_soon)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var status = args.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String
            ? st.GetString()?.Trim().ToLowerInvariant()
            : "any";

        var auctionType = args.TryGetProperty("auctionType", out var at) && at.ValueKind == JsonValueKind.String
            ? at.GetString()?.Trim().ToLowerInvariant()
            : "any";

        bool? hasPhysicalCopy = args.TryGetProperty("hasPhysicalCopy", out var phys) && phys.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? phys.GetBoolean()
            : null;

        var hasBuyNow = args.TryGetProperty("hasBuyNow", out var bn) && bn.ValueKind == JsonValueKind.True;

        decimal? minPrice = args.TryGetProperty("minPrice", out var minP) && minP.ValueKind == JsonValueKind.Number && minP.TryGetDecimal(out var parsedMinP)
            ? parsedMinP
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var mp) && mp.ValueKind == JsonValueKind.Number && mp.TryGetDecimal(out var dec)
            ? dec
            : null;

        DateTimeOffset? endingBefore = args.TryGetProperty("endingBefore", out var eb)
            && eb.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(eb.GetString(), out var parsedEb)
                ? parsedEb
                : null;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "ending_soon";

        var query = db.Auctions
            .AsNoTracking()
            .Where(a => !a.IsDeleted);

        query = status switch
        {
            "scheduled" => query.Where(a => a.Status == AuctionStatus.Scheduled),
            "active" => query.Where(a => a.Status == AuctionStatus.Active),
            "ended" => query.Where(a => a.Status == AuctionStatus.Ended || a.Status == AuctionStatus.Settled),
            _ => query.Where(a => a.Status != AuctionStatus.Cancelled)
        };

        query = auctionType switch
        {
            "standard" => query.Where(a => a.AuctionType == AuctionType.Standard),
            "buynow" => query.Where(a => a.AuctionType == AuctionType.BuyNow),
            _ => query
        };

        if (hasPhysicalCopy.HasValue)
        {
            query = query.Where(a => a.HasPhysicalCopy == hasPhysicalCopy.Value);
        }

        if (hasBuyNow)
        {
            query = query.Where(a => a.BuyNowPrice.HasValue);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice <= maxPrice.Value);
        }

        if (endingBefore.HasValue)
        {
            query = query.Where(a => a.EndAt <= endingBefore.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(a =>
                (a.Artwork != null && a.Artwork.Title.ToLower().Contains(keyword))
                || (a.PhysicalDescription != null && a.PhysicalDescription.ToLower().Contains(keyword)));
        }

        query = sortBy switch
        {
            "price_asc" => query.OrderBy(a => a.CurrentPrice),
            "price_desc" => query.OrderByDescending(a => a.CurrentPrice),
            "most_bids" => query.OrderByDescending(a => a.BidCount).ThenBy(a => a.EndAt),
            _ => query.OrderBy(a => a.EndAt)
        };

        return await query
            .Take(10)
            .Select(a => new
            {
                id = a.Id,
                url = $"/auctions/{a.Id}",
                artworkTitle = a.Artwork != null ? a.Artwork.Title : null,
                artworkThumbnail = a.Artwork != null ? (a.Artwork.WatermarkedUrl ?? a.Artwork.ThumbnailUrl) : null,
                auctionType = a.AuctionType.ToString(),
                a.StartPrice,
                a.BidStep,
                a.BuyNowPrice,
                a.CurrentPrice,
                a.BidCount,
                a.StartAt,
                a.EndAt,
                status = a.Status.ToString(),
                a.HasPhysicalCopy,
                a.PhysicalDescription
            })
            .ToListAsync(cancellationToken);
    }
}
