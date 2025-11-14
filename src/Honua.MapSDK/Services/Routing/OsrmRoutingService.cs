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

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// OSRM (Open Source Routing Machine) routing provider
/// Free and open-source, can be self-hosted or use demo server
/// </summary>
public class OsrmRoutingService : IRoutingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string ProviderName => "OSRM";
    public bool RequiresApiKey => false;

    public List<TravelMode> SupportedTravelModes => new()
    {
        TravelMode.Driving,
        TravelMode.Walking,
        TravelMode.Cycling
    };

    /// <summary>
    /// Create OSRM routing service
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls</param>
    /// <param name="baseUrl">Base URL for OSRM server (default: demo server)</param>
    public OsrmRoutingService(
        HttpClient httpClient,
        string baseUrl = "https://router.project-osrm.org")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<Route> CalculateRouteAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default)
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            throw new RoutingException("At least 2 waypoints are required", "INVALID_WAYPOINTS", ProviderName);
        }

        var profile = GetOsrmProfile(options.TravelMode);
        var coordinates = string.Join(";", waypoints.Select(w => $"{w.Longitude},{w.Latitude}"));

        var url = $"{_baseUrl}/route/v1/{profile}/{coordinates}?" +
                  $"steps={options.IncludeInstructions.ToString().ToLower()}&" +
                  $"geometries={options.GeometryFormat}&" +
                  $"overview={options.Overview}&" +
                  $"alternatives={options.MaxAlternatives > 0}";

        if (options.Annotations.Any())
        {
            url += $"&annotations={string.Join(",", options.Annotations)}";
        }

        try
        {
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetString() != "Ok")
            {
                var message = root.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown error from OSRM";
                throw new RoutingException(message ?? "Routing failed", "OSRM_ERROR", ProviderName);
            }

            var routes = root.GetProperty("routes");
            if (routes.GetArrayLength() == 0)
            {
                throw new RoutingException("No route found", "NO_ROUTE", ProviderName);
            }

            var routeElement = routes[0];
            return ParseOsrmRoute(routeElement, waypoints, options.TravelMode);
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingException($"Failed to connect to OSRM server: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new RoutingException($"Failed to parse OSRM response: {ex.Message}", ex);
        }
    }

    public async Task<List<Route>> GetAlternativesAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default)
    {
        if (waypoints == null || waypoints.Count < 2)
        {
            throw new RoutingException("At least 2 waypoints are required", "INVALID_WAYPOINTS", ProviderName);
        }

        var profile = GetOsrmProfile(options.TravelMode);
        var coordinates = string.Join(";", waypoints.Select(w => $"{w.Longitude},{w.Latitude}"));

        var url = $"{_baseUrl}/route/v1/{profile}/{coordinates}?" +
                  $"steps={options.IncludeInstructions.ToString().ToLower()}&" +
                  $"geometries={options.GeometryFormat}&" +
                  $"overview={options.Overview}&" +
                  $"alternatives=true&" +
                  $"number_of_alternatives={options.MaxAlternatives}";

        try
        {
            var response = await _httpClient.GetStringAsync(url, cancellationToken);
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetString() != "Ok")
            {
                var message = root.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Unknown error from OSRM";
                throw new RoutingException(message ?? "Routing failed", "OSRM_ERROR", ProviderName);
            }

            var routes = root.GetProperty("routes");
            var result = new List<Route>();

            for (int i = 0; i < routes.GetArrayLength(); i++)
            {
                var routeElement = routes[i];
                var route = ParseOsrmRoute(routeElement, waypoints, options.TravelMode);
                route.IsAlternative = i > 0;
                route.AlternativeIndex = i;
                result.Add(route);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            throw new RoutingException($"Failed to connect to OSRM server: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new RoutingException($"Failed to parse OSRM response: {ex.Message}", ex);
        }
    }

    public Task<IsochroneResult> CalculateIsochroneAsync(
        IsochroneOptions options,
        CancellationToken cancellationToken = default)
    {
        // OSRM doesn't have built-in isochrone support
        // This would require additional processing or a different service
        throw new NotSupportedException("OSRM does not support isochrone calculation. Use GraphHopper or OpenRouteService instead.");
    }

    private Route ParseOsrmRoute(JsonElement routeElement, List<Waypoint> waypoints, TravelMode travelMode)
    {
        var route = new Route
        {
            Waypoints = waypoints,
            TravelMode = travelMode,
            RoutingEngine = ProviderName,
            Distance = routeElement.GetProperty("distance").GetDouble(),
            Duration = (int)routeElement.GetProperty("duration").GetDouble()
        };

        // Parse geometry
        if (routeElement.TryGetProperty("geometry", out var geometryElement))
        {
            route.Geometry = JsonSerializer.Deserialize<object>(geometryElement.GetRawText()) ?? new { };
        }

        // Parse instructions from legs
        if (routeElement.TryGetProperty("legs", out var legsElement))
        {
            var instructions = new List<RouteInstruction>();
            int instructionIndex = 0;

            for (int legIndex = 0; legIndex < legsElement.GetArrayLength(); legIndex++)
            {
                var leg = legsElement[legIndex];

                if (leg.TryGetProperty("steps", out var stepsElement))
                {
                    for (int stepIndex = 0; stepIndex < stepsElement.GetArrayLength(); stepIndex++)
                    {
                        var step = stepsElement[stepIndex];
                        var instruction = ParseOsrmStep(step, instructionIndex++);
                        if (instruction != null)
                        {
                            instructions.Add(instruction);
                        }
                    }
                }

                // Create route leg
                var routeLeg = new RouteLeg
                {
                    Distance = leg.GetProperty("distance").GetDouble(),
                    Duration = (int)leg.GetProperty("duration").GetDouble()
                };

                if (legIndex < waypoints.Count - 1)
                {
                    routeLeg.StartWaypoint = waypoints[legIndex];
                    routeLeg.EndWaypoint = waypoints[legIndex + 1];
                }

                route.Legs.Add(routeLeg);
            }

            route.Instructions = instructions;
        }

        // Create summary
        route.Summary = new RouteSummary
        {
            FormattedDistance = FormatDistance(route.Distance),
            FormattedDuration = FormatDuration(route.Duration)
        };

        return route;
    }

    private RouteInstruction? ParseOsrmStep(JsonElement step, int index)
    {
        var maneuver = step.GetProperty("maneuver");
        var maneuverType = maneuver.GetProperty("type").GetString();
        var modifier = maneuver.TryGetProperty("modifier", out var mod) ? mod.GetString() : null;

        var instruction = new RouteInstruction
        {
            Index = index,
            Distance = step.GetProperty("distance").GetDouble(),
            Time = (int)step.GetProperty("duration").GetDouble(),
            Maneuver = ParseManeuverType(maneuverType, modifier),
            Modifier = modifier
        };

        // Get coordinate
        if (maneuver.TryGetProperty("location", out var location) && location.GetArrayLength() >= 2)
        {
            instruction.Coordinate = new[]
            {
                location[0].GetDouble(),
                location[1].GetDouble()
            };
        }

        // Get street name
        if (step.TryGetProperty("name", out var name))
        {
            instruction.StreetName = name.GetString();
        }

        // Build instruction text
        instruction.Text = BuildInstructionText(instruction);

        return instruction;
    }

    private string BuildInstructionText(RouteInstruction instruction)
    {
        var action = instruction.Maneuver switch
        {
            ManeuverType.Depart => "Head",
            ManeuverType.Arrive => "Arrive at",
            ManeuverType.TurnLeft => "Turn left",
            ManeuverType.TurnRight => "Turn right",
            ManeuverType.TurnSlightLeft => "Turn slight left",
            ManeuverType.TurnSlightRight => "Turn slight right",
            ManeuverType.TurnSharpLeft => "Turn sharp left",
            ManeuverType.TurnSharpRight => "Turn sharp right",
            ManeuverType.Continue => "Continue",
            ManeuverType.Merge => "Merge",
            ManeuverType.OnRamp => "Take the ramp",
            ManeuverType.OffRamp => "Take the exit",
            ManeuverType.Roundabout => "Enter the roundabout",
            _ => "Continue"
        };

        var streetPart = !string.IsNullOrEmpty(instruction.StreetName)
            ? $" onto {instruction.StreetName}"
            : "";

        return $"{action}{streetPart}";
    }

    private ManeuverType ParseManeuverType(string? type, string? modifier)
    {
        return type switch
        {
            "depart" => ManeuverType.Depart,
            "arrive" => ManeuverType.Arrive,
            "turn" when modifier == "left" => ManeuverType.TurnLeft,
            "turn" when modifier == "right" => ManeuverType.TurnRight,
            "turn" when modifier == "slight left" => ManeuverType.TurnSlightLeft,
            "turn" when modifier == "slight right" => ManeuverType.TurnSlightRight,
            "turn" when modifier == "sharp left" => ManeuverType.TurnSharpLeft,
            "turn" when modifier == "sharp right" => ManeuverType.TurnSharpRight,
            "turn" when modifier == "uturn" => ManeuverType.UTurn,
            "merge" => ManeuverType.Merge,
            "fork" => ManeuverType.Fork,
            "on ramp" => ManeuverType.OnRamp,
            "off ramp" => ManeuverType.OffRamp,
            "roundabout" => ManeuverType.Roundabout,
            "continue" => ManeuverType.Continue,
            _ => ManeuverType.Straight
        };
    }

    private string GetOsrmProfile(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Driving or TravelMode.DrivingTraffic => "driving",
            TravelMode.Walking => "foot",
            TravelMode.Cycling => "bike",
            _ => "driving"
        };
    }

    private string FormatDistance(double meters)
    {
        if (meters < 1000)
        {
            return $"{meters:F0} m";
        }
        return $"{meters / 1000:F1} km";
    }

    private string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var hours = minutes / 60;

        if (hours > 0)
        {
            var remainingMinutes = minutes % 60;
            return remainingMinutes > 0
                ? $"{hours} h {remainingMinutes} min"
                : $"{hours} h";
        }

        return $"{minutes} min";
    }
}
