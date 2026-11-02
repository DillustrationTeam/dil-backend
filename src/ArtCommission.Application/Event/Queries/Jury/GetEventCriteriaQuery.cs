using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using ArtCommission.Domain.Entities.Event;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetEventCriteriaQuery(Guid EventId) : IRequest<IReadOnlyList<EventCriteriaDto>>;

public class GetEventCriteriaQueryHandler : IRequestHandler<GetEventCriteriaQuery, IReadOnlyList<EventCriteriaDto>>
{
    private readonly IApplicationDbContext _db;

    public GetEventCriteriaQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EventCriteriaDto>> Handle(
        GetEventCriteriaQuery request,
        CancellationToken cancellationToken)
    {
        var criteria = await _db.EventCriteria
            .Where(c => c.EventId == request.EventId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);

        if (criteria.Count == 0)
        {
            var eventExists = await _db.PlatformEvents
                .AnyAsync(e => e.Id == request.EventId && !e.IsDeleted, cancellationToken);

            if (eventExists)
            {
                var now = DateTimeOffset.UtcNow;
                var defaultCriteria = new List<EventCriteria>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = request.EventId,
                        Name = "Sáng tạo và ý tưởng",
                        Description = "Độ độc đáo, tính nguyên bản và thông điệp truyền tải của tác phẩm",
                        MaxScore = 40.0m,
                        Weight = 0.40m,
                        DisplayOrder = 1,
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = request.EventId,
                        Name = "Kỹ thuật thể hiện",
                        Description = "Bố cục, xử lý ánh sáng, màu sắc, chi tiết nét vẽ và độ hoàn thiện",
                        MaxScore = 35.0m,
                        Weight = 0.35m,
                        DisplayOrder = 2,
                        CreatedAt = now
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        EventId = request.EventId,
                        Name = "Bám sát chủ đề",
                        Description = "Sự phù hợp và thể hiện đúng tinh thần thể lệ sự kiện đặt ra",
                        MaxScore = 25.0m,
                        Weight = 0.25m,
                        DisplayOrder = 3,
                        CreatedAt = now
                    }
                };

                _db.EventCriteria.AddRange(defaultCriteria);
                await _db.SaveChangesAsync(cancellationToken);
                criteria = defaultCriteria;
            }
        }

        return criteria.Select(c => new EventCriteriaDto
        {
            Id = c.Id,
            EventId = c.EventId,
            Name = c.Name,
            Description = c.Description,
            MaxScore = c.MaxScore,
            Weight = c.Weight,
            DisplayOrder = c.DisplayOrder
        }).ToList();
    }
}
