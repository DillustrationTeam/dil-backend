using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetCreatorProfileById;

public record GetCreatorProfileByIdQuery(Guid ProfileId) : IRequest<(bool Success, CreatorProfileDto? Data, string[] Errors)>;

public class GetCreatorProfileByIdQueryHandler : IRequestHandler<GetCreatorProfileByIdQuery, (bool Success, CreatorProfileDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetCreatorProfileByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, CreatorProfileDto? Data, string[] Errors)> Handle(GetCreatorProfileByIdQuery request, CancellationToken cancellationToken)
    {
        var profile = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return (false, null, new[] { "Creator profile not found." });
        }

        return (true, new CreatorProfileDto(
            profile.Id,
            profile.UserId,
            profile.DisplayName,
            profile.Headline,
            profile.Bio,
            profile.Specialties,
            profile.Location,
            profile.WebsiteUrl,
            profile.BannerUrl,
            profile.IsAcceptingOrders,
            profile.IsApproved,
            profile.RatingAverage,
            profile.RatingCount,
            profile.FollowerCount,
            profile.CreatedAt
        ), Array.Empty<string>());
    }
}
