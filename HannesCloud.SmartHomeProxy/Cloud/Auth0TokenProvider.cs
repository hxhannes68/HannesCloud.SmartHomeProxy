using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HannesCloud.SmartHomeProxy.Cloud;

public class Auth0TokenProvider(IOptions<CloudOptions> options, ILogger<Auth0TokenProvider> logger)
{
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        logger.LogDebug("Requesting new Auth0 M2M token");

        var auth0 = options.Value.Auth0;
        using var http = new HttpClient();

        var response = await http.PostAsJsonAsync(
            $"https://{auth0.Domain}/oauth/token",
            new
            {
                client_id = auth0.ClientId,
                client_secret = auth0.ClientSecret,
                audience = auth0.Audience,
                grant_type = "client_credentials"
            }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Auth0 token request failed {StatusCode}: {Body}", (int)response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        _cachedToken = result!.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn - 300);

        logger.LogInformation("Auth0 token acquired, expires in {ExpiresIn}s", result.ExpiresIn);
        return _cachedToken;
    }

    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
