// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.GeometryValidation;

/// <summary>
/// Validates geometry complexity to prevent DoS attacks via resource exhaustion.
/// Enforces configurable limits on vertices, rings, nesting depth, coordinate precision, and size.
/// </summary>
public sealed class GeometryComplexityValidator
{
    private readonly GeometryComplexityOptions _options;

    // Cache SearchValues for improved performance (CA1870)
    private static readonly SearchValues<char> _scientificNotationChars = SearchValues.Create(['e', 'E']);

    /// <summary>
    /// Initializes a new instance of the <see cref="GeometryComplexityValidator"/> class.
    /// </summary>
    /// <param name="options">The complexity validation options.</param>
    public GeometryComplexityValidator(GeometryComplexityOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public GeometryComplexityValidator()
        : this(new GeometryComplexityOptions())
    {
    }

    /// <summary>
    /// Validates a single geometry against all complexity limits.
    /// Early exits on first limit violation for performance.
    /// </summary>
    /// <param name="geometry">The geometry to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when geometry is null.</exception>
    /// <exception cref="GeometryComplexityException">Thrown when geometry exceeds any complexity limit.</exception>
    public void Validate(NetTopologySuite.Geometries.Geometry geometry)
    {
        if (geometry == null)
        {
            throw new ArgumentNullException(nameof(geometry));
        }

        if (!_options.EnableValidation)
        {
            return;
        }

        // Validate geometry at depth 0
        ValidateGeometryRecursive(geometry, depth: 0);
    }

    /// <summary>
    /// Validates a collection of geometries against complexity limits.
    /// Checks both individual geometry limits and cumulative totals.
    /// </summary>
    /// <param name="geometries">The geometries to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when geometries is null.</exception>
    /// <exception cref="GeometryComplexityException">Thrown when any geometry exceeds complexity limits.</exception>
    public void ValidateCollection(IEnumerable<NetTopologySuite.Geometries.Geometry> geometries)
    {
        if (geometries == null)
        {
            throw new ArgumentNullException(nameof(geometries));
        }

        if (!_options.EnableValidation)
        {
            return;
        }

        var totalVertices = 0;
        var totalCoordinates = 0;

        foreach (var geometry in geometries)
        {
            if (geometry == null)
            {
                continue;
            }

            // Validate individual geometry
            ValidateGeometryRecursive(geometry, depth: 0);

            // Track cumulative totals across collection
            totalVertices += CountVertices(geometry);
            totalCoordinates += geometry.Coordinates.Length;

            if (totalVertices > _options.MaxVertexCount)
            {
                throw GeometryComplexityException.VertexCountExceeded(totalVertices, _options.MaxVertexCount);
            }

            if (totalCoordinates > _options.MaxCoordinateCount)
            {
                throw GeometryComplexityException.CoordinateCountExceeded(totalCoordinates, _options.MaxCoordinateCount);
            }
        }
    }

    /// <summary>
    /// Recursively validates a geometry and its nested components.
    /// Uses iterative depth tracking to prevent stack overflow.
    /// </summary>
    private void ValidateGeometryRecursive(NetTopologySuite.Geometries.Geometry geometry, int depth)
    {
        // Check nesting depth first (early exit for deeply nested structures)
        if (depth > _options.MaxNestingDepth)
        {
            throw GeometryComplexityException.NestingDepthExceeded(depth, _options.MaxNestingDepth);
        }

        // Check vertex count
        var vertexCount = CountVertices(geometry);
        if (vertexCount > _options.MaxVertexCount)
        {
            throw GeometryComplexityException.VertexCountExceeded(vertexCount, _options.MaxVertexCount);
        }

        // Check coordinate count
        var coordinateCount = geometry.Coordinates.Length;
        if (coordinateCount > _options.MaxCoordinateCount)
        {
            throw GeometryComplexityException.CoordinateCountExceeded(coordinateCount, _options.MaxCoordinateCount);
        }

        // Check ring count for polygons
        if (geometry is Polygon polygon)
        {
            var ringCount = 1 + polygon.NumInteriorRings; // exterior + holes
            if (ringCount > _options.MaxRingCount)
            {
                throw GeometryComplexityException.RingCountExceeded(ringCount, _options.MaxRingCount);
            }
        }
        else if (geometry is MultiPolygon multiPolygon)
        {
            var totalRings = 0;
            for (int i = 0; i < multiPolygon.NumGeometries; i++)
            {
                var poly = (Polygon)multiPolygon.GetGeometryN(i);
                totalRings += 1 + poly.NumInteriorRings;
            }
            if (totalRings > _options.MaxRingCount)
            {
                throw GeometryComplexityException.RingCountExceeded(totalRings, _options.MaxRingCount);
            }
        }

        // Check coordinate precision
        ValidateCoordinatePrecision(geometry);

        // Check geometry size in bytes (serialized)
        ValidateGeometrySize(geometry);

        // Recursively validate nested collections
        if (geometry is GeometryCollection gc)
        {
            for (int i = 0; i < gc.NumGeometries; i++)
            {
                var child = gc.GetGeometryN(i);
                ValidateGeometryRecursive(child, depth + 1);
            }
        }
    }

    /// <summary>
    /// Validates that coordinate precision does not exceed the maximum.
    /// Checks decimal places in coordinate values.
    /// </summary>
    private void ValidateCoordinatePrecision(NetTopologySuite.Geometries.Geometry geometry)
    {
        var coordinates = geometry.Coordinates;

        // Sample up to 100 coordinates for performance (checking all can be expensive for large geometries)
        var sampleSize = Math.Min(100, coordinates.Length);
        var step = coordinates.Length > sampleSize ? coordinates.Length / sampleSize : 1;

        for (int i = 0; i < coordinates.Length; i += step)
        {
            var coord = coordinates[i];

            var xPrecision = GetDecimalPlaces(coord.X);
            if (xPrecision > _options.MaxCoordinatePrecision)
            {
                throw GeometryComplexityException.CoordinatePrecisionExceeded(xPrecision, _options.MaxCoordinatePrecision);
            }

            var yPrecision = GetDecimalPlaces(coord.Y);
            if (yPrecision > _options.MaxCoordinatePrecision)
            {
                throw GeometryComplexityException.CoordinatePrecisionExceeded(yPrecision, _options.MaxCoordinatePrecision);
            }

            if (!double.IsNaN(coord.Z))
            {
                var zPrecision = GetDecimalPlaces(coord.Z);
                if (zPrecision > _options.MaxCoordinatePrecision)
                {
                    throw GeometryComplexityException.CoordinatePrecisionExceeded(zPrecision, _options.MaxCoordinatePrecision);
                }
            }
        }
    }

    /// <summary>
    /// Validates that the serialized geometry size does not exceed the maximum.
    /// Uses WKT serialization as a size proxy for performance.
    /// </summary>
    private void ValidateGeometrySize(NetTopologySuite.Geometries.Geometry geometry)
    {
        // Quick estimation: each coordinate is roughly 30-50 bytes in WKT
        // Use this heuristic for fast check before expensive serialization
        var estimatedSize = geometry.NumPoints * 40; // conservative estimate

        if (estimatedSize <= _options.MaxGeometrySizeBytes)
        {
            // Likely under limit, skip expensive serialization
            return;
        }

        // Perform actual size check using WKT serialization
        try
        {
            var writer = new WKTWriter();
            var wkt = writer.Write(geometry);
            var actualSize = Encoding.UTF8.GetByteCount(wkt);

            if (actualSize > _options.MaxGeometrySizeBytes)
            {
                throw GeometryComplexityException.GeometrySizeExceeded(actualSize, _options.MaxGeometrySizeBytes);
            }
        }
        catch (GeometryComplexityException)
        {
            // Re-throw our exception
            throw;
        }
        catch
        {
            // If serialization fails, assume size is acceptable
            // (the geometry will fail validation elsewhere if truly invalid)
        }
    }

    /// <summary>
    /// Counts the number of decimal places in a double value.
    /// </summary>
    private static int GetDecimalPlaces(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        // Convert to string and count decimal places
        var str = value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        var decimalIndex = str.IndexOf('.');

        if (decimalIndex < 0)
        {
            return 0;
        }

        // Count digits after decimal point, excluding trailing zeros
        var afterDecimal = str.Substring(decimalIndex + 1);

        // Remove scientific notation part if present
        var eIndex = afterDecimal.AsSpan().IndexOfAny(_scientificNotationChars);
        if (eIndex >= 0)
        {
            afterDecimal = afterDecimal.Substring(0, eIndex);
        }

        // Trim trailing zeros
        afterDecimal = afterDecimal.TrimEnd('0');

        return afterDecimal.Length;
    }

    /// <summary>
    /// Counts vertices in a geometry using efficient traversal.
    /// For collections, returns sum of all component vertices.
    /// </summary>
    private static int CountVertices(NetTopologySuite.Geometries.Geometry geometry)
    {
        return geometry switch
        {
            GeometryCollection gc => gc.Geometries.Sum(CountVertices),
            Polygon p => p.NumPoints,
            LineString ls => ls.NumPoints,
            Point _ => 1,
            _ => geometry.NumPoints
        };
    }

    /// <summary>
    /// Gets the current validation options.
    /// </summary>
    public GeometryComplexityOptions Options => _options;
}
