namespace ArtCommission.Application.ArtistStudio.ClientProfiles.DTOs;

public record ClientProfileDto(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string? CoverUrl,
    string? Bio,
    bool IsVerified,
    DateTimeOffset JoinedAt,
    string? Username,
    IReadOnlyList<string> InterestTags,
    string? Country,
    string? Timezone,
    IReadOnlyList<string> PreferredLanguages,
    DateTimeOffset? UsernameChangedAt = null
);
