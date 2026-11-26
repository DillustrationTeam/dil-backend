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
        "Searches or lists public artwork auctions by optional keyword, status, physical copy availability, or max current price.";

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
            hasPhysicalCopy = new
            {
                type = "boolean",
                description = "Optional filter for auctions offering a physical copy"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum current price (VND)"
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

        bool? hasPhysicalCopy = args.TryGetProperty("hasPhysicalCopy", out var phys) && phys.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? phys.GetBoolean()
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var mp) && mp.ValueKind == JsonValueKind.Number && mp.TryGetDecimal(out var dec)
            ? dec
            : null;

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

        if (hasPhysicalCopy.HasValue)
        {
            query = query.Where(a => a.HasPhysicalCopy == hasPhysicalCopy.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(a => a.CurrentPrice <= maxPrice.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(a =>
                (a.Artwork != null && a.Artwork.Title.ToLower().Contains(keyword))
                || (a.PhysicalDescription != null && a.PhysicalDescription.ToLower().Contains(keyword)));
        }

        return await query
            .OrderBy(a => a.EndAt)
            .Take(10)
            .Select(a => new
            {
                id = a.Id,
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
