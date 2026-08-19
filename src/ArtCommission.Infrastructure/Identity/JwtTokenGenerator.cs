using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ArtCommission.Infrastructure.Identity;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _configuration;
    private readonly AppDbContext _dbContext;

    public JwtTokenGenerator(IConfiguration configuration, AppDbContext dbContext)
    {
        _configuration = configuration;
        _dbContext = dbContext;
    }

    public async Task<TokenDto> GenerateTokensAsync(UserDto user, IEnumerable<string> roles, string? clientIp, CancellationToken cancellationToken = default)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "Default_Secret_Key_For_Development_Only_Must_Be_Long_256_Bits";
        var issuer = _configuration["Jwt:Issuer"] ?? "ArtCommissionAPI";
        var audience = _configuration["Jwt:Audience"] ?? "ArtCommissionClient";
        var expiryMinutes = double.TryParse(_configuration["Jwt:ExpiryMinutes"], out var min) ? min : 15;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessTokenExpiresAt.UtcDateTime,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // Generate Refresh Token
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenHash = HashToken(rawRefreshToken);
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = refreshTokenExpiresAt,
            CreatedByIp = clientIp
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TokenDto(accessToken, rawRefreshToken, accessTokenExpiresAt, refreshTokenExpiresAt);
    }

    public async Task<(bool Success, AuthResponseDto? AuthResponse, string[] Errors)> RefreshTokenAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        var inputHash = HashToken(refreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == inputHash, cancellationToken);

        if (existingToken == null || !existingToken.IsActive || existingToken.User.IsDeleted)
        {
            // Security: If revoked token is reused, revoke all active tokens of the user as reuse detection
            if (existingToken is { RevokedAt: not null })
            {
                var activeTokens = await _dbContext.RefreshTokens
                    .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var activeToken in activeTokens)
                {
                    activeToken.RevokedAt = DateTimeOffset.UtcNow;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return (false, null, new[] { "Invalid or expired refresh token." });
        }

        // Revoke current token (Rotation)
        var newRawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var newRefreshTokenHash = HashToken(newRawRefreshToken);

        existingToken.RevokedAt = DateTimeOffset.UtcNow;
        existingToken.ReplacedByTokenHash = newRefreshTokenHash;

        // Get User Roles
        var userRoles = await _dbContext.UserRoles
            .Where(ur => ur.UserId == existingToken.User.Id)
            .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
            .ToListAsync(cancellationToken);

        var userDto = new UserDto(
            existingToken.User.Id,
            existingToken.User.Email!,
            existingToken.User.FullName,
            existingToken.User.IsVerified,
            existingToken.User.CreatedAt
        );

        var secretKey = _configuration["Jwt:SecretKey"] ?? "Default_Secret_Key_For_Development_Only_Must_Be_Long_256_Bits";
        var issuer = _configuration["Jwt:Issuer"] ?? "ArtCommissionAPI";
        var audience = _configuration["Jwt:Audience"] ?? "ArtCommissionClient";
        var expiryMinutes = double.TryParse(_configuration["Jwt:ExpiryMinutes"], out var min) ? min : 15;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userDto.Id.ToString()),
            new(ClaimTypes.Email, userDto.Email),
            new(ClaimTypes.Name, userDto.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var accessTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessTokenExpiresAt.UtcDateTime,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(securityToken);


        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = userDto.Id,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = refreshTokenExpiresAt,
            CreatedByIp = clientIp
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var tokenDto = new TokenDto(accessToken, newRawRefreshToken, accessTokenExpiresAt, refreshTokenExpiresAt);
        var authResponse = new AuthResponseDto(userDto, userRoles, tokenDto);

        return (true, authResponse, Array.Empty<string>());
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        var inputHash = HashToken(refreshToken);

        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == inputHash, cancellationToken);

        if (token == null || !token.IsActive)
        {
            return false;
        }

        token.RevokedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
