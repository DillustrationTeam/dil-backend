namespace ArtCommission.Application.Common.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsVerified,
    DateTimeOffset CreatedAt,
    string? AvatarUrl = null,
    string? CoverUrl = null,
    string? Bio = null,
    IReadOnlyList<string>? SocialLinks = null
);
