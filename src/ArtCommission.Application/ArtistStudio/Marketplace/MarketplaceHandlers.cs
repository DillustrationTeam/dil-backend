using System.Data;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.ArtistStudio.Queries;
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
public record GetCreatorFollowStatusQuery(Guid UserId, Guid CreatorProfileId) : IRequest<bool>;
public record GetFeaturedBannersQuery() : IRequest<IReadOnlyList<BannerDto>>;
public record GetMarketplaceHomeQuery(Guid UserId, int Take = 12) : IRequest<MarketplaceHomeFeedDto>;

public class MarketplaceQueryHandler : IRequestHandler<SearchMarketplaceQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetArtworkDetailQuery, ArtworkDetailDto?>, IRequestHandler<GetArtworkCommentsQuery, IReadOnlyList<ArtworkCommentDto>>, IRequestHandler<GetCreatorServicesQuery, IReadOnlyList<CommissionServiceDto>>, IRequestHandler<GetCreatorReviewsQuery, IReadOnlyList<CreatorReviewDto>>, IRequestHandler<GetMyCollectionsQuery, IReadOnlyList<PersonalCollectionDto>>, IRequestHandler<GetMyFavoritesQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetFollowingFeedQuery, IReadOnlyList<ArtworkDto>>, IRequestHandler<GetCreatorFollowStatusQuery, bool>, IRequestHandler<GetFeaturedBannersQuery, IReadOnlyList<BannerDto>>, IRequestHandler<GetMarketplaceHomeQuery, MarketplaceHomeFeedDto>
{
    private readonly IApplicationDbContext _db;
    public MarketplaceQueryHandler(IApplicationDbContext db) => _db = db;
    private static ArtworkDto Map(Artwork x) => new(x.Id, x.CreatorProfileId, x.Title, x.Description, x.ImageUrl, x.ThumbnailUrl, x.IsAiGenerated, x.AiDetectionScore, x.ModerationStatus, x.ViewCount, x.CreatedAt, x.ArtworkTags.Select(t => t.Tag!.Name).ToList())
    {
        CreatorName = x.CreatorProfile?.DisplayName,
        CreatorHeadline = x.CreatorProfile?.Headline,
        CreatorRating = x.CreatorProfile?.RatingAverage ?? 0,
        AvailableSlots = x.CreatorProfile?.AvailableSlots ?? 0,
        Style = x.Style,
        LicenseType = x.LicenseType,
        StartingPrice = x.StartingPrice
    };
    private static BannerDto MapBanner(Artwork x) => new($"banner-{x.Id}", x.Title, x.Description ?? "Featured artwork", x.ImageUrl, "Khám phá", $"/artworks/{x.Id}");

    public async Task<IReadOnlyList<ArtworkDto>> Handle(SearchMarketplaceQuery query, CancellationToken ct)
    {
        var r = query.Request;
        var ratings = await CreatorRatingStats.LoadAsync(_db, ct);
        var ratedCreators = ratings.Where(pair => pair.Value.Average >= (r.MinRating ?? 0)).Select(pair => pair.Key).ToArray();
        var items = await _db.Artworks.AsNoTracking().Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).Include(x => x.CreatorProfile)
            .Where(x => !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted)
            .Where(x => string.IsNullOrWhiteSpace(r.Query) || x.Title.Contains(r.Query) || (x.Description != null && x.Description.Contains(r.Query)))
            .Where(x => string.IsNullOrWhiteSpace(r.Style) || x.Style == r.Style || x.ArtworkTags.Any(t => t.Tag!.Name == r.Style))
            .Where(x => r.MinPrice == null || x.StartingPrice >= r.MinPrice).Where(x => r.MaxPrice == null || x.StartingPrice <= r.MaxPrice)
            .Where(x => string.IsNullOrWhiteSpace(r.LicenseType) || x.LicenseType == r.LicenseType)
            .Where(x => r.MinRating == null || r.MinRating <= 0 || ratedCreators.Contains(x.CreatorProfileId))
            .Where(x => r.HasAvailableSlots != true || x.CreatorProfile!.AvailableSlots > 0)
            .OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(r.Take, 1, 100)).ToListAsync(ct);
        return items.Select(x => Map(x) with { CreatorRating = ratings.GetValueOrDefault(x.CreatorProfileId).Average }).ToList();
    }
    public async Task<ArtworkDetailDto?> Handle(GetArtworkDetailQuery q, CancellationToken ct)
    {
        var isSubmittedToEvent = await _db.EventSubmissions
            .AnyAsync(s => s.ArtworkId == q.ArtworkId, ct);

        var artwork = await _db.Artworks
            .Include(x => x.ArtworkTags).ThenInclude(x => x.Tag)
            .Include(x => x.CreatorProfile)
            .FirstOrDefaultAsync(x => x.Id == q.ArtworkId && !x.IsDeleted && 
                (x.ModerationStatus == "Approved" || 
                 isSubmittedToEvent || 
                 (q.ViewerUserId != Guid.Empty && x.CreatorProfile != null && x.CreatorProfile.UserId == q.ViewerUserId)) && 
                !x.CreatorProfile!.IsDeleted, ct);
        if (artwork is null) return null;
        artwork.ViewCount++; await _db.SaveChangesAsync(ct);
        var favorites = await _db.ArtworkFavorites.CountAsync(x => x.ArtworkId == q.ArtworkId, ct);
        var saved = q.ViewerUserId != Guid.Empty && await _db.ArtworkFavorites.AnyAsync(x => x.ArtworkId == q.ArtworkId && x.UserId == q.ViewerUserId, ct);
        return new ArtworkDetailDto(Map(artwork), artwork.LicenseType, artwork.StartingPrice, saved, favorites);
    }
    public async Task<IReadOnlyList<ArtworkCommentDto>> Handle(GetArtworkCommentsQuery q, CancellationToken ct) => await _db.ArtworkComments.AsNoTracking().Where(x => x.ArtworkId == q.ArtworkId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => new ArtworkCommentDto(x.Id, x.UserId, "Dillustration member", x.Body, x.CreatedAt)).ToListAsync(ct);
    public async Task<IReadOnlyList<CommissionServiceDto>> Handle(GetCreatorServicesQuery q, CancellationToken ct) => await _db.CommissionServices.AsNoTracking().Where(x => x.CreatorProfileId == q.CreatorId && x.IsActive && !x.IsDeleted).OrderBy(x => x.StartingPrice).Select(x => new CommissionServiceDto(x.Id, x.Title, x.Description, x.StartingPrice, x.DeliveryDays, x.MaxRevisions, x.IsActive)).ToListAsync(ct);
    public async Task<IReadOnlyList<CreatorReviewDto>> Handle(GetCreatorReviewsQuery q, CancellationToken ct)
    {
        var direct = await _db.CreatorReviews.AsNoTracking().Where(x => x.CreatorProfileId == q.CreatorId && !x.IsDeleted)
            .Select(x => new CreatorReviewDto(x.Id, x.ReviewerUserId, x.Rating, x.Comment, x.CreatedAt)).ToListAsync(ct);
        var commissions = await _db.Reviews.AsNoTracking().Where(x => !x.IsDeleted && x.IsVisible && !x.Commission.IsDeleted && x.Commission.CreatorId == q.CreatorId)
            .Select(x => new CreatorReviewDto(x.Id, x.ReviewerId, x.Rating, x.Comment, x.CreatedAt)).ToListAsync(ct);
        return direct.Concat(commissions).OrderByDescending(x => x.CreatedAt).ToList();
    }
    public async Task<IReadOnlyList<PersonalCollectionDto>> Handle(GetMyCollectionsQuery q, CancellationToken ct) => await _db.PersonalCollections.AsNoTracking().Where(x => x.OwnerUserId == q.UserId && !x.IsDeleted).OrderByDescending(x => x.CreatedAt).Select(x => new PersonalCollectionDto(x.Id, x.Name, x.IsPublic, x.CollectionArtworks.Count(a => !a.Artwork!.IsDeleted && a.Artwork.ModerationStatus == "Approved" && !a.Artwork.CreatorProfile!.IsDeleted), x.CreatedAt)).ToListAsync(ct);
    public async Task<IReadOnlyList<ArtworkDto>> Handle(GetMyFavoritesQuery q, CancellationToken ct) { var items = await _db.Artworks.AsNoTracking().Where(a => _db.ArtworkFavorites.Any(f => f.UserId == q.UserId && f.ArtworkId == a.Id)).Where(x => !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).Include(x => x.CreatorProfile).ToListAsync(ct); return items.Select(Map).ToList(); }
    public async Task<IReadOnlyList<ArtworkDto>> Handle(GetFollowingFeedQuery q, CancellationToken ct)
    {
        var ratings = await CreatorRatingStats.LoadAsync(_db, ct);
        var items = await _db.Artworks.AsNoTracking().Where(a => _db.Follows.Any(f => f.FollowerUserId == q.UserId && f.CreatorProfileId == a.CreatorProfileId)).Where(x => !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).Include(x => x.CreatorProfile).OrderByDescending(x => x.CreatedAt).Take(Math.Clamp(q.Take, 1, 100)).ToListAsync(ct);
        return items.Select(x => Map(x) with { CreatorRating = ratings.GetValueOrDefault(x.CreatorProfileId).Average }).ToList();
    }

    public Task<bool> Handle(GetCreatorFollowStatusQuery q, CancellationToken ct) =>
        _db.Follows.AsNoTracking().AnyAsync(
            follow => follow.FollowerUserId == q.UserId && follow.CreatorProfileId == q.CreatorProfileId,
            ct);

    public async Task<IReadOnlyList<BannerDto>> Handle(GetFeaturedBannersQuery _, CancellationToken ct)
    {
        var items = await _db.Artworks.AsNoTracking().Where(x => !x.IsDeleted && x.ModerationStatus == "Approved" && !x.CreatorProfile!.IsDeleted).OrderByDescending(x => x.CreatedAt).Take(3).Include(x => x.ArtworkTags).ThenInclude(x => x.Tag).Include(x => x.CreatorProfile).ToListAsync(ct);
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
            case "unfavorite":
                var likeState = await new ArtworkInteractionHandler(_db).Handle(new SetArtworkLikeCommand(c.TargetId, c.UserId, c.Action == "favorite"), ct);
                return likeState is null ? (false, null, new[] { "Artwork not found." }) : (true, new { favorited = likeState.IsLiked, likeState.LikeCount }, Array.Empty<string>());
            case "comment":
                if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 1000) return (false, null, new[] { "Comment must contain 1 to 1000 characters." });
                if (!await _db.Artworks.AnyAsync(a => a.Id == c.TargetId && !a.IsDeleted && a.ModerationStatus == "Approved" && !a.CreatorProfile!.IsDeleted, ct))
                    return (false, null, new[] { "Artwork not found." });
                var comment = new ArtworkComment { ArtworkId = c.TargetId, UserId = c.UserId, Body = c.Text.Trim() }; _db.ArtworkComments.Add(comment); await _db.SaveChangesAsync(ct); return (true, new ArtworkCommentDto(comment.Id, c.UserId, "You", comment.Body, comment.CreatedAt), Array.Empty<string>());
            case "follow":
                return await FollowAsync(c.UserId, c.TargetId, ct);
            case "unfollow":
                return await UnfollowAsync(c.UserId, c.TargetId, ct);
            case "collection":
                if (string.IsNullOrWhiteSpace(c.Text) || c.Text.Length > 100) return (false, null, new[] { "Collection name must contain 1 to 100 characters." });
                var collection = new PersonalCollection { OwnerUserId = c.UserId, Name = c.Text.Trim(), IsPublic = c.IsPublic }; _db.PersonalCollections.Add(collection); await _db.SaveChangesAsync(ct); return (true, new PersonalCollectionDto(collection.Id, collection.Name, collection.IsPublic, 0, collection.CreatedAt), Array.Empty<string>());
            case "add-collection-artwork":
                if (!Guid.TryParse(c.Text, out var artworkId)) return (false, null, new[] { "Artwork is invalid." });
                var savedState = await new ArtworkInteractionHandler(_db).Handle(new SetCollectionArtworkCommand(c.TargetId, artworkId, c.UserId, true), ct);
                return savedState is null ? (false, null, new[] { "Collection or artwork not found." }) : (true, savedState, Array.Empty<string>());
            case "service":
                var creator = await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.UserId == c.UserId && !x.IsDeleted, ct); if (creator is null || c.Service is null) return (false, null, new[] { "Creator profile and service details are required." });
                if (string.IsNullOrWhiteSpace(c.Service.Title) || c.Service.Title.Length > 150
                    || c.Service.Description?.Length > 2000
                    || c.Service.StartingPrice is <= 0 or > 500_000_000m
                    || c.Service.DeliveryDays is < 1 or > 365
                    || c.Service.MaxRevisions is < 0 or > 20) return (false, null, new[] { "Service details are invalid." });
                var service = new CommissionService { CreatorProfileId = creator.Id, Title = c.Service.Title.Trim(), Description = c.Service.Description, StartingPrice = c.Service.StartingPrice, DeliveryDays = c.Service.DeliveryDays, MaxRevisions = c.Service.MaxRevisions, IsActive = c.Service.IsActive }; _db.CommissionServices.Add(service); await _db.SaveChangesAsync(ct); return (true, new CommissionServiceDto(service.Id, service.Title, service.Description, service.StartingPrice, service.DeliveryDays, service.MaxRevisions, service.IsActive), Array.Empty<string>());
            case "review":
                // Reviews must be tied to a completed commission and checked by CommissionService.
                return (false, null, new[] { "Hãy gửi đánh giá từ đơn commission đã hoàn thành." });
            default: return (false, null, new[] { "Unsupported marketplace action." });
        }
    }

    private async Task<(bool Success, object? Data, string[] Errors)> FollowAsync(Guid userId, Guid creatorProfileId, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var profile = await _db.CreatorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == creatorProfileId && !x.IsDeleted, ct);
        if (profile is null) return (false, null, new[] { "Creator not found." });
        if (profile.UserId == userId) return (false, null, new[] { "You cannot follow your own creator profile." });

        var exists = await _db.Follows.AnyAsync(
            x => x.FollowerUserId == userId && x.CreatorProfileId == creatorProfileId,
            ct);
        if (!exists)
        {
            _db.Follows.Add(new Follow { FollowerUserId = userId, CreatorProfileId = creatorProfileId });
            await _db.SaveChangesAsync(ct);
            await _db.CreatorProfiles
                .Where(x => x.Id == creatorProfileId)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.FollowerCount, x => x.FollowerCount + 1), ct);
        }

        await transaction.CommitAsync(ct);
        return (true, new { following = true }, Array.Empty<string>());
    }

    private async Task<(bool Success, object? Data, string[] Errors)> UnfollowAsync(Guid userId, Guid creatorProfileId, CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var deleted = await _db.Follows
            .Where(x => x.FollowerUserId == userId && x.CreatorProfileId == creatorProfileId)
            .ExecuteDeleteAsync(ct);
        if (deleted > 0)
        {
            await _db.CreatorProfiles
                .Where(x => x.Id == creatorProfileId && x.FollowerCount > 0)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.FollowerCount, x => x.FollowerCount - 1), ct);
        }

        await transaction.CommitAsync(ct);
        return (true, new { following = false }, Array.Empty<string>());
    }
}
