using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Simplify;
using System.Collections.Generic;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks for spatial query operations including intersection, containment, distance,
/// buffer, union, and spatial indexing operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class SpatialQueryBenchmarks
{
    private GeometryFactory _geometryFactory = null!;
    private Polygon _testPolygon = null!;
    private Polygon _largePolygon = null!;
    private Point _testPoint = null!;
    private LineString _testLine = null!;
    private List<Polygon> _polygons100 = null!;
    private List<Polygon> _polygons1000 = null!;
    private List<Point> _points1000 = null!;
    private STRtree<Polygon> _spatialIndex = null!;
    private Envelope _queryEnvelope = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Test polygon
        _testPolygon = CreatePolygon(-122.5, 45.5, 0.01);

        // Large polygon with many vertices
        _largePolygon = CreateComplexPolygon(-122.4, 45.6, 0.05, vertexCount: 1000);

        // Test point
        _testPoint = _geometryFactory.CreatePoint(new Coordinate(-122.45, 45.55));

        // Test line
        var lineCoords = new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.4, 45.6),
            new Coordinate(-122.3, 45.7)
        };
        _testLine = _geometryFactory.CreateLineString(lineCoords);

        // Generate datasets
        _polygons100 = GeneratePolygons(100);
        _polygons1000 = GeneratePolygons(1000);
        _points1000 = GeneratePoints(1000);

        // Build spatial index
        _spatialIndex = new STRtree<Polygon>();
        foreach (var polygon in _polygons1000)
        {
            _spatialIndex.Insert(polygon.EnvelopeInternal, polygon);
        }
        _spatialIndex.Build();

        // Query envelope
        _queryEnvelope = new Envelope(-122.5, -122.3, 45.5, 45.7);
    }

    // =====================================================
    // Bounding Box Operations
    // =====================================================

    [Benchmark(Description = "BBox: Envelope creation")]
    public Envelope CreateEnvelope()
    {
        return new Envelope(-122.5, -122.3, 45.5, 45.7);
    }

    [Benchmark(Description = "BBox: Intersection test (100 polygons)")]
    public int EnvelopeIntersectionTest100()
    {
        var count = 0;
        foreach (var polygon in _polygons100)
        {
            if (_queryEnvelope.Intersects(polygon.EnvelopeInternal))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "BBox: Intersection test (1,000 polygons)")]
    public int EnvelopeIntersectionTest1000()
    {
        var count = 0;
        foreach (var polygon in _polygons1000)
        {
            if (_queryEnvelope.Intersects(polygon.EnvelopeInternal))
            {
                count++;
            }
        }
        return count;
    }

    // =====================================================
    // Spatial Predicates
    // =====================================================

    [Benchmark(Description = "Spatial: Point in polygon (simple)")]
    public bool PointInPolygonSimple()
    {
        return _testPolygon.Contains(_testPoint);
    }

    [Benchmark(Description = "Spatial: Point in polygon (complex, 1000 vertices)")]
    public bool PointInPolygonComplex()
    {
        return _largePolygon.Contains(_testPoint);
    }

    [Benchmark(Description = "Spatial: Polygon intersection (100 tests)")]
    public int PolygonIntersectionTest()
    {
        var count = 0;
        for (int i = 0; i < 100; i++)
        {
            if (_testPolygon.Intersects(_polygons100[i]))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Spatial: Polygon contains (100 tests)")]
    public int PolygonContainsTest()
    {
        var count = 0;
        foreach (var point in _points1000.Take(100))
        {
            if (_testPolygon.Contains(point))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Spatial: Polygon within (100 tests)")]
    public int PolygonWithinTest()
    {
        var count = 0;
        for (int i = 0; i < 100; i++)
        {
            if (_polygons100[i].Within(_testPolygon))
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Spatial: Line intersects polygon")]
    public bool LineIntersectsPolygon()
    {
        return _testLine.Intersects(_testPolygon);
    }

    [Benchmark(Description = "Spatial: Touches predicate")]
    public bool TouchesPredicate()
    {
        var adjacentPoly = CreatePolygon(-122.49, 45.5, 0.01);
        return _testPolygon.Touches(adjacentPoly);
    }

    [Benchmark(Description = "Spatial: Overlaps predicate")]
    public bool OverlapsPredicate()
    {
        var overlappingPoly = CreatePolygon(-122.495, 45.505, 0.01);
        return _testPolygon.Overlaps(overlappingPoly);
    }

    // =====================================================
    // Distance Operations
    // =====================================================

    [Benchmark(Description = "Distance: Point to polygon")]
    public double DistancePointToPolygon()
    {
        return _testPoint.Distance(_testPolygon);
    }

    [Benchmark(Description = "Distance: Polygon to polygon")]
    public double DistancePolygonToPolygon()
    {
        return _testPolygon.Distance(_polygons100[0]);
    }

    [Benchmark(Description = "Distance: Within distance (100 points)")]
    public int WithinDistanceTest()
    {
        var count = 0;
        var threshold = 0.05; // ~5km at this latitude
        foreach (var point in _points1000.Take(100))
        {
            if (_testPoint.Distance(point) <= threshold)
            {
                count++;
            }
        }
        return count;
    }

    [Benchmark(Description = "Distance: Nearest point")]
    public Point NearestPoint()
    {
        var op = new DistanceOp(_testPolygon, _testPoint);
        var coords = op.NearestPoints();
        return _geometryFactory.CreatePoint(coords[0]);
    }

    // =====================================================
    // Buffer Operations
    // =====================================================

    [Benchmark(Description = "Buffer: Point buffer (simple)")]
    public Geometry BufferPointSimple()
    {
        return _testPoint.Buffer(0.01); // ~1km buffer
    }

    [Benchmark(Description = "Buffer: Point buffer (high quality, 64 segments)")]
    public Geometry BufferPointHighQuality()
    {
        return _testPoint.Buffer(0.01, 64);
    }

    [Benchmark(Description = "Buffer: Polygon buffer")]
    public Geometry BufferPolygon()
    {
        return _testPolygon.Buffer(0.01);
    }

    [Benchmark(Description = "Buffer: Line buffer")]
    public Geometry BufferLine()
    {
        return _testLine.Buffer(0.01);
    }

    [Benchmark(Description = "Buffer: Negative buffer (erosion)")]
    public Geometry BufferNegative()
    {
        return _testPolygon.Buffer(-0.001); // Shrink polygon
    }

    // =====================================================
    // Union Operations
    // =====================================================

    [Benchmark(Description = "Union: Two polygons")]
    public Geometry UnionTwoPolygons()
    {
        return _testPolygon.Union(_polygons100[0]);
    }

    [Benchmark(Description = "Union: 10 polygons")]
    public Geometry Union10Polygons()
    {
        Geometry result = _polygons100[0];
        for (int i = 1; i < 10; i++)
        {
            result = result.Union(_polygons100[i]);
        }
        return result;
    }

    [Benchmark(Description = "Union: 100 polygons (cascaded)")]
    public Geometry UnionCascaded100()
    {
        var collection = _geometryFactory.CreateGeometryCollection(_polygons100.ToArray());
        return collection.Union();
    }

    // =====================================================
    // Intersection Operations
    // =====================================================

    [Benchmark(Description = "Intersection: Two polygons")]
    public Geometry IntersectionTwoPolygons()
    {
        var overlappingPoly = CreatePolygon(-122.495, 45.505, 0.01);
        return _testPolygon.Intersection(overlappingPoly);
    }

    [Benchmark(Description = "Intersection: Line with polygon")]
    public Geometry IntersectionLinePolygon()
    {
        return _testLine.Intersection(_testPolygon);
    }

    [Benchmark(Description = "Intersection: Envelope with polygon")]
    public Geometry IntersectionEnvelopePolygon()
    {
        var envelope = _queryEnvelope.ToGeometry(_geometryFactory);
        return envelope.Intersection(_testPolygon);
    }

    // =====================================================
    // Simplification Operations
    // =====================================================

    [Benchmark(Description = "Simplification: DouglasPeucker (tolerance 0.0001)")]
    public Geometry SimplifyDouglasPeucker0001()
    {
        return DouglasPeuckerSimplifier.Simplify(_largePolygon, 0.0001);
    }

    [Benchmark(Description = "Simplification: DouglasPeucker (tolerance 0.001)")]
    public Geometry SimplifyDouglasPeucker001()
    {
        return DouglasPeuckerSimplifier.Simplify(_largePolygon, 0.001);
    }

    [Benchmark(Description = "Simplification: TopologyPreserving")]
    public Geometry SimplifyTopologyPreserving()
    {
        return TopologyPreservingSimplifier.Simplify(_largePolygon, 0.001);
    }

    [Benchmark(Description = "Simplification: VW (Visvalingam-Whyatt)")]
    public Geometry SimplifyVW()
    {
        return VWSimplifier.Simplify(_largePolygon, 0.001);
    }

    // =====================================================
    // Spatial Index Operations
    // =====================================================

    [Benchmark(Description = "Index: STRtree query (envelope)")]
    public List<Polygon> SpatialIndexQuery()
    {
        return _spatialIndex.Query(_queryEnvelope).ToList();
    }

    [Benchmark(Description = "Index: Build STRtree (1,000 polygons)")]
    public STRtree<Polygon> BuildSpatialIndex()
    {
        var index = new STRtree<Polygon>();
        foreach (var polygon in _polygons1000)
        {
            index.Insert(polygon.EnvelopeInternal, polygon);
        }
        index.Build();
        return index;
    }

    [Benchmark(Description = "Index: Nearest neighbor (k=1)")]
    public Polygon NearestNeighbor()
    {
        var itemDist = _spatialIndex.NearestNeighbour(_queryEnvelope, _testPolygon, new GeometryItemDistance());
        return itemDist;
    }

    [Benchmark(Description = "Index: Nearest neighbors (k=10)")]
    public List<Polygon> NearestNeighbors10()
    {
        var results = new List<Polygon>();
        // Simplified k-NN simulation
        var candidates = _spatialIndex.Query(_queryEnvelope).ToList();
        results.AddRange(candidates
            .OrderBy(p => p.Distance(_testPoint))
            .Take(10));
        return results;
    }

    // =====================================================
    // Convex Hull Operations
    // =====================================================

    [Benchmark(Description = "ConvexHull: Single polygon")]
    public Geometry ConvexHullSingle()
    {
        return _testPolygon.ConvexHull();
    }

    [Benchmark(Description = "ConvexHull: 100 points")]
    public Geometry ConvexHull100Points()
    {
        var multiPoint = _geometryFactory.CreateMultiPointFromCoords(_points1000.Take(100).Select(p => p.Coordinate).ToArray());
        return multiPoint.ConvexHull();
    }

    [Benchmark(Description = "ConvexHull: 1,000 points")]
    public Geometry ConvexHull1000Points()
    {
        var multiPoint = _geometryFactory.CreateMultiPointFromCoords(_points1000.Select(p => p.Coordinate).ToArray());
        return multiPoint.ConvexHull();
    }

    // =====================================================
    // Centroid and Area Operations
    // =====================================================

    [Benchmark(Description = "Centroid: Single polygon")]
    public Point CentroidPolygon()
    {
        return _testPolygon.Centroid;
    }

    [Benchmark(Description = "Centroid: 100 polygons")]
    public List<Point> Centroid100Polygons()
    {
        return _polygons100.Select(p => p.Centroid).ToList();
    }

    [Benchmark(Description = "Area: Single polygon")]
    public double AreaPolygon()
    {
        return _testPolygon.Area;
    }

    [Benchmark(Description = "Area: 100 polygons")]
    public double Area100Polygons()
    {
        return _polygons100.Sum(p => p.Area);
    }

    [Benchmark(Description = "Length: Line string")]
    public double LengthLineString()
    {
        return _testLine.Length;
    }

    // =====================================================
    // Validation Operations
    // =====================================================

    [Benchmark(Description = "Validation: IsValid check")]
    public bool IsValidCheck()
    {
        return _testPolygon.IsValid;
    }

    [Benchmark(Description = "Validation: IsSimple check")]
    public bool IsSimpleCheck()
    {
        return _testPolygon.IsSimple;
    }

    [Benchmark(Description = "Validation: 100 polygons")]
    public int ValidatePolygons()
    {
        var count = 0;
        foreach (var polygon in _polygons100)
        {
            if (polygon.IsValid)
            {
                count++;
            }
        }
        return count;
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private Polygon CreatePolygon(double baseLon, double baseLat, double size)
    {
        var coords = new[]
        {
            new Coordinate(baseLon, baseLat),
            new Coordinate(baseLon + size, baseLat),
            new Coordinate(baseLon + size, baseLat + size),
            new Coordinate(baseLon, baseLat + size),
            new Coordinate(baseLon, baseLat)
        };
        var ring = _geometryFactory.CreateLinearRing(coords);
        return _geometryFactory.CreatePolygon(ring);
    }

    private Polygon CreateComplexPolygon(double baseLon, double baseLat, double radius, int vertexCount)
    {
        var coords = new Coordinate[vertexCount + 1];
        for (int i = 0; i < vertexCount; i++)
        {
            var angle = (2 * Math.PI * i) / vertexCount;
            coords[i] = new Coordinate(
                baseLon + radius * Math.Cos(angle),
                baseLat + radius * Math.Sin(angle)
            );
        }
        coords[vertexCount] = coords[0]; // Close the ring

        var ring = _geometryFactory.CreateLinearRing(coords);
        return _geometryFactory.CreatePolygon(ring);
    }

    private List<Polygon> GeneratePolygons(int count)
    {
        var polygons = new List<Polygon>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var baseLon = -122.5 + (random.NextDouble() * 0.5);
            var baseLat = 45.5 + (random.NextDouble() * 0.5);
            var size = 0.001 + (random.NextDouble() * 0.005);
            polygons.Add(CreatePolygon(baseLon, baseLat, size));
        }

        return polygons;
    }

    private List<Point> GeneratePoints(int count)
    {
        var points = new List<Point>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var lon = -122.5 + (random.NextDouble() * 0.5);
            var lat = 45.5 + (random.NextDouble() * 0.5);
            points.Add(_geometryFactory.CreatePoint(new Coordinate(lon, lat)));
        }

        return points;
    }
}

/// <summary>
/// Custom distance function for spatial index nearest neighbor queries.
/// </summary>
public class GeometryItemDistance : IItemDistance<Envelope, Polygon>
{
    public double Distance(IBoundable<Envelope, Polygon> item1, IBoundable<Envelope, Polygon> item2)
    {
        return item1.Item.Distance(item2.Item);
    }
}
