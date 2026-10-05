using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.Services;

public class ExternalRegistrationTicketService : IExternalRegistrationTicketService
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(20);

    private readonly IConfiguration _configuration;

    public ExternalRegistrationTicketService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateTicket(string provider, string providerKey, string email, bool emailVerified, string? fullName)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(TicketLifetime).ToUnixTimeSeconds();
        var payload = string.Join(
            '|',
            Base64UrlEncode(provider),
            Base64UrlEncode(providerKey),
            Base64UrlEncode(email),
            emailVerified ? "1" : "0",
            Base64UrlEncode(fullName ?? string.Empty),
            expiresAtUnix.ToString());

        var signature = Sign(payload);
        return Base64UrlEncode($"{payload}|{signature}");
    }

    public bool TryValidateTicket(
        string ticket,
        out string provider,
        out string providerKey,
        out string email,
        out bool emailVerified,
        out string? fullName)
    {
        provider = string.Empty;
        providerKey = string.Empty;
        email = string.Empty;
        emailVerified = false;
        fullName = null;

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
        if (parts.Length != 7)
        {
            return false;
        }

        var providerPart = parts[0];
        var providerKeyPart = parts[1];
        var emailPart = parts[2];
        var emailVerifiedPart = parts[3];
        var fullNamePart = parts[4];
        var expiresAtUnixText = parts[5];
        var signature = parts[6];

        if (!long.TryParse(expiresAtUnixText, out var expiresAtUnix) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            return false;
        }

        var payload = string.Join('|', providerPart, providerKeyPart, emailPart, emailVerifiedPart, fullNamePart, expiresAtUnixText);
        var expectedSignature = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signature),
            Encoding.UTF8.GetBytes(expectedSignature)))
        {
            return false;
        }

        try
        {
            provider = Base64UrlDecode(providerPart);
            providerKey = Base64UrlDecode(providerKeyPart);
            email = Base64UrlDecode(emailPart);
            emailVerified = emailVerifiedPart == "1";
            var decodedFullName = Base64UrlDecode(fullNamePart);
            fullName = string.IsNullOrEmpty(decodedFullName) ? null : decodedFullName;
        }
        catch (FormatException)
        {
            return false;
        }

        return true;
    }

    private string Sign(string payload)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "Default_Secret_Key_For_Development_Only_Must_Be_Long_256_Bits";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

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
