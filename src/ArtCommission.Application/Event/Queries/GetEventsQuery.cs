using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetEventsQuery(
    string? Title = null,
    string? Search = null,
    EventStatus? Status = null,
    int? MinSubmissions = null,
    int? MaxSubmissions = null,
    string? SortBy = "latest",
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<EventDto> Items, int TotalCount)>;

public class GetEventsQueryHandler 
    : IRequestHandler<GetEventsQuery, (IReadOnlyList<EventDto> Items, int TotalCount)>
{
    private const int MaxPageSize = 50;
    private readonly IApplicationDbContext _db;

    public GetEventsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<EventDto> Items, int TotalCount)> Handle(
        GetEventsQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, MaxPageSize);
        var now = DateTimeOffset.UtcNow;

        var query = _db.PlatformEvents
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (request.Status.HasValue)
        {
            query = query.Where(e => e.Status == request.Status.Value);
        }

        var keyword = !string.IsNullOrWhiteSpace(request.Title) 
            ? request.Title.Trim() 
            : request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(e => e.Title.Contains(keyword));
        }

        if (request.MinSubmissions.HasValue)
        {
            query = query.Where(e => e.Submissions.Count >= request.MinSubmissions.Value);
        }

        if (request.MaxSubmissions.HasValue)
        {
            query = query.Where(e => e.Submissions.Count <= request.MaxSubmissions.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy?.Trim().ToLowerInvariant() switch
        {
            "a-z" => query.OrderBy(e => e.Title),
            "popularity" => query.OrderByDescending(e => e.Submissions.Count).ThenByDescending(e => e.CreatedAt),
            "end date" => query.OrderByDescending(e => e.EndsAt),
            "latest" => query.OrderByDescending(e => e.CreatedAt),
            _ => query.OrderByDescending(e => e.CreatedAt)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EventDto(
                e.Id,
                e.Title,
                e.BannerUrl,
                e.Description,
                e.Prize,
                e.Status.ToString(),
                e.StartAt,
                e.EndsAt,
                e.CreatedByAdminId,
                e.CreatedByAdmin != null ? e.CreatedByAdmin.FullName : null,
                e.Submissions.Count,
                e.CreatedAt,
                e.UpdatedAt,
                e.Status == EventStatus.Open && e.StartAt <= now && e.EndsAt >= now
            ))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
