using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchCreatorsTool(IApplicationDbContext db) : IAiTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string Name => "search_creators";

    public string Description =>
        "Finds and smart-ranks (0-100) approved creators by styles/keywords (name, specialty, headline, bio, artwork style/tag, or package title), commission price range (CommissionServices & RateCard), delivery days, rating, available slots, location, order availability, or AI-verified badge.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional art style, specialty, package keyword, or creator name (e.g. 'anime', 'chibi', 'anime, half-body')"
            },
            styles = new
            {
                type = "array",
                items = new { type = "string" },
                description = "Optional list of desired styles, subjects, or package keywords for multi-criteria matching"
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
                description = "Optional minimum active commission package price (VND)"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum active commission package price (VND)"
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
                @enum = new[] { "smart_match", "rating", "followers", "price_asc", "completed_orders" },
                description = "Optional sort order (defaults to smart_match)"
            },
            limit = new
            {
                type = "integer",
                description = "Optional maximum number of creators to return (1-10, default 8)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var tokens = ExtractTokens(args);

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
            : "smart_match";

        var limit = args.TryGetProperty("limit", out var lim) && lim.ValueKind == JsonValueKind.Number && lim.TryGetInt32(out var parsedLimit)
            ? Math.Clamp(parsedLimit, 1, 10)
            : 8;

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

        var creators = await query
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.Headline,
                p.Specialties,
                p.Bio,
                p.Location,
                p.RatingAverage,
                p.RatingCount,
                p.FollowerCount,
                p.CompletedOrdersCount,
                p.IsAcceptingOrders,
                p.AvailableSlots,
                p.IsAiVerified,
                p.RateCardJson
            })
            .ToListAsync(cancellationToken);

        if (creators.Count == 0)
        {
            return Array.Empty<object>();
        }

        var creatorIds = creators.Select(c => c.Id).ToList();

        var dbServices = await db.CommissionServices
            .AsNoTracking()
            .Where(s => creatorIds.Contains(s.CreatorProfileId) && s.IsActive && !s.IsDeleted)
            .Select(s => new
            {
                s.CreatorProfileId,
                s.Title,
                s.Description,
                s.StartingPrice,
                s.DeliveryDays,
                s.MaxRevisions
            })
            .ToListAsync(cancellationToken);

        var servicesByCreator = dbServices
            .GroupBy(s => s.CreatorProfileId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new PackageCandidate(
                    s.Title,
                    s.Description,
                    null,
                    s.StartingPrice,
                    s.DeliveryDays,
                    s.MaxRevisions,
                    "CommissionService")).ToList());

        var artworkKeywords = await db.Artworks
            .AsNoTracking()
            .Where(a => creatorIds.Contains(a.CreatorProfileId) && !a.IsDeleted && a.ModerationStatus == "Approved")
            .Select(a => new
            {
                a.CreatorProfileId,
                a.Style,
                Tags = a.ArtworkTags
                    .Where(at => at.Tag != null && !at.Tag.IsDeleted)
                    .Select(at => at.Tag!.Name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var artTextByCreator = artworkKeywords
            .GroupBy(a => a.CreatorProfileId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(" ", g.Select(a => $"{a.Style} {string.Join(" ", a.Tags)}")));

        var hasPackageFilter = minPrice.HasValue || maxPrice.HasValue || maxDeliveryDays.HasValue;
        var scored = new List<ScoredCreator>();

        foreach (var c in creators)
        {
            var packages = new List<PackageCandidate>();
            if (servicesByCreator.TryGetValue(c.Id, out var svcList))
            {
                packages.AddRange(svcList);
            }
            packages.AddRange(ParseRateCardPackages(c.RateCardJson));

            var eligiblePackages = packages.Where(pkg =>
                (!minPrice.HasValue || pkg.Price >= minPrice.Value)
                && (!maxPrice.HasValue || pkg.Price <= maxPrice.Value)
                && (!maxDeliveryDays.HasValue || (pkg.DeliveryDays.HasValue && pkg.DeliveryDays.Value <= maxDeliveryDays.Value)))
                .ToList();

            if (hasPackageFilter && eligiblePackages.Count == 0)
            {
                continue;
            }

            // 1. Style / keyword match (0..40)
            artTextByCreator.TryGetValue(c.Id, out var artText);
            var profileCorpus = $"{c.DisplayName} {c.Headline} {c.Specialties} {c.Bio} {artText}";
            var matchedTokens = new List<string>();

            if (tokens.Count > 0)
            {
                foreach (var token in tokens)
                {
                    var alt = token.Replace('-', ' ');
                    var inProfile = ContainsToken(profileCorpus, token, alt);
                    var inPackages = packages.Any(p =>
                        ContainsToken(p.Title, token, alt)
                        || ContainsToken(p.Description, token, alt)
                        || ContainsToken(p.CategoryName, token, alt));

                    if (inProfile || inPackages)
                    {
                        matchedTokens.Add(token);
                    }
                }

                if (matchedTokens.Count == 0)
                {
                    continue;
                }
            }

            var styleScore = tokens.Count > 0
                ? 40.0 * matchedTokens.Count / tokens.Count
                : 20.0;

            var candidatePool = eligiblePackages.Count > 0 ? eligiblePackages : packages;
            var orderedPackages = candidatePool
                .OrderByDescending(p => tokens.Count(t =>
                {
                    var alt = t.Replace('-', ' ');
                    return ContainsToken(p.Title, t, alt)
                        || ContainsToken(p.Description, t, alt)
                        || ContainsToken(p.CategoryName, t, alt);
                }))
                .ThenBy(p => p.Price)
                .ToList();

            var bestPackage = orderedPackages.FirstOrDefault();

            // 2. Budget & delivery fit (0..20)
            double budgetScore;
            if (bestPackage is null)
            {
                budgetScore = 5.0;
            }
            else if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                var ratio = Math.Clamp((double)(bestPackage.Price / maxPrice.Value), 0.0, 1.0);
                budgetScore = 14.0 + 6.0 * (1.0 - ratio);
            }
            else
            {
                budgetScore = 15.0;
            }

            // 3. Quality & trust (0..25)
            var ratingScore = ((double)c.RatingAverage / 5.0) * 15.0;
            var experienceScore = Math.Min(c.CompletedOrdersCount, 20) / 20.0 * 7.0;
            var verifiedScore = c.IsAiVerified ? 3.0 : 0.0;
            var trustScore = ratingScore + experienceScore + verifiedScore;

            // 4. Availability (0..15)
            var availabilityScore = c.IsAcceptingOrders && c.AvailableSlots > 0
                ? 8.0 + Math.Min(c.AvailableSlots, 5) / 5.0 * 7.0
                : c.IsAcceptingOrders ? 5.0 : 0.0;

            var matchScore = (int)Math.Round(Math.Clamp(styleScore + budgetScore + trustScore + availabilityScore, 0.0, 100.0));

            var reasons = new List<string>();
            if (matchedTokens.Count > 0)
            {
                reasons.Add($"Matches: {string.Join(", ", matchedTokens)}");
            }
            if (bestPackage is not null)
            {
                reasons.Add($"Best-fit package: {bestPackage.Title} ({bestPackage.Price:N0} VND)");
            }
            if (c.RatingAverage > 0)
            {
                reasons.Add($"{c.RatingAverage:0.0}★ rating ({c.CompletedOrdersCount} completed orders)");
            }
            if (c.IsAcceptingOrders && c.AvailableSlots > 0)
            {
                reasons.Add($"{c.AvailableSlots} open slot(s)");
            }
            if (c.IsAiVerified)
            {
                reasons.Add("Hand-drawn AI-verified");
            }

            var minServicePrice = orderedPackages.Count > 0 ? orderedPackages.Min(p => p.Price) : (decimal?)null;

            scored.Add(new ScoredCreator(
                matchScore,
                c.RatingAverage,
                c.FollowerCount,
                c.CompletedOrdersCount,
                minServicePrice ?? decimal.MaxValue,
                new
                {
                    id = c.Id,
                    url = $"/creators/{c.Id}",
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
                    matchScore,
                    matchReasons = reasons,
                    minServicePrice,
                    matchingServices = orderedPackages.Take(3).Select(s => new
                    {
                        s.Title,
                        startingPrice = s.Price,
                        s.DeliveryDays,
                        s.MaxRevisions,
                        s.Source
                    }).ToList()
                }));
        }

        IEnumerable<ScoredCreator> ordered = sortBy switch
        {
            "rating" => scored.OrderByDescending(x => x.RatingAverage).ThenByDescending(x => x.FollowerCount),
            "followers" => scored.OrderByDescending(x => x.FollowerCount).ThenByDescending(x => x.RatingAverage),
            "completed_orders" => scored.OrderByDescending(x => x.CompletedOrdersCount).ThenByDescending(x => x.RatingAverage),
            "price_asc" => scored.OrderBy(x => x.MinServicePrice).ThenByDescending(x => x.MatchScore),
            _ => scored.OrderByDescending(x => x.MatchScore).ThenByDescending(x => x.RatingAverage)
        };

        return ordered.Take(limit).Select(x => x.Payload).ToList();
    }

    private static List<string> ExtractTokens(JsonElement args)
    {
        var list = new List<string>();

        if (args.TryGetProperty("styles", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var val = item.GetString()?.Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(val)) list.Add(val);
                }
            }
        }

        if (args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String)
        {
            var raw = kw.GetString()?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var part in raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!string.IsNullOrEmpty(part)) list.Add(part);
                }
            }
        }

        return list.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool ContainsToken(string? text, string token, string altToken) =>
        !string.IsNullOrEmpty(text)
        && (text.Contains(token, StringComparison.OrdinalIgnoreCase)
            || text.Contains(altToken, StringComparison.OrdinalIgnoreCase));

    private static List<PackageCandidate> ParseRateCardPackages(string? rateCardJson)
    {
        if (string.IsNullOrWhiteSpace(rateCardJson))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<RateCardPackageDto>>(rateCardJson, JsonOptions);
            if (parsed is null || parsed.Count == 0)
            {
                return [];
            }

            return parsed
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .Select(p => new PackageCandidate(
                    p.Name!,
                    p.Description,
                    p.CategoryName,
                    p.Price,
                    null,
                    p.MaxRevisions ?? p.Milestones?.Sum(m => m.MaxRevisions ?? 0),
                    "RateCardPackage"))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record ScoredCreator(
        int MatchScore,
        decimal RatingAverage,
        int FollowerCount,
        int CompletedOrdersCount,
        decimal MinServicePrice,
        object Payload);

    private sealed record PackageCandidate(
        string Title,
        string? Description,
        string? CategoryName,
        decimal Price,
        int? DeliveryDays,
        int? MaxRevisions,
        string Source);

    private sealed class RateCardPackageDto
    {
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
