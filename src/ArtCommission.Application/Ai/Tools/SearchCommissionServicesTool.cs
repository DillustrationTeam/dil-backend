using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchCommissionServicesTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_commission_services";

    public string Description =>
        "Searches active commission packages/services across all approved creators by keyword (e.g. 'half-body', 'chibi', 'portrait'), price range, delivery days, revisions, and order availability.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching service title, description, or creator specialties"
            },
            minPrice = new
            {
                type = "number",
                description = "Optional minimum starting price (VND)"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum starting price (VND)"
            },
            maxDeliveryDays = new
            {
                type = "integer",
                description = "Optional maximum delivery days"
            },
            minRevisions = new
            {
                type = "integer",
                description = "Optional minimum number of included revisions"
            },
            acceptingOrdersOnly = new
            {
                type = "boolean",
                description = "If true, only return services from creators currently accepting orders"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "price_asc", "price_desc", "fastest", "rating" },
                description = "Optional sort order (defaults to price_asc)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        decimal? minPrice = args.TryGetProperty("minPrice", out var minP) && minP.ValueKind == JsonValueKind.Number && minP.TryGetDecimal(out var parsedMinP)
            ? parsedMinP
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var maxP) && maxP.ValueKind == JsonValueKind.Number && maxP.TryGetDecimal(out var parsedMaxP)
            ? parsedMaxP
            : null;

        int? maxDeliveryDays = args.TryGetProperty("maxDeliveryDays", out var maxD) && maxD.ValueKind == JsonValueKind.Number && maxD.TryGetInt32(out var parsedMaxD)
            ? parsedMaxD
            : null;

        int? minRevisions = args.TryGetProperty("minRevisions", out var minR) && minR.ValueKind == JsonValueKind.Number && minR.TryGetInt32(out var parsedMinR)
            ? parsedMinR
            : null;

        var acceptingOrdersOnly = args.TryGetProperty("acceptingOrdersOnly", out var open) && open.ValueKind == JsonValueKind.True;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "price_asc";

        var query = db.CommissionServices
            .AsNoTracking()
            .Where(s => s.IsActive
                && !s.IsDeleted
                && s.CreatorProfile != null
                && s.CreatorProfile.IsApproved
                && !s.CreatorProfile.IsDeleted);

        if (acceptingOrdersOnly)
        {
            query = query.Where(s => s.CreatorProfile!.IsAcceptingOrders);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(s => s.StartingPrice >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(s => s.StartingPrice <= maxPrice.Value);
        }

        if (maxDeliveryDays.HasValue)
        {
            query = query.Where(s => s.DeliveryDays <= maxDeliveryDays.Value);
        }

        if (minRevisions.HasValue)
        {
            query = query.Where(s => s.MaxRevisions >= minRevisions.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            var altKeyword = keyword.Replace('-', ' ');
            query = query.Where(s =>
                s.Title.ToLower().Contains(keyword)
                || s.Title.ToLower().Contains(altKeyword)
                || (s.Description != null && (s.Description.ToLower().Contains(keyword) || s.Description.ToLower().Contains(altKeyword)))
                || (s.CreatorProfile!.Specialties != null && (s.CreatorProfile.Specialties.ToLower().Contains(keyword) || s.CreatorProfile.Specialties.ToLower().Contains(altKeyword)))
                || s.CreatorProfile.DisplayName.ToLower().Contains(keyword));
        }

        query = sortBy switch
        {
            "price_desc" => query.OrderByDescending(s => s.StartingPrice),
            "fastest" => query.OrderBy(s => s.DeliveryDays).ThenBy(s => s.StartingPrice),
            "rating" => query.OrderByDescending(s => s.CreatorProfile!.RatingAverage).ThenBy(s => s.StartingPrice),
            _ => query.OrderBy(s => s.StartingPrice).ThenByDescending(s => s.CreatorProfile!.RatingAverage)
        };

        return await query
            .Take(10)
            .Select(s => new
            {
                serviceId = s.Id,
                s.Title,
                s.Description,
                s.StartingPrice,
                s.DeliveryDays,
                s.MaxRevisions,
                creatorProfileId = s.CreatorProfileId,
                creatorDisplayName = s.CreatorProfile!.DisplayName,
                creatorRating = s.CreatorProfile.RatingAverage,
                isAcceptingOrders = s.CreatorProfile.IsAcceptingOrders,
                availableSlots = s.CreatorProfile.AvailableSlots
            })
            .ToListAsync(cancellationToken);
    }
}
