// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.OpenStreetMap;

/// <summary>
/// OSRM (Open Source Routing Machine) implementation of routing provider.
/// API Reference: http://project-osrm.org/docs/v5.24.0/api/
/// </summary>
public class OsrmRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OsrmRoutingProvider> _logger;
    private readonly string _baseUrl;

    public string ProviderKey => "osrm";
    public string ProviderName => "OSRM (OpenStreetMap)";

    public OsrmRoutingProvider(
        HttpClient httpClient,
        ILogger<OsrmRoutingProvider> logger,
        string? baseUrl = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = baseUrl ?? "https://router.project-osrm.org";

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }
    }

    public async Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildRouteUrl(request);
            _logger.LogDebug("OSRM routing request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<OsrmRouteResponse>(
                url,
                cancellationToken);

            if (response == null || response.Code != "Ok")
            {
                throw new InvalidOperationException($"OSRM returned error: {response?.Code ?? "unknown"}");
            }

            return MapToRoutingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "OSRM routing request failed");
            throw new InvalidOperationException($"OSRM routing failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple route test
            var testRequest = new RoutingRequest
            {
                Waypoints = new[]
                {
                    new[] { -0.1278, 51.5074 },  // London
                    new[] { -0.0877, 51.5151 }   // Nearby point
                }
            };

            await CalculateRouteAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OSRM connectivity test failed");
            return false;
        }
    }

    private string BuildRouteUrl(RoutingRequest request)
    {
        if (request.Waypoints.Count < 2)
        {
            throw new ArgumentException("At least 2 waypoints are required for routing", nameof(request));
        }

        // OSRM profile
        var profile = MapTravelMode(request.TravelMode);

        // Build coordinates string: lon1,lat1;lon2,lat2;...
        var coordinates = string.Join(";", request.Waypoints.Select(w =>
            $"{w[0].ToString(CultureInfo.InvariantCulture)},{w[1].ToString(CultureInfo.InvariantCulture)}"));

        var queryParams = new List<string>
        {
            "overview=full",
            "geometries=polyline",
            "steps=true",
            "annotations=true"
        };

        return $"/route/v1/{profile}/{coordinates}?{string.Join("&", queryParams)}";
    }

    private string MapTravelMode(string travelMode)
    {
        // OSRM supports: driving, walking, cycling
        return travelMode.ToLowerInvariant() switch
        {
            "car" => "driving",
            "truck" => "driving",
            "bicycle" => "cycling",
            "pedestrian" => "walking",
            "walking" => "walking",
            _ => "driving"
        };
    }

    private RoutingResponse MapToRoutingResponse(OsrmRouteResponse response)
    {
        var routes = response.Routes?.Select(r => new Route
        {
            DistanceMeters = r.Distance ?? 0,
            DurationSeconds = r.Duration ?? 0,
            Geometry = r.Geometry ?? string.Empty,
            GeometryFormat = "polyline",
            Instructions = r.Legs?.SelectMany(leg => leg.Steps ?? Array.Empty<OsrmStep>())
                .Select(MapStep)
                .ToList(),
            Legs = r.Legs?.Select(MapLeg).ToList(),
            Summary = $"{(r.Distance / 1000):F1} km, {TimeSpan.FromSeconds(r.Duration ?? 0):hh\\:mm}"
        }).ToList() ?? new List<Route>();

        return new RoutingResponse
        {
            Routes = routes,
            Attribution = "© OpenStreetMap contributors, OSRM"
        };
    }

    private RouteInstruction MapStep(OsrmStep step)
    {
        return new RouteInstruction
        {
            Text = step.Maneuver?.Instruction ?? "Continue",
            DistanceMeters = step.Distance ?? 0,
            DurationSeconds = step.Duration ?? 0,
            ManeuverType = step.Maneuver?.Type,
            RoadName = step.Name,
            Location = step.Maneuver?.Location
        };
    }

    private RouteLeg MapLeg(OsrmLeg leg)
    {
        var firstStep = leg.Steps?.FirstOrDefault();
        var lastStep = leg.Steps?.LastOrDefault();

        return new RouteLeg
        {
            DistanceMeters = leg.Distance ?? 0,
            DurationSeconds = leg.Duration ?? 0,
            StartLocation = firstStep?.Maneuver?.Location ?? new[] { 0.0, 0.0 },
            EndLocation = lastStep?.Maneuver?.Location ?? new[] { 0.0, 0.0 },
            Instructions = leg.Steps?.Select(MapStep).ToList()
        };
    }

    #region OSRM Response Models

    private class OsrmRouteResponse
    {
        public string? Code { get; set; }
        public OsrmRoute[]? Routes { get; set; }
        public double[][]? Waypoints { get; set; }
    }

    private class OsrmRoute
    {
        public double? Distance { get; set; }
        public double? Duration { get; set; }
        public string? Geometry { get; set; }
        public OsrmLeg[]? Legs { get; set; }
    }

    private class OsrmLeg
    {
        public double? Distance { get; set; }
        public double? Duration { get; set; }
        public OsrmStep[]? Steps { get; set; }
    }

    private class OsrmStep
    {
        public double? Distance { get; set; }
        public double? Duration { get; set; }
        public string? Name { get; set; }
        public OsrmManeuver? Maneuver { get; set; }
    }

    private class OsrmManeuver
    {
        public string? Type { get; set; }
        public string? Instruction { get; set; }
        public double[]? Location { get; set; }
    }

    #endregion
}
