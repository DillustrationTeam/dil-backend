using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.ClientProfiles.Queries.GetClientReviews;

public record GetClientReviewsQuery(Guid ClientId) : IRequest<IReadOnlyList<ClientReviewDto>>;

public record ClientReviewDto(Guid Id, Guid CreatorId, int Rating, string? Comment, DateTimeOffset CreatedAt);

public class GetClientReviewsQueryHandler : IRequestHandler<GetClientReviewsQuery, IReadOnlyList<ClientReviewDto>>
{
    private readonly IApplicationDbContext _db;

    public GetClientReviewsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ClientReviewDto>> Handle(GetClientReviewsQuery request, CancellationToken cancellationToken)
    {
        return await _db.ClientReviews.AsNoTracking()
            .Where(x => x.ClientId == request.ClientId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ClientReviewDto(x.Id, x.CreatorId, x.Rating, x.Comment, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
