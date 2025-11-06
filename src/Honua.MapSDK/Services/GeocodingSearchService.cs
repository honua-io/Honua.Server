// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for managing geocoding search operations with caching and history.
/// </summary>
public class GeocodingSearchService
{
    private readonly IEnumerable<IGeocodingProvider> _providers;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<GeocodingSearchService> _logger;
    private readonly Dictionary<string, List<SearchResult>> _cache;
    private readonly SemaphoreSlim _cacheLock;
    private const string HistoryStorageKey = "honua.geocoding.history";
    private const int MaxCacheSize = 100;

    public GeocodingSearchService(
        IEnumerable<IGeocodingProvider> providers,
        IJSRuntime jsRuntime,
        ILogger<GeocodingSearchService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new Dictionary<string, List<SearchResult>>();
        _cacheLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets all available geocoding providers.
    /// </summary>
    public IEnumerable<IGeocodingProvider> GetAvailableProviders() => _providers;

    /// <summary>
    /// Gets a provider by key or returns the first available provider.
    /// </summary>
    public IGeocodingProvider? GetProvider(string? providerKey = null)
    {
        if (string.IsNullOrEmpty(providerKey))
        {
            return _providers.FirstOrDefault();
        }

        return _providers.FirstOrDefault(p =>
            p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Searches for locations using the specified query and options.
    /// </summary>
    public async Task<List<SearchResult>> SearchAsync(
        string query,
        GeocodingSearchOptions options,
        string? providerKey = null,
        double[]? mapBounds = null,
        double[]? mapCenter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<SearchResult>();
        }

        // Check cache first
        var cacheKey = GenerateCacheKey(query, providerKey, mapBounds);
        if (await TryGetFromCacheAsync(cacheKey, out var cachedResults))
        {
            _logger.LogDebug("Returning cached results for query: {Query}", query);
            return cachedResults;
        }

        // Get provider
        var provider = GetProvider(providerKey);
        if (provider == null)
        {
            _logger.LogWarning("No geocoding provider available");
            return new List<SearchResult>();
        }

        try
        {
            _logger.LogDebug("Geocoding query '{Query}' with provider {Provider}",
                query, provider.ProviderKey);

            // Build request
            var request = new GeocodingRequest
            {
                Query = query,
                MaxResults = options.MaxResults,
                BoundingBox = options.BiasToViewport && mapBounds != null ? mapBounds : null,
                BiasLocation = options.BiasToViewport && mapCenter != null ? mapCenter : null
            };

            // Execute search
            var response = await provider.GeocodeAsync(request, cancellationToken);

            // Convert to search results
            var searchResults = response.Results
                .Select(r => SearchResult.FromGeocodingResult(
                    r,
                    provider.ProviderKey,
                    options.BiasToViewport ? mapCenter : null))
                .OrderByDescending(r => r.RelevanceScore)
                .ToList();

            // Cache results
            await AddToCacheAsync(cacheKey, searchResults);

            return searchResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Geocoding search failed for query: {Query}", query);
            return new List<SearchResult>();
        }
    }

    /// <summary>
    /// Searches across multiple providers and merges results.
    /// </summary>
    public async Task<List<SearchResult>> SearchMultiProviderAsync(
        string query,
        GeocodingSearchOptions options,
        double[]? mapBounds = null,
        double[]? mapCenter = null,
        CancellationToken cancellationToken = default)
    {
        var allResults = new List<SearchResult>();
        var tasks = _providers.Select(provider =>
            SearchAsync(query, options, provider.ProviderKey, mapBounds, mapCenter, cancellationToken));

        var results = await Task.WhenAll(tasks);

        // Merge and deduplicate results
        foreach (var providerResults in results)
        {
            allResults.AddRange(providerResults);
        }

        // Deduplicate by proximity (results within 100m are considered duplicates)
        var deduped = DeduplicateResults(allResults, proximityThresholdMeters: 100.0);

        // Re-sort by relevance
        return deduped.OrderByDescending(r => r.RelevanceScore).Take(options.MaxResults).ToList();
    }

    /// <summary>
    /// Reverse geocodes coordinates to an address.
    /// </summary>
    public async Task<SearchResult?> ReverseGeocodeAsync(
        double longitude,
        double latitude,
        string? providerKey = null,
        CancellationToken cancellationToken = default)
    {
        var provider = GetProvider(providerKey);
        if (provider == null)
        {
            _logger.LogWarning("No geocoding provider available");
            return null;
        }

        try
        {
            var request = new ReverseGeocodingRequest
            {
                Longitude = longitude,
                Latitude = latitude
            };

            var response = await provider.ReverseGeocodeAsync(request, cancellationToken);
            var firstResult = response.Results.FirstOrDefault();

            if (firstResult == null)
            {
                return null;
            }

            return SearchResult.FromGeocodingResult(firstResult, provider.ProviderKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reverse geocoding failed for location: {Lon}, {Lat}",
                longitude, latitude);
            return null;
        }
    }

    /// <summary>
    /// Gets recent search history from localStorage.
    /// </summary>
    public async Task<List<SearchHistoryItem>> GetSearchHistoryAsync()
    {
        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", HistoryStorageKey);
            if (string.IsNullOrEmpty(json))
            {
                return new List<SearchHistoryItem>();
            }

            var history = System.Text.Json.JsonSerializer.Deserialize<List<SearchHistoryItem>>(json);
            return history ?? new List<SearchHistoryItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve search history");
            return new List<SearchHistoryItem>();
        }
    }

    /// <summary>
    /// Adds a search to history.
    /// </summary>
    public async Task AddToHistoryAsync(string query, SearchResult result, int maxHistoryItems)
    {
        try
        {
            var history = await GetSearchHistoryAsync();

            // Remove duplicate if exists
            history.RemoveAll(h => h.Query.Equals(query, StringComparison.OrdinalIgnoreCase));

            // Add new item at the beginning
            history.Insert(0, new SearchHistoryItem
            {
                Query = query,
                FormattedAddress = result.DisplayAddress,
                Longitude = result.Result.Longitude,
                Latitude = result.Result.Latitude,
                Timestamp = DateTime.UtcNow
            });

            // Limit history size
            if (history.Count > maxHistoryItems)
            {
                history = history.Take(maxHistoryItems).ToList();
            }

            // Save to localStorage
            var json = System.Text.Json.JsonSerializer.Serialize(history);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", HistoryStorageKey, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add search to history");
        }
    }

    /// <summary>
    /// Clears search history.
    /// </summary>
    public async Task ClearHistoryAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", HistoryStorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear search history");
        }
    }

    /// <summary>
    /// Clears the search result cache.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cache.Clear();
            _logger.LogDebug("Search cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<bool> TryGetFromCacheAsync(string key, out List<SearchResult> results)
    {
        results = new List<SearchResult>();
        await _cacheLock.WaitAsync();
        try
        {
            return _cache.TryGetValue(key, out results!);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task AddToCacheAsync(string key, List<SearchResult> results)
    {
        await _cacheLock.WaitAsync();
        try
        {
            // Simple LRU: if cache is full, remove oldest entry
            if (_cache.Count >= MaxCacheSize)
            {
                var oldestKey = _cache.Keys.First();
                _cache.Remove(oldestKey);
            }

            _cache[key] = results;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private string GenerateCacheKey(string query, string? providerKey, double[]? bounds)
    {
        var boundsStr = bounds != null
            ? $"_{bounds[0]:F2}_{bounds[1]:F2}_{bounds[2]:F2}_{bounds[3]:F2}"
            : string.Empty;

        return $"{providerKey ?? "default"}_{query.ToLowerInvariant()}{boundsStr}";
    }

    private List<SearchResult> DeduplicateResults(
        List<SearchResult> results,
        double proximityThresholdMeters)
    {
        var deduped = new List<SearchResult>();

        foreach (var result in results.OrderByDescending(r => r.RelevanceScore))
        {
            var isDuplicate = deduped.Any(existing =>
            {
                var distance = CalculateDistance(
                    existing.Result.Latitude, existing.Result.Longitude,
                    result.Result.Latitude, result.Result.Longitude);

                return distance < proximityThresholdMeters;
            });

            if (!isDuplicate)
            {
                deduped.Add(result);
            }
        }

        return deduped;
    }

    private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000.0;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Represents a search history item.
/// </summary>
public sealed record SearchHistoryItem
{
    public required string Query { get; init; }
    public required string FormattedAddress { get; init; }
    public required double Longitude { get; init; }
    public required double Latitude { get; init; }
    public DateTime Timestamp { get; init; }
}
