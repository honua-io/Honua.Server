// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Data;

/// <summary>
/// Provides coordinate reference system (CRS) transformation capabilities.
/// </summary>
public interface ICrsTransformProvider
{
    /// <summary>
    /// Transforms an envelope (bounding box) from one CRS to another.
    /// </summary>
    /// <param name="minX">Minimum X coordinate</param>
    /// <param name="minY">Minimum Y coordinate</param>
    /// <param name="maxX">Maximum X coordinate</param>
    /// <param name="maxY">Maximum Y coordinate</param>
    /// <param name="sourceSrid">Source spatial reference system ID (EPSG code)</param>
    /// <param name="targetSrid">Target spatial reference system ID (EPSG code)</param>
    /// <returns>Transformed envelope coordinates</returns>
    (double MinX, double MinY, double MaxX, double MaxY) TransformEnvelope(
        double minX,
        double minY,
        double maxX,
        double maxY,
        int sourceSrid,
        int targetSrid);

    /// <summary>
    /// Transforms a single point from one CRS to another.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="sourceSrid">Source spatial reference system ID (EPSG code)</param>
    /// <param name="targetSrid">Target spatial reference system ID (EPSG code)</param>
    /// <returns>Transformed point coordinates</returns>
    (double X, double Y) TransformPoint(
        double x,
        double y,
        int sourceSrid,
        int targetSrid);

    /// <summary>
    /// Transforms a geometry from one CRS to another.
    /// </summary>
    /// <param name="geometry">Geometry to transform</param>
    /// <param name="sourceSrid">Source spatial reference system ID (EPSG code)</param>
    /// <param name="targetSrid">Target spatial reference system ID (EPSG code)</param>
    /// <returns>Transformed geometry</returns>
    NetTopologySuite.Geometries.Geometry? TransformGeometry(
        NetTopologySuite.Geometries.Geometry? geometry,
        int sourceSrid,
        int targetSrid);

    /// <summary>
    /// Checks if this provider supports transformation between the given SRIDs.
    /// </summary>
    /// <param name="sourceSrid">Source SRID</param>
    /// <param name="targetSrid">Target SRID</param>
    /// <returns>True if transformation is supported</returns>
    bool SupportsTransformation(int sourceSrid, int targetSrid);
}
