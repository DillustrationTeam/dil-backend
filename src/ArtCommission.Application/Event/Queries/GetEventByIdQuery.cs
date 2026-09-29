using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetEventByIdQuery(
    Guid EventId
) : IRequest<EventDetailDto?>;

public class GetEventByIdQueryHandler 
    : IRequestHandler<GetEventByIdQuery, EventDetailDto?>
{
    private readonly IApplicationDbContext _db;

    public GetEventByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<EventDetailDto?> Handle(
        GetEventByIdQuery request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        return await _db.PlatformEvents
            .AsNoTracking()
            .Where(e => e.Id == request.EventId && !e.IsDeleted)
            .Select(e => new EventDetailDto(
                e.Id,
                e.Title,
                e.BannerUrl,
                e.Description,
                e.Rules,
                e.Prize,
                e.Status.ToString(),
                e.StartAt,
                e.EndsAt,
                e.CreatedByAdminId,
                e.CreatedByAdmin != null ? e.CreatedByAdmin.FullName : null,
                null,
                e.Submissions.Count,
                e.Submissions.Sum(s => (int?)s.VoteCount) ?? 0,
                e.CreatedAt,
                e.UpdatedAt,
                e.Status == EventStatus.Open && e.StartAt <= now && e.EndsAt >= now
            ))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
