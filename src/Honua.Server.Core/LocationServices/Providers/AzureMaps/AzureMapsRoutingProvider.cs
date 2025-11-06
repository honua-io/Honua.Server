// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.AzureMaps;

/// <summary>
/// Azure Maps implementation of routing provider.
/// API Reference: https://learn.microsoft.com/en-us/rest/api/maps/route
/// </summary>
public class AzureMapsRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _subscriptionKey;
    private readonly ILogger<AzureMapsRoutingProvider> _logger;
    private const string BaseUrl = "https://atlas.microsoft.com";
    private const string ApiVersion = "2024-07-01";

    public string ProviderKey => "azure-maps";
    public string ProviderName => "Azure Maps Routing";

    public AzureMapsRoutingProvider(
        HttpClient httpClient,
        string subscriptionKey,
        ILogger<AzureMapsRoutingProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }
    }

    public async Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildRouteUrl(request);
            _logger.LogDebug("Azure Maps routing request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<AzureMapsRouteResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Azure Maps returned null response");
            }

            return MapToRoutingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure Maps routing request failed");
            throw new InvalidOperationException($"Azure Maps routing failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple route test (Seattle to Redmond)
            var testRequest = new RoutingRequest
            {
                Waypoints = new[]
                {
                    new[] { -122.335167, 47.608013 },
                    new[] { -122.13493, 47.64358 }
                }
            };

            await CalculateRouteAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Maps routing connectivity test failed");
            return false;
        }
    }

    private string BuildRouteUrl(RoutingRequest request)
    {
        if (request.Waypoints.Count < 2)
        {
            throw new ArgumentException("At least 2 waypoints are required for routing", nameof(request));
        }

        // Build waypoints string: lat1,lon1:lat2,lon2:...
        var waypoints = string.Join(":", request.Waypoints.Select(w => $"{w[1]},{w[0]}"));

        var queryParams = new List<string>
        {
            $"api-version={ApiVersion}",
            $"query={waypoints}",
            $"subscription-key={_subscriptionKey}",
            $"travelMode={MapTravelMode(request.TravelMode)}"
        };

        if (request.AvoidTolls)
        {
            queryParams.Add("avoid=tollRoads");
        }

        if (request.AvoidHighways)
        {
            queryParams.Add("avoid=motorways");
        }

        if (request.AvoidFerries)
        {
            queryParams.Add("avoid=ferries");
        }

        if (request.UseTraffic)
        {
            queryParams.Add("traffic=true");
        }

        if (request.DepartureTime.HasValue)
        {
            queryParams.Add($"departAt={request.DepartureTime.Value:O}");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        // Vehicle specifications for truck routing
        if (request.TravelMode.Equals("truck", StringComparison.OrdinalIgnoreCase) && request.Vehicle != null)
        {
            if (request.Vehicle.WeightKg.HasValue)
            {
                queryParams.Add($"vehicleWeight={request.Vehicle.WeightKg.Value}");
            }
            if (request.Vehicle.HeightMeters.HasValue)
            {
                queryParams.Add($"vehicleHeight={request.Vehicle.HeightMeters.Value}");
            }
            if (request.Vehicle.WidthMeters.HasValue)
            {
                queryParams.Add($"vehicleWidth={request.Vehicle.WidthMeters.Value}");
            }
            if (request.Vehicle.LengthMeters.HasValue)
            {
                queryParams.Add($"vehicleLength={request.Vehicle.LengthMeters.Value}");
            }
        }

        return $"/route/directions/json?{string.Join("&", queryParams)}";
    }

    private string MapTravelMode(string travelMode)
    {
        return travelMode.ToLowerInvariant() switch
        {
            "car" => "car",
            "truck" => "truck",
            "bicycle" => "bicycle",
            "pedestrian" => "pedestrian",
            "motorcycle" => "motorcycle",
            "bus" => "bus",
            "taxi" => "taxi",
            _ => "car"
        };
    }

    private RoutingResponse MapToRoutingResponse(AzureMapsRouteResponse response)
    {
        var routes = response.Routes?.Select(r => new Route
        {
            DistanceMeters = r.Summary?.LengthInMeters ?? 0,
            DurationSeconds = r.Summary?.TravelTimeInSeconds ?? 0,
            DurationWithTrafficSeconds = r.Summary?.TrafficDelayInSeconds.HasValue == true
                ? (r.Summary.TravelTimeInSeconds + r.Summary.TrafficDelayInSeconds.Value)
                : null,
            Geometry = EncodePolyline(r.Legs?.SelectMany(leg => leg.Points ?? Array.Empty<AzureMapsPoint>()).ToArray() ?? Array.Empty<AzureMapsPoint>()),
            GeometryFormat = "polyline",
            Instructions = r.Guidance?.Instructions?.Select(MapInstruction).ToList(),
            Legs = r.Legs?.Select(MapLeg).ToList(),
            Summary = r.Summary?.Description,
            Warnings = r.Guidance?.InstructionGroups?.SelectMany(g => g.Instructions ?? Array.Empty<AzureMapsInstruction>())
                .Where(i => i.Message?.Contains("toll") == true || i.Message?.Contains("ferry") == true)
                .Select(i => i.Message ?? string.Empty)
                .ToList()
        }).ToList() ?? new List<Route>();

        return new RoutingResponse
        {
            Routes = routes,
            Attribution = "© Microsoft Azure Maps"
        };
    }

    private RouteInstruction MapInstruction(AzureMapsInstruction instruction)
    {
        return new RouteInstruction
        {
            Text = instruction.Message ?? string.Empty,
            DistanceMeters = instruction.DistanceInMeters ?? 0,
            DurationSeconds = instruction.TimeInSeconds ?? 0,
            ManeuverType = instruction.ManeuverType,
            RoadName = instruction.Street,
            Location = instruction.Point != null ? new[] { instruction.Point.Longitude, instruction.Point.Latitude } : null
        };
    }

    private RouteLeg MapLeg(AzureMapsRouteLeg leg)
    {
        return new RouteLeg
        {
            DistanceMeters = leg.Summary?.LengthInMeters ?? 0,
            DurationSeconds = leg.Summary?.TravelTimeInSeconds ?? 0,
            StartLocation = new[] { leg.Points?[0].Longitude ?? 0, leg.Points?[0].Latitude ?? 0 },
            EndLocation = new[] { leg.Points?[^1].Longitude ?? 0, leg.Points?[^1].Latitude ?? 0 }
        };
    }

    private string EncodePolyline(AzureMapsPoint[] points)
    {
        // Simplified polyline encoding (Google Polyline format)
        // For production, use a proper polyline encoding library
        if (points.Length == 0)
        {
            return string.Empty;
        }

        var encoded = new System.Text.StringBuilder();
        int prevLat = 0, prevLon = 0;

        foreach (var point in points)
        {
            int lat = (int)Math.Round(point.Latitude * 1e5);
            int lon = (int)Math.Round(point.Longitude * 1e5);

            EncodeNumber(encoded, lat - prevLat);
            EncodeNumber(encoded, lon - prevLon);

            prevLat = lat;
            prevLon = lon;
        }

        return encoded.ToString();
    }

    private void EncodeNumber(System.Text.StringBuilder result, int num)
    {
        int sgn_num = num << 1;
        if (num < 0)
        {
            sgn_num = ~sgn_num;
        }

        while (sgn_num >= 0x20)
        {
            result.Append((char)((0x20 | (sgn_num & 0x1f)) + 63));
            sgn_num >>= 5;
        }

        result.Append((char)(sgn_num + 63));
    }

    #region Azure Maps Response Models

    private class AzureMapsRouteResponse
    {
        public AzureMapsRoute[]? Routes { get; set; }
    }

    private class AzureMapsRoute
    {
        public AzureMapsRouteSummary? Summary { get; set; }
        public AzureMapsRouteLeg[]? Legs { get; set; }
        public AzureMapsGuidance? Guidance { get; set; }
    }

    private class AzureMapsRouteSummary
    {
        public double? LengthInMeters { get; set; }
        public double? TravelTimeInSeconds { get; set; }
        public double? TrafficDelayInSeconds { get; set; }
        public string? Description { get; set; }
    }

    private class AzureMapsRouteLeg
    {
        public AzureMapsRouteSummary? Summary { get; set; }
        public AzureMapsPoint[]? Points { get; set; }
    }

    private class AzureMapsPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    private class AzureMapsGuidance
    {
        public AzureMapsInstruction[]? Instructions { get; set; }
        public AzureMapsInstructionGroup[]? InstructionGroups { get; set; }
    }

    private class AzureMapsInstructionGroup
    {
        public AzureMapsInstruction[]? Instructions { get; set; }
    }

    private class AzureMapsInstruction
    {
        public string? Message { get; set; }
        public double? DistanceInMeters { get; set; }
        public double? TimeInSeconds { get; set; }
        public string? ManeuverType { get; set; }
        public string? Street { get; set; }
        public AzureMapsPoint? Point { get; set; }
    }

    #endregion
}
