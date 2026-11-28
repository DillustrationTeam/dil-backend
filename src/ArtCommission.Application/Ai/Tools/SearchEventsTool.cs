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
        "Searches or lists public platform art events (contests) by optional keyword, status, featured flag, date window (fromDate/toDate), and sort order.";

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
            },
            fromDate = new
            {
                type = "string",
                description = "Optional ISO 8601 start of date window (e.g. '2026-12-01' or '2026-12-01T00:00:00Z')"
            },
            toDate = new
            {
                type = "string",
                description = "Optional ISO 8601 end of date window (e.g. '2026-12-31')"
            },
            sortBy = new
            {
                type = "string",
                @enum = new[] { "featured", "soonest", "deadline" },
                description = "Optional sort order (defaults to featured)"
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

        DateTimeOffset? fromDate = args.TryGetProperty("fromDate", out var fd)
            && fd.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(fd.GetString(), out var parsedFrom)
                ? parsedFrom
                : null;

        DateTimeOffset? toDate = args.TryGetProperty("toDate", out var td)
            && td.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(td.GetString(), out var parsedTo)
                ? parsedTo
                : null;

        var sortBy = args.TryGetProperty("sortBy", out var sb) && sb.ValueKind == JsonValueKind.String
            ? sb.GetString()?.Trim().ToLowerInvariant()
            : "featured";

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

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.ResultAnnouncementAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.SubmissionStartAt <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(e =>
                e.Title.ToLower().Contains(keyword)
                || e.Description.ToLower().Contains(keyword)
                || (e.Prize != null && e.Prize.ToLower().Contains(keyword))
                || (e.Rules != null && e.Rules.ToLower().Contains(keyword)));
        }

        query = sortBy switch
        {
            "soonest" => query.OrderBy(e => e.SubmissionStartAt),
            "deadline" => query.OrderBy(e => e.SubmissionEndAt),
            _ => query.OrderByDescending(e => e.IsFeatured).ThenBy(e => e.SubmissionStartAt)
        };

        return await query
            .Take(10)
            .Select(e => new
            {
                id = e.Id,
                url = $"/events/{e.Id}",
                e.Title,
                e.Description,
                e.Prize,
                status = e.Status.ToString(),
                e.IsFeatured,
                e.MaxVote,
                submissionStart = e.SubmissionStartAt,
                submissionEnd = e.SubmissionEndAt,
                judgingStart = e.JudgingStartAt,
                judgingEnd = e.JudgingEndAt,
                votingStart = e.VotingStartAt,
                votingEnd = e.VotingEndAt,
                resultAnnouncementAt = e.ResultAnnouncementAt
            })
            .ToListAsync(cancellationToken);
    }
}
