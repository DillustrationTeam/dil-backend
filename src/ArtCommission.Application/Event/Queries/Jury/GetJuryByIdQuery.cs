using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Event.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Event.Queries;

public record GetJuryByIdQuery(
    Guid Id
) : IRequest<JuryDto?>;

public class GetJuryByIdQueryHandler
    : IRequestHandler<GetJuryByIdQuery, JuryDto?>
{
    private readonly IApplicationDbContext _db;

    public GetJuryByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<JuryDto?> Handle(
        GetJuryByIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _db.Juries
            .AsNoTracking()
            .Include(j => j.Event)
            .Include(j => j.Creator)
                .ThenInclude(c => c!.User)
            .Where(j => j.Id == request.Id && !j.IsDeleted)
            .Select(j => new JuryDto
            {
                Id = j.Id,
                EventId = j.EventId,
                EventTitle = j.Event != null ? j.Event.Title : null,
                CreatorId = j.CreatorId,
                CreatorName = j.Creator != null && j.Creator.User != null ? j.Creator.User.FullName : null,
                CreatorDisplayName = j.Creator != null ? j.Creator.DisplayName : null,
                CreatorAvatarUrl = null,
                CreatorBio = j.Creator != null ? j.Creator.Bio : null,
                IsHeadJury = j.IsHeadJury,
                CreatedAt = j.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
