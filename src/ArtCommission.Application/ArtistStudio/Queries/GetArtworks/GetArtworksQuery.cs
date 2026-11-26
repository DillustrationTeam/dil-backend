using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetArtworks;

public record GetArtworksQuery(
    int Page = 1,
    int PageSize = 12,
    string? Search = null,
    Guid? CategoryId = null
) : IRequest<(bool Success, IReadOnlyList<ArtworkDto> Data, int TotalCount, string[] Errors)>;

public class GetArtworksQueryHandler : IRequestHandler<GetArtworksQuery, (bool Success, IReadOnlyList<ArtworkDto> Data, int TotalCount, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetArtworksQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, IReadOnlyList<ArtworkDto> Data, int TotalCount, string[] Errors)> Handle(GetArtworksQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 60 ? 12 : request.PageSize;

        var query = _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Artwork>()
            .AsNoTracking()
            // Thư viện công khai: chỉ tranh đã duyệt, của creator còn sống,
            // và KHÔNG trả các bản ghi seed nội bộ "[DEMO] …" ra ngoài.
            .Where(x => !x.IsDeleted
                        && x.ModerationStatus == "Approved"
                        && !x.CreatorProfile!.IsDeleted
                        && !x.Title.StartsWith("[DEMO]"));

        if (request.CategoryId is { } categoryId && categoryId != Guid.Empty)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => EF.Functions.Like(x.Title, $"%{term}%")
                                     || (x.Description != null && EF.Functions.Like(x.Description, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var artworks = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ArtworkDto(
                x.Id,
                x.CreatorProfileId,
                x.Title,
                x.Description,
                x.WatermarkedUrl ?? x.ImageUrl,
                x.WatermarkedUrl ?? x.ThumbnailUrl ?? x.ImageUrl,
                x.IsAiGenerated,
                x.AiDetectionScore,
                x.ModerationStatus,
                x.ViewCount,
                x.CreatedAt,
                x.ArtworkTags.Select(at => at.Tag!.Name).ToList())
            {
                CreatorName = x.CreatorProfile != null ? x.CreatorProfile.DisplayName : null,
                Style = x.Style,
                CategoryId = x.CategoryId,
                CategoryName = x.Category != null ? x.Category.Name : x.Style
            })
            .ToListAsync(cancellationToken);

        return (true, artworks, totalCount, Array.Empty<string>());
    }
}
