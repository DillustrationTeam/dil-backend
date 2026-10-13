using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.ExternalServices.R2;

public static class LocalPresignedUrlSigner
{
    public static string GetSigningKey(IConfiguration configuration) =>
        configuration["LocalStorage:SigningKey"]
        ?? configuration["Jwt:SecretKey"]
        ?? throw new InvalidOperationException("LocalStorage:SigningKey or Jwt:SecretKey must be configured.");

    public static string Sign(string privateKey, long expiresAtUnixSeconds, string signingKey)
    {
        var payload = $"{NormalizeKey(privateKey)}\n{expiresAtUnixSeconds}";
        return Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingKey),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool IsValid(
        string privateKey,
        string? expires,
        string? signature,
        string signingKey,
        DateTimeOffset? now = null)
    {
        if (!long.TryParse(expires, out var expiresAt)
            || expiresAt < (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()
            || string.IsNullOrWhiteSpace(signature))
            return false;

        var expected = Sign(privateKey, expiresAt, signingKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(signature));
    }

    public static string NormalizeKey(string privateKey) =>
        privateKey.Replace('\\', '/').TrimStart('/');
}
