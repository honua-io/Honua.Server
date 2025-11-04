// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Data;

/// <summary>
/// Static facade for CRS transformation operations.
/// Delegates to a pluggable <see cref="ICrsTransformProvider"/> implementation.
/// </summary>
public static class CrsTransform
{
    private static ICrsTransformProvider _provider = new ProjNETCrsTransformProvider();

    /// <summary>
    /// Gets or sets the CRS transformation provider.
    /// Defaults to <see cref="ProjNETCrsTransformProvider"/>.
    /// </summary>
    public static ICrsTransformProvider Provider
    {
        get => _provider;
        set => _provider = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Transforms an envelope (bounding box) from one CRS to another.
    /// </summary>
    public static (double MinX, double MinY, double MaxX, double MaxY) TransformEnvelope(
        double minX,
        double minY,
        double maxX,
        double maxY,
        int sourceSrid,
        int targetSrid)
    {
        return _provider.TransformEnvelope(minX, minY, maxX, maxY, sourceSrid, targetSrid);
    }

    /// <summary>
    /// Transforms a single coordinate from one CRS to another.
    /// </summary>
    public static (double X, double Y) TransformCoordinate(double x, double y, int sourceSrid, int targetSrid)
    {
        return _provider.TransformPoint(x, y, sourceSrid, targetSrid);
    }

    /// <summary>
    /// Transforms a geometry from one CRS to another.
    /// </summary>
    public static Geometry TransformGeometry(Geometry geometry, int sourceSrid, int targetSrid)
    {
        var result = _provider.TransformGeometry(geometry, sourceSrid, targetSrid);
        return result ?? geometry;
    }

    /// <summary>
    /// Checks if transformation is supported between the given SRIDs.
    /// </summary>
    public static bool SupportsTransformation(int sourceSrid, int targetSrid)
    {
        return _provider.SupportsTransformation(sourceSrid, targetSrid);
    }

    /// <summary>
    /// Gets the number of cached transformation entries.
    /// </summary>
    internal static int CacheEntryCount
    {
        get
        {
            if (_provider is ProjNETCrsTransformProvider projNetProvider)
            {
                return projNetProvider.CacheEntryCount;
            }
            return 0;
        }
    }

    internal static void ClearCache()
    {
        if (_provider is ProjNETCrsTransformProvider projNetProvider)
        {
            projNetProvider.ClearCache();
        }
    }
}
