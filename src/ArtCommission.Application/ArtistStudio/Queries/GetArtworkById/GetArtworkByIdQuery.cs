using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetArtworkById;

public record GetArtworkByIdQuery(Guid ArtworkId) : IRequest<(bool Success, ArtworkDto? Data, string[] Errors)>;

public class GetArtworkByIdQueryHandler : IRequestHandler<GetArtworkByIdQuery, (bool Success, ArtworkDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetArtworkByIdQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ArtworkDto? Data, string[] Errors)> Handle(GetArtworkByIdQuery request, CancellationToken cancellationToken)
    {
        var artwork = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Artwork>()
            .AsNoTracking()
            .Include(x => x.ArtworkTags)
            .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == request.ArtworkId && !x.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (false, null, new[] { "Artwork not found." });
        }

        return (true, new ArtworkDto(
            artwork.Id,
            artwork.CreatorProfileId,
            artwork.Title,
            artwork.Description,
            artwork.ImageUrl,
            artwork.ThumbnailUrl,
            artwork.IsAiGenerated,
            artwork.AiDetectionScore,
            artwork.ModerationStatus,
            artwork.ViewCount,
            artwork.CreatedAt,
            artwork.ArtworkTags.Select(x => x.Tag != null ? x.Tag.Name : string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
        ), Array.Empty<string>());
    }
}
