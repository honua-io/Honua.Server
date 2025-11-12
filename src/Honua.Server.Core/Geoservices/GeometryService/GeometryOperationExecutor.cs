// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Linemerge;
using NetTopologySuite.Operation.Polygonize;
using NetTopologySuite.Simplify;
using NetTopologySuite.Densify;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public sealed class GeometryOperationExecutor : IGeometryOperationExecutor
{
    private readonly IOptionsMonitor<GeometryServiceOptions> _geometryOptions;

    public GeometryOperationExecutor(IOptionsMonitor<GeometryServiceOptions> geometryOptions)
    {
        _geometryOptions = geometryOptions ?? throw new ArgumentNullException(nameof(geometryOptions));
    }

    public IReadOnlyList<Geometry> Project(GeometryProjectOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateSpatialReferences(operation.InputSpatialReference, operation.OutputSpatialReference, settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        if (operation.InputSpatialReference == operation.OutputSpatialReference)
        {
            return CloneGeometries(operation.Geometries, operation.OutputSpatialReference);
        }

        var results = new List<Geometry>(operation.Geometries.Count);
        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            var sourceSrid = geometry.SRID > 0 ? geometry.SRID : operation.InputSpatialReference;

            try
            {
                var transformed = CrsTransform.TransformGeometry(geometry, sourceSrid, operation.OutputSpatialReference);
                results.Add(transformed);
            }
            catch (Exception exception)
            {
                throw new GeometryServiceException(
                    $"Failed to transform geometry from SRID {sourceSrid} to {operation.OutputSpatialReference}.",
                    exception);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> Buffer(GeometryBufferOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var distance = ConvertBufferDistance(operation.Distance, operation.Unit, operation.SpatialReference);
        var results = new List<Geometry>(operation.Geometries.Count);

        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var buffered = geometry.Buffer(distance);
                buffered.SRID = operation.SpatialReference;
                results.Add(buffered);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to buffer geometry: {ex.Message}", ex);
            }
        }

        if (operation.UnionResults && results.Count > 1)
        {
            try
            {
                var union = results[0];
                for (var i = 1; i < results.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    union = union.Union(results[i]);
                }
                union.SRID = operation.SpatialReference;
                return new[] { union };
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to union buffered geometries: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> Simplify(GeometrySimplifyOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(operation.Geometries.Count);
        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                // Use Douglas-Peucker simplification with automatic tolerance
                var envelope = geometry.EnvelopeInternal;
                var tolerance = Math.Max(envelope.Width, envelope.Height) / 100.0;
                var simplified = DouglasPeuckerSimplifier.Simplify(geometry, tolerance);
                simplified.SRID = operation.SpatialReference;
                results.Add(simplified);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to simplify geometry: {ex.Message}", ex);
            }
        }

        return results;
    }

    public Geometry? Union(GeometrySetOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return null;
        }

        try
        {
            var union = operation.Geometries[0];
            for (var i = 1; i < operation.Geometries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (operation.Geometries[i] is null)
                {
                    throw new GeometryServiceException("Geometries collection contains a null element.");
                }
                union = union.Union(operation.Geometries[i]);
            }
            union.SRID = operation.SpatialReference;
            return union;
        }
        catch (Exception ex)
        {
            throw new GeometryServiceException($"Failed to union geometries: {ex.Message}", ex);
        }
    }

    public IReadOnlyList<Geometry> Intersect(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        var allGeometries = new List<Geometry>(operation.Geometries1.Count + operation.Geometries2.Count);
        allGeometries.AddRange(operation.Geometries1);
        allGeometries.AddRange(operation.Geometries2);
        ValidateGeometryLimits(allGeometries, settings);

        var count = Math.Min(operation.Geometries1.Count, operation.Geometries2.Count);
        if (count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(count);
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var g1 = operation.Geometries1[i];
            var g2 = operation.Geometries2[i];

            if (g1 is null || g2 is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var intersection = g1.Intersection(g2);
                intersection.SRID = operation.SpatialReference;
                results.Add(intersection);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to intersect geometries: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> Difference(GeometryPairwiseOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        var allGeometries = new List<Geometry>(operation.Geometries1.Count + operation.Geometries2.Count);
        allGeometries.AddRange(operation.Geometries1);
        allGeometries.AddRange(operation.Geometries2);
        ValidateGeometryLimits(allGeometries, settings);

        var count = Math.Min(operation.Geometries1.Count, operation.Geometries2.Count);
        if (count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(count);
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var g1 = operation.Geometries1[i];
            var g2 = operation.Geometries2[i];

            if (g1 is null || g2 is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var difference = g1.Difference(g2);
                difference.SRID = operation.SpatialReference;
                results.Add(difference);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute difference: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> ConvexHull(GeometrySetOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(operation.Geometries.Count);
        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var convexHull = geometry.ConvexHull();
                convexHull.SRID = operation.SpatialReference;
                results.Add(convexHull);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute convex hull: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<double> Distance(GeometryDistanceOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        var count = Math.Min(operation.Geometries1.Count, operation.Geometries2.Count);
        if (count == 0)
        {
            return Array.Empty<double>();
        }

        var results = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var g1 = operation.Geometries1[i];
            var g2 = operation.Geometries2[i];

            if (g1 is null || g2 is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var distance = g1.Distance(g2);
                results.Add(distance);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute distance: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<double> Areas(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        if (operation.Polygons.Count == 0)
        {
            return Array.Empty<double>();
        }

        var results = new List<double>(operation.Polygons.Count);
        foreach (var polygon in operation.Polygons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (polygon is null)
            {
                throw new GeometryServiceException("Polygons collection contains a null element.");
            }

            try
            {
                results.Add(polygon.Area);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute area: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<double> Lengths(GeometryMeasurementOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        if (operation.Polygons.Count == 0)
        {
            return Array.Empty<double>();
        }

        var results = new List<double>(operation.Polygons.Count);
        foreach (var polygon in operation.Polygons)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (polygon is null)
            {
                throw new GeometryServiceException("Polygons collection contains a null element.");
            }

            try
            {
                results.Add(polygon.Length);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute length: {ex.Message}", ex);
            }
        }

        return results;
    }

    /// <summary>
    /// Calculates optimal label placement points for polygon geometries.
    /// </summary>
    /// <remarks>
    /// This implementation uses NetTopologySuite's InteriorPoint algorithm, which guarantees
    /// that the returned point will be inside the polygon. This is superior to using the
    /// centroid, which may fall outside the polygon for concave or complex shapes.
    ///
    /// Algorithm: InteriorPoint
    /// - Guaranteed to be within the polygon boundary
    /// - Suitable for most cartographic label placement scenarios
    /// - Fast and reliable for web mapping applications
    ///
    /// Limitations:
    /// - Not necessarily the "most optimal" visual placement
    /// - Does not consider polygon shape complexity or visual balance
    /// - For highest quality label placement, consider implementing the Pole of Inaccessibility
    ///   algorithm (point farthest from all polygon edges)
    ///
    /// Future Improvements:
    /// - Implement Pole of Inaccessibility for better visual placement
    /// - Support MultiPolygon by returning the best point per sub-polygon
    /// - Add configurable strategy selection (centroid/interior/pole)
    /// </remarks>
    public IReadOnlyList<Geometry> LabelPoints(GeometryLabelPointsOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(operation.Geometries.Count);

        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            // Validate that we're working with polygon geometries
            if (geometry is not Polygon && geometry is not MultiPolygon)
            {
                throw new GeometryServiceException(
                    "LabelPoints operation requires Polygon or MultiPolygon geometries.");
            }

            try
            {
                // Use InteriorPoint for guaranteed interior placement
                // This is better than Centroid which may fall outside concave polygons
                var labelPoint = geometry.InteriorPoint;
                labelPoint.SRID = operation.SpatialReference;
                results.Add(labelPoint);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException(
                    $"Failed to compute label point: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> Cut(GeometryCutOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        if (operation.Target is null)
        {
            throw new GeometryServiceException("Target geometry is required for cut operation.");
        }

        if (operation.Cutter is null)
        {
            throw new GeometryServiceException("Cutter geometry is required for cut operation.");
        }

        ValidateGeometryLimits(new[] { operation.Target, operation.Cutter }, settings);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (operation.Target is not (Polygon or MultiPolygon or LineString or MultiLineString))
            {
                throw new GeometryServiceException(
                    "Cut operation only supports polygon and linestring geometries as target.");
            }

            if (operation.Cutter is not (LineString or MultiLineString))
            {
                throw new GeometryServiceException(
                    "Cutter geometry must be a linestring or multilinestring.");
            }

            if (!operation.Target.Intersects(operation.Cutter))
            {
                var original = operation.Target.Copy();
                original.SRID = operation.SpatialReference;
                return new[] { original };
            }

            var results = new List<Geometry>();

            if (operation.Target is Polygon or MultiPolygon)
            {
                var cutterBuffer = operation.Cutter.Buffer(0.0001);
                var difference = operation.Target.Difference(cutterBuffer);

                if (difference is GeometryCollection gc)
                {
                    for (var i = 0; i < gc.NumGeometries; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var geom = gc.GetGeometryN(i);
                        geom.SRID = operation.SpatialReference;
                        results.Add(geom);
                    }
                }
                else if (!difference.IsEmpty)
                {
                    difference.SRID = operation.SpatialReference;
                    results.Add(difference);
                }
            }
            else if (operation.Target is LineString or MultiLineString)
            {
                var difference = operation.Target.Difference(operation.Cutter.Buffer(0.0001));

                if (difference is GeometryCollection gc)
                {
                    for (var i = 0; i < gc.NumGeometries; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var geom = gc.GetGeometryN(i);
                        geom.SRID = operation.SpatialReference;
                        results.Add(geom);
                    }
                }
                else if (!difference.IsEmpty)
                {
                    difference.SRID = operation.SpatialReference;
                    results.Add(difference);
                }
            }

            if (results.Count == 0)
            {
                var original = operation.Target.Copy();
                original.SRID = operation.SpatialReference;
                return new[] { original };
            }

            return results;
        }
        catch (Exception ex) when (ex is not GeometryServiceException)
        {
            throw new GeometryServiceException($"Failed to cut geometry: {ex.Message}", ex);
        }
    }

    public Geometry Reshape(GeometryReshapeOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);

        if (operation.Target is null)
        {
            throw new GeometryServiceException("Target geometry is required for reshape operation.");
        }

        if (operation.Reshaper is null)
        {
            throw new GeometryServiceException("Reshaper geometry is required for reshape operation.");
        }

        ValidateGeometryLimits(new[] { operation.Target, operation.Reshaper }, settings);

        throw new GeometryServiceException(
            "Reshape operation requires advanced topology editing support. " +
            "This operation is not yet implemented. Consider using cut/union operations as an alternative.");
    }

    public IReadOnlyList<Geometry> Densify(GeometryDensifyOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        if (operation.MaxSegmentLength <= 0)
        {
            throw new GeometryServiceException("maxSegmentLength must be positive.");
        }

        var results = new List<Geometry>(operation.Geometries.Count);
        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var densified = Densifier.Densify(geometry, operation.MaxSegmentLength);
                densified.SRID = operation.SpatialReference;
                results.Add(densified);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to densify geometry: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> Generalize(GeometryGeneralizeOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        if (operation.MaxDeviation <= 0)
        {
            throw new GeometryServiceException("maxDeviation must be positive.");
        }

        var results = new List<Geometry>(operation.Geometries.Count);
        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            try
            {
                var generalized = VWSimplifier.Simplify(geometry, operation.MaxDeviation);
                generalized.SRID = operation.SpatialReference;
                results.Add(generalized);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to generalize geometry: {ex.Message}", ex);
            }
        }

        return results;
    }

    private static double ConvertBufferDistance(double distance, string unit, int srid)
    {
        // For simplicity, assume distance is already in the correct units for the SRID
        // In a full implementation, you'd convert based on unit and coordinate system
        if (string.IsNullOrWhiteSpace(unit))
        {
            return distance;
        }

        // Basic unit conversions (assuming planar coordinates)
        return unit.ToLowerInvariant() switch
        {
            "meter" or "meters" or "9001" => distance,
            "kilometer" or "kilometers" => distance * 1000.0,
            "foot" or "feet" or "9002" => distance * 0.3048,
            "mile" or "miles" => distance * 1609.344,
            _ => distance
        };
    }

    private static void EnsureServiceEnabled(GeometryServiceOptions settings)
    {
        if (!settings.Enabled)
        {
            throw new GeometryServiceException("Geometry service is disabled.");
        }
    }

    private static void ValidateSpatialReferences(int input, int output, GeometryServiceOptions settings)
    {
        if (input <= 0)
        {
            throw new GeometryServiceException("Input spatial reference must be a positive WKID.");
        }

        if (output <= 0)
        {
            throw new GeometryServiceException("Output spatial reference must be a positive WKID.");
        }

        if (settings.AllowedSrids is { Count: > 0 } allowed)
        {
            if (!Contains(allowed, input) || !Contains(allowed, output))
            {
                throw new GeometryServiceException("Requested spatial reference is not permitted for this service.");
            }
        }

        static bool Contains(IReadOnlyList<int> values, int srid)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == srid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static void ValidateGeometryLimits(IReadOnlyList<Geometry> geometries, GeometryServiceOptions settings)
    {
        if (settings.MaxGeometries > 0 && geometries.Count > settings.MaxGeometries)
        {
            throw new GeometryServiceException($"Request exceeds configured maximum of {settings.MaxGeometries} geometries.");
        }

        if (settings.MaxCoordinateCount <= 0)
        {
            return;
        }

        long totalCoordinates = 0;
        foreach (var geometry in geometries)
        {
            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            totalCoordinates += geometry.NumPoints;
            if (totalCoordinates > settings.MaxCoordinateCount)
            {
                throw new GeometryServiceException($"Request exceeds configured coordinate budget of {settings.MaxCoordinateCount}.");
            }
        }
    }

    private static IReadOnlyList<Geometry> CloneGeometries(IReadOnlyList<Geometry> source, int srid)
    {
        if (source.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var clones = new List<Geometry>(source.Count);
        foreach (var geometry in source)
        {
            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            var copy = geometry.Copy();
            copy.SRID = srid;
            clones.Add(copy);
        }

        return clones;
    }

    public IReadOnlyList<Geometry> Offset(GeometryOffsetOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Geometries, settings);

        if (operation.Geometries.Count == 0)
        {
            return Array.Empty<Geometry>();
        }

        var results = new List<Geometry>(operation.Geometries.Count);

        // Map ArcGIS offset style to NTS JoinStyle
        var joinStyle = operation.OffsetHow switch
        {
            "esriGeometryOffsetRounded" => JoinStyle.Round,
            "esriGeometryOffsetBevelled" => JoinStyle.Bevel,
            "esriGeometryOffsetMitered" => JoinStyle.Mitre,
            _ => JoinStyle.Round
        };

        foreach (var geometry in operation.Geometries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (geometry is null)
            {
                throw new GeometryServiceException("Geometries collection contains a null element.");
            }

            // Validate that the geometry is a linear type (LineString, MultiLineString, or Polygon)
            if (geometry is not LineString && geometry is not MultiLineString && geometry is not Polygon && geometry is not MultiPolygon)
            {
                throw new GeometryServiceException(
                    "Offset operation requires LineString, MultiLineString, Polygon, or MultiPolygon geometries.");
            }

            try
            {
                // Create buffer parameters for offset curve
                var bufferParams = new BufferParameters
                {
                    JoinStyle = joinStyle,
                    MitreLimit = operation.BevelRatio,
                    EndCapStyle = EndCapStyle.Flat
                };

                // Use OffsetCurve to create a single-sided offset
                // For LineStrings and MultiLineStrings
                Geometry offsetGeometry;

                // Use BufferOp for all geometry types with single-sided parameter
                bufferParams.IsSingleSided = true;
                var bufferOp = new BufferOp(geometry, bufferParams);
                offsetGeometry = bufferOp.GetResultGeometry(operation.OffsetDistance);

                offsetGeometry.SRID = operation.SpatialReference;
                results.Add(offsetGeometry);
            }
            catch (Exception ex)
            {
                throw new GeometryServiceException($"Failed to compute offset: {ex.Message}", ex);
            }
        }

        return results;
    }

    public IReadOnlyList<Geometry> TrimExtend(GeometryTrimExtendOperation operation, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        var settings = _geometryOptions.CurrentValue;
        EnsureServiceEnabled(settings);
        ValidateGeometryLimits(operation.Polylines, settings);

        // TrimExtend is a highly specialized CAD/GIS editing operation that requires:
        // 1. Line endpoint detection and manipulation
        // 2. Line extension along bearing/azimuth
        // 3. Complex intersection calculation with trimming geometry
        // 4. Line segment trimming at precise intersection points
        //
        // NetTopologySuite does not provide built-in support for this operation.
        // This would require significant custom implementation involving:
        // - Bearing calculation from line endpoints
        // - Ray extension algorithms
        // - Intersection point computation
        // - Line segment reconstruction
        //
        // Implementation Priority: LOW
        // This operation is rarely used in web-based GIS contexts and is more
        // commonly found in desktop CAD/GIS editing workflows.
        //
        // Recommended Alternatives:
        // 1. Use client-side editing tools (e.g., ArcGIS JavaScript API, Leaflet.Draw)
        // 2. Implement as needed using Cut + Union operations for simpler cases
        // 3. Use dedicated CAD software for complex editing workflows

        throw new GeometryServiceException(
            "TrimExtend operation is not currently supported. " +
            "This operation requires advanced CAD topology editing capabilities that are not available in NetTopologySuite. " +
            "For line editing, consider using Cut/Union operations or client-side editing tools. " +
            "Operation details: extendHow=" + operation.ExtendHow);
    }
}
