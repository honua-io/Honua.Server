// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.AwsLocation;

/// <summary>
/// AWS Location Service implementation of routing provider.
/// API Reference: https://docs.aws.amazon.com/location/latest/developerguide/calculate-route.html
/// </summary>
public class AwsLocationRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string _region;
    private readonly string _routeCalculatorName;
    private readonly ILogger<AwsLocationRoutingProvider> _logger;
    private readonly AwsSignatureHelper _signatureHelper;

    public string ProviderKey => "aws-location";
    public string ProviderName => "AWS Location Service Routing";

    public AwsLocationRoutingProvider(
        HttpClient httpClient,
        string accessKeyId,
        string secretAccessKey,
        string region,
        string routeCalculatorName,
        ILogger<AwsLocationRoutingProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _routeCalculatorName = routeCalculatorName ?? throw new ArgumentNullException(nameof(routeCalculatorName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _signatureHelper = new AwsSignatureHelper(_accessKeyId, _secretAccessKey, _region, "geo-routes");
    }

    public async Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request.Waypoints.Count < 2)
            {
                throw new ArgumentException("At least 2 waypoints are required for routing", nameof(request));
            }

            var requestBody = BuildRouteRequest(request);
            var url = $"https://routes.geo.{_region}.amazonaws.com/routes/v0/calculators/{_routeCalculatorName}/calculate/route";

            _logger.LogDebug("AWS Location Service routing request for {Count} waypoints", request.Waypoints.Count);

            var httpRequest = await _signatureHelper.CreateSignedPostRequestAsync(
                url,
                JsonSerializer.Serialize(requestBody),
                cancellationToken);

            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var response = await httpResponse.Content.ReadFromJsonAsync<AwsRouteResponse>(
                cancellationToken: cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("AWS Location Service returned null response");
            }

            return MapToRoutingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AWS Location Service routing request failed");
            throw new InvalidOperationException($"AWS Location Service routing failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple route test (Seattle to Portland)
            var testRequest = new RoutingRequest
            {
                Waypoints = new[]
                {
                    new[] { -122.3321, 47.6062 },
                    new[] { -122.6765, 45.5231 }
                }
            };

            await CalculateRouteAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AWS Location Service routing connectivity test failed");
            return false;
        }
    }

    private object BuildRouteRequest(RoutingRequest request)
    {
        var awsRequest = new Dictionary<string, object>
        {
            ["DeparturePosition"] = new[] { request.Waypoints[0][0], request.Waypoints[0][1] },
            ["DestinationPosition"] = new[] { request.Waypoints[^1][0], request.Waypoints[^1][1] },
            ["TravelMode"] = MapTravelMode(request.TravelMode),
            ["IncludeLegGeometry"] = true
        };

        // Add waypoints if more than 2
        if (request.Waypoints.Count > 2)
        {
            var intermediateWaypoints = request.Waypoints
                .Skip(1)
                .Take(request.Waypoints.Count - 2)
                .Select(w => new Dictionary<string, object>
                {
                    ["Position"] = new[] { w[0], w[1] }
                })
                .ToArray();
            awsRequest["WaypointPositions"] = intermediateWaypoints;
        }

        // Add avoidances
        var avoid = new List<string>();
        if (request.AvoidTolls) avoid.Add("Tolls");
        if (request.AvoidFerries) avoid.Add("Ferries");
        if (request.AvoidHighways) avoid.Add("Motorways");
        if (avoid.Count > 0)
        {
            awsRequest["Avoid"] = new Dictionary<string, object>
            {
                ["Avoidances"] = avoid
            };
        }

        // Add departure time
        if (request.DepartureTime.HasValue)
        {
            awsRequest["DepartureTime"] = request.DepartureTime.Value.ToString("O");
            awsRequest["OptimizeFor"] = request.UseTraffic ? "FastestRoute" : "ShortestRoute";
        }

        // Add truck specifications
        if (request.TravelMode.Equals("truck", StringComparison.OrdinalIgnoreCase) && request.Vehicle != null)
        {
            var truckSpecs = new Dictionary<string, object>();

            if (request.Vehicle.WeightKg.HasValue)
            {
                truckSpecs["GrossWeight"] = request.Vehicle.WeightKg.Value;
            }

            var dimensions = new Dictionary<string, object>();
            if (request.Vehicle.HeightMeters.HasValue)
            {
                dimensions["Height"] = request.Vehicle.HeightMeters.Value;
            }
            if (request.Vehicle.WidthMeters.HasValue)
            {
                dimensions["Width"] = request.Vehicle.WidthMeters.Value;
            }
            if (request.Vehicle.LengthMeters.HasValue)
            {
                dimensions["Length"] = request.Vehicle.LengthMeters.Value;
            }
            if (dimensions.Count > 0)
            {
                truckSpecs["Dimensions"] = dimensions;
            }

            if (request.Vehicle.AxleCount.HasValue)
            {
                truckSpecs["AxleCount"] = request.Vehicle.AxleCount.Value;
            }

            if (truckSpecs.Count > 0)
            {
                awsRequest["TruckModeOptions"] = truckSpecs;
            }
        }

        // Add distance unit
        awsRequest["DistanceUnit"] = request.UnitSystem.Equals("metric", StringComparison.OrdinalIgnoreCase)
            ? "Kilometers"
            : "Miles";

        return awsRequest;
    }

    private string MapTravelMode(string travelMode)
    {
        return travelMode.ToLowerInvariant() switch
        {
            "car" => "Car",
            "truck" => "Truck",
            "walking" or "pedestrian" => "Walking",
            _ => "Car"
        };
    }

    private RoutingResponse MapToRoutingResponse(AwsRouteResponse response)
    {
        var routes = new List<Route>();

        if (response.Summary != null && response.Legs != null)
        {
            var route = new Route
            {
                DistanceMeters = (response.Summary.Distance ?? 0) * 1000, // AWS returns km, convert to meters
                DurationSeconds = response.Summary.DurationSeconds ?? 0,
                Geometry = EncodePolyline(response.Legs.SelectMany(leg => leg.Geometry?.LineString ?? Array.Empty<double[]>()).ToArray()),
                GeometryFormat = "polyline",
                Legs = response.Legs.Select(MapLeg).ToList(),
                Summary = $"{response.Summary.Distance:F1} km, {TimeSpan.FromSeconds(response.Summary.DurationSeconds ?? 0):hh\\:mm}",
                Warnings = new List<string>()
            };

            routes.Add(route);
        }

        return new RoutingResponse
        {
            Routes = routes,
            Attribution = "© AWS Location Service"
        };
    }

    private RouteLeg MapLeg(AwsRouteLeg leg)
    {
        var lineString = leg.Geometry?.LineString ?? Array.Empty<double[]>();

        return new RouteLeg
        {
            DistanceMeters = (leg.Distance ?? 0) * 1000, // Convert km to meters
            DurationSeconds = leg.DurationSeconds ?? 0,
            StartLocation = lineString.Length > 0 ? lineString[0] : new[] { 0.0, 0.0 },
            EndLocation = lineString.Length > 0 ? lineString[^1] : new[] { 0.0, 0.0 },
            Instructions = leg.Steps?.Select(MapStep).ToList()
        };
    }

    private RouteInstruction MapStep(AwsRouteStep step)
    {
        return new RouteInstruction
        {
            Text = step.StartPosition != null
                ? $"Continue for {step.Distance:F2} km"
                : "Continue",
            DistanceMeters = (step.Distance ?? 0) * 1000, // Convert km to meters
            DurationSeconds = step.DurationSeconds ?? 0,
            Location = step.StartPosition
        };
    }

    private string EncodePolyline(double[][] points)
    {
        if (points.Length == 0)
        {
            return string.Empty;
        }

        var encoded = new System.Text.StringBuilder();
        int prevLat = 0, prevLon = 0;

        foreach (var point in points)
        {
            if (point.Length < 2) continue;

            int lat = (int)Math.Round(point[1] * 1e5);
            int lon = (int)Math.Round(point[0] * 1e5);

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

    #region AWS Location Service Response Models

    private class AwsRouteResponse
    {
        public AwsRouteLeg[]? Legs { get; set; }
        public AwsRouteSummary? Summary { get; set; }
    }

    private class AwsRouteSummary
    {
        public double? Distance { get; set; } // in kilometers
        public double? DurationSeconds { get; set; }
        public string? DistanceUnit { get; set; }
    }

    private class AwsRouteLeg
    {
        public double? Distance { get; set; } // in kilometers
        public double? DurationSeconds { get; set; }
        public AwsGeometry? Geometry { get; set; }
        public AwsRouteStep[]? Steps { get; set; }
    }

    private class AwsGeometry
    {
        public double[][]? LineString { get; set; }
    }

    private class AwsRouteStep
    {
        public double? Distance { get; set; } // in kilometers
        public double? DurationSeconds { get; set; }
        public double[]? StartPosition { get; set; }
        public double[]? EndPosition { get; set; }
    }

    #endregion
}
