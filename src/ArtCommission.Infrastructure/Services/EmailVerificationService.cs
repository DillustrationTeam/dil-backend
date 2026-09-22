using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(15);

    private readonly IConfiguration _configuration;

    public EmailVerificationService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateTicket(string email)
    {
        var normalizedEmail = Normalize(email);
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(TicketLifetime).ToUnixTimeSeconds();
        var payload = $"{normalizedEmail}|{expiresAtUnix}";
        var signature = Sign(payload);

        return Base64UrlEncode($"{payload}|{signature}");
    }

    public bool ValidateTicket(string email, string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Base64UrlDecode(ticket);
        }
        catch (FormatException)
        {
            return false;
        }

        var parts = decoded.Split('|');
        if (parts.Length != 3)
        {
            return false;
        }

        var ticketEmail = parts[0];
        var expiresAtUnixText = parts[1];
        var signature = parts[2];

        if (!long.TryParse(expiresAtUnixText, out var expiresAtUnix))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            return false;
        }

        if (!string.Equals(ticketEmail, Normalize(email), StringComparison.Ordinal))
        {
            return false;
        }

        var payload = $"{ticketEmail}|{expiresAtUnixText}";
        var expectedSignature = Sign(payload);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature));
    }

    private string Sign(string payload)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "Default_Secret_Key_For_Development_Only_Must_Be_Long_256_Bits";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string Base64UrlEncode(string value)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }
}
