using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientFollowing;

public record GetClientFollowingQuery(Guid ClientId) : IRequest<IReadOnlyList<FollowedCreatorDto>>;

public record FollowedCreatorDto(Guid CreatorProfileId, Guid CreatorUserId, string DisplayName, string? AvatarUrl);

public class GetClientFollowingQueryHandler : IRequestHandler<GetClientFollowingQuery, IReadOnlyList<FollowedCreatorDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClientFollowingQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FollowedCreatorDto>> Handle(GetClientFollowingQuery request, CancellationToken cancellationToken)
    {
        return await _db.Follows.AsNoTracking()
            .Where(x => x.FollowerUserId == request.ClientId)
            .Select(x => x.CreatorProfile!)
            .Where(x => !x.IsDeleted)
            .Select(x => new FollowedCreatorDto(x.Id, x.UserId, x.DisplayName, x.User!.AvatarUrl))
            .ToListAsync(cancellationToken);
    }
}
