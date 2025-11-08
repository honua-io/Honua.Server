// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;
using ServerRoute = Honua.Server.Core.LocationServices.Models.Route;
using ServerRouteInstruction = Honua.Server.Core.LocationServices.Models.RouteInstruction;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Services.Routing;

/// <summary>
/// Service for rendering and visualizing routes on maps.
/// </summary>
public sealed class RouteVisualizationService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;
    private readonly Dictionary<string, RouteRenderState> _activeRoutes = new();

    public RouteVisualizationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Initializes the JavaScript module.
    /// </summary>
    private async Task EnsureModuleAsync()
    {
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/Honua.MapSDK/js/routing-visualization.js");
    }

    /// <summary>
    /// Decodes a polyline string to coordinates.
    /// </summary>
    /// <param name="polyline">Encoded polyline string.</param>
    /// <param name="precision">Precision (5 for Google, 6 for OSRM).</param>
    /// <returns>List of [longitude, latitude] coordinates.</returns>
    public List<double[]> DecodePolyline(string polyline, int precision = 5)
    {
        var coordinates = new List<double[]>();
        int index = 0;
        int lat = 0, lng = 0;
        int factor = (int)Math.Pow(10, precision);

        while (index < polyline.Length)
        {
            int result = 0, shift = 0, b;
            do
            {
                b = polyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            result = 0;
            shift = 0;
            do
            {
                b = polyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            coordinates.Add(new[] { lng / (double)factor, lat / (double)factor });
        }

        return coordinates;
    }

    /// <summary>
    /// Calculates the bounding box for a set of coordinates.
    /// </summary>
    /// <param name="coordinates">List of [longitude, latitude] coordinates.</param>
    /// <returns>Bounding box as [minLng, minLat, maxLng, maxLat].</returns>
    private double[] CalculateBoundingBox(List<double[]> coordinates)
    {
        if (!coordinates.Any())
        {
            return new[] { 0.0, 0.0, 0.0, 0.0 };
        }

        var minLng = coordinates.Min(c => c[0]);
        var minLat = coordinates.Min(c => c[1]);
        var maxLng = coordinates.Max(c => c[0]);
        var maxLat = coordinates.Max(c => c[1]);

        return new[] { minLng, minLat, maxLng, maxLat };
    }

    /// <summary>
    /// Parses route geometry based on format.
    /// </summary>
    /// <param name="route">Route with geometry data.</param>
    /// <returns>List of [longitude, latitude] coordinates.</returns>
    public List<double[]> ParseRouteGeometry(Route route)
    {
        // Try to parse as GeoJSON first
        if (route.Geometry is JsonElement geoJson)
        {
            try
            {
                if (geoJson.TryGetProperty("coordinates", out var coords))
                {
                    return coords.EnumerateArray()
                        .Select(c => new[] { c[0].GetDouble(), c[1].GetDouble() })
                        .ToList();
                }
            }
            catch
            {
                // Fall through to string format
            }
        }

        // Try to parse Geometry as string (polyline or JSON string)
        if (route.Geometry is string geometryStr)
        {
            // Try parsing as JSON first
            try
            {
                var parsed = JsonSerializer.Deserialize<JsonElement>(geometryStr);
                if (parsed.TryGetProperty("coordinates", out var coords))
                {
                    return coords.EnumerateArray()
                        .Select(c => new[] { c[0].GetDouble(), c[1].GetDouble() })
                        .ToList();
                }
            }
            catch
            {
                // Fall through to polyline decoding
            }

            // Default to polyline format
            return DecodePolyline(geometryStr);
        }

        // Return empty list if geometry can't be parsed
        return new List<double[]>();
    }

    /// <summary>
    /// Adds a route to the map with visualization.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Unique route identifier.</param>
    /// <param name="route">Route data to visualize.</param>
    /// <param name="style">Styling options.</param>
    /// <param name="animation">Animation options.</param>
    public async Task AddRouteToMapAsync(
        string mapId,
        string routeId,
        Route route,
        RouteStyle? style = null,
        RouteAnimationOptions? animation = null)
    {
        await EnsureModuleAsync();

        var coordinates = ParseRouteGeometry(route);
        style ??= new RouteStyle();
        animation ??= new RouteAnimationOptions();

        _activeRoutes[routeId] = new RouteRenderState
        {
            RouteId = routeId,
            Route = route,
            Coordinates = coordinates,
            Style = style
        };

        await _module!.InvokeVoidAsync("addRoute", mapId, new
        {
            routeId,
            coordinates,
            style = new
            {
                color = style.Color,
                width = style.Width,
                opacity = style.Opacity,
                outlineColor = style.OutlineColor,
                outlineWidth = style.OutlineWidth,
                zIndex = style.ZIndex
            },
            animation = new
            {
                enabled = animation.Enabled,
                durationMs = animation.DurationMs,
                easing = animation.Easing
            }
        });

        if (animation.FitBoundsAfter)
        {
            // Calculate bounding box from geometry
            var routeCoordinates = ParseRouteGeometry(route);
            if (routeCoordinates.Any())
            {
                var bbox = CalculateBoundingBox(routeCoordinates);
                await FitMapToBoundsAsync(mapId, bbox, animation.BoundsPadding);
            }
        }
    }

    /// <summary>
    /// Adds turn markers to the map for navigation instructions.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier.</param>
    /// <param name="instructions">Route instructions.</param>
    /// <param name="markerStyle">Marker styling options.</param>
    public async Task AddTurnMarkersAsync(
        string mapId,
        string routeId,
        IReadOnlyList<RouteInstruction> instructions,
        TurnMarkerStyle? markerStyle = null)
    {
        await EnsureModuleAsync();

        markerStyle ??= new TurnMarkerStyle();

        var markers = instructions
            .Where(i => i.Coordinate != null && i.Coordinate.Length >= 2)
            .Select((instruction, index) => new
            {
                id = $"{routeId}-marker-{index}",
                location = instruction.Coordinate,
                icon = ManeuverIcons.GetIcon(instruction.Maneuver.ToString()),
                size = markerStyle.Size,
                text = instruction.Text,
                maneuver = instruction.Maneuver
            })
            .ToList();

        await _module!.InvokeVoidAsync("addTurnMarkers", mapId, routeId, markers);
    }

    /// <summary>
    /// Adds start, end, and waypoint markers.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier.</param>
    /// <param name="waypoints">List of waypoints.</param>
    /// <param name="markerStyle">Marker styling options.</param>
    public async Task AddWaypointMarkersAsync(
        string mapId,
        string routeId,
        IReadOnlyList<Waypoint> waypoints,
        TurnMarkerStyle? markerStyle = null)
    {
        await EnsureModuleAsync();

        markerStyle ??= new TurnMarkerStyle();

        var markers = waypoints.Select((wp, index) => new
        {
            id = wp.Id,
            location = wp.Coordinates,
            icon = index == 0 ? markerStyle.StartIcon :
                   index == waypoints.Count - 1 ? markerStyle.EndIcon :
                   markerStyle.WaypointIcon,
            size = markerStyle.Size,
            text = wp.Name ?? wp.Address,
            draggable = wp.IsDraggable,
            order = wp.Order
        }).ToList();

        await _module!.InvokeVoidAsync("addWaypointMarkers", mapId, routeId, markers);
    }

    /// <summary>
    /// Fits the map viewport to show the entire route.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="boundingBox">Bounding box [west, south, east, north].</param>
    /// <param name="padding">Padding in pixels.</param>
    public async Task FitMapToBoundsAsync(string mapId, double[] boundingBox, int padding = 50)
    {
        await EnsureModuleAsync();

        await _module!.InvokeVoidAsync("fitBounds", mapId, new
        {
            west = boundingBox[0],
            south = boundingBox[1],
            east = boundingBox[2],
            north = boundingBox[3],
            padding
        });
    }

    /// <summary>
    /// Animates drawing the route along its path.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    public async Task AnimateRouteDrawingAsync(string mapId, string routeId, int durationMs = 2000)
    {
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("animateRoute", mapId, routeId, durationMs);
    }

    /// <summary>
    /// Highlights a specific route (for showing alternative routes).
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier to highlight.</param>
    /// <param name="highlight">True to highlight, false to unhighlight.</param>
    public async Task HighlightRouteAsync(string mapId, string routeId, bool highlight = true)
    {
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("highlightRoute", mapId, routeId, highlight);
    }

    /// <summary>
    /// Updates route style dynamically.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier.</param>
    /// <param name="style">New style to apply.</param>
    public async Task UpdateRouteStyleAsync(string mapId, string routeId, RouteStyle style)
    {
        await EnsureModuleAsync();

        if (_activeRoutes.TryGetValue(routeId, out var state))
        {
            state.Style = style;
        }

        await _module!.InvokeVoidAsync("updateRouteStyle", mapId, routeId, new
        {
            color = style.Color,
            width = style.Width,
            opacity = style.Opacity,
            outlineColor = style.OutlineColor,
            outlineWidth = style.OutlineWidth,
            zIndex = style.ZIndex
        });
    }

    /// <summary>
    /// Clears a specific route from the map.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier to clear.</param>
    public async Task ClearRouteAsync(string mapId, string routeId)
    {
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("clearRoute", mapId, routeId);
        _activeRoutes.Remove(routeId);
    }

    /// <summary>
    /// Clears all routes from the map.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    public async Task ClearAllRoutesAsync(string mapId)
    {
        await EnsureModuleAsync();
        await _module!.InvokeVoidAsync("clearAllRoutes", mapId);
        _activeRoutes.Clear();
    }

    /// <summary>
    /// Visualizes traffic congestion on a route.
    /// </summary>
    /// <param name="mapId">Map instance identifier.</param>
    /// <param name="routeId">Route identifier.</param>
    /// <param name="trafficData">Traffic data segments.</param>
    /// <param name="colorScheme">Traffic color scheme.</param>
    public async Task VisualizeTrafficAsync(
        string mapId,
        string routeId,
        IReadOnlyList<TrafficSegment> trafficData,
        TrafficColorScheme? colorScheme = null)
    {
        await EnsureModuleAsync();

        colorScheme ??= new TrafficColorScheme();

        await _module!.InvokeVoidAsync("addTrafficOverlay", mapId, routeId, new
        {
            segments = trafficData.Select(s => new
            {
                coordinates = s.Coordinates,
                congestionLevel = s.CongestionLevel,
                color = GetTrafficColor(s.CongestionLevel, colorScheme)
            })
        });
    }

    /// <summary>
    /// Gets color for traffic congestion level.
    /// </summary>
    private static string GetTrafficColor(string congestionLevel, TrafficColorScheme scheme) =>
        congestionLevel.ToLowerInvariant() switch
        {
            "free" or "low" => scheme.FreeFlow,
            "moderate" => scheme.Moderate,
            "heavy" => scheme.Heavy,
            "severe" or "blocked" => scheme.Severe,
            _ => scheme.FreeFlow
        };

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}

/// <summary>
/// Internal state for tracking rendered routes.
/// </summary>
internal sealed class RouteRenderState
{
    public required string RouteId { get; init; }
    public required Route Route { get; init; }
    public required List<double[]> Coordinates { get; init; }
    public required RouteStyle Style { get; set; }
}

/// <summary>
/// Traffic segment data for visualization.
/// </summary>
public sealed record TrafficSegment
{
    /// <summary>
    /// Segment coordinates.
    /// </summary>
    public required List<double[]> Coordinates { get; init; }

    /// <summary>
    /// Congestion level (e.g., "free", "moderate", "heavy", "severe").
    /// </summary>
    public required string CongestionLevel { get; init; }

    /// <summary>
    /// Current speed in km/h (if available).
    /// </summary>
    public double? CurrentSpeedKmh { get; init; }

    /// <summary>
    /// Free-flow speed in km/h (if available).
    /// </summary>
    public double? FreeFlowSpeedKmh { get; init; }
}
