using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Marketplace;

public record SearchMarketplaceQuery(ArtworkSearchRequest Request) : IRequest<IReadOnlyList<ArtworkDto>>;
public record GetArtworkDetailQuery(Guid ArtworkId, Guid ViewerUserId) : IRequest<ArtworkDetailDto?>;
public record GetArtworkCommentsQuery(Guid ArtworkId) : IRequest<IReadOnlyList<ArtworkCommentDto>>;
public record GetCreatorServicesQuery(Guid CreatorId) : IRequest<IReadOnlyList<CommissionServiceDto>>;
public record GetCreatorReviewsQuery(Guid CreatorId) : IRequest<IReadOnlyList<CreatorReviewDto>>;
public record GetMyCollectionsQuery(Guid UserId) : IRequest<IReadOnlyList<PersonalCollectionDto>>;
public record GetMyFavoritesQuery(Guid UserId) : IRequest<IReadOnlyList<ArtworkDto>>;
public record GetFollowingFeedQuery(Guid UserId, int Take = 30) : IRequest<IReadOnlyList<ArtworkDto>>;
public record GetFeaturedBannersQuery() : IRequest<IReadOnlyList<BannerDto>>;
public record GetMarketplaceHomeQuery(Guid UserId, int Take = 12) : IRequest<MarketplaceHomeFeedDto>;

public class MarketplaceQueryHandler : IRequestHandler<SearchMarketplaceQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetArtworkDetailQuery, ArtworkDetailDto?>, IRequestHandler<GetArtworkCommentsQuery, IReadOnlyList<ArtworkCommentDto>>, IRequestHandler<GetCreatorServicesQuery, IReadOnlyList<CommissionServiceDto>>, IRequestHandler<GetCreatorReviewsQuery, IReadOnlyList<CreatorReviewDto>>, IRequestHandler<GetMyCollectionsQuery, IReadOnlyList<PersonalCollectionDto>>, IRequestHandler<GetMyFavoritesQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetFollowingFeedQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetFeaturedBannersQuery, IReadOnlyList<BannerDto>>, IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeFeedDto>
{
    private readonly IApplicationDbContext _db;
    public MarketplaceQueryHandler(IApplicationDbContext db) => _db = db;
    private static ArtworkDto Map(Artwork x) => new(x.Id, x.CreatorProfileId, x.Title, x.Description, x.ImageUrl, x.ThumbnailUrl, x.IsAiGenerated, x.AiDetectionScore, x.ModerationStatus, x.ViewCount, x.CreatedAt, x.ArtworkTags.Select(t => t.Tag!.Name).ToList());
    private static BannerDto MapBanner(Artwork x) => new($"banner-{x.Id}", x.Title, x.Description ?? "Featured artwork", x.ImageUrl, "Khám phá", $"/artworks/{x.Id}");

    public async Task<IReadOnlyList<ArtworkDto>> Handle(SearchMarketplaceQuery query, CancellationToken ct)
    {
        var r = query.Request;
        var items = await _db.Artworks.AsNoTracking().Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).Include(x => x.CreatorProfile)
            .Where(x => !x.IsDeleted)
            .Where(x => string.IsNullOrWhiteSpace(r.Query) || x.Title.Contains(r.Query) || (x.Description != null && x.Description.Contains(r.Query)))
            .Where(x => string.IsNullOrWhiteSpace(r.Style) || x.Style == r.Style || x.ArtworkTags.Any(t => t.Tag!.Name == r.Style))
            .Where(x => r.MinPrice == null || x.StartingPrice >= r.MinPrice).Where(x => r.MaxPrice == null || x.StartingPrice <= r.MaxPrice)
            .Where(x => string.IsNullOrWhiteSpace(r.LicenseType) || x.LicenseType == r.LicenseType)
            .Where(x => r.MinRating == null || x.CreatorProfile!.RatingAverage >= r.MinRating)
            .Where(x => r.HasAvailableSlots != true || x.CreatorProfile!.AvailableSlots > 0)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(r.Take, 1, 100)).ToListAsync(ct);
        return items.Select(Map).ToList();
    }
    public async Task<ArtworkDetailDto?> Handle(GetArtworkDetailQuery q, CancellationToken ct)
    {
        var artwork = await _db.Artworks.Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).FirstOrDefaultAsync(x => x.Id == q.ArtworkId && !x.IsDeleted, ct);
        if (artwork is null) return null;
        artwork.ViewCount++; await _db.SaveChangesAsync(ct);
        var favorites = await _db.ArtworkFavorites.CountAsync(x => x.ArtworkId == q.ArtworkId, ct);
        var saved = q.ViewerUserId != Guid.Empty && await _db.ArtworkFavorites.AnyAsync(x => x.ArtworkId == q.ArtworkId && x.UserId == q.ViewerUserId, ct);
        return new ArtworkDetailDto(Map(artwork), artwork.LicenseType, artwork.StartingPrice, saved, favorites);
    }
    public async Task<IReadOnlyList<ArtworkCommentDto>> Handle(GetArtworkCommentsQuery q, CancellationToken ct) => await _db.ArtworkComments.AsNoTracking().Where(x => x.ArtworkId == q.ArtworkId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => new ArtworkCommentDto(x.Id, x.UserId, "Dillustration member", x.Body, x.CreatedAt)).ToListAsync(ct);
    public async Task<IReadOnlyList<CommissionServiceDto>> Handle(GetCreatorServicesQuery q, CancellationToken ct) => await _db.CommissionServices.AsNoTracking().Where(x => x.CreatorProfileId == q.CreatorId && x.IsActive && !x.IsDeleted).OrderBy(x => x.StartingPrice).Select(x => new CommissionServiceDto(x.Id, x.Title, x.Description, x.StartingPrice, x.DeliveryDays, x.MaxRevisions, x.IsActive)).ToListAsync(ct);
    public async Task<IReadOnlyList<CreatorReviewDto>> Handle(GetCreatorReviewsQuery q, CancellationToken ct) => await _db.CreatorReviews.AsNoTracking().Where(x => x.CreatorProfileId == q.CreatorId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => new CreatorReviewDto(x.Id, x.ReviewerUserId, x.Rating, x.Comment, x.CreatedAt)).ToListAsync(ct);
    public async Task<IReadOnlyList<PersonalCollectionDto>> Handle(GetMyCollectionsQuery q, CancellationToken ct) => await _db.PersonalCollections.AsNoTracking().Where(x => x.OwnerUserId == q.UserId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => new PersonalCollectionDto(x.Id, x.Name, x.IsPublic, x.CollectionArtworks.Count, x.CreatedAt)).ToListAsync(ct);
    public async Task<IReadOnlyList<ArtworkDto>> Handle(GetMyFavoritesQuery q, CancellationToken ct) { var items = await _db.ArtworkFavorites.AsNoTracking().Where(x => x.UserId == q.UserId).Select(x => x.Artwork!).Where(x => !x.IsDeleted).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).ToListAsync(ct); return items.Select(Map).ToList(); }
    public async Task<IReadOnlyList<ArtworkDto>> Handle(GetFollowingFeedQuery q, CancellationToken ct)
    {
        var items = await _db.Follows.AsNoTracking().Where(x => x.FollowerUserId == q.UserId).Select(x => x.CreatorProfile!).SelectMany(x => x.Artworks).Where(x => !x.IsDeleted).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(q.Take, 1, 100)).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<BannerDto>> Handle(GetFeaturedBannersQuery _, CancellationToken ct)
    {
        var items = await _db.Artworks.AsNoTracking().Where(x => !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Take(3).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).ToListAsync(ct);
        return items.Count == 0 ? new[] { new BannerDto("banner-artverse", "Artverse Weekend", "Tận hưởng hội chợ tranh giới hạn", "/weblogo.png", "Khám phá", "/explore") } : items.Select(MapBanner).ToList();
    }

    public async Task<MarketplaceHomeFeedDto> Handle(GetMarketplaceHomeQuery q, CancellationToken ct)
    {
        var recommended = await Handle(new SearchMarketplaceQuery(new ArtworkSearchRequest(null, null, null, null, null, null, null, Math.Clamp(q.Take, 1, 30))), ct);
        var following = q.UserId != Guid.Empty ? await Handle(new GetFollowingFeedQuery(q.UserId, Math.Clamp(q.Take, 1, 30)), ct) : Array.Empty<ArtworkDto>();
        var banners = await Handle(new GetFeaturedBannersQuery(), ct);
        return new MarketplaceHomeFeedDto(banners, recommended, following);
    }
}

public record MarketplaceMutationCommand(string Action, Guid UserId, Guid TargetId, string? Text = null, bool IsPublic = false, CreateCommissionServiceRequest? Service = null, CreateCreatorReviewRequest? Review = null) : IRequest<(bool Success, object? Data, string[] Errors)>;
public class MarketplaceMutationHandler : IRequestHandler<MarketplaceMutationCommand, (bool Success, object? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db; public MarketplaceMutationHandler(IApplicationDbContext db) => _db = db;
    public async Task<(bool Success, object? Data, string[] Errors)> Handle(MarketplaceMutationCommand c, CancellationToken ct)
    {
        if (c.UserId == Guid.Empty) return (false, null, new[] { "Authentication is required." });
        switch (c.Action)
        {
            case "favorite":
                if (!await _db.Artworks.AnyAsync(x => x.Id == c.TargetId && !x.IsDeleted, ct)) return (false, null, new[] { "Artwork not found." });
                if (!await _db.ArtworkFavorites.AnyAsync(x => x.UserId == c.UserId && x.ArtworkId == c.TargetId, ct)) _db.ArtworkFavorites.Add(new ArtworkFavorite { UserId = c.UserId, ArtworkId = c.TargetId });
                await _db.SaveChangesAsync(ct); return (true, new { favorited = true }, Array.Empty<string>());
            case "unfavorite":
                var favorite = await _db.ArtworkFavorites.FindAsync(new object[] { c.UserId, c.TargetId }, ct); if (favorite != null) _db.ArtworkFavorites.Remove(favorite); await _db.SaveChangesAsync(ct); return (true, new { favorited = false }, Array.Empty<string>());
            case "comment":
                if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 1000) return (false, null, new[] { "Comment must contain 1 to 1000 characters." });
                var comment = new ArtworkComment { ArtworkId = c.TargetId, UserId = c.UserId, Body = c.Text.Trim() }; _db.ArtworkComments.Add(comment); await _db.SaveChangesAsync(ct); return (true, new ArtworkCommentDto(comment.Id, c.UserId, "You", comment.Body, comment.CreatedAt), Array.Empty<string>());
            case "follow":
                var profile = await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.Id == c.TargetId && !x.IsDeleted, ct); if (profile is null) return (false, null, new[] { "Creator not found." });
                if (!await _db.Follows.AnyAsync(x => x.FollowerUserId == c.UserId && x.CreatorProfileId == c.TargetId, ct)) { _db.Follows.Add(new Follow { FollowerUserId = c.UserId, CreatorProfileId = c.TargetId }); profile.FollowerCount++; await _db.SaveChangesAsync(ct); } return (true, new { following = true }, Array.Empty<string>());
            case "unfollow":
                var follow = await _db.Follows.FindAsync(new object[] { c.UserId, c.TargetId }, ct); if (follow != null) { _db.Follows.Remove(follow); var p = await _db.CreatorProfiles.FindAsync(new object[] { c.TargetId }, ct); if (p != null && p.FollowerCount > 0) p.FollowerCount--; await _db.SaveChangesAsync(ct); } return (true, new { following = false }, Array.Empty<string>());
            case "collection":
                if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 100) return (false, null, new[] { "Collection name must contain 1 to 100 characters." });
                var collection = new PersonalCollection { OwnerUserId = c.UserId, Name = c.Text.Trim(), IsPublic = c.IsPublic }; _db.PersonalCollections.Add(collection); await _db.SaveChangesAsync(ct); return (true, new PersonalCollectionDto(collection.Id, collection.Name, collection.IsPublic, 0, collection.CreatedAt), Array.Empty<string>());
            case "add-collection-artwork":
                var owns = await _db.PersonalCollections.AnyAsync(x => x.Id == c.TargetId && x.OwnerUserId == c.UserId && !x.IsDeleted, ct); if (!owns || !Guid.TryParse(c.Text, out var artworkId)) return (false, null, new[] { "Collection or artwork is invalid." });
                if (!await _db.CollectionArtworks.AnyAsync(x => x.CollectionId == c.TargetId && x.ArtworkId == artworkId, ct)) { _db.CollectionArtworks.Add(new CollectionArtwork { CollectionId = c.TargetId, ArtworkId = artworkId }); await _db.SaveChangesAsync(ct); } return (true, null, Array.Empty<string>());
            case "service":
                var creator = await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.UserId == c.UserId && !x.IsDeleted, ct); if (creator is null || c.Service is null) return (false, null, new[] { "Creator profile and service details are required." });
                if (string.IsNullOrWhiteSpace(c.Service.Title) || c.Service.StartingPrice < 0 || c.Service.DeliveryDays < 1) return (false, null, new[] { "Service details are invalid." });
                var service = new CommissionService { CreatorProfileId = creator.Id, Title = c.Service.Title.Trim(), Description = c.Service.Description, StartingPrice = c.Service.StartingPrice, DeliveryDays = c.Service.DeliveryDays, MaxRevisions = c.Service.MaxRevisions, IsActive = c.Service.IsActive }; _db.CommissionServices.Add(service); await _db.SaveChangesAsync(ct); return (true, new CommissionServiceDto(service.Id, service.Title, service.Description, service.StartingPrice, service.DeliveryDays, service.MaxRevisions, service.IsActive), Array.Empty<string>());
            case "review":
                if (c.Review is null || c.Review.Rating is < 1 or > 5) return (false, null, new[] { "Rating must be from 1 to 5." });
                var reviewed = await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.Id == c.TargetId && !x.IsDeleted, ct); if (reviewed is null) return (false, null, new[] { "Creator not found." });
                var review = new CreatorReview { CreatorProfileId = c.TargetId, ReviewerUserId = c.UserId, Rating = c.Review.Rating, Comment = c.Review.Comment?.Trim() }; _db.CreatorReviews.Add(review); reviewed.RatingAverage = ((reviewed.RatingAverage * reviewed.RatingCount) + review.Rating) / ++reviewed.RatingCount; await _db.SaveChangesAsync(ct); return (true, new CreatorReviewDto(review.Id, c.UserId, review.Rating, review.Comment, review.CreatedAt), Array.Empty<string>());
            default: return (false, null, new[] { "Unsupported marketplace action." });
        }
    }
}
