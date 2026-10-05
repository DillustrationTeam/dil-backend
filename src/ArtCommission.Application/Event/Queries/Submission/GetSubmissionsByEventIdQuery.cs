using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetSubmissionsByEventIdQuery(
    Guid EventId,
    string? Title = null,
    string? SubmitterName = null,
    string? Search = null,
    bool? AiScanPassed = null,
    string? SortBy = "date",
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<EventSubmissionDto> Items, int TotalCount)>;

public class GetSubmissionsByEventIdQueryHandler 
    : IRequestHandler<GetSubmissionsByEventIdQuery, (IReadOnlyList<EventSubmissionDto> Items, int TotalCount)>
{
    private const int MaxPageSize = 50;
    private readonly IApplicationDbContext _db;

    public GetSubmissionsByEventIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<EventSubmissionDto> Items, int TotalCount)> Handle(
        GetSubmissionsByEventIdQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, MaxPageSize);

        var query = _db.EventSubmissions
            .AsNoTracking()
            .Where(s => s.EventId == request.EventId && (s.Event == null || !s.Event.IsDeleted));

        if (request.AiScanPassed.HasValue)
        {
            query = query.Where(s => s.AiScanPassed == request.AiScanPassed.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            var title = request.Title.Trim();
            query = query.Where(s => s.Title.Contains(title));
        }

        if (!string.IsNullOrWhiteSpace(request.SubmitterName))
        {
            var submitter = request.SubmitterName.Trim();
            query = query.Where(s => s.Submitter != null && (
                (s.Submitter.FullName != null && s.Submitter.FullName.Contains(submitter)) ||
                (s.Submitter.UserName != null && s.Submitter.UserName.Contains(submitter))
            ));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var keyword = request.Search.Trim();
            query = query.Where(s =>
                s.Title.Contains(keyword) ||
                (s.Description != null && s.Description.Contains(keyword)) ||
                (s.Submitter != null && (
                    (s.Submitter.FullName != null && s.Submitter.FullName.Contains(keyword)) ||
                    (s.Submitter.UserName != null && s.Submitter.UserName.Contains(keyword))
                )) ||
                (s.Artwork != null && s.Artwork.Title != null && s.Artwork.Title.Contains(keyword))
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortField = request.SortBy?.Trim().ToLowerInvariant() ?? "date";
        if (sortField.EndsWith("_desc"))
        {
            sortField = sortField[..^5];
        }
        else if (sortField.EndsWith("_asc"))
        {
            sortField = sortField[..^4];
        }

        // Quy tắc: thứ tự sort luôn là descending
        query = sortField switch
        {
            "title" => query.OrderByDescending(s => s.Title).ThenByDescending(s => s.SubmittedAt),
            "vote" or "votes" or "votecount" or "popularity" => query.OrderByDescending(s => s.VoteCount).ThenByDescending(s => s.SubmittedAt),
            "score" => query.OrderByDescending(s => s.Score).ThenByDescending(s => s.VoteCount).ThenByDescending(s => s.SubmittedAt),
            _ => query.OrderByDescending(s => s.SubmittedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new EventSubmissionDto
            {
                Id = s.Id,
                EventId = s.EventId,
                EventTitle = s.Event != null ? s.Event.Title : null,
                SubmitterId = s.SubmitterId,
                SubmitterName = s.Submitter != null ? s.Submitter.FullName : null,
                SubmitterUsername = s.Submitter != null ? s.Submitter.UserName : null,
                SubmitterAvatarUrl = null,
                ArtworkId = s.ArtworkId,
                Title = s.Title,
                Description = s.Description,
                ArtworkTitle = s.Artwork != null ? s.Artwork.Title : null,
                ArtworkImageUrl = s.Artwork != null ? s.Artwork.ImageUrl : null,
                ArtworkThumbnailUrl = s.Artwork != null ? s.Artwork.ThumbnailUrl : null,
                AiScanPassed = s.AiScanPassed,
                VoteCount = s.VoteCount,
                Score = s.Score,
                AdminNote = s.AdminNote,
                SubmittedAt = s.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

