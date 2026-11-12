using ArtCommission.Application.ArtistStudio.Categories.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Categories.Queries;

public record GetCategoriesQuery(bool IncludeInactive = false) : IRequest<IReadOnlyList<CategoryDto>>;

public sealed class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, IReadOnlyList<CategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CategoryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.ArtworkCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!request.IncludeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.DisplayOrder,
                c.IsActive,
                c.Artworks.Count(a => !a.IsDeleted && a.ModerationStatus == "Approved" && !a.CreatorProfile!.IsDeleted)
            ))
            .ToListAsync(cancellationToken);
    }
}

public record AdminGetCategoriesQuery(int TrendingTagLimit = 30) : IRequest<AdminCategoriesOverviewDto>;

public sealed class AdminGetCategoriesQueryHandler : IRequestHandler<AdminGetCategoriesQuery, AdminCategoriesOverviewDto>
{
    private readonly IApplicationDbContext _db;

    public AdminGetCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AdminCategoriesOverviewDto> Handle(AdminGetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var categories = await _db.ArtworkCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.Description,
                c.DisplayOrder,
                c.IsActive,
                c.Artworks.Count(a => !a.IsDeleted)
            ))
            .ToListAsync(cancellationToken);

        var limit = Math.Clamp(request.TrendingTagLimit, 1, 100);
        var trendingTags = await _db.Tags
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .Select(t => new
            {
                t.Id,
                t.Name,
                ArtworkCount = t.ArtworkTags.Count(at => at.Artwork != null && !at.Artwork.IsDeleted),
                t.IsAiGenerated
            })
            .Where(t => t.ArtworkCount > 0)
            .OrderByDescending(t => t.ArtworkCount)
            .ThenBy(t => t.Name)
            .Take(limit)
            .Select(t => new TrendingTagDto(t.Id, t.Name, t.ArtworkCount, t.IsAiGenerated))
            .ToListAsync(cancellationToken);

        return new AdminCategoriesOverviewDto(categories, trendingTags);
    }
}
