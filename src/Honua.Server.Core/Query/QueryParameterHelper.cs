// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query;

/// <summary>
/// Shared utilities for parsing and validating query parameters across different API standards
/// (OGC API Features, WFS, ArcGIS GeoServices REST).
/// Provides parameter-agnostic parsing without assuming specific parameter names or error formats.
/// </summary>
public static class QueryParameterHelper
{
    /// <summary>
    /// Parses a limit parameter (maximum number of features to return).
    /// Returns the parsed limit clamped to the effective maximum, or the effective maximum/fallback if not provided.
    /// If no raw value, maximums, or fallback are provided, returns null to allow the caller to decide on pagination.
    /// </summary>
    /// <param name="raw">Raw parameter value (may be null or empty).</param>
    /// <param name="serviceMax">Service-level maximum limit.</param>
    /// <param name="layerMax">Layer-level maximum limit.</param>
    /// <param name="fallback">Fallback limit if no maximums are configured. If null, returns null when no raw value is provided.</param>
    /// <returns>Tuple of (parsed limit or null, error message). Error is null on success.</returns>
    public static (int? Value, string? Error) ParseLimit(
        string? raw,
        int? serviceMax,
        int? layerMax,
        int? fallback = null)
    {
        // Calculate effective maximum from configured limits
        int? effectiveMax = null;
        if (layerMax.HasValue && serviceMax.HasValue)
        {
            effectiveMax = Math.Min(layerMax.Value, serviceMax.Value);
        }
        else if (layerMax.HasValue)
        {
            effectiveMax = layerMax.Value;
        }
        else if (serviceMax.HasValue)
        {
            effectiveMax = serviceMax.Value;
        }

        // If no raw value provided, return configured limit or fallback (may be null)
        if (raw.IsNullOrWhiteSpace())
        {
            return (effectiveMax ?? fallback, null);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            return (null, "limit must be a positive integer.");
        }

        // Allow zero to mean "use default" in some APIs (GeoServices)
        if (parsed == 0)
        {
            return (effectiveMax ?? fallback, null);
        }

        // Clamp to effective max if one exists
        if (effectiveMax.HasValue)
        {
            return (Math.Min(parsed, effectiveMax.Value), null);
        }

        return (parsed, null);
    }

    /// <summary>
    /// Parses an offset parameter (starting index for pagination).
    /// Returns null if not provided or zero.
    /// </summary>
    /// <param name="raw">Raw parameter value (may be null or empty).</param>
    /// <returns>Tuple of (parsed offset or null, error message). Error is null on success.</returns>
    public static (int? Value, string? Error) ParseOffset(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return (null, "offset must be a non-negative integer.");
        }

        return (parsed == 0 ? null : parsed, null);
    }

    /// <summary>
    /// Parses a bounding box parameter.
    /// Supports 2D (4 values: minX, minY, maxX, maxY) and 3D (6 values: minX, minY, minZ, maxX, maxY, maxZ).
    /// </summary>
    /// <param name="raw">Raw parameter value (comma-separated coordinates).</param>
    /// <param name="crs">Optional CRS for the bounding box.</param>
    /// <returns>Tuple of (bounding box or null, error message). Error is null on success.</returns>
    public static (BoundingBox? Value, string? Error) ParseBoundingBox(string? raw, string? crs)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4 && parts.Length != 6)
        {
            return (null, "bounding box must contain 4 values (2D) or 6 values (3D).");
        }

        var coords = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var coord))
            {
                return (null, $"bounding box coordinate '{parts[i]}' is not a valid number.");
            }
            coords[i] = coord;
        }

        // Validate min < max
        var minX = coords[0];
        var minY = coords[1];
        var maxX = parts.Length == 4 ? coords[2] : coords[3];
        var maxY = parts.Length == 4 ? coords[3] : coords[4];

        var wrapsDateline = minX > maxX && minX > 0 && maxX < 0;
        if (!wrapsDateline && minX >= maxX)
        {
            return (null, "bounding box minX must be less than maxX.");
        }

        if (minY >= maxY)
        {
            return (null, "bounding box minY must be less than maxY.");
        }

        // Parse CRS if provided (optional for bounding box)
        var normalizedCrs = crs.IsNullOrWhiteSpace()
            ? CrsHelper.DefaultCrsIdentifier
            : CrsHelper.NormalizeIdentifier(crs);

        if (string.Equals(normalizedCrs, CrsHelper.DefaultCrsIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            static bool IsLongitudeValid(double value) => value is >= -180 and <= 180;
            static bool IsLatitudeValid(double value) => value is >= -90 and <= 90;

            if (!IsLongitudeValid(minX) || !IsLongitudeValid(maxX) || !IsLatitudeValid(minY) || !IsLatitudeValid(maxY))
            {
                return (null, "bounding box coordinates must fall within WGS84 ranges (-180..180 longitude, -90..90 latitude).");
            }
        }

        // Support 3D coordinates
        double? minZ = null;
        double? maxZ = null;
        if (parts.Length == 6)
        {
            minZ = coords[2];
            maxZ = coords[5];
        }

        return (new BoundingBox(minX, minY, maxX, maxY, minZ, maxZ, normalizedCrs), null);
    }

    /// <summary>
    /// Parses a CRS/spatial reference parameter and validates it against a list of supported CRS.
    /// Returns the normalized CRS identifier (e.g., "EPSG:4326").
    /// </summary>
    /// <param name="raw">Raw CRS value (e.g., "EPSG:4326", "4326", "http://www.opengis.net/def/crs/EPSG/0/4326").</param>
    /// <param name="supported">List of supported CRS identifiers.</param>
    /// <param name="defaultCrs">Default CRS if none specified.</param>
    /// <returns>Tuple of (normalized CRS or null, error message). Error is null on success.</returns>
    public static (string? Value, string? Error) ParseCrs(
        string? raw,
        IReadOnlyList<string> supported,
        string? defaultCrs)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (defaultCrs, null);
        }

        var normalized = CrsHelper.NormalizeIdentifier(raw);
        if (supported.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            return (normalized, null);
        }

        var supportedList = string.Join(", ", supported);
        return (null, $"CRS '{raw}' is not supported. Supported CRS: {supportedList}");
    }

    /// <summary>
    /// Parses a CRS parameter to an SRID (integer EPSG code).
    /// </summary>
    /// <param name="raw">Raw CRS value.</param>
    /// <returns>Tuple of (SRID or null, error message). Error is null on success.</returns>
    public static (int? Value, string? Error) ParseCrsToSrid(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        try
        {
            var normalized = CrsHelper.NormalizeIdentifier(raw);
            var srid = CrsHelper.ParseCrs(normalized);
            return (srid, null);
        }
        catch (Exception ex)
        {
            return (null, $"Invalid CRS '{raw}': {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a temporal range parameter.
    /// Supports ISO 8601 formats: single instant, date range, or open-ended ranges.
    /// </summary>
    /// <param name="raw">Raw temporal value (e.g., "2023-01-15T10:30:00Z" or "2023-01-01/2023-12-31").</param>
    /// <returns>Tuple of (temporal interval or null, error message). Error is null on success.</returns>
    public static (TemporalInterval? Value, string? Error) ParseTemporalRange(string? raw)
    {
        if (raw.IsNullOrWhiteSpace() || raw == "..")
        {
            return (null, null);
        }

        // Check for range separator
        var parts = raw.Split('/', StringSplitOptions.None);
        if (parts.Length > 2)
        {
            return (null, "temporal parameter must be a single instant or a range (start/end).");
        }

        if (parts.Length == 1)
        {
            // Single instant
            var (instant, error) = ParseDateTimeOffset(parts[0]);
            if (error is not null)
            {
                return (null, error);
            }
            return (new TemporalInterval(instant, instant), null);
        }

        // Range: start/end
        var (start, startError) = ParseDateTimeOffset(parts[0]);
        if (startError is not null && parts[0].HasValue() && parts[0] != "..")
        {
            return (null, $"temporal range start: {startError}");
        }

        var (end, endError) = ParseDateTimeOffset(parts[1]);
        if (endError is not null && parts[1].HasValue() && parts[1] != "..")
        {
            return (null, $"temporal range end: {endError}");
        }

        return (new TemporalInterval(start, end), null);
    }

    /// <summary>
    /// Parses a property names parameter (comma-separated list of field names).
    /// Validates that all fields exist in the layer definition and excludes geometry and ID fields.
    /// </summary>
    /// <param name="raw">Raw property names (comma-separated or "*" for all).</param>
    /// <param name="availableFields">Set of available field names from layer definition.</param>
    /// <param name="idField">ID field name (automatically included).</param>
    /// <param name="geometryField">Geometry field name (automatically excluded).</param>
    /// <returns>Tuple of (property name list or null for all, error message). Error is null on success.</returns>
    public static (IReadOnlyList<string>? Value, string? Error) ParsePropertyNames(
        string? raw,
        IReadOnlyCollection<string> availableFields,
        string? idField,
        string? geometryField)
    {
        if (raw.IsNullOrWhiteSpace() || string.Equals(raw, "*", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null); // null means "all fields"
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var requested = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(f => f.Trim())
            .Where(f => f.HasValue())
            .ToList();

        if (requested.Count == 0)
        {
            return (null, "property names must contain at least one field or '*'.");
        }

        // Validate field existence
        var validFields = new HashSet<string>(availableFields, comparer);
        if (idField.HasValue())
        {
            validFields.Add(idField);
        }

        var invalidFields = requested.Where(f => !validFields.Contains(f)).ToList();
        if (invalidFields.Count > 0)
        {
            var invalid = string.Join(", ", invalidFields);
            return (null, $"Unknown fields: {invalid}");
        }

        // Remove geometry field if present
        var result = requested
            .Where(f => geometryField.IsNullOrWhiteSpace() || !string.Equals(f, geometryField, StringComparison.OrdinalIgnoreCase))
            .Distinct(comparer)
            .ToList();

        return (result, null);
    }

    /// <summary>
    /// Parses a sort order parameter.
    /// Supports multiple sort fields with optional direction (ASC/DESC or +/-).
    /// </summary>
    /// <param name="raw">Raw sort specification (e.g., "field1 ASC,field2 DESC" or "field1:asc,field2:desc").</param>
    /// <param name="availableFields">Set of available field names from layer definition.</param>
    /// <param name="separator">Separator between field name and direction (default: space or colon).</param>
    /// <returns>Tuple of (sort order list or null, error message). Error is null on success.</returns>
    public static (IReadOnlyList<FeatureSortOrder>? Value, string? Error) ParseSortOrders(
        string? raw,
        IReadOnlyCollection<string> availableFields,
        char separator = ' ')
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var validFields = new HashSet<string>(availableFields, comparer);
        var orders = new List<FeatureSortOrder>();

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (token.IsNullOrWhiteSpace())
            {
                continue;
            }

            // Parse field and direction
            var parts = separator == ' '
                ? token.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : token.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                continue;
            }

            var fieldName = parts[0];
            if (!validFields.Contains(fieldName))
            {
                return (null, $"sort field '{fieldName}' does not exist.");
            }

            var direction = FeatureSortDirection.Ascending;
            if (parts.Length > 1)
            {
                var modifier = parts[1];
                if (modifier.Equals("DESC", StringComparison.OrdinalIgnoreCase) ||
                    modifier.Equals("D", StringComparison.OrdinalIgnoreCase) ||
                    modifier.Equals("-", StringComparison.Ordinal))
                {
                    direction = FeatureSortDirection.Descending;
                }
                else if (!modifier.Equals("ASC", StringComparison.OrdinalIgnoreCase) &&
                         !modifier.Equals("A", StringComparison.OrdinalIgnoreCase) &&
                         !modifier.Equals("+", StringComparison.Ordinal))
                {
                    return (null, $"sort direction '{modifier}' is not supported. Use ASC or DESC.");
                }
            }

            orders.Add(new FeatureSortOrder(fieldName, direction));
        }

        return (orders.Count > 0 ? orders : null, null);
    }

    /// <summary>
    /// Parses a result type parameter.
    /// Converts various representations (hits/results, count only, boolean) to FeatureResultType.
    /// </summary>
    /// <param name="raw">Raw result type value (e.g., "hits", "results", "true", "false").</param>
    /// <param name="defaultValue">Default result type if not specified.</param>
    /// <returns>Tuple of (result type, error message). Error is null on success.</returns>
    public static (FeatureResultType Value, string? Error) ParseResultType(
        string? raw,
        FeatureResultType defaultValue = FeatureResultType.Results)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (defaultValue, null);
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "results" => (FeatureResultType.Results, null),
            "hits" => (FeatureResultType.Hits, null),
            "count" => (FeatureResultType.Hits, null),
            "true" => (FeatureResultType.Hits, null),  // For returnCountOnly=true
            "false" => (FeatureResultType.Results, null),  // For returnCountOnly=false
            "1" => (FeatureResultType.Hits, null),
            "0" => (FeatureResultType.Results, null),
            _ => (defaultValue, $"result type '{raw}' is not supported. Use 'results' or 'hits'.")
        };
    }

    /// <summary>
    /// Parses a boolean parameter with flexible input formats.
    /// Supports: true/false, True/False, TRUE/FALSE, 1/0, yes/no, y/n.
    /// </summary>
    /// <param name="raw">Raw boolean value.</param>
    /// <param name="defaultValue">Default value if not specified.</param>
    /// <returns>Tuple of (boolean value, error message). Error is null on success.</returns>
    public static (bool Value, string? Error) ParseBoolean(string? raw, bool defaultValue)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (defaultValue, null);
        }

        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "true" or "t" or "yes" or "y" or "1" => (true, null),
            "false" or "f" or "no" or "n" or "0" => (false, null),
            _ => (defaultValue, $"boolean value '{raw}' is not supported. Use true, false, 1, or 0.")
        };
    }

    /// <summary>
    /// Parses a comma-separated list parameter.
    /// Returns an empty list for null/empty input.
    /// </summary>
    /// <param name="raw">Raw comma-separated value.</param>
    /// <returns>List of trimmed non-empty values.</returns>
    public static IReadOnlyList<string> ParseCommaSeparatedList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length == 0 ? Array.Empty<string>() : tokens;
    }

    /// <summary>
    /// Parses a positive integer parameter with validation.
    /// </summary>
    /// <param name="raw">Raw parameter value.</param>
    /// <param name="allowZero">Whether to allow zero as a valid value.</param>
    /// <returns>Tuple of (parsed value or null, error message). Error is null on success.</returns>
    public static (int? Value, string? Error) ParsePositiveInt(string? raw, bool allowZero = false)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return (null, "must be a valid integer.");
        }

        if (parsed < 0 || (!allowZero && parsed == 0))
        {
            return (null, allowZero ? "must be a non-negative integer." : "must be a positive integer.");
        }

        return (parsed, null);
    }

    /// <summary>
    /// Parses a double parameter with validation.
    /// </summary>
    /// <param name="raw">Raw parameter value.</param>
    /// <returns>Tuple of (parsed value or null, error message). Error is null on success.</returns>
    public static (double? Value, string? Error) ParseDouble(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return (null, "must be a valid number.");
        }

        return (parsed, null);
    }

    /// <summary>
    /// Parses a bounding box array without CRS information.
    /// Returns a double array with 4 or 6 values depending on dimensionality.
    /// </summary>
    /// <param name="raw">Raw bounding box string.</param>
    /// <param name="allowAltitude">Whether to allow 3D coordinates (6 values).</param>
    /// <returns>Tuple of (coordinate array or null, error message). Error is null on success.</returns>
    public static (double[]? Value, string? Error) ParseBoundingBoxArray(string? raw, bool allowAltitude = false)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return (null, null);
        }

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var expectedLengths = allowAltitude ? new[] { 4, 6 } : new[] { 4 };

        if (Array.IndexOf(expectedLengths, parts.Length) < 0)
        {
            var detail = allowAltitude
                ? $"must contain 4 coordinates (2D) or 6 coordinates (3D), but received {parts.Length} values."
                : $"must contain 4 coordinates (2D), but received {parts.Length} values.";
            return (null, detail);
        }

        var coords = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out coords[i]))
            {
                return (null, $"coordinate at position {i + 1} ('{parts[i]}') is not a valid number.");
            }
        }

        // Validate min < max
        var minX = coords[0];
        var minY = coords[1];
        var maxX = parts.Length == 4 ? coords[2] : coords[3];
        var maxY = parts.Length == 4 ? coords[3] : coords[4];

        if (minX >= maxX)
        {
            return (null, $"minimum X ({minX}) must be less than maximum X ({maxX}).");
        }

        if (minY >= maxY)
        {
            return (null, $"minimum Y ({minY}) must be less than maximum Y ({maxY}).");
        }

        if (parts.Length == 6 && coords[2] >= coords[5])
        {
            return (null, $"minimum Z ({coords[2]}) must be less than maximum Z ({coords[5]}).");
        }

        return (coords, null);
    }

    /// <summary>
    /// Normalizes a format parameter for output.
    /// Supports common format aliases and MIME types.
    /// </summary>
    /// <param name="raw">Raw format value (e.g., "json", "geojson", "application/geo+json").</param>
    /// <param name="defaultFormat">Default format if not specified.</param>
    /// <returns>Normalized format string in lowercase.</returns>
    public static string NormalizeFormat(string? raw, string defaultFormat = "json")
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return defaultFormat;
        }

        var normalized = raw.Trim().ToLowerInvariant();

        // Map MIME types and aliases to canonical format names
        return normalized switch
        {
            "application/geo+json" or "application/json" or "application/vnd.geo+json" => "geojson",
            "application/gml+xml" or "gml32" or "gml" => "gml",
            "text/csv" or "csv" => "csv",
            "application/x-shapefile" or "application/zip" or "shapefile" or "shape" or "shp" => "shapefile",
            "application/geopackage+sqlite3" or "application/vnd.sqlite3" or "geopackage" or "geopkg" => "geopackage",
            "application/vnd.google-earth.kml+xml" or "kml" => "kml",
            "application/vnd.google-earth.kmz" or "kmz" => "kmz",
            "application/topo+json" or "topojson" => "topojson",
            "text/wkt" or "application/wkt" or "wkt" => "wkt",
            "application/wkb" or "application/vnd.ogc.wkb" or "wkb" => "wkb",
            "text/html" or "application/xhtml+xml" or "html" => "html",
            "application/vnd.mapbox-vector-tile" or "mvt" => "mvt",
            "json" or "pjson" => "json",
            _ => normalized // Return as-is if not recognized
        };
    }

    /// <summary>
    /// Parses a DateTimeOffset from an ISO 8601 string or epoch milliseconds.
    /// Returns null for empty/null values or ".." (open-ended).
    /// </summary>
    private static (DateTimeOffset? Value, string? Error) ParseDateTimeOffset(string? raw)
    {
        if (raw.IsNullOrWhiteSpace() || raw == "..")
        {
            return (null, null);
        }

        // Try epoch milliseconds first
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochMillis))
        {
            try
            {
                return (DateTimeOffset.FromUnixTimeMilliseconds(epochMillis), null);
            }
            catch (ArgumentOutOfRangeException)
            {
                return (null, $"epoch milliseconds '{raw}' is outside the valid range.");
            }
        }

        // Try ISO 8601
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return (parsed, null);
        }

        return (null, $"temporal value '{raw}' must be ISO 8601 format or epoch milliseconds.");
    }
}
