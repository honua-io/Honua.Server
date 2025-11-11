// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;
using Honua.MapSDK.Services.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services;

/// <summary>
/// Service for managing isochrone generation with multiple providers
/// </summary>
public class IsochroneService
{
    private readonly Dictionary<string, IIsochroneProvider> _providers;
    private readonly ILogger<IsochroneService>? _logger;
    private readonly Dictionary<string, IsochroneResult> _cache;
    private readonly SemaphoreSlim _cacheLock;
    private const int MaxCacheSize = 50;

    public IsochroneService(
        IEnumerable<IIsochroneProvider> providers,
        ILogger<IsochroneService>? logger = null)
    {
        _providers = providers?.ToDictionary(p => p.ProviderKey, p => p)
            ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger;
        _cache = new Dictionary<string, IsochroneResult>();
        _cacheLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets all available isochrone providers
    /// </summary>
    public IEnumerable<IIsochroneProvider> GetAvailableProviders()
    {
        return _providers.Values.Where(p => p.IsConfigured());
    }

    /// <summary>
    /// Gets a provider by key or returns the first configured provider
    /// </summary>
    public IIsochroneProvider? GetProvider(string? providerKey = null)
    {
        if (string.IsNullOrEmpty(providerKey))
        {
            return _providers.Values.FirstOrDefault(p => p.IsConfigured());
        }

        if (_providers.TryGetValue(providerKey, out var provider) && provider.IsConfigured())
        {
            return provider;
        }

        return null;
    }

    /// <summary>
    /// Calculate isochrone using specified provider
    /// </summary>
    public async Task<IsochroneResult> CalculateAsync(
        IsochroneOptions options,
        string? providerKey = null,
        CancellationToken cancellationToken = default)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Validate options
        if (options.Center == null || options.Center.Length != 2)
        {
            throw new ArgumentException("Invalid center coordinates", nameof(options));
        }

        if (options.Intervals == null || options.Intervals.Count == 0)
        {
            throw new ArgumentException("At least one interval is required", nameof(options));
        }

        // Check cache
        var cacheKey = GenerateCacheKey(options, providerKey);
        var (found, cachedResult) = await TryGetFromCacheAsync(cacheKey);
        if (found)
        {
            _logger?.LogDebug("Returning cached isochrone result for key: {Key}", cacheKey);
            return cachedResult;
        }

        // Get provider
        var provider = GetProvider(providerKey);
        if (provider == null)
        {
            throw new InvalidOperationException(
                $"No configured isochrone provider available" +
                (string.IsNullOrEmpty(providerKey) ? "" : $" for key: {providerKey}"));
        }

        _logger?.LogInformation("Calculating isochrone with provider {Provider} for {Intervals} intervals",
            provider.DisplayName, options.Intervals.Count);

        try
        {
            // Calculate isochrone
            var result = await provider.CalculateAsync(options, cancellationToken);

            // Cache result
            await AddToCacheAsync(cacheKey, result);

            _logger?.LogInformation("Successfully calculated isochrone with {Count} polygons",
                result.Polygons.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to calculate isochrone with provider {Provider}",
                provider.DisplayName);
            throw;
        }
    }

    /// <summary>
    /// Export isochrone as GeoJSON
    /// </summary>
    public string ExportAsGeoJson(IsochroneResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        var features = result.Polygons.Select(polygon => new
        {
            type = "Feature",
            properties = new
            {
                interval = polygon.Interval,
                color = polygon.Color,
                opacity = polygon.Opacity,
                area = polygon.Area,
                travelMode = result.TravelMode.ToString()
            },
            geometry = polygon.Geometry
        }).ToList();

        var featureCollection = new
        {
            type = "FeatureCollection",
            properties = new
            {
                center = result.Center,
                travelMode = result.TravelMode.ToString(),
                generatedAt = DateTime.UtcNow.ToString("O")
            },
            features
        };

        return System.Text.Json.JsonSerializer.Serialize(featureCollection, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Clear the isochrone cache
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _cache.Clear();
            _logger?.LogDebug("Isochrone cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Get provider configuration information
    /// </summary>
    public IsochroneProviderConfig GetProviderConfig(string providerKey)
    {
        var provider = GetProvider(providerKey);
        if (provider == null)
        {
            throw new ArgumentException($"Provider not found: {providerKey}");
        }

        return new IsochroneProviderConfig
        {
            Provider = Enum.Parse<IsochroneProvider>(providerKey, ignoreCase: true),
            DisplayName = provider.DisplayName,
            IsEnabled = provider.IsConfigured(),
            MaxIntervals = provider.MaxIntervals,
            MaxIntervalMinutes = 60, // Default
            SupportedTravelModes = provider.SupportedTravelModes
        };
    }

    private async Task<(bool found, IsochroneResult result)> TryGetFromCacheAsync(string key)
    {
        await _cacheLock.WaitAsync();
        try
        {
            if (_cache.TryGetValue(key, out var result))
            {
                return (true, result);
            }
            return (false, null!);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task AddToCacheAsync(string key, IsochroneResult result)
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

            _cache[key] = result;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private string GenerateCacheKey(IsochroneOptions options, string? providerKey)
    {
        var intervals = string.Join(",", options.Intervals.OrderBy(i => i));
        var center = $"{options.Center[0]:F5},{options.Center[1]:F5}";
        var provider = providerKey ?? "default";

        return $"{provider}_{options.TravelMode}_{center}_{intervals}_{options.Type}";
    }
}
