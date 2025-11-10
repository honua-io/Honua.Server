// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Elevation;

/// <summary>
/// Enriches GeoJSON geometries with elevation (Z) coordinates.
/// Converts 2D coordinates [lon, lat] to 3D coordinates [lon, lat, z].
/// </summary>
public static class GeoJsonElevationEnricher
{
    /// <summary>
    /// Adds elevation to a GeoJSON geometry object.
    /// </summary>
    /// <param name="geometry">GeoJSON geometry as JsonNode</param>
    /// <param name="elevationService">Elevation service to use</param>
    /// <param name="context">Context for elevation lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New geometry with Z coordinates added</returns>
    public static async Task<JsonNode?> EnrichGeometryAsync(
        JsonNode? geometry,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(elevationService);
        Guard.NotNull(context);

        if (geometry is null)
        {
            return null;
        }

        if (geometry is not JsonObject geometryObj)
        {
            return geometry;
        }

        if (!geometryObj.TryGetPropertyValue("type", out var typeNode) ||
            typeNode is null)
        {
            return geometry;
        }

        var type = typeNode.GetValue<string>();
        if (!geometryObj.TryGetPropertyValue("coordinates", out var coordsNode) ||
            coordsNode is null)
        {
            return geometry;
        }

        // Clone the geometry to avoid modifying the original
        var enrichedGeometry = geometryObj.DeepClone().AsObject();

        // Process coordinates based on geometry type
        var enrichedCoords = type switch
        {
            "Point" => await EnrichPointCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            "LineString" => await EnrichLineStringCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            "Polygon" => await EnrichPolygonCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            "MultiPoint" => await EnrichMultiPointCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            "MultiLineString" => await EnrichMultiLineStringCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            "MultiPolygon" => await EnrichMultiPolygonCoordinatesAsync(coordsNode, elevationService, context, cancellationToken).ConfigureAwait(false),
            _ => coordsNode // Unknown type, return as-is
        };

        enrichedGeometry["coordinates"] = enrichedCoords;
        return enrichedGeometry;
    }

    private static async Task<JsonNode?> EnrichPointCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray coords || coords.Count < 2)
        {
            return coordsNode;
        }

        var lon = coords[0]?.GetValue<double>() ?? 0;
        var lat = coords[1]?.GetValue<double>() ?? 0;

        // Get elevation
        var elevation = await elevationService.GetElevationAsync(lon, lat, context, cancellationToken)
            .ConfigureAwait(false);

        // Create new array with Z coordinate
        var enriched = new JsonArray(
            coords[0]!.DeepClone(),
            coords[1]!.DeepClone(),
            elevation ?? 0.0
        );

        return enriched;
    }

    private static async Task<JsonNode?> EnrichLineStringCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray coordsArray)
        {
            return coordsNode;
        }

        // Extract all coordinates for batch processing
        var coordinates = new List<(double Lon, double Lat)>();
        foreach (var coord in coordsArray)
        {
            if (coord is JsonArray point && point.Count >= 2)
            {
                var lon = point[0]?.GetValue<double>() ?? 0;
                var lat = point[1]?.GetValue<double>() ?? 0;
                coordinates.Add((lon, lat));
            }
        }

        // Get elevations in batch
        var elevations = await elevationService.GetElevationsAsync(coordinates, context, cancellationToken)
            .ConfigureAwait(false);

        // Build enriched coordinates array
        var enriched = new JsonArray();
        for (int i = 0; i < coordinates.Count; i++)
        {
            var coord = coordsArray[i] as JsonArray;
            if (coord is not null && coord.Count >= 2)
            {
                var point = new JsonArray(
                    coord[0]!.DeepClone(),
                    coord[1]!.DeepClone(),
                    elevations[i] ?? 0.0
                );
                enriched.Add(point);
            }
        }

        return enriched;
    }

    private static async Task<JsonNode?> EnrichPolygonCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray ringsArray)
        {
            return coordsNode;
        }

        var enriched = new JsonArray();
        foreach (var ring in ringsArray)
        {
            var enrichedRing = await EnrichLineStringCoordinatesAsync(ring, elevationService, context, cancellationToken)
                .ConfigureAwait(false);
            enriched.Add(enrichedRing);
        }

        return enriched;
    }

    private static async Task<JsonNode?> EnrichMultiPointCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray pointsArray)
        {
            return coordsNode;
        }

        // Extract all coordinates for batch processing
        var coordinates = new List<(double Lon, double Lat)>();
        foreach (var point in pointsArray)
        {
            if (point is JsonArray coord && coord.Count >= 2)
            {
                var lon = coord[0]?.GetValue<double>() ?? 0;
                var lat = coord[1]?.GetValue<double>() ?? 0;
                coordinates.Add((lon, lat));
            }
        }

        // Get elevations in batch
        var elevations = await elevationService.GetElevationsAsync(coordinates, context, cancellationToken)
            .ConfigureAwait(false);

        // Build enriched coordinates array
        var enriched = new JsonArray();
        for (int i = 0; i < coordinates.Count; i++)
        {
            var coord = pointsArray[i] as JsonArray;
            if (coord is not null && coord.Count >= 2)
            {
                var point = new JsonArray(
                    coord[0]!.DeepClone(),
                    coord[1]!.DeepClone(),
                    elevations[i] ?? 0.0
                );
                enriched.Add(point);
            }
        }

        return enriched;
    }

    private static async Task<JsonNode?> EnrichMultiLineStringCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray lineStringsArray)
        {
            return coordsNode;
        }

        var enriched = new JsonArray();
        foreach (var lineString in lineStringsArray)
        {
            var enrichedLineString = await EnrichLineStringCoordinatesAsync(lineString, elevationService, context, cancellationToken)
                .ConfigureAwait(false);
            enriched.Add(enrichedLineString);
        }

        return enriched;
    }

    private static async Task<JsonNode?> EnrichMultiPolygonCoordinatesAsync(
        JsonNode coordsNode,
        IElevationService elevationService,
        ElevationContext context,
        CancellationToken cancellationToken)
    {
        if (coordsNode is not JsonArray polygonsArray)
        {
            return coordsNode;
        }

        var enriched = new JsonArray();
        foreach (var polygon in polygonsArray)
        {
            var enrichedPolygon = await EnrichPolygonCoordinatesAsync(polygon, elevationService, context, cancellationToken)
                .ConfigureAwait(false);
            enriched.Add(enrichedPolygon);
        }

        return enriched;
    }

    /// <summary>
    /// Adds building height property to a feature for 3D extrusion.
    /// This follows the deck.gl convention of storing height in properties.
    /// </summary>
    /// <param name="properties">Feature properties object</param>
    /// <param name="context">Elevation context with feature attributes</param>
    /// <returns>Properties with height added (if available)</returns>
    public static JsonObject? AddBuildingHeight(JsonObject? properties, ElevationContext context)
    {
        Guard.NotNull(context);

        var height = AttributeElevationService.GetBuildingHeight(context);
        if (!height.HasValue || properties is null)
        {
            return properties;
        }

        // Clone properties to avoid modifying original
        var enrichedProps = properties.DeepClone().AsObject();

        // Add height for deck.gl extrusion
        // Use standard property name for building height
        enrichedProps["height"] = height.Value;

        return enrichedProps;
    }
}
