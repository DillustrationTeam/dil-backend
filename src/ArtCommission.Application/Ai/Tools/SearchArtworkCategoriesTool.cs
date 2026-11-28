using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchArtworkCategoriesTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_artwork_categories";

    public string Description =>
        "Lists or searches active artwork categories (with approved public artwork counts) and public non-AI tags.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching category name, slug, description, or tag name"
            },
            includeTags = new
            {
                type = "boolean",
                description = "Whether to include matching public tags (default true)"
            },
            nonEmptyOnly = new
            {
                type = "boolean",
                description = "If true, only return categories that have at least one approved public artwork"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "display_order", "artwork_count", "name" },
                description = "Optional sort order for categories (defaults to display_order)"
            },
            tagLimit = new
            {
                type = "integer",
                description = "Optional maximum number of tags to return (1-50, default 30)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var includeTags = !args.TryGetProperty("includeTags", out var inc) || inc.ValueKind != JsonValueKind.False;
        var nonEmptyOnly = args.TryGetProperty("nonEmptyOnly", out var ne) && ne.ValueKind == JsonValueKind.True;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "display_order";

        var tagLimit = args.TryGetProperty("tagLimit", out var tl) && tl.ValueKind == JsonValueKind.Number && tl.TryGetInt32(out var parsedLimit)
            ? Math.Clamp(parsedLimit, 1, 50)
            : 30;

        var catQuery = db.ArtworkCategories
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted)
            .Select(c => new
            {
                c.Name,
                c.Slug,
                c.Description,
                c.DisplayOrder,
                approvedArtworkCount = c.Artworks.Count(a =>
                    !a.IsDeleted
                    && a.ModerationStatus == "Approved"
                    && a.CreatorProfile != null
                    && a.CreatorProfile.IsApproved
                    && !a.CreatorProfile.IsDeleted)
            });

        if (nonEmptyOnly)
        {
            catQuery = catQuery.Where(c => c.approvedArtworkCount > 0);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            catQuery = catQuery.Where(c =>
                c.Name.ToLower().Contains(keyword)
                || c.Slug.ToLower().Contains(keyword)
                || (c.Description != null && c.Description.ToLower().Contains(keyword)));
        }

        catQuery = sortBy switch
        {
            "artwork_count" => catQuery.OrderByDescending(c => c.approvedArtworkCount).ThenBy(c => c.DisplayOrder),
            "name" => catQuery.OrderBy(c => c.Name),
            _ => catQuery.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
        };

        var categories = await catQuery
            .Select(c => new
            {
                c.Name,
                c.Slug,
                url = $"/explore?category={c.Slug}",
                c.Description,
                c.approvedArtworkCount
            })
            .ToListAsync(cancellationToken);

        object? tags = null;
        if (includeTags)
        {
            var tagQuery = db.Tags
                .AsNoTracking()
                .Where(t => !t.IsDeleted && !t.IsAiGenerated);

            if (!string.IsNullOrEmpty(keyword))
            {
                tagQuery = tagQuery.Where(t => t.Name.ToLower().Contains(keyword));
            }

            tags = await tagQuery
                .Select(t => new
                {
                    t.Name,
                    approvedArtworkCount = t.ArtworkTags.Count(at =>
                        at.Artwork != null
                        && !at.Artwork.IsDeleted
                        && at.Artwork.ModerationStatus == "Approved"
                        && at.Artwork.CreatorProfile != null
                        && at.Artwork.CreatorProfile.IsApproved
                        && !at.Artwork.CreatorProfile.IsDeleted)
                })
                .OrderByDescending(t => t.approvedArtworkCount)
                .ThenBy(t => t.Name)
                .Take(tagLimit)
                .ToListAsync(cancellationToken);
        }

        return new { categories, tags };
    }
}
