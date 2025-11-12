// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// OpenRouteService Isochrone API provider
/// </summary>
public class OpenRouteServiceIsochroneProvider : IIsochroneProvider
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly ILogger<OpenRouteServiceIsochroneProvider>? _logger;
    private const string BaseUrl = "https://api.openrouteservice.org/v2/isochrones";

    public string ProviderKey => "openrouteservice";
    public string DisplayName => "OpenRouteService";
    public bool RequiresApiKey => true;
    public int MaxIntervals => 10;

    public List<TravelMode> SupportedTravelModes => new()
    {
        TravelMode.Driving,
        TravelMode.Walking,
        TravelMode.Cycling
    };

    public OpenRouteServiceIsochroneProvider(
        HttpClient httpClient,
        string? apiKey = null,
        ILogger<OpenRouteServiceIsochroneProvider>? logger = null)
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
            throw new InvalidOperationException("OpenRouteService API key is not configured");
        }

        if (options.Intervals.Count > MaxIntervals)
        {
            throw new ArgumentException($"OpenRouteService supports maximum {MaxIntervals} intervals");
        }

        try
        {
            var profile = MapTravelMode(options.TravelMode);
            var url = $"{BaseUrl}/{profile}";

            // Build request body
            var requestBody = new
            {
                locations = new[] { new[] { options.Center[0], options.Center[1] } },
                range = options.Intervals.Select(i => i * 60).ToArray(), // Convert to seconds
                interval = options.Intervals.Count > 1 ? options.Intervals[1] - options.Intervals[0] : options.Intervals[0],
                range_type = "time",
                location_type = "start",
                attributes = new[] { "area" },
                smoothing = options.Smoothing
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger?.LogDebug("Requesting OpenRouteService isochrone for profile: {Profile}", profile);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = content
            };
            request.Headers.Add("Authorization", _apiKey);
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var geoJson = JsonDocument.Parse(responseJson);

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
                var value = properties.GetProperty("value").GetInt32();
                var interval = value / 60; // Convert seconds to minutes

                var area = 0.0;
                if (properties.TryGetProperty("area", out var areaProperty))
                {
                    area = areaProperty.GetDouble();
                }

                var polygon = new IsochronePolygon
                {
                    Interval = interval,
                    Geometry = JsonSerializer.Deserialize<object>(feature.GetProperty("geometry").GetRawText())!,
                    Color = index < options.Colors.Count ? options.Colors[index] : options.Colors[^1],
                    Opacity = options.Opacity,
                    Area = area
                };

                result.Polygons.Add(polygon);
                index++;
            }

            // Sort by interval (smallest to largest)
            result.Polygons = result.Polygons.OrderBy(p => p.Interval).ToList();

            _logger?.LogInformation("Successfully calculated OpenRouteService isochrone with {Count} polygons",
                result.Polygons.Count);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "OpenRouteService isochrone API request failed");
            throw new RoutingException("Failed to calculate isochrone from OpenRouteService", "OpenRouteService", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error calculating OpenRouteService isochrone");
            throw;
        }
    }

    private string MapTravelMode(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Driving => "driving-car",
            TravelMode.Walking => "foot-walking",
            TravelMode.Cycling => "cycling-regular",
            _ => "driving-car"
        };
    }
}
