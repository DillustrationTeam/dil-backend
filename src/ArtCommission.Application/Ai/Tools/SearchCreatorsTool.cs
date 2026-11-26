using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchCreatorsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_creators";

    public string Description =>
        "Finds approved creators by keyword (name, specialty, headline, artwork style/tag), commission service price range, delivery days, rating, available slots, location, order availability, or AI-verified badge.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional art style, specialty, or creator name keyword (e.g. 'anime', 'chibi')"
            },
            acceptingOrdersOnly = new
            {
                type = "boolean",
                description = "If true, only return creators currently accepting commission orders"
            },
            aiVerifiedOnly = new
            {
                type = "boolean",
                description = "If true, only return creators with the hand-drawn AI-verified badge"
            },
            minPrice = new
            {
                type = "number",
                description = "Optional minimum active commission service starting price (VND)"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum active commission service starting price (VND)"
            },
            maxDeliveryDays = new
            {
                type = "integer",
                description = "Optional maximum delivery days for an active commission service"
            },
            minRating = new
            {
                type = "number",
                description = "Optional minimum average rating (0-5)"
            },
            minAvailableSlots = new
            {
                type = "integer",
                description = "Optional minimum number of open commission slots"
            },
            location = new
            {
                type = "string",
                description = "Optional creator location filter"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "rating", "followers", "price_asc", "completed_orders" },
                description = "Optional sort order (defaults to rating)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var acceptingOrdersOnly = args.TryGetProperty("acceptingOrdersOnly", out var open) && open.ValueKind == JsonValueKind.True;
        var aiVerifiedOnly = args.TryGetProperty("aiVerifiedOnly", out var verified) && verified.ValueKind == JsonValueKind.True;

        decimal? minPrice = args.TryGetProperty("minPrice", out var minP) && minP.ValueKind == JsonValueKind.Number && minP.TryGetDecimal(out var parsedMinP)
            ? parsedMinP
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var maxP) && maxP.ValueKind == JsonValueKind.Number && maxP.TryGetDecimal(out var parsedMaxP)
            ? parsedMaxP
            : null;

        int? maxDeliveryDays = args.TryGetProperty("maxDeliveryDays", out var maxD) && maxD.ValueKind == JsonValueKind.Number && maxD.TryGetInt32(out var parsedMaxD)
            ? parsedMaxD
            : null;

        decimal? minRating = args.TryGetProperty("minRating", out var minR) && minR.ValueKind == JsonValueKind.Number && minR.TryGetDecimal(out var parsedMinR)
            ? parsedMinR
            : null;

        int? minAvailableSlots = args.TryGetProperty("minAvailableSlots", out var minS) && minS.ValueKind == JsonValueKind.Number && minS.TryGetInt32(out var parsedMinS)
            ? parsedMinS
            : null;

        var location = args.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.String
            ? loc.GetString()?.Trim().ToLower()
            : null;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "rating";

        var query = db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.IsApproved && !p.IsDeleted);

        if (acceptingOrdersOnly)
        {
            query = query.Where(p => p.IsAcceptingOrders);
        }

        if (aiVerifiedOnly)
        {
            query = query.Where(p => p.IsAiVerified);
        }

        if (minRating.HasValue)
        {
            query = query.Where(p => p.RatingAverage >= minRating.Value);
        }

        if (minAvailableSlots.HasValue)
        {
            query = query.Where(p => p.AvailableSlots >= minAvailableSlots.Value);
        }

        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(p => p.Location != null && p.Location.ToLower().Contains(location));
        }

        if (minPrice.HasValue || maxPrice.HasValue || maxDeliveryDays.HasValue)
        {
            query = query.Where(p => db.CommissionServices.Any(s =>
                s.CreatorProfileId == p.Id
                && s.IsActive
                && !s.IsDeleted
                && (!minPrice.HasValue || s.StartingPrice >= minPrice.Value)
                && (!maxPrice.HasValue || s.StartingPrice <= maxPrice.Value)
                && (!maxDeliveryDays.HasValue || s.DeliveryDays <= maxDeliveryDays.Value)));
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.DisplayName.ToLower().Contains(keyword)
                || (p.Specialties != null && p.Specialties.ToLower().Contains(keyword))
                || (p.Headline != null && p.Headline.ToLower().Contains(keyword))
                || (p.Bio != null && p.Bio.ToLower().Contains(keyword))
                || p.Artworks.Any(a =>
                    !a.IsDeleted
                    && a.ModerationStatus == "Approved"
                    && ((a.Style != null && a.Style.ToLower().Contains(keyword))
                        || a.ArtworkTags.Any(at => at.Tag != null && at.Tag.Name.ToLower().Contains(keyword)))));
        }

        query = sortBy switch
        {
            "followers" => query.OrderByDescending(p => p.FollowerCount).ThenByDescending(p => p.RatingAverage),
            "completed_orders" => query.OrderByDescending(p => p.CompletedOrdersCount).ThenByDescending(p => p.RatingAverage),
            "price_asc" => query.OrderBy(p => db.CommissionServices
                .Where(s => s.CreatorProfileId == p.Id && s.IsActive && !s.IsDeleted)
                .Select(s => (decimal?)s.StartingPrice)
                .Min() ?? decimal.MaxValue),
            _ => query.OrderByDescending(p => p.RatingAverage).ThenByDescending(p => p.FollowerCount)
        };

        var creators = await query
            .Take(8)
            .Select(p => new
            {
                id = p.Id,
                p.DisplayName,
                p.Headline,
                p.Specialties,
                p.Location,
                p.RatingAverage,
                p.RatingCount,
                p.FollowerCount,
                p.CompletedOrdersCount,
                p.IsAcceptingOrders,
                p.AvailableSlots,
                p.IsAiVerified
            })
            .ToListAsync(cancellationToken);

        if (creators.Count == 0)
        {
            return creators;
        }

        var creatorIds = creators.Select(c => c.id).ToList();
        var matchingServices = await db.CommissionServices
            .AsNoTracking()
            .Where(s => creatorIds.Contains(s.CreatorProfileId)
                && s.IsActive
                && !s.IsDeleted
                && (!minPrice.HasValue || s.StartingPrice >= minPrice.Value)
                && (!maxPrice.HasValue || s.StartingPrice <= maxPrice.Value)
                && (!maxDeliveryDays.HasValue || s.DeliveryDays <= maxDeliveryDays.Value))
            .OrderBy(s => s.StartingPrice)
            .Select(s => new
            {
                s.CreatorProfileId,
                s.Title,
                s.StartingPrice,
                s.DeliveryDays,
                s.MaxRevisions
            })
            .ToListAsync(cancellationToken);

        var servicesByCreator = matchingServices
            .GroupBy(s => s.CreatorProfileId)
            .ToDictionary(
                g => g.Key,
                g => g.Take(3).Select(s => new
                {
                    s.Title,
                    s.StartingPrice,
                    s.DeliveryDays,
                    s.MaxRevisions
                }).ToList());

        return creators.Select(c =>
        {
            servicesByCreator.TryGetValue(c.id, out var services);
            return new
            {
                c.id,
                c.DisplayName,
                c.Headline,
                c.Specialties,
                c.Location,
                c.RatingAverage,
                c.RatingCount,
                c.FollowerCount,
                c.CompletedOrdersCount,
                c.IsAcceptingOrders,
                c.AvailableSlots,
                c.IsAiVerified,
                minServicePrice = services is { Count: > 0 } ? services.Min(s => s.StartingPrice) : (decimal?)null,
                matchingServices = services
            };
        }).ToList();
    }
}
