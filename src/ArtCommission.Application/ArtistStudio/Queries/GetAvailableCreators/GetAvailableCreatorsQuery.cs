using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetAvailableCreators;

public record AvailableCreatorDto(Guid Id, string DisplayName, string? Headline);
public record GetAvailableCreatorsQuery : IRequest<List<AvailableCreatorDto>>;

public class GetAvailableCreatorsQueryHandler : IRequestHandler<GetAvailableCreatorsQuery, List<AvailableCreatorDto>>
{
    private readonly IApplicationDbContext _db;
    public GetAvailableCreatorsQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<List<AvailableCreatorDto>> Handle(GetAvailableCreatorsQuery request, CancellationToken cancellationToken) =>
        _db.CreatorProfiles.AsNoTracking()
            .Where(profile => !profile.IsDeleted && profile.IsAcceptingOrders)
            .OrderBy(profile => profile.DisplayName)
            .Select(profile => new AvailableCreatorDto(profile.Id, profile.DisplayName, profile.Headline))
            .ToListAsync(cancellationToken);
}
