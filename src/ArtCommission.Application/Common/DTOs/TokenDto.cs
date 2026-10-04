namespace ArtCommission.Application.Common.DTOs;

public record TokenDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    Guid SessionId
);
