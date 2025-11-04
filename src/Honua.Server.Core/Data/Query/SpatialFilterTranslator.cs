// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.Data.Query;

/// <summary>
/// Base class for translating spatial filter expressions to vendor-specific SQL.
/// Provides common logic for geometry normalization and predicate mapping.
/// </summary>
public static class SpatialFilterTranslator
{
    /// <summary>
    /// Builds a spatial filter SQL expression using vendor-specific SQL function names.
    /// </summary>
    /// <param name="expression">The spatial expression to translate</param>
    /// <param name="geometryColumn">The fully qualified geometry column reference</param>
    /// <param name="buildPredicateSql">Function that builds vendor-specific predicate SQL given (predicate, geomColumn, geometrySql)</param>
    /// <param name="parameters">Parameter dictionary for adding spatial parameter values</param>
    /// <param name="parameterPrefix">The parameter prefix for the SQL dialect (@ or :)</param>
    /// <returns>The SQL expression for the spatial filter</returns>
    public static string BuildSpatialFilter(
        QuerySpatialExpression expression,
        string geometryColumn,
        Func<SpatialPredicate, string, string, string> buildPredicateSql,
        IDictionary<string, object?> parameters,
        string parameterPrefix = "@")
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(geometryColumn);
        ArgumentNullException.ThrowIfNull(buildPredicateSql);
        ArgumentNullException.ThrowIfNull(parameters);

        // Extract and validate geometry value
        if (expression.TestGeometry is not QueryConstant { Value: QueryGeometryValue geometryValue })
        {
            throw new NotSupportedException("Spatial expressions require a geometry constant for the test geometry.");
        }

        // Normalize geometry to WKT
        var wkt = NormalizeGeometryLiteral(geometryValue.WellKnownText);
        var paramKey = $"filter_spatial_{parameters.Count}";
        parameters[paramKey] = wkt;
        var geometrySql = $"{parameterPrefix}{paramKey}";

        // Build the predicate SQL using vendor-specific function
        return buildPredicateSql(expression.Predicate, geometryColumn, geometrySql);
    }

    /// <summary>
    /// Normalizes a geometry literal to Well-Known Text (WKT) format.
    /// Supports WKT, WKB, and GeoJSON inputs.
    /// </summary>
    /// <param name="geometry">The geometry value (WKT string, byte[] WKB, or object)</param>
    /// <returns>Normalized WKT string</returns>
    public static string NormalizeGeometryLiteral(object geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        // If already a WKT string, return as-is (most common case)
        if (geometry is string wkt)
        {
            return wkt;
        }

        // If WKB byte array, convert to WKT
        if (geometry is byte[] wkb)
        {
            var reader = new WKBReader();
            var geom = reader.Read(wkb);
            var writer = new WKTWriter();
            return writer.Write(geom);
        }

        // If NTS Geometry object, convert to WKT
        if (geometry is Geometry geomObj)
        {
            var writer = new WKTWriter();
            return writer.Write(geomObj);
        }

        // For other types (e.g., GeoJSON objects), attempt to parse as GeoJSON
        // This is a fallback for custom geometry types
        throw new NotSupportedException($"Geometry type '{geometry.GetType().Name}' is not supported. Use WKT string, WKB byte array, or NTS Geometry.");
    }

    /// <summary>
    /// Maps a spatial predicate enum to a standard SQL function name.
    /// Vendors can override this to provide their specific function names.
    /// </summary>
    /// <param name="predicate">The spatial predicate</param>
    /// <param name="prefix">Optional prefix (e.g., "ST_" for PostGIS/MySQL, "" for SpatiaLite)</param>
    /// <param name="casing">Function name casing (Pascal for SQL Server methods, default for others)</param>
    /// <returns>The SQL function name</returns>
    public static string GetSpatialPredicateName(
        SpatialPredicate predicate,
        string prefix = "ST_",
        FunctionNameCasing casing = FunctionNameCasing.Default)
    {
        var baseName = predicate switch
        {
            SpatialPredicate.Intersects => "Intersects",
            SpatialPredicate.Contains => "Contains",
            SpatialPredicate.Within => "Within",
            SpatialPredicate.Crosses => "Crosses",
            SpatialPredicate.Overlaps => "Overlaps",
            SpatialPredicate.Touches => "Touches",
            SpatialPredicate.Disjoint => "Disjoint",
            SpatialPredicate.Equals => "Equals",
            SpatialPredicate.DWithin => "DWithin",
            SpatialPredicate.Beyond => "Beyond",
            _ => throw new NotSupportedException($"Spatial predicate '{predicate}' is not supported.")
        };

        var functionName = prefix + baseName;

        return casing switch
        {
            FunctionNameCasing.Pascal => functionName,
            FunctionNameCasing.Upper => functionName.ToUpperInvariant(),
            FunctionNameCasing.Lower => functionName.ToLowerInvariant(),
            _ => functionName
        };
    }

    /// <summary>
    /// Validates that a spatial predicate is supported.
    /// </summary>
    public static void ValidateSpatialPredicate(SpatialPredicate predicate, params SpatialPredicate[] supportedPredicates)
    {
        if (supportedPredicates.Length == 0)
        {
            return; // No restrictions
        }

        foreach (var supported in supportedPredicates)
        {
            if (predicate == supported)
            {
                return;
            }
        }

        throw new NotSupportedException($"Spatial predicate '{predicate}' is not supported.");
    }

    /// <summary>
    /// Extracts the envelope (bounding box) from a WKT geometry for spatial index optimization.
    /// Returns null if envelope extraction fails.
    /// </summary>
    public static Envelope? ExtractEnvelope(string wkt)
    {
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return null;
        }

        try
        {
            var reader = new WKTReader();
            var geometry = reader.Read(wkt);
            var envelope = geometry.EnvelopeInternal;

            return envelope?.IsNull == false ? envelope : null;
        }
        catch
        {
            // If envelope extraction fails, return null to skip optimization
            return null;
        }
    }

    /// <summary>
    /// Adds a parameter to the parameter dictionary and returns the parameter reference.
    /// </summary>
    public static string AddParameter(
        IDictionary<string, object?> parameters,
        string prefix,
        object? value,
        string parameterPrefix = "@")
    {
        var key = $"{prefix}_{parameters.Count}";
        parameters[key] = value;
        return $"{parameterPrefix}{key}";
    }
}

/// <summary>
/// Function name casing options for vendor-specific SQL.
/// </summary>
public enum FunctionNameCasing
{
    /// <summary>Default casing (PascalCase with prefix)</summary>
    Default,

    /// <summary>PascalCase (e.g., ST_Intersects, STIntersects)</summary>
    Pascal,

    /// <summary>UPPER CASE (e.g., ST_INTERSECTS)</summary>
    Upper,

    /// <summary>lower case (e.g., st_intersects)</summary>
    Lower
}
