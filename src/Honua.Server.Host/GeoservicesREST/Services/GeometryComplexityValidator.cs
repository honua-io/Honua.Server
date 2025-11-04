// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Validates geometry complexity to prevent DoS attacks via resource exhaustion.
/// Enforces limits on vertices, coordinates, and collection nesting depth.
/// </summary>
public sealed class GeometryComplexityValidator
{
    private const int MaxVertices = 100_000;
    private const int MaxCoordinates = 1_000_000;
    private const int MaxNestingDepth = 10;

    /// <summary>
    /// Validates a single geometry against complexity limits.
    /// </summary>
    /// <param name="geometry">The geometry to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when geometry is null.</exception>
    /// <exception cref="ArgumentException">Thrown when geometry exceeds complexity limits.</exception>
    public static void Validate(Geometry geometry)
    {
        Guard.NotNull(geometry);

        ValidateGeometry(geometry, depth: 0);
    }

    /// <summary>
    /// Validates a collection of geometries against complexity limits.
    /// </summary>
    /// <param name="geometries">The geometries to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when geometries is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any geometry exceeds complexity limits.</exception>
    public static void ValidateCollection(IEnumerable<Geometry> geometries)
    {
        Guard.NotNull(geometries);

        var totalVertices = 0;
        var totalCoordinates = 0;

        foreach (var geometry in geometries)
        {
            if (geometry == null)
            {
                continue;
            }

            ValidateGeometry(geometry, depth: 0);

            // Track cumulative totals across collection
            totalVertices += CountVertices(geometry);
            totalCoordinates += geometry.Coordinates.Length;

            if (totalVertices > MaxVertices)
            {
                throw new ArgumentException(
                    $"Collection has {totalVertices:N0} total vertices, exceeding maximum of {MaxVertices:N0}");
            }

            if (totalCoordinates > MaxCoordinates)
            {
                throw new ArgumentException(
                    $"Collection has {totalCoordinates:N0} total coordinates, exceeding maximum of {MaxCoordinates:N0}");
            }
        }
    }

    private static void ValidateGeometry(Geometry geometry, int depth)
    {
        if (depth > MaxNestingDepth)
        {
            throw new ArgumentException(
                $"Geometry collection nesting depth {depth} exceeds maximum of {MaxNestingDepth}");
        }

        var vertexCount = CountVertices(geometry);
        if (vertexCount > MaxVertices)
        {
            throw new ArgumentException(
                $"Geometry has {vertexCount:N0} vertices, exceeding maximum of {MaxVertices:N0}");
        }

        var coordinateCount = geometry.Coordinates.Length;
        if (coordinateCount > MaxCoordinates)
        {
            throw new ArgumentException(
                $"Geometry has {coordinateCount:N0} coordinates, exceeding maximum of {MaxCoordinates:N0}");
        }

        // Recursively validate nested collections
        if (geometry is GeometryCollection gc)
        {
            foreach (var child in gc.Geometries)
            {
                ValidateGeometry(child, depth + 1);
            }
        }
    }

    private static int CountVertices(Geometry geometry)
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
}
