using ArtCommission.Application.Common.DTOs;

namespace ArtCommission.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    Task<TokenDto> GenerateTokensAsync(UserDto user, IEnumerable<string> roles, string? clientIp, CancellationToken cancellationToken = default);
    Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> RefreshTokenAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
}
