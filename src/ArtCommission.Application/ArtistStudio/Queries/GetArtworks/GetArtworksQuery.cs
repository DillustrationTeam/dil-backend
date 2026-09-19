using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetArtworks;

public record GetArtworksQuery() : IRequest<(bool Success, IReadOnlyList<ArtworkDto> Data, string[] Errors)>;

public class GetArtworksQueryHandler : IRequestHandler<GetArtworksQuery, (bool Success, IReadOnlyList<ArtworkDto> Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetArtworksQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, IReadOnlyList<ArtworkDto> Data, string[] Errors)> Handle(GetArtworksQuery request, CancellationToken cancellationToken)
    {
        var artworks = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Artwork>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ArtworkDto(
                x.Id,
                x.CreatorProfileId,
                x.Title,
                x.Description,
                x.ImageUrl,
                x.ThumbnailUrl,
                x.IsAiGenerated,
                x.AiDetectionScore,
                x.ModerationStatus,
                x.ViewCount,
                x.CreatedAt,
                x.ArtworkTags.Select(at => at.Tag!.Name).ToList()))
            .ToListAsync(cancellationToken);

        return (true, artworks, Array.Empty<string>());
    }
}
