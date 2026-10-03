using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetAvailableCreators;

public record AvailableCreatorDto(Guid Id, string DisplayName, string? Headline, string? Specialties, decimal RatingAverage, int RatingCount, int FollowerCount, int AvailableSlots);
public record GetAvailableCreatorsQuery : IRequest<List<AvailableCreatorDto>>;

public class GetAvailableCreatorsQueryHandler : IRequestHandler<GetAvailableCreatorsQuery, List<AvailableCreatorDto>>
{
    private readonly IApplicationDbContext _db;
    public GetAvailableCreatorsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<AvailableCreatorDto>> Handle(GetAvailableCreatorsQuery request, CancellationToken cancellationToken)
    {
        var ratings = await CreatorRatingStats.LoadAsync(_db, cancellationToken);
        var creators = await _db.CreatorProfiles.AsNoTracking()
            .Where(profile => !profile.IsDeleted && profile.IsAcceptingOrders)
            .OrderBy(profile => profile.DisplayName)
            .Select(profile => new AvailableCreatorDto(profile.Id, profile.DisplayName, profile.Headline, profile.Specialties, profile.RatingAverage, profile.RatingCount, profile.FollowerCount, profile.AvailableSlots))
            .ToListAsync(cancellationToken);
        return creators.Select(c => c with { RatingAverage = ratings.GetValueOrDefault(c.Id).Average, RatingCount = ratings.GetValueOrDefault(c.Id).Count }).ToList();
    }
}
