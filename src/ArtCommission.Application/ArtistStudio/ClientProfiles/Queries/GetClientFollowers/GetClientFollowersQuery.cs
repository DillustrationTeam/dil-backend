using ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientFollowing;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientFollowers;

public record GetClientFollowersQuery(Guid ClientId) : IRequest<IReadOnlyList<FollowerUserDto>>;

public record FollowerUserDto(Guid UserId, string DisplayName, string? AvatarUrl, IReadOnlyList<string> Roles);

public class GetClientFollowersQueryHandler : IRequestHandler<GetClientFollowersQuery, IReadOnlyList<FollowerUserDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClientFollowersQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FollowerUserDto>> Handle(GetClientFollowersQuery request, CancellationToken cancellationToken)
    {
        var creatorProfileId = await _db.CreatorProfiles.AsNoTracking()
            .Where(x => x.UserId == request.ClientId && !x.IsDeleted)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (creatorProfileId is null)
        {
            return Array.Empty<FollowerUserDto>();
        }

        var followers = await _db.Follows.AsNoTracking()
            .Where(x => x.CreatorProfileId == creatorProfileId)
            .Select(x => x.Follower!)
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.FullName, x.AvatarUrl })
            .ToListAsync(cancellationToken);

        if (followers.Count == 0)
        {
            return Array.Empty<FollowerUserDto>();
        }

        var userIds = followers.Select(f => f.Id).ToList();
        var rolesByUser = await GetClientFollowingQueryHandler.GetRolesByUserAsync(_db, userIds, cancellationToken);

        return followers
            .Select(f => new FollowerUserDto(
                f.Id,
                f.FullName,
                f.AvatarUrl,
                rolesByUser.TryGetValue(f.Id, out var roles) ? roles : Array.Empty<string>()))
            .ToList();
    }
}
