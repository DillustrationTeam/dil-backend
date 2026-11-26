using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchEventsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_events";

    public string Description =>
        "Searches or lists public platform art events (contests) by optional keyword, status, or featured flag.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching event title, description, prize, or rules"
            },
            status = new
            {
                type = "string",
                @enum = new[] { "upcoming", "open", "judging", "ended", "any" },
                description = "Optional event status filter (defaults to any non-draft)"
            },
            featuredOnly = new
            {
                type = "boolean",
                description = "If true, only return featured events"
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

        var featuredOnly = args.TryGetProperty("featuredOnly", out var feat) && feat.ValueKind == JsonValueKind.True;

        var query = db.PlatformEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status != EventStatus.Draft);

        query = status switch
        {
            "upcoming" => query.Where(e => e.Status == EventStatus.Upcoming),
            "open" => query.Where(e => e.Status == EventStatus.Open),
            "judging" => query.Where(e => e.Status == EventStatus.Judging),
            "ended" => query.Where(e => e.Status == EventStatus.Ended),
            _ => query
        };

        if (featuredOnly)
        {
            query = query.Where(e => e.IsFeatured);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(e =>
                e.Title.ToLower().Contains(keyword)
                || e.Description.ToLower().Contains(keyword)
                || (e.Prize != null && e.Prize.ToLower().Contains(keyword))
                || (e.Rules != null && e.Rules.ToLower().Contains(keyword)));
        }

        return await query
            .OrderByDescending(e => e.IsFeatured)
            .ThenBy(e => e.SubmissionStartAt)
            .Take(10)
            .Select(e => new
            {
                id = e.Id,
                e.Title,
                e.Description,
                e.Prize,
                status = e.Status.ToString(),
                e.IsFeatured,
                submissionStart = e.SubmissionStartAt,
                submissionEnd = e.SubmissionEndAt,
                votingEnd = e.VotingEndAt,
                resultAnnouncementAt = e.ResultAnnouncementAt
            })
            .ToListAsync(cancellationToken);
    }
}
