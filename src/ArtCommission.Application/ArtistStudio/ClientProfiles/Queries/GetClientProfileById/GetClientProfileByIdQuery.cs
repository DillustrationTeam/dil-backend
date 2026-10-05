using ArtCommission.Application.ArtistStudio.ClientProfiles.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientProfileById;

public record GetClientProfileByIdQuery(Guid UserId) : IRequest<(bool Success, ClientProfileDto? Data, string[] Errors)>;

public class GetClientProfileByIdQueryHandler : IRequestHandler<GetClientProfileByIdQuery, (bool Success, ClientProfileDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetClientProfileByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ClientProfileDto? Data, string[] Errors)> Handle(GetClientProfileByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.UserId && !x.IsDeleted, cancellationToken);

        if (user is null)
        {
            return (false, null, new[] { "Client not found." });
        }

        var profile = await _db.ClientProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        return (true, new ClientProfileDto(
            user.Id,
            user.FullName,
            user.AvatarUrl,
            user.CoverUrl,
            user.Bio,
            user.IsVerified,
            user.CreatedAt,
            profile?.Username,
            profile?.InterestTags ?? new List<string>(),
            profile?.Country,
            profile?.Timezone,
            profile?.PreferredLanguages ?? new List<string>(),
            profile?.UsernameChangedAt
        ), Array.Empty<string>());
    }
}
