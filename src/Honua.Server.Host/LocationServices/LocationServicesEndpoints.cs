// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.LocationServices;

/// <summary>
/// Endpoint handlers for location services (geocoding, routing, basemap tiles).
/// </summary>
internal static class LocationServicesEndpoints
{
    /// <summary>
    /// Geocodes an address to geographic coordinates.
    /// GET /api/location/geocode?query={address}&provider={provider?}
    /// </summary>
    public static async Task<IResult> HandleGeocodeAsync(
        [FromQuery] string query,
        [FromQuery] string? provider,
        [FromServices] IGeocodingProvider defaultProvider,
        [FromServices] IServiceProvider services,
        [FromServices] LocationServiceConfiguration config,
        [FromServices] ILogger<IGeocodingProvider> logger,
        CancellationToken cancellationToken)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.BadRequest(new { error = "Query parameter is required" });
        }

        if (query.Length > 500)
        {
            return Results.BadRequest(new { error = "Query parameter is too long (max 500 characters)" });
        }

        try
        {
            // Select provider: use specified provider or default
            var geocodingProvider = string.IsNullOrWhiteSpace(provider)
                ? defaultProvider
                : services.GetGeocodingProvider(provider);

            logger.LogInformation(
                "Geocoding request - Provider: {Provider}, Query: {Query}",
                geocodingProvider.ProviderKey,
                query);

            var request = new GeocodingRequest
            {
                Query = query,
                MaxResults = 10
            };

            var response = await geocodingProvider.GeocodeAsync(request, cancellationToken);

            return Results.Ok(new
            {
                query,
                provider = geocodingProvider.ProviderKey,
                results = response.Results,
                attribution = response.Attribution
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid provider specified: {Provider}", provider);
            return Results.BadRequest(new { error = $"Invalid provider: {provider}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Geocoding request failed - Provider: {Provider}, Query: {Query}", provider, query);
            return Results.Problem(
                title: "Geocoding failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Reverse geocodes coordinates to an address.
    /// GET /api/location/reverse?lat={lat}&lon={lon}&provider={provider?}
    /// </summary>
    public static async Task<IResult> HandleReverseGeocodeAsync(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] string? provider,
        [FromServices] IGeocodingProvider defaultProvider,
        [FromServices] IServiceProvider services,
        [FromServices] LocationServiceConfiguration config,
        [FromServices] ILogger<IGeocodingProvider> logger,
        CancellationToken cancellationToken)
    {
        // Validate coordinates
        if (lat < -90 || lat > 90)
        {
            return Results.BadRequest(new { error = "Latitude must be between -90 and 90" });
        }

        if (lon < -180 || lon > 180)
        {
            return Results.BadRequest(new { error = "Longitude must be between -180 and 180" });
        }

        try
        {
            // Select provider: use specified provider or default
            var geocodingProvider = string.IsNullOrWhiteSpace(provider)
                ? defaultProvider
                : services.GetGeocodingProvider(provider);

            logger.LogInformation(
                "Reverse geocoding request - Provider: {Provider}, Lat: {Lat}, Lon: {Lon}",
                geocodingProvider.ProviderKey,
                lat,
                lon);

            var request = new ReverseGeocodingRequest
            {
                Latitude = lat,
                Longitude = lon
            };

            var response = await geocodingProvider.ReverseGeocodeAsync(request, cancellationToken);

            return Results.Ok(new
            {
                location = new { lat, lon },
                provider = geocodingProvider.ProviderKey,
                results = response.Results,
                attribution = response.Attribution
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid provider specified: {Provider}", provider);
            return Results.BadRequest(new { error = $"Invalid provider: {provider}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reverse geocoding request failed - Provider: {Provider}, Lat: {Lat}, Lon: {Lon}", provider, lat, lon);
            return Results.Problem(
                title: "Reverse geocoding failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Calculates a route between waypoints.
    /// POST /api/location/route
    /// </summary>
    public static async Task<IResult> HandleRouteAsync(
        [FromBody] RoutingRequestDto request,
        [FromQuery] string? provider,
        [FromServices] IRoutingProvider defaultProvider,
        [FromServices] IServiceProvider services,
        [FromServices] LocationServiceConfiguration config,
        [FromServices] ILogger<IRoutingProvider> logger,
        CancellationToken cancellationToken)
    {
        // Validate input
        if (request.Waypoints == null || request.Waypoints.Count < 2)
        {
            return Results.BadRequest(new { error = "At least 2 waypoints are required" });
        }

        if (request.Waypoints.Count > 25)
        {
            return Results.BadRequest(new { error = "Maximum 25 waypoints allowed" });
        }

        // Validate waypoint format
        foreach (var waypoint in request.Waypoints)
        {
            if (waypoint.Length != 2)
            {
                return Results.BadRequest(new { error = "Each waypoint must be [longitude, latitude]" });
            }

            var lon = waypoint[0];
            var lat = waypoint[1];

            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            {
                return Results.BadRequest(new { error = "Invalid waypoint coordinates" });
            }
        }

        try
        {
            // Select provider: use specified provider or default
            var routingProvider = string.IsNullOrWhiteSpace(provider)
                ? defaultProvider
                : services.GetRoutingProvider(provider);

            logger.LogInformation(
                "Routing request - Provider: {Provider}, Waypoints: {WaypointCount}, Mode: {TravelMode}",
                routingProvider.ProviderKey,
                request.Waypoints.Count,
                request.TravelMode ?? "car");

            var routingRequest = new RoutingRequest
            {
                Waypoints = request.Waypoints,
                TravelMode = request.TravelMode ?? "car",
                AvoidTolls = request.AvoidTolls,
                AvoidHighways = request.AvoidHighways,
                AvoidFerries = request.AvoidFerries,
                UseTraffic = request.UseTraffic,
                DepartureTime = request.DepartureTime,
                Language = request.Language,
                UnitSystem = request.UnitSystem ?? "metric"
            };

            var response = await routingProvider.CalculateRouteAsync(routingRequest, cancellationToken);

            return Results.Ok(new
            {
                provider = routingProvider.ProviderKey,
                routes = response.Routes,
                attribution = response.Attribution
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid provider specified: {Provider}", provider);
            return Results.BadRequest(new { error = $"Invalid provider: {provider}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Routing request failed - Provider: {Provider}", provider);
            return Results.Problem(
                title: "Routing failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a basemap tile.
    /// GET /api/location/tiles/{provider}/{tilesetId}/{z}/{x}/{y}
    /// </summary>
    public static async Task<IResult> HandleGetTileAsync(
        string provider,
        string tilesetId,
        int z,
        int x,
        int y,
        [FromQuery] string? format,
        [FromServices] IBasemapTileProvider defaultProvider,
        [FromServices] IServiceProvider services,
        [FromServices] LocationServiceConfiguration config,
        [FromServices] ILogger<IBasemapTileProvider> logger,
        CancellationToken cancellationToken)
    {
        // Validate tile coordinates
        if (z < 0 || z > 22)
        {
            return Results.BadRequest(new { error = "Zoom level must be between 0 and 22" });
        }

        var maxTile = Math.Pow(2, z);
        if (x < 0 || x >= maxTile || y < 0 || y >= maxTile)
        {
            return Results.BadRequest(new { error = "Invalid tile coordinates for zoom level" });
        }

        try
        {
            // Select provider: use specified provider or default
            var tileProvider = string.IsNullOrWhiteSpace(provider)
                ? defaultProvider
                : services.GetBasemapTileProvider(provider);

            var request = new TileRequest
            {
                TilesetId = tilesetId,
                Z = z,
                X = x,
                Y = y,
                ImageFormat = format
            };

            var response = await tileProvider.GetTileAsync(request, cancellationToken);

            // Set cache headers (tiles are typically immutable)
            var result = Results.Bytes(response.Data, response.ContentType);

            // Note: In a real implementation, you would use TypedResults or a custom result
            // to add headers. This is simplified for the example.
            return Results.Stream(
                new MemoryStream(response.Data),
                contentType: response.ContentType,
                enableRangeProcessing: false);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid provider or tileset - Provider: {Provider}, Tileset: {Tileset}", provider, tilesetId);
            return Results.NotFound(new { error = $"Provider or tileset not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tile request failed - Provider: {Provider}, Tileset: {Tileset}, Z: {Z}, X: {X}, Y: {Y}",
                provider, tilesetId, z, x, y);
            return Results.Problem(
                title: "Tile request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Lists available location service providers.
    /// GET /api/location/providers
    /// </summary>
    public static async Task<IResult> HandleGetProvidersAsync(
        [FromServices] IServiceProvider services,
        [FromServices] LocationServiceConfiguration config,
        CancellationToken cancellationToken)
    {
        var providers = new
        {
            geocoding = new
            {
                defaultProvider = config.GeocodingProvider,
                availableProviders = GetAvailableProviders<IGeocodingProvider>(services)
            },
            routing = new
            {
                defaultProvider = config.RoutingProvider,
                availableProviders = GetAvailableProviders<IRoutingProvider>(services)
            },
            basemapTiles = new
            {
                defaultProvider = config.BasemapTileProvider,
                availableProviders = GetAvailableProviders<IBasemapTileProvider>(services)
            }
        };

        return await Task.FromResult(Results.Ok(providers));
    }

    /// <summary>
    /// Lists available tilesets for a provider.
    /// GET /api/location/tilesets?provider={provider}
    /// </summary>
    public static async Task<IResult> HandleGetTilesetsAsync(
        [FromQuery] string? provider,
        [FromServices] IBasemapTileProvider defaultProvider,
        [FromServices] IServiceProvider services,
        [FromServices] ILogger<IBasemapTileProvider> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            // Select provider: use specified provider or default
            var tileProvider = string.IsNullOrWhiteSpace(provider)
                ? defaultProvider
                : services.GetBasemapTileProvider(provider);

            logger.LogInformation("Tilesets request - Provider: {Provider}", tileProvider.ProviderKey);

            var tilesets = await tileProvider.GetAvailableTilesetsAsync(cancellationToken);

            return Results.Ok(new
            {
                provider = tileProvider.ProviderKey,
                tilesets
            });
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid provider specified: {Provider}", provider);
            return Results.BadRequest(new { error = $"Invalid provider: {provider}" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tilesets request failed - Provider: {Provider}", provider);
            return Results.Problem(
                title: "Tilesets request failed",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static List<string> GetAvailableProviders<T>(IServiceProvider services)
    {
        // This is a simplified implementation. In a real scenario, you would use
        // IServiceProviderIsKeyedService to get all registered keyed services.
        // For now, we'll return the known provider keys.
        var knownProviders = new List<string>();

        var testProviders = new[] { "azure-maps", "nominatim", "osrm", "openstreetmap" };
        foreach (var key in testProviders)
        {
            try
            {
                services.GetKeyedService<T>(key);
                knownProviders.Add(key);
            }
            catch
            {
                // Provider not registered
            }
        }

        return knownProviders;
    }
}

/// <summary>
/// DTO for routing requests.
/// </summary>
public sealed record RoutingRequestDto
{
    /// <summary>
    /// List of waypoints [longitude, latitude] defining the route.
    /// </summary>
    public required List<double[]> Waypoints { get; init; }

    /// <summary>
    /// Travel mode (e.g., "car", "truck", "bicycle", "pedestrian").
    /// </summary>
    public string? TravelMode { get; init; }

    /// <summary>
    /// Whether to avoid tolls.
    /// </summary>
    public bool AvoidTolls { get; init; }

    /// <summary>
    /// Whether to avoid highways/motorways.
    /// </summary>
    public bool AvoidHighways { get; init; }

    /// <summary>
    /// Whether to avoid ferries.
    /// </summary>
    public bool AvoidFerries { get; init; }

    /// <summary>
    /// Whether to include traffic information in the route.
    /// </summary>
    public bool UseTraffic { get; init; }

    /// <summary>
    /// Departure time for traffic-aware routing.
    /// </summary>
    public DateTimeOffset? DepartureTime { get; init; }

    /// <summary>
    /// Optional language code for instructions (ISO 639-1).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Unit system for distances ("metric" or "imperial").
    /// </summary>
    public string? UnitSystem { get; init; }
}
