using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetEventLeaderboardQuery(
    Guid EventId,
    string? SortBy = "votes",
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<EventLeaderboardItemDto> Items, int TotalCount, string? EventTitle)>;

public class GetEventLeaderboardQueryHandler
    : IRequestHandler<GetEventLeaderboardQuery, (IReadOnlyList<EventLeaderboardItemDto> Items, int TotalCount, string? EventTitle)>
{
    private readonly IApplicationDbContext _db;

    public GetEventLeaderboardQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<EventLeaderboardItemDto> Items, int TotalCount, string? EventTitle)> Handle(
        GetEventLeaderboardQuery request,
        CancellationToken cancellationToken)
    {
        var platformEvent = await _db.PlatformEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

        if (platformEvent == null)
        {
            return (Array.Empty<EventLeaderboardItemDto>(), 0, null);
        }

        var query = _db.EventSubmissions
            .AsNoTracking()
            .Include(s => s.Submitter)
            .Include(s => s.Artwork)
            .Where(s => s.EventId == request.EventId);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var sortBy = request.SortBy?.Trim().ToLowerInvariant() ?? "votes";

        query = sortBy switch
        {
            "score" => query
                .OrderByDescending(s => s.Score.HasValue)
                .ThenByDescending(s => s.Score)
                .ThenByDescending(s => s.VoteCount)
                .ThenBy(s => s.SubmittedAt),
            _ => query
                .OrderByDescending(s => s.VoteCount)
                .ThenByDescending(s => s.Score.HasValue)
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.SubmittedAt)
        };

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.SubmitterId,
                SubmitterName = s.Submitter != null ? s.Submitter.FullName : null,
                SubmitterUsername = s.Submitter != null ? s.Submitter.UserName : null,
                s.ArtworkId,
                ArtworkTitle = s.Artwork != null ? s.Artwork.Title : null,
                ArtworkThumbnailUrl = s.Artwork != null ? s.Artwork.ThumbnailUrl : null,
                ArtworkImageUrl = s.Artwork != null ? (s.Artwork.WatermarkedUrl ?? s.Artwork.ImageUrl) : null,
                s.Score,
                s.VoteCount,
                s.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        var baseRank = (page - 1) * pageSize;
        var items = rawItems.Select((s, index) => new EventLeaderboardItemDto
        {
            Rank = baseRank + index + 1,
            SubmissionId = s.Id,
            SubmissionTitle = s.Title,
            SubmitterId = s.SubmitterId,
            SubmitterName = s.SubmitterName,
            SubmitterUsername = s.SubmitterUsername,
            SubmitterAvatarUrl = null,
            ArtworkId = s.ArtworkId,
            ArtworkTitle = s.ArtworkTitle,
            ArtworkThumbnailUrl = s.ArtworkThumbnailUrl,
            ArtworkImageUrl = s.ArtworkImageUrl,
            Score = s.Score,
            VoteCount = s.VoteCount,
            SubmittedAt = s.SubmittedAt
        }).ToList();

        return (items, totalCount, platformEvent.Title);
    }
}
