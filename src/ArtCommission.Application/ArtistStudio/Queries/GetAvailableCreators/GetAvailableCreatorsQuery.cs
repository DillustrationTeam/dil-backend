using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Categories.DTOs;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetAvailableCreators;

public record AvailableCreatorDto(
    Guid Id,
    string DisplayName,
    string? Headline,
    string? Specialties,
    decimal RatingAverage,
    int RatingCount,
    int FollowerCount,
    int AvailableSlots,
    string? BannerUrl = null,
    IReadOnlyList<CategorySummaryDto>? Categories = null
);

public record GetAvailableCreatorsQuery(
    Guid? CategoryId = null,
    string? Search = null,
    bool? OnlyOpenSlots = null
) : IRequest<List<AvailableCreatorDto>>;

public class GetAvailableCreatorsQueryHandler : IRequestHandler<GetAvailableCreatorsQuery, List<AvailableCreatorDto>>
{
    private readonly IApplicationDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new RateCardIdConverter() }
    };

    public GetAvailableCreatorsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<AvailableCreatorDto>> Handle(GetAvailableCreatorsQuery request, CancellationToken cancellationToken)
    {
        var ratings = await CreatorRatingStats.LoadAsync(_db, cancellationToken);
        var activeCategories = await _db.ArtworkCategories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new CategorySummaryDto(c.Id, c.Name, c.Slug))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var profilesQuery = _db.CreatorProfiles
            .AsNoTracking()
            .Where(profile => !profile.IsDeleted);

        if (request.OnlyOpenSlots == true)
        {
            profilesQuery = profilesQuery.Where(profile => profile.IsAcceptingOrders && profile.AvailableSlots > 0);
        }
        else if (!request.CategoryId.HasValue && string.IsNullOrWhiteSpace(request.Search) && request.OnlyOpenSlots is null)
        {
            // Preserve existing default behavior when called without filters
            profilesQuery = profilesQuery.Where(profile => profile.IsAcceptingOrders);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            profilesQuery = profilesQuery.Where(profile =>
                profile.DisplayName.Contains(term)
                || (profile.Headline != null && profile.Headline.Contains(term))
                || (profile.Specialties != null && profile.Specialties.Contains(term)));
        }

        var profiles = await profilesQuery
            .OrderByDescending(profile => profile.AvailableSlots > 0 && profile.IsAcceptingOrders)
            .ThenBy(profile => profile.DisplayName)
            .Select(profile => new
            {
                profile.Id,
                profile.DisplayName,
                profile.Headline,
                profile.Specialties,
                profile.FollowerCount,
                profile.AvailableSlots,
                profile.BannerUrl,
                profile.RateCardJson
            })
            .ToListAsync(cancellationToken);

        var creatorIds = profiles.Select(p => p.Id).ToList();
        var artworkCategoriesByCreator = creatorIds.Count > 0
            ? (await _db.Artworks
                .AsNoTracking()
                .Where(a => creatorIds.Contains(a.CreatorProfileId) && !a.IsDeleted && a.ModerationStatus == "Approved" && a.CategoryId != null)
                .Select(a => new { a.CreatorProfileId, CategoryId = a.CategoryId!.Value })
                .Distinct()
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.CreatorProfileId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToHashSet())
            : new Dictionary<Guid, HashSet<Guid>>();

        var result = new List<AvailableCreatorDto>();
        foreach (var profile in profiles)
        {
            var catIds = new HashSet<Guid>();

            if (!string.IsNullOrWhiteSpace(profile.RateCardJson))
            {
                try
                {
                    var packages = JsonSerializer.Deserialize<List<RateCardPackageDto>>(profile.RateCardJson, JsonOptions);
                    if (packages is not null)
                    {
                        foreach (var pkg in packages)
                        {
                            if (pkg.CategoryId.HasValue && pkg.CategoryId.Value != Guid.Empty)
                            {
                                catIds.Add(pkg.CategoryId.Value);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed rate card JSON
                }
            }

            if (artworkCategoriesByCreator.TryGetValue(profile.Id, out var artCats))
            {
                foreach (var catId in artCats)
                {
                    catIds.Add(catId);
                }
            }

            if (request.CategoryId.HasValue && !catIds.Contains(request.CategoryId.Value))
            {
                continue;
            }

            var creatorCategories = catIds
                .Where(activeCategories.ContainsKey)
                .Select(id => activeCategories[id])
                .ToList();

            var rating = ratings.GetValueOrDefault(profile.Id);
            result.Add(new AvailableCreatorDto(
                profile.Id,
                profile.DisplayName,
                profile.Headline,
                profile.Specialties,
                rating.Average,
                rating.Count,
                profile.FollowerCount,
                profile.AvailableSlots,
                profile.BannerUrl,
                creatorCategories));
        }

        return result;
    }
}
