using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchArtworksTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_artworks";

    public string Description =>
        "Searches approved public artworks by optional keyword, art style, category slug, or AI-generated filter.";

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
            excludeAiGenerated = new
            {
                type = "boolean",
                description = "If true, exclude artworks marked as AI-generated"
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

        var excludeAi = args.TryGetProperty("excludeAiGenerated", out var ai) && ai.ValueKind == JsonValueKind.True;

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

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(a =>
                a.Title.ToLower().Contains(keyword)
                || (a.Description != null && a.Description.ToLower().Contains(keyword))
                || a.ArtworkTags.Any(at => at.Tag != null && at.Tag.Name.ToLower().Contains(keyword)));
        }

        return await query
            .OrderByDescending(a => a.LikeCount)
            .ThenByDescending(a => a.ViewCount)
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
