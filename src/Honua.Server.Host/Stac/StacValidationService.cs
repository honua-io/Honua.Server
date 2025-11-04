// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Host.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Stac;

/// <summary>
/// Service for validating STAC JSON documents against the STAC specification.
/// </summary>
public interface IStacValidationService
{
    /// <summary>
    /// Validates a STAC collection JSON document.
    /// </summary>
    /// <param name="collectionJson">The collection JSON to validate</param>
    /// <returns>A validation result with any errors found</returns>
    StacValidationResult ValidateCollection(JsonObject collectionJson);

    /// <summary>
    /// Validates a STAC item JSON document.
    /// </summary>
    /// <param name="itemJson">The item JSON to validate</param>
    /// <returns>A validation result with any errors found</returns>
    StacValidationResult ValidateItem(JsonObject itemJson);
}

/// <summary>
/// Result of STAC JSON validation.
/// </summary>
public sealed record StacValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<StacValidationError> Errors { get; init; } = Array.Empty<StacValidationError>();

    public static StacValidationResult Success() => new() { Errors = Array.Empty<StacValidationError>() };
    public static StacValidationResult Failure(params StacValidationError[] errors) => new() { Errors = errors.ToList() };
}

/// <summary>
/// Represents a detailed validation error with field-level information.
/// </summary>
public sealed record StacValidationError
{
    /// <summary>
    /// The field that failed validation (e.g., "id", "bbox", "properties.datetime")
    /// </summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable error message explaining what went wrong
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The actual value that failed validation (optional, for context)
    /// </summary>
    public string? ActualValue { get; init; }

    /// <summary>
    /// The expected value or format (optional, for guidance)
    /// </summary>
    public string? ExpectedFormat { get; init; }

    /// <summary>
    /// An example of a valid value (optional, for user guidance)
    /// </summary>
    public string? Example { get; init; }

    /// <summary>
    /// Returns a formatted error message combining all available information
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string> { $"Field '{Field}': {Message}" };

        if (!ActualValue.IsNullOrEmpty())
        {
            parts.Add($"Actual value: '{ActualValue}'");
        }

        if (!ExpectedFormat.IsNullOrEmpty())
        {
            parts.Add($"Expected format: {ExpectedFormat}");
        }

        if (!Example.IsNullOrEmpty())
        {
            parts.Add($"Example: {Example}");
        }

        return string.Join(". ", parts);
    }
}

/// <summary>
/// Default implementation of STAC validation service.
/// Validates against STAC 1.0.0 specification requirements with detailed error messages.
/// </summary>
public sealed class StacValidationService : IStacValidationService
{
    private readonly ILogger<StacValidationService> _logger;
    private static readonly HashSet<string> SupportedGeometryTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Point",
        "MultiPoint",
        "LineString",
        "MultiLineString",
        "Polygon",
        "MultiPolygon",
        "GeometryCollection"
    };

    public StacValidationService(ILogger<StacValidationService> logger)
    {
        _logger = Guard.NotNull(logger);
    }

    public StacValidationResult ValidateCollection(JsonObject collectionJson)
    {
        var errors = new List<StacValidationError>();
        string? collectionId = null;

        // Try to get collection ID for logging
        if (collectionJson.TryGetPropertyValue("id", out var idNode))
        {
            collectionId = idNode?.GetValue<string>();
        }

        _logger.LogDebug("Validating STAC collection: {CollectionId}", collectionId ?? "unknown");

        // Validate required 'id' field
        if (collectionId.IsNullOrWhiteSpace())
        {
            errors.Add(new StacValidationError
            {
                Field = "id",
                Message = "Required field is missing or empty",
                ExpectedFormat = "A non-empty string identifier",
                Example = "my-collection-123"
            });
            _logger.LogWarning("Collection validation failed: missing or empty 'id' field");
        }
        else if (collectionId.Length > 256)
        {
            errors.Add(new StacValidationError
            {
                Field = "id",
                Message = "Collection ID exceeds maximum length",
                ActualValue = $"{collectionId.Length} characters",
                ExpectedFormat = "Maximum 256 characters"
            });
        }

        // Validate required 'description' field
        if (!collectionJson.TryGetPropertyValue("description", out var descNode) || descNode?.GetValue<string>().IsNullOrWhiteSpace() != false)
        {
            errors.Add(new StacValidationError
            {
                Field = "description",
                Message = "Required field is missing or empty",
                ExpectedFormat = "A non-empty string describing the collection",
                Example = "Satellite imagery of agricultural land cover"
            });
            _logger.LogDebug("Collection {CollectionId} validation: missing 'description'", collectionId ?? "unknown");
        }

        // Validate required 'license' field
        if (!collectionJson.TryGetPropertyValue("license", out var licenseNode) || licenseNode?.GetValue<string>().IsNullOrWhiteSpace() != false)
        {
            errors.Add(new StacValidationError
            {
                Field = "license",
                Message = "Required field is missing or empty",
                ExpectedFormat = "A valid SPDX license identifier or 'proprietary' or 'various'",
                Example = "CC-BY-4.0, MIT, proprietary"
            });
            _logger.LogDebug("Collection {CollectionId} validation: missing 'license'", collectionId ?? "unknown");
        }

        // Validate required 'extent' field
        if (collectionJson.TryGetPropertyValue("extent", out var extentNode) && extentNode is JsonObject extentObj)
        {
            ValidateExtent(extentObj, errors);
        }
        else
        {
            errors.Add(new StacValidationError
            {
                Field = "extent",
                Message = "Required field is missing or not an object",
                ExpectedFormat = "An object with 'spatial' and 'temporal' properties"
            });
            _logger.LogDebug("Collection {CollectionId} validation: missing 'extent'", collectionId ?? "unknown");
        }

        // Validate 'stac_version' if present
        if (collectionJson.TryGetPropertyValue("stac_version", out var versionNode))
        {
            var version = versionNode?.GetValue<string>();
            if (version.HasValue() && !version.StartsWith("1."))
            {
                errors.Add(new StacValidationError
                {
                    Field = "stac_version",
                    Message = "Unsupported STAC version",
                    ActualValue = version,
                    ExpectedFormat = "Version 1.0.0 or compatible (1.x.x)",
                    Example = "1.0.0"
                });
                _logger.LogWarning("Collection {CollectionId} validation: unsupported STAC version {Version}", collectionId ?? "unknown", version);
            }
        }

        // Validate 'type' if present (should be 'Collection')
        if (collectionJson.TryGetPropertyValue("type", out var typeNode))
        {
            var type = typeNode?.GetValue<string>();
            if (type.HasValue() && type != "Collection")
            {
                errors.Add(new StacValidationError
                {
                    Field = "type",
                    Message = "Invalid type for STAC collection",
                    ActualValue = type,
                    ExpectedFormat = "Must be 'Collection'",
                    Example = "Collection"
                });
            }
        }

        if (errors.Count == 0)
        {
            _logger.LogDebug("STAC collection validation successful: {CollectionId}", collectionId ?? "unknown");
        }
        else
        {
            _logger.LogWarning("STAC collection validation failed for {CollectionId}: {ErrorCount} errors found",
                collectionId ?? "unknown", errors.Count);
        }

        return errors.Count == 0 ? StacValidationResult.Success() : new StacValidationResult { Errors = errors };
    }

    public StacValidationResult ValidateItem(JsonObject itemJson)
    {
        var errors = new List<StacValidationError>();
        string? itemId = null;

        // Try to get item ID for logging
        if (itemJson.TryGetPropertyValue("id", out var idNode))
        {
            itemId = idNode?.GetValue<string>();
        }

        _logger.LogDebug("Validating STAC item: {ItemId}", itemId ?? "unknown");

        // Validate required 'id' field
        if (itemId.IsNullOrWhiteSpace())
        {
            errors.Add(new StacValidationError
            {
                Field = "id",
                Message = "Required field is missing or empty",
                ExpectedFormat = "A non-empty string identifier unique within the collection",
                Example = "item-2024-01-01-abc123"
            });
            _logger.LogWarning("Item validation failed: missing or empty 'id' field");
        }
        else if (itemId.Length > 256)
        {
            errors.Add(new StacValidationError
            {
                Field = "id",
                Message = "Item ID exceeds maximum length",
                ActualValue = $"{itemId.Length} characters",
                ExpectedFormat = "Maximum 256 characters"
            });
        }

        // Validate required 'type' field
        if (!itemJson.TryGetPropertyValue("type", out var typeNode) || typeNode?.GetValue<string>() != "Feature")
        {
            var actualType = typeNode?.GetValue<string>();
            errors.Add(new StacValidationError
            {
                Field = "type",
                Message = "Invalid or missing type field",
                ActualValue = actualType ?? "(missing)",
                ExpectedFormat = "Must be 'Feature' (GeoJSON Feature type)",
                Example = "Feature"
            });
            _logger.LogDebug("Item {ItemId} validation: invalid 'type' field", itemId ?? "unknown");
        }

        // Validate required 'geometry' field
        if (!itemJson.TryGetPropertyValue("geometry", out var geometryNode))
        {
            errors.Add(new StacValidationError
            {
                Field = "geometry",
                Message = "Required field is missing",
                ExpectedFormat = "A valid GeoJSON geometry object or null",
                Example = "{\"type\": \"Point\", \"coordinates\": [-122.5, 45.5]}"
            });
            _logger.LogDebug("Item {ItemId} validation: missing 'geometry'", itemId ?? "unknown");
        }
        else if (geometryNode is not null && geometryNode is not JsonObject && geometryNode.GetValueKind() != JsonValueKind.Null)
        {
            errors.Add(new StacValidationError
            {
                Field = "geometry",
                Message = "Invalid geometry type",
                ActualValue = geometryNode.GetValueKind().ToString(),
                ExpectedFormat = "A GeoJSON geometry object or null"
            });
            _logger.LogDebug("Item {ItemId} validation: invalid 'geometry' type", itemId ?? "unknown");
        }
        else if (geometryNode is JsonObject geometryObj)
        {
            ValidateGeometry(geometryObj, errors, "geometry");
        }

        // Validate required 'properties' field
        if (!itemJson.TryGetPropertyValue("properties", out var propertiesNode) || propertiesNode is not JsonObject)
        {
            errors.Add(new StacValidationError
            {
                Field = "properties",
                Message = "Required field is missing or not an object",
                ExpectedFormat = "A JSON object containing item properties",
                Example = "{\"datetime\": \"2024-01-01T00:00:00Z\"}"
            });
            _logger.LogDebug("Item {ItemId} validation: missing or invalid 'properties'", itemId ?? "unknown");
        }
        else
        {
            var propsObj = propertiesNode as JsonObject;
            ValidateItemDateTime(propsObj!, errors, itemId);
        }

        // Validate required 'assets' field
        if (!itemJson.TryGetPropertyValue("assets", out var assetsNode))
        {
            errors.Add(new StacValidationError
            {
                Field = "assets",
                Message = "Required field is missing",
                ExpectedFormat = "A JSON object containing asset definitions",
                Example = "{\"data\": {\"href\": \"https://example.com/data.tif\", \"type\": \"image/tiff\"}}"
            });
            _logger.LogDebug("Item {ItemId} validation: missing 'assets'", itemId ?? "unknown");
        }
        else if (assetsNode is not JsonObject)
        {
            errors.Add(new StacValidationError
            {
                Field = "assets",
                Message = "Invalid assets type",
                ActualValue = assetsNode.GetValueKind().ToString(),
                ExpectedFormat = "A JSON object where each key is an asset identifier"
            });
            _logger.LogDebug("Item {ItemId} validation: invalid 'assets' type", itemId ?? "unknown");
        }

        // Validate 'links' if present
        if (itemJson.TryGetPropertyValue("links", out var linksNode) && linksNode is JsonArray linksArray)
        {
            ValidateLinks(linksArray, errors);
        }

        // Validate 'bbox' if present
        if (itemJson.TryGetPropertyValue("bbox", out var bboxNode) && bboxNode is JsonArray bboxArray)
        {
            ValidateBbox(bboxArray, "bbox", errors);
        }

        if (errors.Count == 0)
        {
            _logger.LogDebug("STAC item validation successful: {ItemId}", itemId ?? "unknown");
        }
        else
        {
            _logger.LogWarning("STAC item validation failed for {ItemId}: {ErrorCount} errors found",
                itemId ?? "unknown", errors.Count);
        }

        return errors.Count == 0 ? StacValidationResult.Success() : new StacValidationResult { Errors = errors };
    }

    private static void ValidateExtent(JsonObject extentObj, List<StacValidationError> errors)
    {
        // Validate spatial extent
        if (!extentObj.TryGetPropertyValue("spatial", out var spatialNode) || spatialNode is not JsonObject)
        {
            errors.Add(new StacValidationError
            {
                Field = "extent.spatial",
                Message = "Required field is missing or not an object",
                ExpectedFormat = "An object with a 'bbox' array property"
            });
        }
        else
        {
            var spatialObj = spatialNode as JsonObject;
            if (!spatialObj!.TryGetPropertyValue("bbox", out var bboxNode) || bboxNode is not JsonArray)
            {
                errors.Add(new StacValidationError
                {
                    Field = "extent.spatial.bbox",
                    Message = "Required field is missing or not an array",
                    ExpectedFormat = "An array of bounding box arrays",
                    Example = "[[-180, -90, 180, 90]]"
                });
            }
            else
            {
                var bboxArray = bboxNode as JsonArray;
                // Validate each bbox in the array
                for (int i = 0; i < bboxArray!.Count; i++)
                {
                    if (bboxArray[i] is JsonArray innerBbox)
                    {
                        ValidateBbox(innerBbox, $"extent.spatial.bbox[{i}]", errors);
                    }
                }
            }
        }

        // Validate temporal extent
        if (!extentObj.TryGetPropertyValue("temporal", out var temporalNode) || temporalNode is not JsonObject)
        {
            errors.Add(new StacValidationError
            {
                Field = "extent.temporal",
                Message = "Required field is missing or not an object",
                ExpectedFormat = "An object with an 'interval' array property"
            });
        }
        else
        {
            var temporalObj = temporalNode as JsonObject;
            if (!temporalObj!.TryGetPropertyValue("interval", out var intervalNode) || intervalNode is not JsonArray)
            {
                errors.Add(new StacValidationError
                {
                    Field = "extent.temporal.interval",
                    Message = "Required field is missing or not an array",
                    ExpectedFormat = "An array of time interval arrays (start/end pairs)",
                    Example = "[[\"2020-01-01T00:00:00Z\", \"2020-12-31T23:59:59Z\"]]"
                });
            }
            else
            {
                ValidateTemporalInterval(intervalNode as JsonArray, errors);
            }
        }
    }

    private static void ValidateBbox(JsonArray bboxArray, string fieldPath, List<StacValidationError> errors)
    {
        if (bboxArray.Count != 4 && bboxArray.Count != 6)
        {
            var coordList = string.Join(", ", bboxArray.Select(v => v?.ToString() ?? "null"));
            errors.Add(new StacValidationError
            {
                Field = fieldPath,
                Message = $"Invalid number of coordinates: expected 4 or 6, but received {bboxArray.Count}",
                ActualValue = $"[{coordList}]",
                ExpectedFormat = "4 coordinates: [minX, minY, maxX, maxY] or 6 coordinates: [minX, minY, minZ, maxX, maxY, maxZ]",
                Example = "[-180, -90, 180, 90]"
            });
            return;
        }

        // Validate that all coordinates are numbers
        var coords = new double[bboxArray.Count];
        for (int i = 0; i < bboxArray.Count; i++)
        {
            if (bboxArray[i]?.GetValueKind() != JsonValueKind.Number)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[{i}]",
                    Message = "Coordinate must be a number",
                    ActualValue = bboxArray[i]?.ToString() ?? "null",
                    ExpectedFormat = "A numeric value (integer or decimal)"
                });
                return;
            }
            try
            {
                coords[i] = bboxArray[i]!.GetValue<double>();
            }
            catch
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[{i}]",
                    Message = "Failed to parse coordinate as number",
                    ActualValue = bboxArray[i]?.ToString() ?? "null"
                });
                return;
            }
        }

        // Validate coordinate ranges and min < max constraints
        if (bboxArray.Count == 4)
        {
            // 2D bbox: [minX, minY, maxX, maxY]
            if (coords[0] < -180 || coords[0] > 180)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[0]",
                    Message = "Minimum longitude (minX) is out of valid range",
                    ActualValue = coords[0].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -180 and 180"
                });
            }
            if (coords[1] < -90 || coords[1] > 90)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[1]",
                    Message = "Minimum latitude (minY) is out of valid range",
                    ActualValue = coords[1].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -90 and 90"
                });
            }
            if (coords[2] < -180 || coords[2] > 180)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[2]",
                    Message = "Maximum longitude (maxX) is out of valid range",
                    ActualValue = coords[2].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -180 and 180"
                });
            }
            if (coords[3] < -90 || coords[3] > 90)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[3]",
                    Message = "Maximum latitude (maxY) is out of valid range",
                    ActualValue = coords[3].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -90 and 90"
                });
            }
            if (coords[0] > coords[2])
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Minimum longitude must be less than or equal to maximum longitude",
                    ActualValue = $"minX={coords[0]}, maxX={coords[2]}",
                    ExpectedFormat = "minX <= maxX"
                });
            }
            if (coords[1] > coords[3])
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Minimum latitude must be less than or equal to maximum latitude",
                    ActualValue = $"minY={coords[1]}, maxY={coords[3]}",
                    ExpectedFormat = "minY <= maxY"
                });
            }
        }
        else if (bboxArray.Count == 6)
        {
            // 3D bbox: [minX, minY, minZ, maxX, maxY, maxZ]
            if (coords[0] < -180 || coords[0] > 180)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[0]",
                    Message = "Minimum longitude (minX) is out of valid range",
                    ActualValue = coords[0].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -180 and 180"
                });
            }
            if (coords[1] < -90 || coords[1] > 90)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[1]",
                    Message = "Minimum latitude (minY) is out of valid range",
                    ActualValue = coords[1].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -90 and 90"
                });
            }
            if (coords[3] < -180 || coords[3] > 180)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[3]",
                    Message = "Maximum longitude (maxX) is out of valid range",
                    ActualValue = coords[3].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -180 and 180"
                });
            }
            if (coords[4] < -90 || coords[4] > 90)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPath}[4]",
                    Message = "Maximum latitude (maxY) is out of valid range",
                    ActualValue = coords[4].ToString(CultureInfo.InvariantCulture),
                    ExpectedFormat = "Must be between -90 and 90"
                });
            }
            if (coords[0] > coords[3])
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Minimum longitude must be less than or equal to maximum longitude",
                    ActualValue = $"minX={coords[0]}, maxX={coords[3]}",
                    ExpectedFormat = "minX <= maxX"
                });
            }
            if (coords[1] > coords[4])
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Minimum latitude must be less than or equal to maximum latitude",
                    ActualValue = $"minY={coords[1]}, maxY={coords[4]}",
                    ExpectedFormat = "minY <= maxY"
                });
            }
            if (coords[2] > coords[5])
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Minimum altitude must be less than or equal to maximum altitude",
                    ActualValue = $"minZ={coords[2]}, maxZ={coords[5]}",
                    ExpectedFormat = "minZ <= maxZ"
                });
            }
        }
    }

    private static void ValidateTemporalInterval(JsonArray? intervalArray, List<StacValidationError> errors)
    {
        if (intervalArray.IsNullOrEmpty())
        {
            return;
        }

        for (int i = 0; i < intervalArray.Count; i++)
        {
            if (intervalArray[i] is not JsonArray pairArray)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"extent.temporal.interval[{i}]",
                    Message = "Temporal interval must be an array",
                    ExpectedFormat = "An array with exactly 2 elements: [start, end]",
                    Example = "[\"2020-01-01T00:00:00Z\", \"2020-12-31T23:59:59Z\"]"
                });
                continue;
            }

            if (pairArray.Count != 2)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"extent.temporal.interval[{i}]",
                    Message = $"Temporal interval must have exactly 2 elements, but has {pairArray.Count}",
                    ExpectedFormat = "[start, end] where each can be an RFC 3339 datetime string or null",
                    Example = "[\"2020-01-01T00:00:00Z\", null]"
                });
                continue;
            }

            // Validate start datetime
            var startValid = ValidateDateTime(pairArray[0], $"extent.temporal.interval[{i}][0]", errors, allowNull: true);
            var endValid = ValidateDateTime(pairArray[1], $"extent.temporal.interval[{i}][1]", errors, allowNull: true);

            // If both are valid and not null, check that start <= end
            if (startValid && endValid &&
                pairArray[0]?.GetValueKind() == JsonValueKind.String &&
                pairArray[1]?.GetValueKind() == JsonValueKind.String)
            {
                var startStr = pairArray[0]!.GetValue<string>();
                var endStr = pairArray[1]!.GetValue<string>();
                if (DateTimeOffset.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start) &&
                    DateTimeOffset.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end))
                {
                    if (start > end)
                    {
                        errors.Add(new StacValidationError
                        {
                            Field = $"extent.temporal.interval[{i}]",
                            Message = "Start datetime must be less than or equal to end datetime",
                            ActualValue = $"start={startStr}, end={endStr}",
                            ExpectedFormat = "start <= end"
                        });
                    }
                }
            }
        }
    }

    private static void ValidateGeometry(JsonObject geometryObj, List<StacValidationError> errors, string fieldPrefix)
    {
        if (!geometryObj.TryGetPropertyValue("type", out var typeNode) || typeNode?.GetValueKind() != JsonValueKind.String)
        {
            errors.Add(new StacValidationError
            {
                Field = $"{fieldPrefix}.type",
                Message = "Geometry type is required and must be a string",
                ExpectedFormat = "One of Point, MultiPoint, LineString, MultiLineString, Polygon, MultiPolygon, GeometryCollection"
            });
            return;
        }

        var geometryType = typeNode!.GetValue<string>() ?? string.Empty;

        if (!SupportedGeometryTypes.Contains(geometryType))
        {
            errors.Add(new StacValidationError
            {
                Field = $"{fieldPrefix}.type",
                Message = $"Unsupported geometry type '{geometryType}'",
                ExpectedFormat = "One of Point, MultiPoint, LineString, MultiLineString, Polygon, MultiPolygon, GeometryCollection"
            });
            return;
        }

        if (string.Equals(geometryType, "GeometryCollection", StringComparison.OrdinalIgnoreCase))
        {
            if (!geometryObj.TryGetPropertyValue("geometries", out var geometriesNode) || geometriesNode is not JsonArray geometriesArray)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"{fieldPrefix}.geometries",
                    Message = "GeometryCollection must include a 'geometries' array",
                    ExpectedFormat = "An array of GeoJSON geometry objects"
                });
                return;
            }

            for (int i = 0; i < geometriesArray.Count; i++)
            {
                if (geometriesArray[i] is JsonObject childGeometry)
                {
                    ValidateGeometry(childGeometry, errors, $"{fieldPrefix}.geometries[{i}]");
                }
                else
                {
                    errors.Add(new StacValidationError
                    {
                        Field = $"{fieldPrefix}.geometries[{i}]",
                        Message = "GeometryCollection entries must be GeoJSON geometry objects",
                        ExpectedFormat = "A JSON object with 'type' and 'coordinates' or 'geometries'"
                    });
                }
            }

            return;
        }

        if (!geometryObj.TryGetPropertyValue("coordinates", out var coordsNode) || coordsNode is not JsonArray coordsArray)
        {
            errors.Add(new StacValidationError
            {
                Field = $"{fieldPrefix}.coordinates",
                Message = "Geometry must include a 'coordinates' array",
                ExpectedFormat = "GeoJSON coordinate structure"
            });
            return;
        }

        if (!IsCoordinateStructureValid(geometryType, coordsArray))
        {
            errors.Add(new StacValidationError
            {
                Field = $"{fieldPrefix}.coordinates",
                Message = $"Coordinates for {geometryType} geometry are malformed",
                ExpectedFormat = "GeoJSON coordinate structure"
            });
        }
    }

    private static bool IsCoordinateStructureValid(string geometryType, JsonArray coordinates)
    {
        return geometryType switch
        {
            "Point" => IsPosition(coordinates),
            "MultiPoint" or "LineString" => coordinates.All(node => node is JsonArray position && IsPosition(position)),
            "MultiLineString" or "Polygon" => coordinates.All(node => node is JsonArray segment && segment.All(inner => inner is JsonArray position && IsPosition(position))),
            "MultiPolygon" => coordinates.All(node => node is JsonArray polygon && polygon.All(ring => ring is JsonArray segment && segment.All(pos => pos is JsonArray position && IsPosition(position)))),
            _ => true
        };
    }

    private static bool IsPosition(JsonArray position)
    {
        if (position.Count < 2)
        {
            return false;
        }

        return TryGetDouble(position[0], out _) && TryGetDouble(position[1], out _);
    }

    private static bool TryGetDouble(JsonNode? node, out double value)
    {
        value = default;

        if (node is null)
        {
            return false;
        }

        try
        {
            value = node.GetValue<double>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateItemDateTime(JsonObject propsObj, List<StacValidationError> errors, string? itemId)
    {
        var hasDatetime = propsObj.TryGetPropertyValue("datetime", out var datetimeNode);
        var hasStartDatetime = propsObj.TryGetPropertyValue("start_datetime", out var startNode);
        var hasEndDatetime = propsObj.TryGetPropertyValue("end_datetime", out var endNode);

        // Check if datetime is null
        bool datetimeIsNull = hasDatetime && (datetimeNode is null || datetimeNode.GetValueKind() == JsonValueKind.Null);

        if (!hasDatetime || datetimeIsNull)
        {
            // If datetime is null or missing, start_datetime and end_datetime are required
            if (!hasStartDatetime || !hasEndDatetime)
            {
                errors.Add(new StacValidationError
                {
                    Field = "properties.datetime",
                    Message = "Either 'datetime' must be provided, or both 'start_datetime' and 'end_datetime' are required",
                    ExpectedFormat = "RFC 3339 datetime string",
                    Example = "\"datetime\": \"2024-01-01T00:00:00Z\" or \"start_datetime\": \"2024-01-01T00:00:00Z\", \"end_datetime\": \"2024-01-01T23:59:59Z\""
                });
                return;
            }
        }

        // Validate datetime format if present
        if (hasDatetime && !datetimeIsNull)
        {
            ValidateDateTime(datetimeNode, "properties.datetime", errors, allowNull: false);
        }

        // Validate start_datetime format if present
        if (hasStartDatetime)
        {
            ValidateDateTime(startNode, "properties.start_datetime", errors, allowNull: false);
        }

        // Validate end_datetime format if present
        if (hasEndDatetime)
        {
            ValidateDateTime(endNode, "properties.end_datetime", errors, allowNull: false);
        }

        // Validate that start_datetime <= end_datetime if both are present
        if (hasStartDatetime && hasEndDatetime &&
            startNode?.GetValueKind() == JsonValueKind.String &&
            endNode?.GetValueKind() == JsonValueKind.String)
        {
            var startStr = startNode.GetValue<string>();
            var endStr = endNode.GetValue<string>();
            if (DateTimeOffset.TryParse(startStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var start) &&
                DateTimeOffset.TryParse(endStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var end))
            {
                if (start > end)
                {
                    errors.Add(new StacValidationError
                    {
                        Field = "properties",
                        Message = "start_datetime must be less than or equal to end_datetime",
                        ActualValue = $"start={startStr}, end={endStr}",
                        ExpectedFormat = "start_datetime <= end_datetime"
                    });
                }
            }
        }
    }

    private static bool ValidateDateTime(JsonNode? node, string fieldPath, List<StacValidationError> errors, bool allowNull)
    {
        if (node is null || node.GetValueKind() == JsonValueKind.Null)
        {
            if (!allowNull)
            {
                errors.Add(new StacValidationError
                {
                    Field = fieldPath,
                    Message = "Datetime cannot be null",
                    ExpectedFormat = "RFC 3339 datetime string",
                    Example = "2024-01-01T00:00:00Z"
                });
                return false;
            }
            return true;
        }

        if (node.GetValueKind() != JsonValueKind.String)
        {
            errors.Add(new StacValidationError
            {
                Field = fieldPath,
                Message = "Datetime must be a string",
                ActualValue = node.GetValueKind().ToString(),
                ExpectedFormat = "RFC 3339 datetime string",
                Example = "2024-01-01T00:00:00Z"
            });
            return false;
        }

        var datetimeStr = node.GetValue<string>();
        if (datetimeStr.IsNullOrWhiteSpace())
        {
            errors.Add(new StacValidationError
            {
                Field = fieldPath,
                Message = "Datetime string is empty",
                ExpectedFormat = "RFC 3339 datetime string",
                Example = "2024-01-01T00:00:00Z"
            });
            return false;
        }

        // Try to parse as RFC 3339 datetime
        if (!DateTimeOffset.TryParse(datetimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            errors.Add(new StacValidationError
            {
                Field = fieldPath,
                Message = "Invalid datetime format",
                ActualValue = datetimeStr,
                ExpectedFormat = "RFC 3339 datetime string (ISO 8601 with timezone)",
                Example = "2024-01-01T00:00:00Z or 2024-01-01T12:30:00+05:30 or 2024-01-01T12:30:00.123Z"
            });
            return false;
        }

        // Validate that the datetime is not too far in the future or past (sanity check)
        var now = DateTimeOffset.UtcNow;
        var minDate = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var maxDate = now.AddYears(100);

        if (parsed < minDate || parsed > maxDate)
        {
            errors.Add(new StacValidationError
            {
                Field = fieldPath,
                Message = "Datetime is outside reasonable range",
                ActualValue = datetimeStr,
                ExpectedFormat = $"Between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}"
            });
            return false;
        }

        return true;
    }

    private static void ValidateLinks(JsonArray linksArray, List<StacValidationError> errors)
    {
        for (int i = 0; i < linksArray.Count; i++)
        {
            if (linksArray[i] is not JsonObject linkObj)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"links[{i}]",
                    Message = "Link must be an object",
                    ExpectedFormat = "A JSON object with 'href' and 'rel' properties"
                });
                continue;
            }

            if (!linkObj.TryGetPropertyValue("href", out var hrefNode) || hrefNode?.GetValue<string>().IsNullOrWhiteSpace() == true)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"links[{i}].href",
                    Message = "Required field is missing or empty",
                    ExpectedFormat = "A non-empty URL string",
                    Example = "https://example.com/collection/item"
                });
            }

            if (!linkObj.TryGetPropertyValue("rel", out var relNode) || relNode?.GetValue<string>().IsNullOrWhiteSpace() == true)
            {
                errors.Add(new StacValidationError
                {
                    Field = $"links[{i}].rel",
                    Message = "Required field is missing or empty",
                    ExpectedFormat = "A non-empty link relation type",
                    Example = "self, parent, collection, item"
                });
            }
        }
    }
}
