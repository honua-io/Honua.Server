// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.MapSDK.Services.Esri;

/// <summary>
/// Service for managing Esri token authentication
/// </summary>
public class EsriAuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, EsriTokenInfo> _tokenCache = new();
    private readonly object _lock = new();

    public EsriAuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Generate a token for ArcGIS Server
    /// </summary>
    /// <param name="tokenUrl">Token generation URL (e.g., https://server/arcgis/tokens/generateToken)</param>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <param name="expiration">Token expiration in minutes (default 60)</param>
    /// <param name="referer">Referer URL (optional, some servers require this)</param>
    /// <returns>Token information</returns>
    public async Task<EsriTokenInfo> GenerateTokenAsync(
        string tokenUrl,
        string username,
        string password,
        int expiration = 60,
        string? referer = null)
    {
        // Check cache first
        var cacheKey = $"{tokenUrl}|{username}";
        lock (_lock)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var cachedToken))
            {
                if (cachedToken.IsValid())
                {
                    return cachedToken;
                }
                _tokenCache.Remove(cacheKey);
            }
        }

        // Generate new token
        var parameters = new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password,
            ["expiration"] = expiration.ToString(),
            ["f"] = "json"
        };

        if (!string.IsNullOrEmpty(referer))
        {
            parameters["referer"] = referer;
        }

        var content = new FormUrlEncodedContent(parameters);
        var response = await _httpClient.PostAsync(tokenUrl, content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<EsriTokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tokenResponse?.Token == null)
        {
            throw new InvalidOperationException("Failed to generate token: No token in response");
        }

        var tokenInfo = new EsriTokenInfo
        {
            Token = tokenResponse.Token,
            Expires = tokenResponse.Expires,
            Ssl = tokenResponse.Ssl
        };

        // Cache the token
        lock (_lock)
        {
            _tokenCache[cacheKey] = tokenInfo;
        }

        return tokenInfo;
    }

    /// <summary>
    /// Get a cached token if available and valid
    /// </summary>
    public EsriTokenInfo? GetCachedToken(string tokenUrl, string username)
    {
        var cacheKey = $"{tokenUrl}|{username}";
        lock (_lock)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var token) && token.IsValid())
            {
                return token;
            }
        }
        return null;
    }

    /// <summary>
    /// Clear all cached tokens
    /// </summary>
    public void ClearTokenCache()
    {
        lock (_lock)
        {
            _tokenCache.Clear();
        }
    }

    /// <summary>
    /// Append token to URL query string
    /// </summary>
    public static string AppendTokenToUrl(string url, string token)
    {
        var separator = url.Contains("?") ? "&" : "?";
        return $"{url}{separator}token={Uri.EscapeDataString(token)}";
    }

    /// <summary>
    /// Extract token URL from service URL
    /// For ArcGIS Server: https://server/arcgis/rest/services/... -> https://server/arcgis/tokens/generateToken
    /// For ArcGIS Online: Use OAuth (not implemented in this basic version)
    /// </summary>
    public static string GetTokenUrlFromServiceUrl(string serviceUrl)
    {
        var uri = new Uri(serviceUrl);
        var baseUrl = $"{uri.Scheme}://{uri.Authority}";

        // Check if it's ArcGIS Server
        if (serviceUrl.Contains("/arcgis/rest/services/", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/arcgis/tokens/generateToken";
        }

        // For ArcGIS Online, OAuth is recommended (not implemented in basic version)
        if (serviceUrl.Contains("arcgis.com", StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseUrl}/sharing/rest/generateToken";
        }

        throw new InvalidOperationException("Cannot determine token URL from service URL");
    }
}

/// <summary>
/// Token information
/// </summary>
public class EsriTokenInfo
{
    /// <summary>
    /// Token string
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// Expiration timestamp (milliseconds since epoch)
    /// </summary>
    public long Expires { get; set; }

    /// <summary>
    /// SSL required
    /// </summary>
    public bool Ssl { get; set; }

    /// <summary>
    /// Check if token is still valid (with 5-minute buffer)
    /// </summary>
    public bool IsValid()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var buffer = 5 * 60 * 1000; // 5 minutes
        return now < (Expires - buffer);
    }

    /// <summary>
    /// Get expiration as DateTime
    /// </summary>
    public DateTime GetExpirationDateTime()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(Expires).DateTime;
    }
}

/// <summary>
/// Token response from generateToken endpoint
/// </summary>
internal class EsriTokenResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("expires")]
    public long Expires { get; set; }

    [JsonPropertyName("ssl")]
    public bool Ssl { get; set; }
}
