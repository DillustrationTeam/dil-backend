namespace ArtCommission.Application.ArtistStudio.DTOs;

public record CreatorProfileDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool IsAcceptingOrders,
    bool IsApproved,
    decimal RatingAverage,
    int RatingCount,
    int FollowerCount,
    DateTimeOffset CreatedAt
);

public record CreateCreatorProfileRequest(
    string DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool IsAcceptingOrders = true
);

public record UpdateCreatorProfileRequest(
    string? DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool? IsAcceptingOrders
);
