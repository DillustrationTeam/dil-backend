using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchCreatorsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_creators";

    public string Description =>
        "Finds approved creators by optional keyword (display name, specialty, headline, or artwork style/tag), order availability, or AI-verified badge.";

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

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(p =>
                p.DisplayName.ToLower().Contains(keyword)
                || (p.Specialties != null && p.Specialties.ToLower().Contains(keyword))
                || (p.Headline != null && p.Headline.ToLower().Contains(keyword))
                || p.Artworks.Any(a =>
                    !a.IsDeleted
                    && a.ModerationStatus == "Approved"
                    && ((a.Style != null && a.Style.ToLower().Contains(keyword))
                        || a.ArtworkTags.Any(at => at.Tag != null && at.Tag.Name.ToLower().Contains(keyword)))));
        }

        return await query
            .OrderByDescending(p => p.RatingAverage)
            .ThenByDescending(p => p.FollowerCount)
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
                p.IsAcceptingOrders,
                p.AvailableSlots,
                p.IsAiVerified
            })
            .ToListAsync(cancellationToken);
    }
}
