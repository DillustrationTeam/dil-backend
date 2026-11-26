using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchArtworksTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_artworks";

    public string Description =>
        "Searches approved public artworks by optional keyword, art style, category slug, creator name, license type, price range, AI-generated filter, and sort order.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching title, description, or tag"
            },
            style = new
            {
                type = "string",
                description = "Optional art style filter (e.g. 'anime', 'watercolor')"
            },
            categorySlug = new
            {
                type = "string",
                description = "Optional artwork category slug"
            },
            creatorName = new
            {
                type = "string",
                description = "Optional creator display name filter"
            },
            licenseType = new
            {
                type = "string",
                @enum = new[] { "Personal", "Commercial" },
                description = "Optional license type filter"
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
            excludeAiGenerated = new
            {
                type = "boolean",
                description = "If true, exclude artworks marked as AI-generated"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "popular", "newest", "views", "price_asc", "price_desc" },
                description = "Optional sort order (defaults to popular)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var style = args.TryGetProperty("style", out var st) && st.ValueKind == JsonValueKind.String
            ? st.GetString()?.Trim().ToLower()
            : null;

        var categorySlug = args.TryGetProperty("categorySlug", out var cat) && cat.ValueKind == JsonValueKind.String
            ? cat.GetString()?.Trim().ToLower()
            : null;

        var creatorName = args.TryGetProperty("creatorName", out var cn) && cn.ValueKind == JsonValueKind.String
            ? cn.GetString()?.Trim().ToLower()
            : null;

        var licenseType = args.TryGetProperty("licenseType", out var lt) && lt.ValueKind == JsonValueKind.String
            ? lt.GetString()?.Trim().ToLower()
            : null;

        decimal? minPrice = args.TryGetProperty("minPrice", out var minP) && minP.ValueKind == JsonValueKind.Number && minP.TryGetDecimal(out var parsedMinP)
            ? parsedMinP
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var maxP) && maxP.ValueKind == JsonValueKind.Number && maxP.TryGetDecimal(out var parsedMaxP)
            ? parsedMaxP
            : null;

        var excludeAi = args.TryGetProperty("excludeAiGenerated", out var ai) && ai.ValueKind == JsonValueKind.True;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "popular";

        var query = db.Artworks
            .AsNoTracking()
            .Where(a => !a.IsDeleted
                && a.ModerationStatus == "Approved"
                && a.CreatorProfile != null
                && a.CreatorProfile.IsApproved
                && !a.CreatorProfile.IsDeleted);

        if (excludeAi)
        {
            query = query.Where(a => !a.IsAiGenerated);
        }

        if (!string.IsNullOrEmpty(style))
        {
            query = query.Where(a => a.Style != null && a.Style.ToLower().Contains(style));
        }

        if (!string.IsNullOrEmpty(categorySlug))
        {
            query = query.Where(a => a.Category != null && a.Category.Slug.ToLower() == categorySlug);
        }

        if (!string.IsNullOrEmpty(creatorName))
        {
            query = query.Where(a => a.CreatorProfile!.DisplayName.ToLower().Contains(creatorName));
        }

        if (!string.IsNullOrEmpty(licenseType))
        {
            query = query.Where(a => a.LicenseType.ToLower() == licenseType);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(a => a.StartingPrice.HasValue && a.StartingPrice.Value >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(a => a.StartingPrice.HasValue && a.StartingPrice.Value <= maxPrice.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(a =>
                a.Title.ToLower().Contains(keyword)
                || (a.Description != null && a.Description.ToLower().Contains(keyword))
                || a.ArtworkTags.Any(at => at.Tag != null && at.Tag.Name.ToLower().Contains(keyword)));
        }

        query = sortBy switch
        {
            "newest" => query.OrderByDescending(a => a.CreatedAt),
            "views" => query.OrderByDescending(a => a.ViewCount).ThenByDescending(a => a.LikeCount),
            "price_asc" => query.OrderBy(a => a.StartingPrice ?? decimal.MaxValue),
            "price_desc" => query.OrderByDescending(a => a.StartingPrice ?? -1m),
            _ => query.OrderByDescending(a => a.LikeCount).ThenByDescending(a => a.ViewCount)
        };

        return await query
            .Take(10)
            .Select(a => new
            {
                id = a.Id,
                a.Title,
                a.Description,
                a.Style,
                category = a.Category != null ? a.Category.Name : null,
                tags = a.ArtworkTags
                    .Where(at => at.Tag != null && !at.Tag.IsDeleted)
                    .Select(at => at.Tag!.Name)
                    .ToList(),
                a.LikeCount,
                a.ViewCount,
                a.LicenseType,
                a.StartingPrice,
                a.IsAiGenerated,
                previewUrl = a.WatermarkedUrl ?? a.ThumbnailUrl,
                creatorDisplayName = a.CreatorProfile!.DisplayName
            })
            .ToListAsync(cancellationToken);
    }
}
