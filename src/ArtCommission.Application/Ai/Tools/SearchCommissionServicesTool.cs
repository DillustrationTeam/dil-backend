using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchCommissionServicesTool(IApplicationDbContext db) : IAiTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "search_commission_services";

    public string Description =>
        "Searches active commission packages/services (both CommissionServices and creator rate-card packages) across all approved creators by keyword (e.g. 'half-body', 'chibi', 'portrait'), price range, delivery days, revisions, and order availability.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching package/service name, description, category, or creator specialties"
            },
            minPrice = new
            {
                type = "number",
                description = "Optional minimum price (VND)"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum price (VND)"
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
                description = "If true, only return packages from creators currently accepting orders"
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
        var altKeyword = keyword?.Replace('-', ' ');

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

        // 1. CommissionServices table
        var serviceQuery = db.CommissionServices
            .AsNoTracking()
            .Where(s => s.IsActive
                && !s.IsDeleted
                && s.CreatorProfile != null
                && s.CreatorProfile.IsApproved
                && !s.CreatorProfile.IsDeleted);

        if (acceptingOrdersOnly)
        {
            serviceQuery = serviceQuery.Where(s => s.CreatorProfile!.IsAcceptingOrders);
        }
        if (minPrice.HasValue)
        {
            serviceQuery = serviceQuery.Where(s => s.StartingPrice >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            serviceQuery = serviceQuery.Where(s => s.StartingPrice <= maxPrice.Value);
        }
        if (maxDeliveryDays.HasValue)
        {
            serviceQuery = serviceQuery.Where(s => s.DeliveryDays <= maxDeliveryDays.Value);
        }
        if (minRevisions.HasValue)
        {
            serviceQuery = serviceQuery.Where(s => s.MaxRevisions >= minRevisions.Value);
        }
        if (!string.IsNullOrEmpty(keyword))
        {
            serviceQuery = serviceQuery.Where(s =>
                s.Title.ToLower().Contains(keyword)
                || s.Title.ToLower().Contains(altKeyword!)
                || (s.Description != null && (s.Description.ToLower().Contains(keyword) || s.Description.ToLower().Contains(altKeyword!)))
                || (s.CreatorProfile!.Specialties != null && (s.CreatorProfile.Specialties.ToLower().Contains(keyword) || s.CreatorProfile.Specialties.ToLower().Contains(altKeyword!)))
                || s.CreatorProfile.DisplayName.ToLower().Contains(keyword));
        }

        var dbServices = await serviceQuery
            .Select(s => new ServiceMatch(
                s.Id.ToString(),
                "CommissionService",
                s.Title,
                s.Description,
                null,
                s.StartingPrice,
                s.DeliveryDays,
                s.MaxRevisions,
                s.CreatorProfileId,
                s.CreatorProfile!.DisplayName,
                s.CreatorProfile.RatingAverage,
                s.CreatorProfile.IsAcceptingOrders,
                s.CreatorProfile.AvailableSlots))
            .ToListAsync(cancellationToken);

        // 2. CreatorProfile.RateCardJson packages (unless maxDeliveryDays is required, since rate-card packages have no deliveryDays field)
        var rateCardMatches = new List<ServiceMatch>();
        if (!maxDeliveryDays.HasValue)
        {
            var creatorQuery = db.CreatorProfiles
                .AsNoTracking()
                .Where(p => p.IsApproved && !p.IsDeleted && p.RateCardJson != null && p.RateCardJson != "");

            if (acceptingOrdersOnly)
            {
                creatorQuery = creatorQuery.Where(p => p.IsAcceptingOrders);
            }

            var creatorsWithRateCard = await creatorQuery
                .Select(p => new
                {
                    p.Id,
                    p.DisplayName,
                    p.Specialties,
                    p.RatingAverage,
                    p.IsAcceptingOrders,
                    p.AvailableSlots,
                    p.RateCardJson
                })
                .ToListAsync(cancellationToken);

            foreach (var creator in creatorsWithRateCard)
            {
                List<RateCardPackageDto>? packages;
                try
                {
                    packages = JsonSerializer.Deserialize<List<RateCardPackageDto>>(creator.RateCardJson!, JsonOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (packages is null || packages.Count == 0)
                {
                    continue;
                }

                foreach (var pkg in packages)
                {
                    if (string.IsNullOrWhiteSpace(pkg.Name))
                    {
                        continue;
                    }

                    if (minPrice.HasValue && pkg.Price < minPrice.Value) continue;
                    if (maxPrice.HasValue && pkg.Price > maxPrice.Value) continue;

                    var revisions = pkg.MaxRevisions
                        ?? pkg.Milestones?.Sum(m => m.MaxRevisions ?? 0);
                    if (minRevisions.HasValue && (revisions ?? 0) < minRevisions.Value) continue;

                    if (!string.IsNullOrEmpty(keyword))
                    {
                        var matchesKeyword =
                            ContainsKeyword(pkg.Name, keyword, altKeyword!)
                            || ContainsKeyword(pkg.Description, keyword, altKeyword!)
                            || ContainsKeyword(pkg.CategoryName, keyword, altKeyword!)
                            || ContainsKeyword(creator.Specialties, keyword, altKeyword!)
                            || ContainsKeyword(creator.DisplayName, keyword, altKeyword!);

                        if (!matchesKeyword)
                        {
                            continue;
                        }
                    }

                    rateCardMatches.Add(new ServiceMatch(
                        pkg.Id ?? string.Empty,
                        "RateCardPackage",
                        pkg.Name,
                        pkg.Description,
                        pkg.CategoryName,
                        pkg.Price,
                        null,
                        revisions,
                        creator.Id,
                        creator.DisplayName,
                        creator.RatingAverage,
                        creator.IsAcceptingOrders,
                        creator.AvailableSlots));
                }
            }
        }

        IEnumerable<ServiceMatch> combined = dbServices.Concat(rateCardMatches);

        combined = sortBy switch
        {
            "price_desc" => combined.OrderByDescending(s => s.StartingPrice),
            "fastest" => combined.OrderBy(s => s.DeliveryDays ?? int.MaxValue).ThenBy(s => s.StartingPrice),
            "rating" => combined.OrderByDescending(s => s.CreatorRating).ThenBy(s => s.StartingPrice),
            _ => combined.OrderBy(s => s.StartingPrice).ThenByDescending(s => s.CreatorRating)
        };

        return combined.Take(10).ToList();
    }

    private static bool ContainsKeyword(string? text, string keyword, string altKeyword) =>
        !string.IsNullOrEmpty(text)
        && (text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            || text.Contains(altKeyword, StringComparison.OrdinalIgnoreCase));

    private sealed record ServiceMatch(
        string ServiceId,
        string Source,
        string Title,
        string? Description,
        string? CategoryName,
        decimal StartingPrice,
        int? DeliveryDays,
        int? MaxRevisions,
        Guid CreatorProfileId,
        string CreatorDisplayName,
        decimal CreatorRating,
        bool IsAcceptingOrders,
        int AvailableSlots);

    private sealed class RateCardPackageDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int? MaxRevisions { get; set; }
        public string? CategoryName { get; set; }
        public List<RateCardMilestoneDto>? Milestones { get; set; }
    }

    private sealed class RateCardMilestoneDto
    {
        public int? MaxRevisions { get; set; }
    }
}
