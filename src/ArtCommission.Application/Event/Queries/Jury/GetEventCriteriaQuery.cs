using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
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
            .AsNoTracking()
            .Where(c => c.EventId == request.EventId && !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new EventCriteriaDto
            {
                Id = c.Id,
                EventId = c.EventId,
                Name = c.Name,
                Description = c.Description,
                MaxScore = c.MaxScore,
                Weight = c.Weight,
                DisplayOrder = c.DisplayOrder
            })
            .ToListAsync(cancellationToken);

        if (criteria.Count > 0)
        {
            return criteria;
        }

        // Default standard rubric if none configured in database for this event
        return new List<EventCriteriaDto>
        {
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                EventId = request.EventId,
                Name = "Sáng tạo và ý tưởng",
                Description = "Độ độc đáo, tính nguyên bản và thông điệp truyền tải của tác phẩm",
                MaxScore = 40.0m,
                Weight = 0.40m,
                DisplayOrder = 1
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                EventId = request.EventId,
                Name = "Kỹ thuật thể hiện",
                Description = "Bố cục, xử lý ánh sáng, màu sắc, chi tiết nét vẽ và độ hoàn thiện",
                MaxScore = 35.0m,
                Weight = 0.35m,
                DisplayOrder = 2
            },
            new()
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                EventId = request.EventId,
                Name = "Bám sát chủ đề",
                Description = "Sự phù hợp và thể hiện đúng tinh thần thể lệ sự kiện đặt ra",
                MaxScore = 25.0m,
                Weight = 0.25m,
                DisplayOrder = 3
            }
        };
    }
}
