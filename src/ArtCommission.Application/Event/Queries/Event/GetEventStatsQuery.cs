using System.Globalization;
using System.Text.RegularExpressions;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetEventStatsQuery : IRequest<EventStatsDto>;

public class GetEventStatsQueryHandler : IRequestHandler<GetEventStatsQuery, EventStatsDto>
{
    private readonly IApplicationDbContext _db;

    public GetEventStatsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EventStatsDto> Handle(GetEventStatsQuery request, CancellationToken cancellationToken)
    {
        var events = await _db.PlatformEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted)
            .Select(e => new
            {
                e.Status,
                e.Prize,
                SubmissionsCount = e.Submissions.Count
            })
            .ToListAsync(cancellationToken);

        var totalEventsCount = events.Count;
        var openEventsCount = events.Count(e => e.Status == EventStatus.Open);
        var totalSubmissionsCount = events.Sum(e => e.SubmissionsCount);

        long totalPrizePool = 0;
        foreach (var ev in events)
        {
            if (!string.IsNullOrWhiteSpace(ev.Prize))
            {
                totalPrizePool += ParsePrizeAmount(ev.Prize);
            }
        }

        var formattedPrize = totalPrizePool > 0
            ? $"{totalPrizePool.ToString("#,##0", CultureInfo.InvariantCulture)}+ đ"
            : "0 đ";

        return new EventStatsDto
        {
            TotalPrizePool = totalPrizePool,
            FormattedTotalPrizePool = formattedPrize,
            OpenEventsCount = openEventsCount,
            TotalSubmissionsCount = totalSubmissionsCount,
            TotalEventsCount = totalEventsCount
        };
    }

    public static long ParsePrizeAmount(string prize)
    {
        if (string.IsNullOrWhiteSpace(prize))
            return 0;

        // Formatted numbers: e.g. 45,000,000 or 45.000.000
        var match = Regex.Match(prize, @"(\d{1,3}(?:[.,]\d{3})+)");
        if (match.Success)
        {
            var raw = match.Groups[1].Value.Replace(".", "").Replace(",", "");
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                return val;
            }
        }

        // Word-based numbers: e.g. 45 triệu, 45 tr, 45 million
        var trieuMatch = Regex.Match(prize, @"(\d+(?:[.,]\d+)?)\s*(?:triệu|trieu|tr|million|m\b)", RegexOptions.IgnoreCase);
        if (trieuMatch.Success)
        {
            var raw = trieuMatch.Groups[1].Value.Replace(",", ".");
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var mVal) && mVal > 0)
            {
                return (long)(mVal * 1_000_000m);
            }
        }

        // Plain 6+ digit numbers: e.g. 50000000
        var plainMatch = Regex.Match(prize, @"(\d{6,})");
        if (plainMatch.Success)
        {
            if (long.TryParse(plainMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) && val > 0)
            {
                return val;
            }
        }

        return 0;
    }
}
