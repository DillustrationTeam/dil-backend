using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchArtworkCategoriesTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_artwork_categories";

    public string Description =>
        "Lists or searches active artwork categories and public non-AI tags.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching category name, description, or tag name"
            },
            includeTags = new
            {
                type = "boolean",
                description = "Whether to include matching public tags (default true)"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var includeTags = !args.TryGetProperty("includeTags", out var inc) || inc.ValueKind != JsonValueKind.False;

        var catQuery = db.ArtworkCategories
            .AsNoTracking()
            .Where(c => c.IsActive && !c.IsDeleted);

        if (!string.IsNullOrEmpty(keyword))
        {
            catQuery = catQuery.Where(c =>
                c.Name.ToLower().Contains(keyword)
                || c.Slug.ToLower().Contains(keyword)
                || (c.Description != null && c.Description.ToLower().Contains(keyword)));
        }

        var categories = await catQuery
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Name,
                c.Slug,
                c.Description
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
                .OrderBy(t => t.Name)
                .Take(30)
                .Select(t => t.Name)
                .ToListAsync(cancellationToken);
        }

        return new { categories, tags };
    }
}
