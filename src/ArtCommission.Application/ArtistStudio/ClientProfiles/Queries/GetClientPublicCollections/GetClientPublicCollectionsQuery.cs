using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientPublicCollections;

public record GetClientPublicCollectionsQuery(Guid ClientId) : IRequest<IReadOnlyList<PersonalCollectionDto>>;

public class GetClientPublicCollectionsQueryHandler : IRequestHandler<GetClientPublicCollectionsQuery, IReadOnlyList<PersonalCollectionDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClientPublicCollectionsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PersonalCollectionDto>> Handle(GetClientPublicCollectionsQuery request, CancellationToken cancellationToken)
    {
        return await _db.PersonalCollections.AsNoTracking()
            .Where(x => x.OwnerUserId == request.ClientId && x.IsPublic && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PersonalCollectionDto(x.Id, x.Name, x.IsPublic, x.CollectionArtworks.Count, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
