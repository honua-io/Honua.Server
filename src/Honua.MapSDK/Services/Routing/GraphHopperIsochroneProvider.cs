// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// GraphHopper Isochrone API provider
/// </summary>
public class GraphHopperIsochroneProvider : IIsochroneProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<GraphHopperIsochroneProvider>? _logger;
    private const string BaseUrl = "https://graphhopper.com/api/1/isochrone";

    public string ProviderKey => "graphhopper";
    public string DisplayName => "GraphHopper";
    public bool RequiresApiKey => true;
    public int MaxIntervals => 5;

    public List<TravelMode> SupportedTravelModes => new()
    {
        TravelMode.Driving,
        TravelMode.Walking,
        TravelMode.Cycling
    };

    public GraphHopperIsochroneProvider(
        HttpClient httpClient,
        string? apiKey = null,
        ILogger<GraphHopperIsochroneProvider>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey;
        _logger = logger;
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<IsochroneResult> CalculateAsync(
        IsochroneOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured())
        {
            throw new InvalidOperationException("GraphHopper API key is not configured");
        }

        if (options.Intervals.Count > MaxIntervals)
        {
            throw new ArgumentException($"GraphHopper supports maximum {MaxIntervals} intervals");
        }

        try
        {
            var profile = MapTravelMode(options.TravelMode);

            // GraphHopper expects time_limit in seconds
            var timeLimits = string.Join(",", options.Intervals.Select(i => i * 60));

            var url = $"{BaseUrl}?" +
                      $"point={options.Center[1]},{options.Center[0]}&" + // Note: lat,lon order
                      $"profile={profile}&" +
                      $"time_limit={timeLimits}&" +
                      $"buckets={options.Intervals.Count}&" +
                      $"key={_apiKey}";

            _logger?.LogDebug("Requesting GraphHopper isochrone: {Url}", url.Replace(_apiKey!, "[REDACTED]"));

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var geoJson = JsonDocument.Parse(json);

            var result = new IsochroneResult
            {
                Center = options.Center,
                TravelMode = options.TravelMode
            };

            // GraphHopper returns polygons in nested structure
            if (geoJson.RootElement.TryGetProperty("polygons", out var polygons))
            {
                var index = 0;
                foreach (var polygon in polygons.EnumerateArray())
                {
                    var properties = polygon.GetProperty("properties");
                    var bucket = properties.GetProperty("bucket").GetInt32();

                    // Determine interval from bucket
                    var interval = bucket < options.Intervals.Count ?
                        options.Intervals[bucket] : options.Intervals[^1];

                    var isoPolygon = new IsochronePolygon
                    {
                        Interval = interval,
                        Geometry = JsonSerializer.Deserialize<object>(polygon.GetProperty("geometry").GetRawText())!,
                        Color = index < options.Colors.Count ? options.Colors[index] : options.Colors[^1],
                        Opacity = options.Opacity
                    };

                    result.Polygons.Add(isoPolygon);
                    index++;
                }
            }

            // Sort by interval (smallest to largest)
            result.Polygons = result.Polygons.OrderBy(p => p.Interval).ToList();

            _logger?.LogInformation("Successfully calculated GraphHopper isochrone with {Count} polygons",
                result.Polygons.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "GraphHopper isochrone API request failed");
            throw new RoutingException("Failed to calculate isochrone from GraphHopper", ex)
            {
                RoutingEngine = "GraphHopper"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating GraphHopper isochrone");
            throw;
        }
    }

    private string MapTravelMode(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Driving => "car",
            TravelMode.Walking => "foot",
            TravelMode.Cycling => "bike",
            _ => "car"
        };
    }
}
