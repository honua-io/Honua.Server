// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NetTopologySuite.Geometries;
using Honua.Benchmarks.Helpers;

namespace Honua.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for NetTopologySuite geometry operations used throughout Honua.
/// Tests intersection, union, buffer, contains, and other spatial operations.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class GeometryOperationsBenchmarks
{
    private Polygon _smallPolygon1 = null!;
    private Polygon _smallPolygon2 = null!;
    private Polygon _mediumPolygon1 = null!;
    private Polygon _mediumPolygon2 = null!;
    private Polygon _largePolygon1 = null!;
    private Polygon _largePolygon2 = null!;
    private Polygon _complexPolygon = null!;
    private Point _testPoint = null!;
    private LineString _testLine = null!;
    private List<Geometry> _geometryCollection = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Small polygons (10 vertices)
        _smallPolygon1 = GeometryDataGenerator.GeneratePolygon(10, 0, 0, 10);
        _smallPolygon2 = GeometryDataGenerator.GeneratePolygon(10, 5, 5, 10);

        // Medium polygons (100 vertices)
        _mediumPolygon1 = GeometryDataGenerator.GeneratePolygon(100, 0, 0, 10);
        _mediumPolygon2 = GeometryDataGenerator.GeneratePolygon(100, 5, 5, 10);

        // Large polygons (1000 vertices)
        _largePolygon1 = GeometryDataGenerator.GeneratePolygon(1000, 0, 0, 10);
        _largePolygon2 = GeometryDataGenerator.GeneratePolygon(1000, 5, 5, 10);

        // Complex polygon with holes
        _complexPolygon = GeometryDataGenerator.GeneratePolygonWithHoles(100, 5, 20);

        // Test geometries
        _testPoint = GeometryDataGenerator.GeneratePoint(0, 10, 0, 10);
        _testLine = GeometryDataGenerator.GenerateLineString(50, 0, 10, 0, 10);

        // Collection for bulk operations
        _geometryCollection = GeometryDataGenerator.GenerateGeometryCollection(
            100,
            i => GeometryDataGenerator.GeneratePolygon(20, i * 0.5, i * 0.5, 5)
        );
    }

    // ========== INTERSECTION BENCHMARKS ==========

    [Benchmark]
    public Geometry Intersection_Small()
    {
        return _smallPolygon1.Intersection(_smallPolygon2);
    }

    [Benchmark]
    public Geometry Intersection_Medium()
    {
        return _mediumPolygon1.Intersection(_mediumPolygon2);
    }

    [Benchmark]
    public Geometry Intersection_Large()
    {
        return _largePolygon1.Intersection(_largePolygon2);
    }

    // ========== UNION BENCHMARKS ==========

    [Benchmark]
    public Geometry Union_Small()
    {
        return _smallPolygon1.Union(_smallPolygon2);
    }

    [Benchmark]
    public Geometry Union_Medium()
    {
        return _mediumPolygon1.Union(_mediumPolygon2);
    }

    [Benchmark]
    public Geometry Union_Large()
    {
        return _largePolygon1.Union(_largePolygon2);
    }

    // ========== BUFFER BENCHMARKS ==========

    [Benchmark]
    public Geometry Buffer_Point_Distance1()
    {
        return _testPoint.Buffer(1.0);
    }

    [Benchmark]
    public Geometry Buffer_Point_Distance10()
    {
        return _testPoint.Buffer(10.0);
    }

    [Benchmark]
    public Geometry Buffer_LineString_Distance1()
    {
        return _testLine.Buffer(1.0);
    }

    [Benchmark]
    public Geometry Buffer_Polygon_Small_Distance1()
    {
        return _smallPolygon1.Buffer(1.0);
    }

    [Benchmark]
    public Geometry Buffer_Polygon_Medium_Distance1()
    {
        return _mediumPolygon1.Buffer(1.0);
    }

    // ========== CONTAINS/INTERSECTS BENCHMARKS ==========

    [Benchmark]
    public bool Contains_Small()
    {
        return _smallPolygon1.Contains(_testPoint);
    }

    [Benchmark]
    public bool Contains_Medium()
    {
        return _mediumPolygon1.Contains(_testPoint);
    }

    [Benchmark]
    public bool Contains_Large()
    {
        return _largePolygon1.Contains(_testPoint);
    }

    [Benchmark]
    public bool Intersects_Small()
    {
        return _smallPolygon1.Intersects(_smallPolygon2);
    }

    [Benchmark]
    public bool Intersects_Medium()
    {
        return _mediumPolygon1.Intersects(_mediumPolygon2);
    }

    [Benchmark]
    public bool Intersects_Large()
    {
        return _largePolygon1.Intersects(_largePolygon2);
    }

    // ========== DIFFERENCE BENCHMARKS ==========

    [Benchmark]
    public Geometry Difference_Small()
    {
        return _smallPolygon1.Difference(_smallPolygon2);
    }

    [Benchmark]
    public Geometry Difference_Medium()
    {
        return _mediumPolygon1.Difference(_mediumPolygon2);
    }

    // ========== COMPLEX OPERATIONS ==========

    [Benchmark]
    public Geometry ConvexHull_Medium()
    {
        return _mediumPolygon1.ConvexHull();
    }

    [Benchmark]
    public Geometry ConvexHull_Large()
    {
        return _largePolygon1.ConvexHull();
    }

    [Benchmark]
    public Geometry Simplify_Medium()
    {
        return NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(_mediumPolygon1, 0.5);
    }

    [Benchmark]
    public Geometry Simplify_Large()
    {
        return NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(_largePolygon1, 0.5);
    }

    [Benchmark]
    public Geometry PolygonWithHoles_Intersection()
    {
        return _complexPolygon.Intersection(_mediumPolygon2);
    }

    // ========== AREA AND LENGTH CALCULATIONS ==========

    [Benchmark]
    public double Area_Small()
    {
        return _smallPolygon1.Area;
    }

    [Benchmark]
    public double Area_Medium()
    {
        return _mediumPolygon1.Area;
    }

    [Benchmark]
    public double Area_Large()
    {
        return _largePolygon1.Area;
    }

    [Benchmark]
    public double Length_LineString()
    {
        return _testLine.Length;
    }

    // ========== DISTANCE CALCULATIONS ==========

    [Benchmark]
    public double Distance_Point_To_Polygon_Small()
    {
        return _testPoint.Distance(_smallPolygon1);
    }

    [Benchmark]
    public double Distance_Point_To_Polygon_Medium()
    {
        return _testPoint.Distance(_mediumPolygon1);
    }

    [Benchmark]
    public double Distance_Polygon_To_Polygon_Small()
    {
        return _smallPolygon1.Distance(_smallPolygon2);
    }

    // ========== BULK OPERATIONS ==========

    [Benchmark]
    public int BulkIntersectionCheck()
    {
        int count = 0;
        var testGeometry = _mediumPolygon1;
        foreach (var geometry in _geometryCollection)
        {
            if (testGeometry.Intersects(geometry))
                count++;
        }
        return count;
    }

    [Benchmark]
    public List<Geometry> BulkBufferOperation()
    {
        return _geometryCollection.Select(g => g.Buffer(1.0)).ToList();
    }
}
