// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Honua.Server.Integration.Tests.Helpers;

/// <summary>
/// Helper methods for making HTTP requests in integration tests.
/// </summary>
public static class HttpClientHelper
{
    /// <summary>
    /// Creates an HttpContent object from a JSON object.
    /// </summary>
    public static HttpContent CreateJsonContent(object content)
    {
        var json = JsonSerializer.Serialize(content);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Creates an HttpContent object from a JSON string.
    /// </summary>
    public static HttpContent CreateJsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// Reads the response as a JSON object of the specified type.
    /// </summary>
    public static async Task<T?> ReadAsJsonAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return default;
        }
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Reads the response as a raw JSON string.
    /// </summary>
    public static async Task<string> ReadAsJsonStringAsync(HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Adds authentication headers to the HTTP client.
    /// </summary>
    public static void AddBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Adds accept headers for GeoJSON responses.
    /// </summary>
    public static void AddGeoJsonAcceptHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/geo+json"));
    }

    /// <summary>
    /// Adds accept headers for JSON responses.
    /// </summary>
    public static void AddJsonAcceptHeader(HttpClient client)
    {
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Builds a query string from a dictionary of parameters.
    /// </summary>
    public static string BuildQueryString(Dictionary<string, string?> parameters)
    {
        var validParams = parameters.Where(p => p.Value != null);
        return string.Join("&", validParams.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));
    }

    /// <summary>
    /// Appends query parameters to a URL.
    /// </summary>
    public static string AppendQueryString(string baseUrl, Dictionary<string, string?> parameters)
    {
        var queryString = BuildQueryString(parameters);
        if (string.IsNullOrEmpty(queryString))
        {
            return baseUrl;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}{queryString}";
    }
}
