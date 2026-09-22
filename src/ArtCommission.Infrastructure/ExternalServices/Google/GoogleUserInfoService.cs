using System.Net.Http.Headers;
using System.Text.Json;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ArtCommission.Infrastructure.ExternalServices.Google;

public class GoogleUserInfoService : IGoogleUserInfoService
{
    private const string TokenInfoUrl = "https://www.googleapis.com/oauth2/v3/tokeninfo";
    private const string UserInfoUrl = "https://www.googleapis.com/oauth2/v3/userinfo";

    private readonly HttpClient _httpClient;
    private readonly GoogleAuthOptions _options;

    public GoogleUserInfoService(HttpClient httpClient, IOptions<GoogleAuthOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GoogleUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        // 1. Chặn access token được cấp cho một app Google khác bị dùng để mạo danh:
        // xác nhận "aud" (audience) của token khớp đúng Client ID của app này.
        var tokenInfoResponse = await _httpClient.GetAsync(
            $"{TokenInfoUrl}?access_token={Uri.EscapeDataString(accessToken)}", cancellationToken);
        if (!tokenInfoResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var tokenInfoDoc = JsonDocument.Parse(await tokenInfoResponse.Content.ReadAsStringAsync(cancellationToken));
        var audience = tokenInfoDoc.RootElement.TryGetProperty("aud", out var audElement) ? audElement.GetString() : null;
        if (_options.IsConfigured && !string.Equals(audience, _options.ClientId, StringComparison.Ordinal))
        {
            return null;
        }

        // 2. Lấy profile thật từ Google bằng chính access_token đó.
        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        userInfoRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var userInfoResponse = await _httpClient.SendAsync(userInfoRequest, cancellationToken);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            return null;
        }

        using var userInfoDoc = JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync(cancellationToken));
        var root = userInfoDoc.RootElement;

        var sub = root.TryGetProperty("sub", out var subEl) ? subEl.GetString() : null;
        var email = root.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        var name = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
        var emailVerified = ReadBoolLoosely(root, "email_verified");

        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new GoogleUserInfo(sub, email, emailVerified, name);
    }

    /// <summary>Google trả "email_verified" là bool JSON thật ở /userinfo nhưng là chuỗi "true"/"false" ở /tokeninfo — parse phòng thủ cả hai dạng.</summary>
    private static bool ReadBoolLoosely(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var parsed) && parsed,
            _ => false
        };
    }
}
