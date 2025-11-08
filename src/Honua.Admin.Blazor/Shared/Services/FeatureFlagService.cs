// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net.Http.Json;
using Honua.Admin.Blazor.Shared.Models;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for checking feature flags based on licensing.
/// Provides caching and convenient access to feature availability.
/// </summary>
public sealed class FeatureFlagService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FeatureFlagService> _logger;
    private FeatureFlagState? _cachedFlags;
    private DateTimeOffset _cacheExpiration;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public FeatureFlagService(IHttpClientFactory httpClientFactory, ILogger<FeatureFlagService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AdminApi");
        _logger = logger;
    }

    /// <summary>
    /// Checks if a specific feature is enabled.
    /// </summary>
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var state = await GetFeatureFlagsAsync(cancellationToken);
        return state?.IsFeatureEnabled(featureName) ?? false;
    }

    /// <summary>
    /// Gets all feature flags with caching.
    /// </summary>
    public async Task<FeatureFlagState?> GetFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_cachedFlags != null && DateTimeOffset.UtcNow < _cacheExpiration)
        {
            return _cachedFlags;
        }

        // Acquire lock to prevent thundering herd
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check cache after acquiring lock
            if (_cachedFlags != null && DateTimeOffset.UtcNow < _cacheExpiration)
            {
                return _cachedFlags;
            }

            // Fetch from API
            try
            {
                var response = await _httpClient.GetAsync("/admin/feature-flags", cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _cachedFlags = await response.Content.ReadFromJsonAsync<FeatureFlagState>(cancellationToken: cancellationToken);
                    _cacheExpiration = DateTimeOffset.UtcNow.Add(_cacheLifetime);
                    _logger.LogInformation("Loaded feature flags, tier: {Tier}", _cachedFlags?.Tier);
                }
                else
                {
                    _logger.LogWarning("Failed to fetch feature flags, status code: {StatusCode}", response.StatusCode);
                    // Return default (free tier) on error
                    _cachedFlags = FeatureFlagState.GetDefault();
                    _cacheExpiration = DateTimeOffset.UtcNow.AddMinutes(1); // Shorter cache on error
                }

                return _cachedFlags;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching feature flags");
                // Return default (free tier) on exception
                _cachedFlags = FeatureFlagState.GetDefault();
                _cacheExpiration = DateTimeOffset.UtcNow.AddMinutes(1); // Shorter cache on error
                return _cachedFlags;
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Clears the feature flag cache, forcing a reload on next access.
    /// </summary>
    public async Task ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            _cachedFlags = null;
            _cacheExpiration = DateTimeOffset.MinValue;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}
