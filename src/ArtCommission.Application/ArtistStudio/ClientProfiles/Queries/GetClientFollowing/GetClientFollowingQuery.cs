using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientFollowing;

public record GetClientFollowingQuery(Guid ClientId) : IRequest<IReadOnlyList<FollowedCreatorDto>>;

public record FollowedCreatorDto(Guid CreatorProfileId, Guid CreatorUserId, string DisplayName, string? AvatarUrl, IReadOnlyList<string> Roles);

public class GetClientFollowingQueryHandler : IRequestHandler<GetClientFollowingQuery, IReadOnlyList<FollowedCreatorDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClientFollowingQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FollowedCreatorDto>> Handle(GetClientFollowingQuery request, CancellationToken cancellationToken)
    {
        var creators = await _db.Follows.AsNoTracking()
            .Where(x => x.FollowerUserId == request.ClientId)
            .Select(x => x.CreatorProfile!)
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.UserId, x.DisplayName, AvatarUrl = x.User!.AvatarUrl })
            .ToListAsync(cancellationToken);

        if (creators.Count == 0)
        {
            return Array.Empty<FollowedCreatorDto>();
        }

        var userIds = creators.Select(c => c.UserId).ToList();
        var rolesByUser = await GetRolesByUserAsync(_db, userIds, cancellationToken);

        return creators
            .Select(c => new FollowedCreatorDto(
                c.Id,
                c.UserId,
                c.DisplayName,
                c.AvatarUrl,
                rolesByUser.TryGetValue(c.UserId, out var roles) ? roles : Array.Empty<string>()))
            .ToList();
    }

    internal static async Task<Dictionary<Guid, List<string>>> GetRolesByUserAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var userRolesList = await (
            from ur in db.Set<IdentityUserRole<Guid>>()
            join r in db.Set<IdentityRole<Guid>>() on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name }
        ).ToListAsync(cancellationToken);

        return userRolesList
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleName ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToList());
    }
}
