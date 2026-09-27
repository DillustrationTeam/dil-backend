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
    DateTimeOffset CreatedAt,
    int AvailableSlots = 0
);

public record CreateCreatorProfileRequest(
    string DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool IsAcceptingOrders = true,
    int AvailableSlots = 0
);

public record UpdateCreatorProfileRequest(
    string? DisplayName,
    string? Headline,
    string? Bio,
    string? Specialties,
    string? Location,
    string? WebsiteUrl,
    string? BannerUrl,
    bool? IsAcceptingOrders,
    int? AvailableSlots
);
