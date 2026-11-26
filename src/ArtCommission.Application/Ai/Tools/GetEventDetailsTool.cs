using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class GetEventDetailsTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "get_event_details";

    public string Description =>
        "Gets rules, judging criteria, and (for ended events) top public submissions of a platform art event by eventId or titleKeyword.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            eventId = new
            {
                type = "string",
                description = "Optional GUID of the event"
            },
            titleKeyword = new
            {
                type = "string",
                description = "Optional keyword matching the event title"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        Guid? eventId = args.TryGetProperty("eventId", out var idProp)
            && idProp.ValueKind == JsonValueKind.String
            && Guid.TryParse(idProp.GetString(), out var parsedId)
                ? parsedId
                : null;

        var titleKeyword = args.TryGetProperty("titleKeyword", out var kwProp) && kwProp.ValueKind == JsonValueKind.String
            ? kwProp.GetString()?.Trim().ToLower()
            : null;

        if (eventId is null && string.IsNullOrEmpty(titleKeyword))
        {
            return new { error = "Either eventId or titleKeyword is required." };
        }

        var query = db.PlatformEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status != EventStatus.Draft);

        if (eventId.HasValue)
        {
            query = query.Where(e => e.Id == eventId.Value);
        }
        else
        {
            query = query.Where(e => e.Title.ToLower().Contains(titleKeyword!));
        }

        var ev = await query
            .OrderByDescending(e => e.IsFeatured)
            .ThenBy(e => e.SubmissionStartAt)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Description,
                e.Rules,
                e.Prize,
                e.Status,
                e.MaxVote,
                e.SubmissionStartAt,
                e.SubmissionEndAt,
                e.JudgingStartAt,
                e.JudgingEndAt,
                e.VotingStartAt,
                e.VotingEndAt,
                e.ResultAnnouncementAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (ev is null)
        {
            return new { found = false, message = "No matching public event was found." };
        }

        var criteria = await db.EventCriteria
            .AsNoTracking()
            .Where(c => c.EventId == ev.Id && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Name,
                c.Description,
                c.MaxScore,
                c.Weight
            })
            .ToListAsync(cancellationToken);

        object? topSubmissions = null;
        if (ev.Status == EventStatus.Ended)
        {
            topSubmissions = await db.EventSubmissions
                .AsNoTracking()
                .Where(s => s.EventId == ev.Id && !s.IsDisqualified)
                .OrderByDescending(s => s.Score ?? 0m)
                .ThenByDescending(s => s.VoteCount)
                .Take(10)
                .Select(s => new
                {
                    s.Title,
                    s.VoteCount,
                    s.Score
                })
                .ToListAsync(cancellationToken);
        }

        return new
        {
            found = true,
            @event = new
            {
                ev.Id,
                ev.Title,
                ev.Description,
                ev.Rules,
                ev.Prize,
                status = ev.Status.ToString(),
                ev.MaxVote,
                ev.SubmissionStartAt,
                ev.SubmissionEndAt,
                ev.JudgingStartAt,
                ev.JudgingEndAt,
                ev.VotingStartAt,
                ev.VotingEndAt,
                ev.ResultAnnouncementAt
            },
            criteria,
            topSubmissions
        };
    }
}
