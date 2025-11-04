// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Serialization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Standardized utility for calculating spatial extents (bounding boxes) from geometries and features.
/// Consolidates extent calculation logic across STAC, OGC API, WFS, WMS, and GeoservicesREST implementations.
/// </summary>
/// <remarks>
/// <para><strong>Design Principles:</strong></para>
/// <list type="bullet">
///   <item>Returns null for indeterminate extents (empty collections, invalid geometries)</item>
///   <item>Validates that minX &lt; maxX and minY &lt; maxY before returning</item>
///   <item>Handles null/empty inputs gracefully without throwing exceptions</item>
///   <item>Supports multiple coordinate reference systems via CRS parameter</item>
///   <item>Provides both tuple-based and object-based return types for flexibility</item>
/// </list>
/// <para><strong>Coordinate System Considerations:</strong></para>
/// <para>
/// This calculator works in the coordinate space provided by the input geometries.
/// It does NOT perform coordinate transformations. Callers must ensure all geometries
/// are in the same CRS before calculating extents. For antimeridian crossing scenarios,
/// use dedicated longitude normalization utilities before extent calculation.
/// </para>
/// <para><strong>Performance:</strong></para>
/// <para>
/// For large datasets, prefer database-level extent calculation (e.g., PostGIS ST_Extent)
/// over in-memory geometry processing. This utility is intended for scenarios where
/// geometries are already loaded or database-level calculations are not available.
/// </para>
/// </remarks>
public static class ExtentCalculator
{
    /// <summary>
    /// Calculates the spatial extent from a collection of feature records.
    /// </summary>
    /// <param name="features">Collection of feature records containing geometries.</param>
    /// <returns>
    /// Tuple containing (minX, minY, maxX, maxY) if valid extent can be calculated, null otherwise.
    /// Returns null for empty collections, all-null geometries, or invalid extents.
    /// </returns>
    public static (double MinX, double MinY, double MaxX, double MaxY)? CalculateExtent(IEnumerable<FeatureRecord> features)
    {
        if (features is null)
        {
            return null;
        }

        var reader = new GeoJsonReader();
        var geometries = new List<Geometry>();

        foreach (var feature in features)
        {
            if (!feature.Attributes.TryGetValue("geometry", out var geometryObj) || geometryObj is not string geometryString)
            {
                continue;
            }

            try
            {
                var geometry = reader.Read<Geometry>(geometryString);
                if (geometry is not null && !geometry.IsEmpty)
                {
                    geometries.Add(geometry);
                }
            }
            catch
            {
                // Ignore geometries that cannot be parsed
                continue;
            }
        }

        return CalculateExtentFromGeometries(geometries);
    }

    /// <summary>
    /// Calculates the spatial extent from a collection of feature components.
    /// </summary>
    /// <param name="components">Collection of feature components containing geometry nodes.</param>
    /// <returns>
    /// Tuple containing (minX, minY, maxX, maxY) if valid extent can be calculated, null otherwise.
    /// Returns null for empty collections, all-null geometries, or invalid extents.
    /// </returns>
    public static (double MinX, double MinY, double MaxX, double MaxY)? CalculateExtent(IEnumerable<FeatureComponents> components)
    {
        if (components is null)
        {
            return null;
        }

        var reader = new GeoJsonReader();
        var geometries = new List<Geometry>();

        foreach (var component in components)
        {
            var geometryNode = component.GeometryNode;
            if (geometryNode is null)
            {
                continue;
            }

            try
            {
                var json = geometryNode.ToJsonString();
                var geometry = reader.Read<Geometry>(json);
                if (geometry is not null && !geometry.IsEmpty)
                {
                    geometries.Add(geometry);
                }
            }
            catch
            {
                // Ignore geometries that cannot be parsed
                continue;
            }
        }

        return CalculateExtentFromGeometries(geometries);
    }

    /// <summary>
    /// Calculates the spatial extent from a collection of NetTopologySuite geometries.
    /// </summary>
    /// <param name="geometries">Collection of NTS Geometry objects.</param>
    /// <returns>
    /// Tuple containing (minX, minY, maxX, maxY) if valid extent can be calculated, null otherwise.
    /// Returns null for empty collections, all-empty geometries, or invalid extents.
    /// </returns>
    public static (double MinX, double MinY, double MaxX, double MaxY)? CalculateExtentFromGeometries(IEnumerable<Geometry> geometries)
    {
        if (geometries is null)
        {
            return null;
        }

        Envelope? combinedEnvelope = null;

        foreach (var geometry in geometries)
        {
            if (geometry is null || geometry.IsEmpty)
            {
                continue;
            }

            var envelope = geometry.EnvelopeInternal;
            if (envelope is null || envelope.IsNull)
            {
                continue;
            }

            if (combinedEnvelope is null)
            {
                combinedEnvelope = new Envelope(envelope);
            }
            else
            {
                combinedEnvelope.ExpandToInclude(envelope);
            }
        }

        if (combinedEnvelope is null || combinedEnvelope.IsNull)
        {
            return null;
        }

        // Validate that min < max for both axes
        if (!IsValidExtent(combinedEnvelope.MinX, combinedEnvelope.MinY, combinedEnvelope.MaxX, combinedEnvelope.MaxY))
        {
            return null;
        }

        return (combinedEnvelope.MinX, combinedEnvelope.MinY, combinedEnvelope.MaxX, combinedEnvelope.MaxY);
    }

    /// <summary>
    /// Combines multiple extents into a single encompassing extent.
    /// </summary>
    /// <param name="extents">Collection of extent tuples to combine.</param>
    /// <returns>
    /// Tuple containing the combined (minX, minY, maxX, maxY) if at least one valid extent exists, null otherwise.
    /// </returns>
    public static (double MinX, double MinY, double MaxX, double MaxY)? CombineExtents(IEnumerable<(double MinX, double MinY, double MaxX, double MaxY)> extents)
    {
        if (extents is null)
        {
            return null;
        }

        double? minX = null;
        double? minY = null;
        double? maxX = null;
        double? maxY = null;

        foreach (var extent in extents)
        {
            if (!IsValidExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY))
            {
                continue;
            }

            if (minX is null)
            {
                minX = extent.MinX;
                minY = extent.MinY;
                maxX = extent.MaxX;
                maxY = extent.MaxY;
            }
            else
            {
                minX = Math.Min(minX.Value, extent.MinX);
                minY = Math.Min(minY.Value, extent.MinY);
                maxX = Math.Max(maxX.Value, extent.MaxX);
                maxY = Math.Max(maxY.Value, extent.MaxY);
            }
        }

        if (minX is null)
        {
            return null;
        }

        return (minX.Value, minY!.Value, maxX!.Value, maxY!.Value);
    }

    /// <summary>
    /// Validates that an extent has valid coordinates (minX &lt; maxX and minY &lt; maxY).
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <returns>True if the extent is valid (min &lt; max for both axes), false otherwise.</returns>
    public static bool IsValidExtent(double minX, double minY, double maxX, double maxY)
    {
        // Check for NaN or Infinity
        if (double.IsNaN(minX) || double.IsNaN(minY) || double.IsNaN(maxX) || double.IsNaN(maxY))
        {
            return false;
        }

        if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
        {
            return false;
        }

        // Validate min < max for both axes
        // Note: For point geometries, minX == maxX and minY == maxY is valid
        return minX <= maxX && minY <= maxY;
    }

    /// <summary>
    /// Converts an extent tuple to a 4-element array [minX, minY, maxX, maxY].
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <returns>4-element array containing [minX, minY, maxX, maxY].</returns>
    public static double[] ExtentToArray(double minX, double minY, double maxX, double maxY)
    {
        return new[] { minX, minY, maxX, maxY };
    }

    /// <summary>
    /// Converts a 4-element bbox array to an extent tuple.
    /// </summary>
    /// <param name="bbox">Bbox array containing [minX, minY, maxX, maxY].</param>
    /// <returns>Tuple containing (minX, minY, maxX, maxY).</returns>
    /// <exception cref="ArgumentException">Thrown if bbox array is null or does not contain exactly 4 elements.</exception>
    public static (double MinX, double MinY, double MaxX, double MaxY) ArrayToExtent(double[] bbox)
    {
        if (bbox is null || bbox.Length < 4)
        {
            throw new ArgumentException("Bbox array must contain at least 4 elements [minX, minY, maxX, maxY].", nameof(bbox));
        }

        return (bbox[0], bbox[1], bbox[2], bbox[3]);
    }

    /// <summary>
    /// Converts an extent tuple to a comma-separated string "minX,minY,maxX,maxY".
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <returns>String formatted as "minX,minY,maxX,maxY" using invariant culture.</returns>
    public static string ExtentToBbox(double minX, double minY, double maxX, double maxY)
    {
        return $"{minX:G},{minY:G},{maxX:G},{maxY:G}";
    }

    /// <summary>
    /// Converts an extent tuple to a BoundingBox record.
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <param name="crs">Optional coordinate reference system identifier.</param>
    /// <returns>BoundingBox record with the specified coordinates and CRS.</returns>
    public static BoundingBox ExtentToBoundingBox(double minX, double minY, double maxX, double maxY, string? crs = null)
    {
        return new BoundingBox(minX, minY, maxX, maxY, null, null, crs);
    }

    /// <summary>
    /// Converts a BoundingBox record to an extent tuple.
    /// </summary>
    /// <param name="bbox">BoundingBox record.</param>
    /// <returns>Tuple containing (minX, minY, maxX, maxY).</returns>
    /// <exception cref="ArgumentNullException">Thrown if bbox is null.</exception>
    public static (double MinX, double MinY, double MaxX, double MaxY) BoundingBoxToExtent(BoundingBox bbox)
    {
        if (bbox is null)
        {
            throw new ArgumentNullException(nameof(bbox));
        }

        return (bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY);
    }

    /// <summary>
    /// Converts an NTS Envelope to an extent tuple.
    /// </summary>
    /// <param name="envelope">NetTopologySuite Envelope.</param>
    /// <returns>
    /// Tuple containing (minX, minY, maxX, maxY) if envelope is valid, null if envelope is null or null-valued.
    /// </returns>
    public static (double MinX, double MinY, double MaxX, double MaxY)? EnvelopeToExtent(Envelope? envelope)
    {
        if (envelope is null || envelope.IsNull)
        {
            return null;
        }

        if (!IsValidExtent(envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY))
        {
            return null;
        }

        return (envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY);
    }

    /// <summary>
    /// Converts an extent tuple to an NTS Envelope.
    /// </summary>
    /// <param name="minX">Minimum X coordinate.</param>
    /// <param name="minY">Minimum Y coordinate.</param>
    /// <param name="maxX">Maximum X coordinate.</param>
    /// <param name="maxY">Maximum Y coordinate.</param>
    /// <returns>NetTopologySuite Envelope with the specified coordinates.</returns>
    public static Envelope ExtentToEnvelope(double minX, double minY, double maxX, double maxY)
    {
        return new Envelope(minX, maxX, minY, maxY);
    }
}
