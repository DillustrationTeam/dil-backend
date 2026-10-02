using ArtCommission.Application.Common.DTOs;

namespace ArtCommission.Application.Common.Interfaces;

public interface IJwtTokenGenerator
{
    Task<TokenDto> GenerateTokensAsync(UserDto user, IEnumerable<string> roles, string? clientIp, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> RefreshTokenAsync(string refreshToken, string? clientIp, string? userAgent = null, CancellationToken cancellationToken = default);
    Task<bool> RevokeTokenAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);

    /// <summary>Thu hồi mọi refresh token đang active của user, trừ phiên có Id = exceptSessionId (nếu có). Trả về số lượng đã thu hồi.</summary>
    Task<int> RevokeAllTokensExceptAsync(Guid userId, Guid? exceptSessionId, CancellationToken cancellationToken = default);
}
