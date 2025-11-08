// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.LocationServices;

/// <summary>
/// Extension methods for mapping location services endpoints.
/// </summary>
public static class LocationServicesEndpointExtensions
{
    /// <summary>
    /// Maps all location services endpoints for geocoding, routing, and basemap tiles.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <remarks>
    /// Provides endpoints for:
    /// - Geocoding addresses to coordinates
    /// - Reverse geocoding coordinates to addresses
    /// - Calculating routes between waypoints
    /// - Serving basemap tiles from various providers
    /// - Listing available providers and tilesets
    ///
    /// All endpoints support optional provider selection to use different backends
    /// (Azure Maps, OpenStreetMap, OSRM, Nominatim, etc.).
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example geocoding request:
    /// GET /api/location/geocode?query=1600+Amphitheatre+Parkway,+Mountain+View,+CA
    ///
    /// // Example reverse geocoding request:
    /// GET /api/location/reverse?lat=37.4224&amp;lon=-122.0840
    ///
    /// // Example routing request:
    /// POST /api/location/route
    /// {
    ///   "waypoints": [[-122.0840, 37.4224], [-122.4194, 37.7749]],
    ///   "travelMode": "car"
    /// }
    ///
    /// // Example tile request:
    /// GET /api/location/tiles/azure-maps/road/5/5/12
    ///
    /// // Example provider listing:
    /// GET /api/location/providers
    ///
    /// // Example tileset listing:
    /// GET /api/location/tilesets?provider=azure-maps
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapLocationServicesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        var group = endpoints.MapGroup("/api/location")
            .WithTags("Location Services")
            .RequireAuthorization("RequireViewer");

        // Geocoding endpoint
        group.MapGet("/geocode", LocationServicesEndpoints.HandleGeocodeAsync)
            .WithName("GeocodeAddress")
            .WithSummary("Geocode an address to coordinates")
            .WithDescription(
                "Converts an address or place name to geographic coordinates. " +
                "Optionally specify a provider to use a specific geocoding service.")
            .Produces<object>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireRateLimiting("location-services");

        // Reverse geocoding endpoint
        group.MapGet("/reverse", LocationServicesEndpoints.HandleReverseGeocodeAsync)
            .WithName("ReverseGeocode")
            .WithSummary("Reverse geocode coordinates to an address")
            .WithDescription(
                "Converts geographic coordinates to an address or place name. " +
                "Optionally specify a provider to use a specific geocoding service.")
            .Produces<object>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireRateLimiting("location-services");

        // Routing endpoint
        group.MapPost("/route", LocationServicesEndpoints.HandleRouteAsync)
            .WithName("CalculateRoute")
            .WithSummary("Calculate a route between waypoints")
            .WithDescription(
                "Calculates a route with turn-by-turn directions between multiple waypoints. " +
                "Supports various travel modes (car, truck, bicycle, pedestrian) and routing options. " +
                "Optionally specify a provider to use a specific routing service.")
            .Accepts<RoutingRequestDto>("application/json")
            .Produces<object>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .RequireRateLimiting("location-services");

        // Tile endpoint
        group.MapGet("/tiles/{provider}/{tilesetId}/{z:int}/{x:int}/{y:int}", LocationServicesEndpoints.HandleGetTileAsync)
            .WithName("GetBasemapTile")
            .WithSummary("Get a basemap tile")
            .WithDescription(
                "Retrieves a basemap tile for rendering maps. " +
                "Tiles are returned in PNG, JPEG, or vector tile format depending on the tileset. " +
                "Supports standard XYZ tile addressing scheme.")
            .Produces(StatusCodes.Status200OK, contentType: "image/png")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status200OK, contentType: "application/vnd.mapbox-vector-tile")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .CacheOutput(policy =>
            {
                // Tiles are immutable and can be cached aggressively
                policy.Expire(TimeSpan.FromDays(7));
                policy.Tag("basemap-tiles");
            })
            .RequireRateLimiting("tile-requests");

        // Provider listing endpoint
        group.MapGet("/providers", LocationServicesEndpoints.HandleGetProvidersAsync)
            .WithName("ListLocationProviders")
            .WithSummary("List available location service providers")
            .WithDescription(
                "Returns a list of all configured location service providers " +
                "for geocoding, routing, and basemap tiles, including the default provider for each service.")
            .Produces<object>(StatusCodes.Status200OK)
            .CacheOutput(policy =>
            {
                // Provider list rarely changes
                policy.Expire(TimeSpan.FromMinutes(15));
                policy.Tag("providers");
            });

        // Tileset listing endpoint
        group.MapGet("/tilesets", LocationServicesEndpoints.HandleGetTilesetsAsync)
            .WithName("ListTilesets")
            .WithSummary("List available tilesets for a provider")
            .WithDescription(
                "Returns a list of all available basemap tilesets from a specific provider. " +
                "Optionally specify a provider to list tilesets from a different service.")
            .Produces<object>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .CacheOutput(policy =>
            {
                // Tileset list rarely changes
                policy.Expire(TimeSpan.FromMinutes(15));
                policy.Tag("tilesets");
            });

        return endpoints;
    }
}
