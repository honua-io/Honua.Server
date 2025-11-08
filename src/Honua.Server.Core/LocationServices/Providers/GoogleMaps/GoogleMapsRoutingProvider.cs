// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.GoogleMaps;

/// <summary>
/// Google Maps implementation of routing provider.
/// API Reference: https://developers.google.com/maps/documentation/directions
/// </summary>
public class GoogleMapsRoutingProvider : IRoutingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _clientId;
    private readonly string? _clientSignature;
    private readonly ILogger<GoogleMapsRoutingProvider> _logger;
    private const string BaseUrl = "https://maps.googleapis.com";

    public string ProviderKey => "google-maps";
    public string ProviderName => "Google Maps Routing";

    public GoogleMapsRoutingProvider(
        HttpClient httpClient,
        string apiKey,
        ILogger<GoogleMapsRoutingProvider> logger,
        string? clientId = null,
        string? clientSignature = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientId = clientId;
        _clientSignature = clientSignature;

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
            _logger.LogDebug("Google Maps routing request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<GoogleMapsDirectionsResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Google Maps returned null response");
            }

            if (response.Status != "OK" && response.Status != "ZERO_RESULTS")
            {
                throw new InvalidOperationException($"Google Maps routing failed with status: {response.Status}. {response.ErrorMessage}");
            }

            return MapToRoutingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Maps routing request failed");
            throw new InvalidOperationException($"Google Maps routing failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple route test (Los Angeles to San Francisco)
            var testRequest = new RoutingRequest
            {
                Waypoints = new[]
                {
                    new[] { -118.2437, 34.0522 },
                    new[] { -122.4194, 37.7749 }
                }
            };

            await CalculateRouteAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Maps routing connectivity test failed");
            return false;
        }
    }

    private string BuildRouteUrl(RoutingRequest request)
    {
        if (request.Waypoints.Count < 2)
        {
            throw new ArgumentException("At least 2 waypoints are required for routing", nameof(request));
        }

        var queryParams = new List<string>();

        // Origin and destination
        var origin = request.Waypoints.First();
        var destination = request.Waypoints.Last();
        queryParams.Add($"origin={origin[1]},{origin[0]}");
        queryParams.Add($"destination={destination[1]},{destination[0]}");

        // Waypoints (intermediate points)
        if (request.Waypoints.Count > 2)
        {
            var waypoints = string.Join("|", request.Waypoints
                .Skip(1)
                .Take(request.Waypoints.Count - 2)
                .Select(w => $"{w[1]},{w[0]}"));
            queryParams.Add($"waypoints={waypoints}");
        }

        AddAuthenticationParams(queryParams);

        // Travel mode
        queryParams.Add($"mode={MapTravelMode(request.TravelMode)}");

        // Avoids
        var avoids = new List<string>();
        if (request.AvoidTolls) avoids.Add("tolls");
        if (request.AvoidHighways) avoids.Add("highways");
        if (request.AvoidFerries) avoids.Add("ferries");
        if (avoids.Count > 0)
        {
            queryParams.Add($"avoid={string.Join("|", avoids)}");
        }

        // Traffic model
        if (request.UseTraffic)
        {
            queryParams.Add("departure_time=now");
            queryParams.Add("traffic_model=best_guess");
        }
        else if (request.DepartureTime.HasValue)
        {
            var unixTime = new DateTimeOffset(request.DepartureTime.Value.DateTime).ToUnixTimeSeconds();
            queryParams.Add($"departure_time={unixTime}");
        }

        // Unit system
        queryParams.Add($"units={request.UnitSystem.ToLowerInvariant()}");

        // Language
        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        // Request alternatives
        queryParams.Add("alternatives=true");

        return $"/maps/api/directions/json?{string.Join("&", queryParams)}";
    }

    private void AddAuthenticationParams(List<string> queryParams)
    {
        // Use API Key or Premium Plan client ID/signature
        if (!string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSignature))
        {
            queryParams.Add($"client={_clientId}");
            queryParams.Add($"signature={_clientSignature}");
        }
        else
        {
            queryParams.Add($"key={_apiKey}");
        }
    }

    private string MapTravelMode(string travelMode)
    {
        return travelMode.ToLowerInvariant() switch
        {
            "car" => "driving",
            "truck" => "driving",
            "bicycle" => "bicycling",
            "pedestrian" => "walking",
            "walking" => "walking",
            "transit" => "transit",
            _ => "driving"
        };
    }

    private RoutingResponse MapToRoutingResponse(GoogleMapsDirectionsResponse response)
    {
        var routes = response.Routes?.Select(r => new Route
        {
            DistanceMeters = r.Legs?.Sum(leg => leg.Distance?.Value ?? 0) ?? 0,
            DurationSeconds = r.Legs?.Sum(leg => leg.Duration?.Value ?? 0) ?? 0,
            DurationWithTrafficSeconds = r.Legs?.Sum(leg => leg.DurationInTraffic?.Value) ?? null,
            Geometry = r.OverviewPolyline?.Points ?? string.Empty,
            GeometryFormat = "polyline",
            BoundingBox = r.Bounds != null ? new[]
            {
                r.Bounds.Southwest?.Lng ?? 0,
                r.Bounds.Southwest?.Lat ?? 0,
                r.Bounds.Northeast?.Lng ?? 0,
                r.Bounds.Northeast?.Lat ?? 0
            } : null,
            Instructions = r.Legs?.SelectMany(leg => leg.Steps ?? Array.Empty<GoogleMapsStep>())
                .Select(MapInstruction).ToList(),
            Legs = r.Legs?.Select(MapLeg).ToList(),
            Summary = r.Summary,
            Warnings = r.Warnings?.ToList() ?? (r.Legs?.Any(leg =>
                leg.Steps?.Any(s => s.HtmlInstructions?.Contains("toll") == true ||
                                   s.HtmlInstructions?.Contains("ferry") == true) == true) == true
                ? new List<string> { "Route may include tolls or ferries" }
                : null)
        }).ToList() ?? new List<Route>();

        return new RoutingResponse
        {
            Routes = routes,
            Attribution = "© Google Maps"
        };
    }

    private RouteInstruction MapInstruction(GoogleMapsStep step)
    {
        return new RouteInstruction
        {
            Text = StripHtmlTags(step.HtmlInstructions ?? string.Empty),
            DistanceMeters = step.Distance?.Value ?? 0,
            DurationSeconds = step.Duration?.Value ?? 0,
            ManeuverType = step.Maneuver,
            RoadName = ExtractRoadName(step.HtmlInstructions),
            Location = step.StartLocation != null
                ? new[] { step.StartLocation.Lng, step.StartLocation.Lat }
                : null
        };
    }

    private RouteLeg MapLeg(GoogleMapsLeg leg)
    {
        return new RouteLeg
        {
            DistanceMeters = leg.Distance?.Value ?? 0,
            DurationSeconds = leg.Duration?.Value ?? 0,
            StartLocation = leg.StartLocation != null
                ? new[] { leg.StartLocation.Lng, leg.StartLocation.Lat }
                : new[] { 0.0, 0.0 },
            EndLocation = leg.EndLocation != null
                ? new[] { leg.EndLocation.Lng, leg.EndLocation.Lat }
                : new[] { 0.0, 0.0 },
            StartAddress = leg.StartAddress,
            EndAddress = leg.EndAddress,
            Instructions = leg.Steps?.Select(MapInstruction).ToList()
        };
    }

    private string StripHtmlTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        // Simple HTML tag removal
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        return System.Net.WebUtility.HtmlDecode(result);
    }

    private string? ExtractRoadName(string? htmlInstructions)
    {
        if (string.IsNullOrEmpty(htmlInstructions))
        {
            return null;
        }

        // Try to extract road name from HTML (e.g., "Turn left onto <b>Main St</b>")
        var match = System.Text.RegularExpressions.Regex.Match(htmlInstructions, @"<b>(.*?)</b>");
        return match.Success ? match.Groups[1].Value : null;
    }

    #region Google Maps Response Models

    private class GoogleMapsDirectionsResponse
    {
        [JsonPropertyName("routes")]
        public GoogleMapsRoute[]? Routes { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    private class GoogleMapsRoute
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("legs")]
        public GoogleMapsLeg[]? Legs { get; set; }

        [JsonPropertyName("overview_polyline")]
        public GoogleMapsPolyline? OverviewPolyline { get; set; }

        [JsonPropertyName("bounds")]
        public GoogleMapsBounds? Bounds { get; set; }

        [JsonPropertyName("warnings")]
        public string[]? Warnings { get; set; }

        [JsonPropertyName("waypoint_order")]
        public int[]? WaypointOrder { get; set; }
    }

    private class GoogleMapsLeg
    {
        [JsonPropertyName("distance")]
        public GoogleMapsTextValue? Distance { get; set; }

        [JsonPropertyName("duration")]
        public GoogleMapsTextValue? Duration { get; set; }

        [JsonPropertyName("duration_in_traffic")]
        public GoogleMapsTextValue? DurationInTraffic { get; set; }

        [JsonPropertyName("start_location")]
        public GoogleMapsLocation? StartLocation { get; set; }

        [JsonPropertyName("end_location")]
        public GoogleMapsLocation? EndLocation { get; set; }

        [JsonPropertyName("start_address")]
        public string? StartAddress { get; set; }

        [JsonPropertyName("end_address")]
        public string? EndAddress { get; set; }

        [JsonPropertyName("steps")]
        public GoogleMapsStep[]? Steps { get; set; }
    }

    private class GoogleMapsStep
    {
        [JsonPropertyName("distance")]
        public GoogleMapsTextValue? Distance { get; set; }

        [JsonPropertyName("duration")]
        public GoogleMapsTextValue? Duration { get; set; }

        [JsonPropertyName("start_location")]
        public GoogleMapsLocation? StartLocation { get; set; }

        [JsonPropertyName("end_location")]
        public GoogleMapsLocation? EndLocation { get; set; }

        [JsonPropertyName("html_instructions")]
        public string? HtmlInstructions { get; set; }

        [JsonPropertyName("maneuver")]
        public string? Maneuver { get; set; }

        [JsonPropertyName("polyline")]
        public GoogleMapsPolyline? Polyline { get; set; }

        [JsonPropertyName("travel_mode")]
        public string? TravelMode { get; set; }
    }

    private class GoogleMapsTextValue
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }

    private class GoogleMapsLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    private class GoogleMapsPolyline
    {
        [JsonPropertyName("points")]
        public string? Points { get; set; }
    }

    private class GoogleMapsBounds
    {
        [JsonPropertyName("northeast")]
        public GoogleMapsLocation? Northeast { get; set; }

        [JsonPropertyName("southwest")]
        public GoogleMapsLocation? Southwest { get; set; }
    }

    #endregion
}
