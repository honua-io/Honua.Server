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
/// Mapbox Isochrone API provider
/// </summary>
public class MapboxIsochroneProvider : IIsochroneProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<MapboxIsochroneProvider>? _logger;
    private const string BaseUrl = "https://api.mapbox.com/isochrone/v1/mapbox";

    public string ProviderKey => "mapbox";
    public string DisplayName => "Mapbox";
    public bool RequiresApiKey => true;
    public int MaxIntervals => 4;

    public List<TravelMode> SupportedTravelModes => new()
    {
        TravelMode.Driving,
        TravelMode.Walking,
        TravelMode.Cycling
    };

    public MapboxIsochroneProvider(
        HttpClient httpClient,
        string? apiKey = null,
        ILogger<MapboxIsochroneProvider>? logger = null)
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
            throw new InvalidOperationException("Mapbox API key is not configured");
        }

        if (options.Intervals.Count > MaxIntervals)
        {
            throw new ArgumentException($"Mapbox supports maximum {MaxIntervals} intervals");
        }

        try
        {
            var profile = MapTravelMode(options.TravelMode);
            var coordinates = $"{options.Center[0]},{options.Center[1]}";

            // Convert minutes to seconds for Mapbox
            var contours = string.Join(",", options.Intervals.Select(i => i * 60));

            var url = $"{BaseUrl}/{profile}/{coordinates}?" +
                      $"contours_minutes={string.Join(",", options.Intervals)}&" +
                      $"polygons=true&" +
                      $"denoise={options.Smoothing}&" +
                      $"access_token={_apiKey}";

            _logger?.LogDebug("Requesting Mapbox isochrone: {Url}", url.Replace(_apiKey!, "[REDACTED]"));

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var geoJson = JsonDocument.Parse(json);

            var result = new IsochroneResult
            {
                Center = options.Center,
                TravelMode = options.TravelMode
            };

            var features = geoJson.RootElement.GetProperty("features");
            var index = 0;

            foreach (var feature in features.EnumerateArray())
            {
                var properties = feature.GetProperty("properties");
                var contour = properties.GetProperty("contour").GetInt32();
                var interval = contour / 60; // Convert seconds back to minutes

                var polygon = new IsochronePolygon
                {
                    Interval = interval,
                    Geometry = JsonSerializer.Deserialize<object>(feature.GetProperty("geometry").GetRawText())!,
                    Color = index < options.Colors.Count ? options.Colors[index] : options.Colors[^1],
                    Opacity = options.Opacity
                };

                result.Polygons.Add(polygon);
                index++;
            }

            // Sort by interval (smallest to largest)
            result.Polygons = result.Polygons.OrderBy(p => p.Interval).ToList();

            _logger?.LogInformation("Successfully calculated Mapbox isochrone with {Count} polygons",
                result.Polygons.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Mapbox isochrone API request failed");
            throw new RoutingException("Failed to calculate isochrone from Mapbox", ex)
            {
                RoutingEngine = "Mapbox"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating Mapbox isochrone");
            throw;
        }
    }

    private string MapTravelMode(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Driving => "driving",
            TravelMode.Walking => "walking",
            TravelMode.Cycling => "cycling",
            _ => "driving"
        };
    }
}
