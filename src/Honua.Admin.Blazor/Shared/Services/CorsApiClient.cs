// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// API client for CORS configuration management
/// </summary>
public sealed class CorsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CorsApiClient> _logger;

    public CorsApiClient(IHttpClientFactory httpClientFactory, ILogger<CorsApiClient> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Gets the current CORS configuration
    /// </summary>
    public async Task<CorsConfiguration?> GetCorsConfigurationAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/admin/server/cors", cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CorsConfigurationResponse>(cancellationToken: cancellationToken);
            return result?.Cors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching CORS configuration");
            throw;
        }
    }

    /// <summary>
    /// Updates the CORS configuration
    /// </summary>
    public async Task<CorsConfiguration?> UpdateCorsConfigurationAsync(
        CorsConfiguration config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new UpdateCorsRequest { Cors = config };
            var response = await _httpClient.PutAsJsonAsync("/admin/server/cors", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CorsConfigurationResponse>(cancellationToken: cancellationToken);
            return result?.Cors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating CORS configuration");
            throw;
        }
    }

    /// <summary>
    /// Tests CORS configuration with a specific origin
    /// </summary>
    public async Task<CorsTestResult?> TestCorsAsync(
        string origin,
        string? method = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = $"?origin={Uri.EscapeDataString(origin)}";
            if (!string.IsNullOrWhiteSpace(method))
            {
                queryParams += $"&method={Uri.EscapeDataString(method)}";
            }

            var response = await _httpClient.GetAsync($"/admin/server/cors/test{queryParams}", cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CorsTestResult>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing CORS configuration");
            throw;
        }
    }
}

/// <summary>
/// Result of CORS configuration test
/// </summary>
public sealed class CorsTestResult
{
    /// <summary>Whether the origin is allowed</summary>
    public bool IsAllowed { get; set; }

    /// <summary>Whether the method is allowed</summary>
    public bool MethodAllowed { get; set; }

    /// <summary>Reason why request was allowed/denied</summary>
    public string? Reason { get; set; }

    /// <summary>Matched pattern (for wildcard origins)</summary>
    public string? MatchedPattern { get; set; }
}
