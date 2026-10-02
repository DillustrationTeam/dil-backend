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
            .Select(e => new EventDetailDto
            {
                Id = e.Id,
                Title = e.Title,
                BannerUrl = e.BannerUrl,
                Description = e.Description,
                Rules = e.Rules,
                Prize = e.Prize,
                Status = e.Status.ToString(),
                StartAt = e.StartAt,
                EndsAt = e.EndsAt,
                CreatedByAdminId = e.CreatedByAdminId,
                CreatedByAdminName = e.CreatedByAdmin != null ? e.CreatedByAdmin.FullName : null,
                CreatedByAdminAvatarUrl = null,
                SubmissionCount = e.Submissions.Count,
                TotalVoteCount = e.Submissions.Sum(s => (int?)s.VoteCount) ?? 0,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                IsOpenForSubmission = e.Status == EventStatus.Open && e.StartAt <= now && e.EndsAt >= now
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
