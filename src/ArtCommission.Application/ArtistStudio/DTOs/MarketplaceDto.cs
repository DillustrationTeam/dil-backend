namespace ArtCommission.Application.ArtistStudio.DTOs;

public record ArtworkSearchRequest(string? Query, string? Style, decimal? MinPrice, decimal? MaxPrice, decimal? MinRating, bool? HasAvailableSlots, string? LicenseType, int Take = 30);
public record VisualSearchRequest(string? ReferenceImageUrl, IReadOnlyList<string>? Tags, string? Style, int Take = 12);
public record BannerDto(string Id, string Title, string Subtitle, string ImageUrl, string? CtaLabel, string? CtaUrl);
public record MarketplaceHomeFeedDto(IReadOnlyList<BannerDto> FeaturedBanners, IReadOnlyList<ArtworkDto> RecommendedArtworks, IReadOnlyList<ArtworkDto> FollowingArtworks);
public record ArtworkDetailDto(ArtworkDto Artwork, string LicenseType, decimal? StartingPrice, bool IsFavorited, int FavoriteCount);
public record ArtworkCommentDto(Guid Id, Guid UserId, string AuthorName, string Body, DateTimeOffset CreatedAt);
public record CommissionServiceDto(Guid Id, string Title, string? Description, decimal StartingPrice, int DeliveryDays, int MaxRevisions, bool IsActive);
public record CreatorReviewDto(Guid Id, Guid ReviewerUserId, int Rating, string? Comment, DateTimeOffset CreatedAt);
public record PersonalCollectionDto(Guid Id, string Name, bool IsPublic, int ArtworkCount, DateTimeOffset CreatedAt);
public record CreateCollectionRequest(string Name, bool IsPublic);
public record CreateCommentRequest(string Body);
public record CreateCommissionServiceRequest(string Title, string? Description, decimal StartingPrice, int DeliveryDays, int MaxRevisions, bool IsActive = true);
public record CreateCreatorReviewRequest(int Rating, string? Comment);
