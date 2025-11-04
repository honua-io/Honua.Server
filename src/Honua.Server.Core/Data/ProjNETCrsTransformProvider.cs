// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Honua.Server.Core.Data;

/// <summary>
/// ProjNET-based implementation of CRS transformation provider.
/// Provides coordinate transformations using the ProjNET library instead of GDAL.
/// </summary>
public sealed class ProjNETCrsTransformProvider : ICrsTransformProvider, IDisposable
{
    private static readonly Meter Meter = new("Honua.Server.Core.Crs", "1.0.0");
    private static readonly Counter<long> TransformCounter = Meter.CreateCounter<long>(
        "honua_crs_transformations_total",
        description: "Total number of CRS transformation operations.");
    private static readonly Counter<long> CacheHitCounter = Meter.CreateCounter<long>(
        "honua_crs_cache_hits_total",
        description: "Total number of CRS transformation cache hits.");
    private static readonly Counter<long> CacheMissCounter = Meter.CreateCounter<long>(
        "honua_crs_cache_misses_total",
        description: "Total number of CRS transformation cache misses.");
    private static readonly Histogram<double> TransformDuration = Meter.CreateHistogram<double>(
        "honua_crs_transform_duration_seconds",
        unit: "s",
        description: "Elapsed time for CRS transformations.");
    private static readonly Counter<long> UnsupportedSridCounter = Meter.CreateCounter<long>(
        "honua_crs_unsupported_srid_total",
        description: "Total number of unsupported SRID requests.");

    private const int MaxCacheSize = 1000;
    private readonly ConcurrentDictionary<(int Source, int Target), Lazy<TransformationEntry?>> _cache = new();
    private readonly LinkedList<(int Source, int Target)> _lruList = new();
    private readonly ReaderWriterLockSlim _lruLock = new();
    private readonly CoordinateSystemFactory _csFactory;
    private readonly CoordinateTransformationFactory _ctFactory;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProjNETCrsTransformProvider"/> class.
    /// </summary>
    public ProjNETCrsTransformProvider()
    {
        _csFactory = new CoordinateSystemFactory();
        _ctFactory = new CoordinateTransformationFactory();
    }

    /// <inheritdoc />
    public (double MinX, double MinY, double MaxX, double MaxY) TransformEnvelope(
        double minX,
        double minY,
        double maxX,
        double maxY,
        int sourceSrid,
        int targetSrid)
    {
        var entry = GetEntry(sourceSrid, targetSrid);
        if (entry is null)
        {
            return (minX, minY, maxX, maxY);
        }

        var stopwatch = Stopwatch.StartNew();

        var xs = ArrayPool<double>.Shared.Rent(4);
        var ys = ArrayPool<double>.Shared.Rent(4);
        double minXOut;
        double maxXOut;
        double minYOut;
        double maxYOut;

        try
        {
            xs[0] = minX; ys[0] = minY;
            xs[1] = maxX; ys[1] = maxY;
            xs[2] = minX; ys[2] = maxY;
            xs[3] = maxX; ys[3] = minY;

            entry.TransformPoints(xs, ys, null, 4);

            minXOut = Math.Min(Math.Min(xs[0], xs[1]), Math.Min(xs[2], xs[3]));
            maxXOut = Math.Max(Math.Max(xs[0], xs[1]), Math.Max(xs[2], xs[3]));
            minYOut = Math.Min(Math.Min(ys[0], ys[1]), Math.Min(ys[2], ys[3]));
            maxYOut = Math.Max(Math.Max(ys[0], ys[1]), Math.Max(ys[2], ys[3]));
        }
        finally
        {
            ArrayPool<double>.Shared.Return(xs);
            ArrayPool<double>.Shared.Return(ys);
        }

        stopwatch.Stop();
        RecordMetrics(entry, stopwatch.Elapsed);
        return (minXOut, minYOut, maxXOut, maxYOut);
    }

    /// <inheritdoc />
    public (double X, double Y) TransformPoint(
        double x,
        double y,
        int sourceSrid,
        int targetSrid)
    {
        var entry = GetEntry(sourceSrid, targetSrid);
        if (entry is null)
        {
            return (x, y);
        }

        var z = 0d;
        var stopwatch = Stopwatch.StartNew();
        entry.Transform(ref x, ref y, ref z);
        stopwatch.Stop();
        RecordMetrics(entry, stopwatch.Elapsed);
        return (x, y);
    }

    /// <inheritdoc />
    public Geometry? TransformGeometry(
        Geometry? geometry,
        int sourceSrid,
        int targetSrid)
    {
        if (geometry is null)
        {
            return null;
        }

        var entry = GetEntry(sourceSrid, targetSrid);
        if (entry is null)
        {
            geometry.SRID = targetSrid;
            return geometry;
        }

        var copy = geometry.Copy();
        var stopwatch = Stopwatch.StartNew();
        copy.Apply(new CoordinateTransformFilter(entry));
        copy.GeometryChanged();
        stopwatch.Stop();
        RecordMetrics(entry, stopwatch.Elapsed);
        copy.SRID = targetSrid;
        return copy;
    }

    /// <inheritdoc />
    public bool SupportsTransformation(int sourceSrid, int targetSrid)
    {
        if (sourceSrid == targetSrid || sourceSrid == 0 || targetSrid == 0)
        {
            return true;
        }

        try
        {
            var sourceCs = CreateCoordinateSystem(sourceSrid);
            var targetCs = CreateCoordinateSystem(targetSrid);
            return sourceCs is not null && targetCs is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the number of cached transformation entries.
    /// </summary>
    public int CacheEntryCount => _cache.Count;

    /// <summary>
    /// Clears the transformation cache.
    /// </summary>
    public void ClearCache()
    {
        _lruLock.EnterWriteLock();
        try
        {
            foreach (var key in _cache.Keys)
            {
                if (!_cache.TryRemove(key, out var lazy))
                {
                    continue;
                }

                if (lazy.IsValueCreated && lazy.Value is { } entry)
                {
                    entry.Dispose();
                }
            }

            _lruList.Clear();
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearCache();
        _lruLock.Dispose();
        _disposed = true;
    }

    private TransformationEntry? GetEntry(int sourceSrid, int targetSrid)
    {
        if (sourceSrid == targetSrid || sourceSrid == 0 || targetSrid == 0)
        {
            return null;
        }

        var key = (Source: sourceSrid, Target: targetSrid);
        if (!_cache.TryGetValue(key, out var existing))
        {
            // Enforce LRU limit before adding new entry
            EnforceLruLimit();

            var lazy = new Lazy<TransformationEntry?>(() => CreateEntry(sourceSrid, targetSrid), LazyThreadSafetyMode.ExecutionAndPublication);
            existing = _cache.GetOrAdd(key, lazy);
            if (existing == lazy)
            {
                var created = existing.Value;
                CacheMissCounter.Add(1, BuildTags(sourceSrid, targetSrid));

                // Add to LRU list (most recently used at front)
                UpdateLruList(key);

                return created;
            }
        }

        CacheHitCounter.Add(1, BuildTags(sourceSrid, targetSrid));

        // Update LRU position on cache hit
        UpdateLruList(key);

        return existing.Value;
    }

    private void UpdateLruList((int Source, int Target) key)
    {
        _lruLock.EnterWriteLock();
        try
        {
            // Remove existing entry if present
            _lruList.Remove(key);

            // Add to front (most recently used)
            _lruList.AddFirst(key);
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    private void EnforceLruLimit()
    {
        if (_cache.Count < MaxCacheSize)
        {
            return;
        }

        _lruLock.EnterWriteLock();
        try
        {
            while (_cache.Count >= MaxCacheSize && _lruList.Count > 0)
            {
                // Remove least recently used (from the back)
                var oldest = _lruList.Last!.Value;
                _lruList.RemoveLast();

                if (_cache.TryRemove(oldest, out var lazy))
                {
                    // Dispose resources properly
                    if (lazy.IsValueCreated && lazy.Value is { } entry)
                    {
                        entry.Dispose();
                    }
                }
            }
        }
        finally
        {
            _lruLock.ExitWriteLock();
        }
    }

    private TransformationEntry? CreateEntry(int sourceSrid, int targetSrid)
    {
        try
        {
            var sourceCs = CreateCoordinateSystem(sourceSrid);
            if (sourceCs is null)
            {
                UnsupportedSridCounter.Add(1, new TagList { { "srid", sourceSrid }, { "position", "source" } });
                return null;
            }

            var targetCs = CreateCoordinateSystem(targetSrid);
            if (targetCs is null)
            {
                UnsupportedSridCounter.Add(1, new TagList { { "srid", targetSrid }, { "position", "target" } });
                return null;
            }

            var transformation = _ctFactory.CreateFromCoordinateSystems(sourceCs, targetCs);
            return new TransformationEntry(sourceSrid, targetSrid, transformation);
        }
        catch
        {
            UnsupportedSridCounter.Add(1, new TagList { { "source_srid", sourceSrid }, { "target_srid", targetSrid } });
            return null;
        }
    }

    private CoordinateSystem? CreateCoordinateSystem(int srid)
    {
        // Support common EPSG codes using ProjNET's factory
        // For codes not supported, we return null and log
        try
        {
            return srid switch
            {
                // WGS84 - Geographic (EPSG:4326)
                4326 => GeographicCoordinateSystem.WGS84,

                // Web Mercator (EPSG:3857)
                3857 => ProjectedCoordinateSystem.WebMercator,

                // For other EPSG codes, try to create from the factory using WKT
                // ProjNET 2.1.0 has limited built-in support, so we only support the most common ones
                _ => TryCreateFromWkt(srid)
            };
        }
        catch
        {
            return null;
        }
    }

    private CoordinateSystem? TryCreateFromWkt(int srid)
    {
        // For unsupported SRIDs, we could potentially load from EPSG database or WKT strings
        // However, ProjNET 2.1.0 doesn't include a comprehensive EPSG database
        // So we return null and let the caller handle it (will return unchanged coordinates)

        // Common additional EPSG codes that can be constructed programmatically
        // For now, we'll only support the most common ones (WGS84 and Web Mercator)
        // Additional codes can be added as needed

        return null;
    }

    private static void RecordMetrics(TransformationEntry entry, TimeSpan elapsed)
    {
        var tags = entry.GetTags();
        TransformCounter.Add(1, tags);
        TransformDuration.Record(elapsed.TotalSeconds, tags);
    }

    private static TagList BuildTags(int source, int target)
    {
        var tags = new TagList
        {
            { "source_srid", source },
            { "target_srid", target }
        };
        return tags;
    }

    private sealed class CoordinateTransformFilter : ICoordinateSequenceFilter
    {
        private readonly TransformationEntry _entry;
        private bool _transformed;

        public CoordinateTransformFilter(TransformationEntry entry)
        {
            _entry = entry;
        }

        public bool Done => _transformed;

        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            if (_transformed)
            {
                return;
            }

            var count = seq.Count;
            if (count == 0)
            {
                _transformed = true;
                return;
            }

            var hasZ = seq.Dimension >= 3;
            var xs = ArrayPool<double>.Shared.Rent(count);
            var ys = ArrayPool<double>.Shared.Rent(count);
            double[]? zs = hasZ ? ArrayPool<double>.Shared.Rent(count) : null;

            try
            {
                for (var index = 0; index < count; index++)
                {
                    xs[index] = seq.GetX(index);
                    ys[index] = seq.GetY(index);
                    if (hasZ && zs is not null)
                    {
                        zs[index] = seq.GetZ(index);
                    }
                }

                _entry.TransformPoints(xs, ys, zs, count);

                for (var index = 0; index < count; index++)
                {
                    seq.SetX(index, xs[index]);
                    seq.SetY(index, ys[index]);
                    if (hasZ && zs is not null)
                    {
                        seq.SetZ(index, zs[index]);
                    }
                }

                _transformed = true;
            }
            finally
            {
                ArrayPool<double>.Shared.Return(xs);
                ArrayPool<double>.Shared.Return(ys);
                if (zs is not null)
                {
                    ArrayPool<double>.Shared.Return(zs);
                }
            }
        }
    }

    private sealed class TransformationEntry : IDisposable
    {
        private readonly object _syncRoot = new();
        private readonly ICoordinateTransformation _transformation;
        private readonly TagList _tags;
        private bool _disposed;

        public TransformationEntry(int sourceSrid, int targetSrid, ICoordinateTransformation transformation)
        {
            _transformation = transformation ?? throw new ArgumentNullException(nameof(transformation));
            _tags = BuildTags(sourceSrid, targetSrid);
        }

        public void Transform(ref double x, ref double y, ref double z)
        {
            var xs = ArrayPool<double>.Shared.Rent(1);
            var ys = ArrayPool<double>.Shared.Rent(1);
            var zs = ArrayPool<double>.Shared.Rent(1);
            try
            {
                xs[0] = x;
                ys[0] = y;
                zs[0] = z;

                TransformPoints(xs, ys, zs, 1);

                x = xs[0];
                y = ys[0];
                z = zs[0];
            }
            finally
            {
                ArrayPool<double>.Shared.Return(xs);
                ArrayPool<double>.Shared.Return(ys);
                ArrayPool<double>.Shared.Return(zs);
            }
        }

        public TagList GetTags() => _tags;

        public void TransformPoints(double[] xs, double[] ys, double[]? zs, int count)
        {
            if (xs is null)
            {
                throw new ArgumentNullException(nameof(xs));
            }

            if (ys is null)
            {
                throw new ArgumentNullException(nameof(ys));
            }

            if (count <= 0)
            {
                return;
            }

            lock (_syncRoot)
            {
                ThrowIfDisposed();

                // Transform each point using ProjNET's MathTransform
                var mathTransform = _transformation.MathTransform;

                for (var i = 0; i < count; i++)
                {
                    var coord = zs is not null
                        ? new[] { xs[i], ys[i], zs[i] }
                        : new[] { xs[i], ys[i] };

                    var transformed = mathTransform.Transform(coord);

                    xs[i] = transformed[0];
                    ys[i] = transformed[1];
                    if (zs is not null && transformed.Length > 2)
                    {
                        zs[i] = transformed[2];
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                // ProjNET's ICoordinateTransformation doesn't require explicit disposal
                // But we set the flag to prevent future use
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TransformationEntry));
            }
        }
    }
}
