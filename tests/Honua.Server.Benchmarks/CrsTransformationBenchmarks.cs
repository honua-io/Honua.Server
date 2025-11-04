using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks for Coordinate Reference System (CRS) transformations.
/// Tests performance of common CRS operations: WGS84 to Web Mercator, State Plane conversions,
/// UTM zones, and datum transformations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class CrsTransformationBenchmarks
{
    private GeometryFactory _wgs84Factory = null!;
    private GeometryFactory _webMercatorFactory = null!;
    private CoordinateTransformationFactory _ctFactory = null!;

    private Point _pointWgs84 = null!;
    private Polygon _polygonWgs84 = null!;
    private LineString _lineStringWgs84 = null!;
    private List<Point> _points100Wgs84 = null!;
    private List<Point> _points1000Wgs84 = null!;
    private List<Polygon> _polygons100Wgs84 = null!;

    private CoordinateSystem _wgs84Cs = null!;
    private CoordinateSystem _webMercatorCs = null!;
    private CoordinateSystem _nad83Cs = null!;
    private CoordinateSystem _utm10NCs = null!;
    private CoordinateSystem _statePlaneOregonNorthCs = null!;

    private ICoordinateTransformation _wgs84ToWebMercator = null!;
    private ICoordinateTransformation _webMercatorToWgs84 = null!;
    private ICoordinateTransformation _wgs84ToUtm10N = null!;
    private ICoordinateTransformation _wgs84ToNad83 = null!;
    private ICoordinateTransformation _wgs84ToStatePlane = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wgs84Factory = new GeometryFactory(new PrecisionModel(), 4326);
        _webMercatorFactory = new GeometryFactory(new PrecisionModel(), 3857);
        _ctFactory = new CoordinateTransformationFactory();

        // Define coordinate systems
        _wgs84Cs = GeographicCoordinateSystem.WGS84;
        _webMercatorCs = ProjectedCoordinateSystem.WebMercator;

        // NAD83
        _nad83Cs = new GeographicCoordinateSystem(
            HorizontalDatum.NorthAmericanDatum1983,
            AngularUnit.Degrees,
            PrimeMeridian.Greenwich,
            new AxisInfo("Lon", AxisOrientationEnum.East),
            new AxisInfo("Lat", AxisOrientationEnum.North)
        );

        // UTM Zone 10N (covers Portland, Oregon area)
        _utm10NCs = ProjectedCoordinateSystem.WGS84_UTM(10, true);

        // State Plane Oregon North (EPSG:2991)
        _statePlaneOregonNorthCs = CreateStatePlaneOregonNorth();

        // Create transformations
        _wgs84ToWebMercator = _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _webMercatorCs);
        _webMercatorToWgs84 = _ctFactory.CreateFromCoordinateSystems(_webMercatorCs, _wgs84Cs);
        _wgs84ToUtm10N = _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _utm10NCs);
        _wgs84ToNad83 = _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _nad83Cs);
        _wgs84ToStatePlane = _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _statePlaneOregonNorthCs);

        // Create test geometries
        _pointWgs84 = _wgs84Factory.CreatePoint(new Coordinate(-122.6765, 45.5231)); // Portland
        _polygonWgs84 = CreatePolygonWgs84(-122.7, 45.5, 0.01);

        var lineCoords = Enumerable.Range(0, 100)
            .Select(i => new Coordinate(-122.7 + i * 0.001, 45.5 + i * 0.001))
            .ToArray();
        _lineStringWgs84 = _wgs84Factory.CreateLineString(lineCoords);

        _points100Wgs84 = GeneratePoints(100);
        _points1000Wgs84 = GeneratePoints(1000);
        _polygons100Wgs84 = GeneratePolygons(100);
    }

    // =====================================================
    // WGS84 to Web Mercator (Most Common)
    // =====================================================

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (single point)")]
    public Point TransformPointWgs84ToWebMercator()
    {
        var coord = _wgs84ToWebMercator.MathTransform.Transform(_pointWgs84.Coordinate);
        return _webMercatorFactory.CreatePoint(coord);
    }

    [Benchmark(Description = "CRS: Web Mercator -> WGS84 (single point)")]
    public Point TransformPointWebMercatorToWgs84()
    {
        var pointWebMercator = TransformPointWgs84ToWebMercator();
        var coord = _webMercatorToWgs84.MathTransform.Transform(pointWebMercator.Coordinate);
        return _wgs84Factory.CreatePoint(coord);
    }

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (polygon)")]
    public Polygon TransformPolygonWgs84ToWebMercator()
    {
        var coords = TransformCoordinates(_polygonWgs84.Coordinates, _wgs84ToWebMercator);
        var ring = _webMercatorFactory.CreateLinearRing(coords);
        return _webMercatorFactory.CreatePolygon(ring);
    }

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (line, 100 points)")]
    public LineString TransformLineStringWgs84ToWebMercator()
    {
        var coords = TransformCoordinates(_lineStringWgs84.Coordinates, _wgs84ToWebMercator);
        return _webMercatorFactory.CreateLineString(coords);
    }

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (100 points)")]
    public List<Point> Transform100PointsWgs84ToWebMercator()
    {
        var results = new List<Point>(_points100Wgs84.Count);
        foreach (var point in _points100Wgs84)
        {
            var coord = _wgs84ToWebMercator.MathTransform.Transform(point.Coordinate);
            results.Add(_webMercatorFactory.CreatePoint(coord));
        }
        return results;
    }

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (1,000 points)")]
    public List<Point> Transform1000PointsWgs84ToWebMercator()
    {
        var results = new List<Point>(_points1000Wgs84.Count);
        foreach (var point in _points1000Wgs84)
        {
            var coord = _wgs84ToWebMercator.MathTransform.Transform(point.Coordinate);
            results.Add(_webMercatorFactory.CreatePoint(coord));
        }
        return results;
    }

    [Benchmark(Description = "CRS: WGS84 -> Web Mercator (100 polygons)")]
    public List<Polygon> Transform100PolygonsWgs84ToWebMercator()
    {
        var results = new List<Polygon>(_polygons100Wgs84.Count);
        foreach (var polygon in _polygons100Wgs84)
        {
            var coords = TransformCoordinates(polygon.Coordinates, _wgs84ToWebMercator);
            var ring = _webMercatorFactory.CreateLinearRing(coords);
            results.Add(_webMercatorFactory.CreatePolygon(ring));
        }
        return results;
    }

    // =====================================================
    // WGS84 to UTM Transformations
    // =====================================================

    [Benchmark(Description = "CRS: WGS84 -> UTM 10N (single point)")]
    public Point TransformPointWgs84ToUtm10N()
    {
        var coord = _wgs84ToUtm10N.MathTransform.Transform(_pointWgs84.Coordinate);
        return _wgs84Factory.CreatePoint(coord);
    }

    [Benchmark(Description = "CRS: WGS84 -> UTM 10N (polygon)")]
    public Polygon TransformPolygonWgs84ToUtm10N()
    {
        var coords = TransformCoordinates(_polygonWgs84.Coordinates, _wgs84ToUtm10N);
        var ring = _wgs84Factory.CreateLinearRing(coords);
        return _wgs84Factory.CreatePolygon(ring);
    }

    [Benchmark(Description = "CRS: WGS84 -> UTM 10N (100 points)")]
    public List<Point> Transform100PointsWgs84ToUtm10N()
    {
        var results = new List<Point>(_points100Wgs84.Count);
        foreach (var point in _points100Wgs84)
        {
            var coord = _wgs84ToUtm10N.MathTransform.Transform(point.Coordinate);
            results.Add(_wgs84Factory.CreatePoint(coord));
        }
        return results;
    }

    // =====================================================
    // Datum Transformations
    // =====================================================

    [Benchmark(Description = "CRS: WGS84 -> NAD83 (single point)")]
    public Point TransformPointWgs84ToNad83()
    {
        var coord = _wgs84ToNad83.MathTransform.Transform(_pointWgs84.Coordinate);
        return _wgs84Factory.CreatePoint(coord);
    }

    [Benchmark(Description = "CRS: WGS84 -> NAD83 (100 points)")]
    public List<Point> Transform100PointsWgs84ToNad83()
    {
        var results = new List<Point>(_points100Wgs84.Count);
        foreach (var point in _points100Wgs84)
        {
            var coord = _wgs84ToNad83.MathTransform.Transform(point.Coordinate);
            results.Add(_wgs84Factory.CreatePoint(coord));
        }
        return results;
    }

    // =====================================================
    // State Plane Transformations
    // =====================================================

    [Benchmark(Description = "CRS: WGS84 -> State Plane Oregon North (point)")]
    public Point TransformPointWgs84ToStatePlane()
    {
        var coord = _wgs84ToStatePlane.MathTransform.Transform(_pointWgs84.Coordinate);
        return _wgs84Factory.CreatePoint(coord);
    }

    [Benchmark(Description = "CRS: WGS84 -> State Plane Oregon North (polygon)")]
    public Polygon TransformPolygonWgs84ToStatePlane()
    {
        var coords = TransformCoordinates(_polygonWgs84.Coordinates, _wgs84ToStatePlane);
        var ring = _wgs84Factory.CreateLinearRing(coords);
        return _wgs84Factory.CreatePolygon(ring);
    }

    [Benchmark(Description = "CRS: WGS84 -> State Plane Oregon North (100 points)")]
    public List<Point> Transform100PointsWgs84ToStatePlane()
    {
        var results = new List<Point>(_points100Wgs84.Count);
        foreach (var point in _points100Wgs84)
        {
            var coord = _wgs84ToStatePlane.MathTransform.Transform(point.Coordinate);
            results.Add(_wgs84Factory.CreatePoint(coord));
        }
        return results;
    }

    // =====================================================
    // Batch Transformation Optimizations
    // =====================================================

    [Benchmark(Description = "CRS: Batch transform (sequential, 100 points)")]
    public List<Coordinate> BatchTransformSequential()
    {
        var results = new List<Coordinate>(_points100Wgs84.Count);
        foreach (var point in _points100Wgs84)
        {
            results.Add(_wgs84ToWebMercator.MathTransform.Transform(point.Coordinate));
        }
        return results;
    }

    [Benchmark(Description = "CRS: Batch transform (array, 100 points)")]
    public Coordinate[] BatchTransformArray()
    {
        var coords = _points100Wgs84.Select(p => p.Coordinate).ToArray();
        var results = new Coordinate[coords.Length];

        for (int i = 0; i < coords.Length; i++)
        {
            results[i] = _wgs84ToWebMercator.MathTransform.Transform(coords[i]);
        }

        return results;
    }

    // =====================================================
    // Coordinate System Definition Operations
    // =====================================================

    [Benchmark(Description = "CRS: Create transformation (WGS84 -> Web Mercator)")]
    public ICoordinateTransformation CreateTransformationWgs84ToWebMercator()
    {
        return _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _webMercatorCs);
    }

    [Benchmark(Description = "CRS: Create transformation (WGS84 -> UTM 10N)")]
    public ICoordinateTransformation CreateTransformationWgs84ToUtm()
    {
        return _ctFactory.CreateFromCoordinateSystems(_wgs84Cs, _utm10NCs);
    }

    [Benchmark(Description = "CRS: Parse EPSG code")]
    public int ParseEpsgCode()
    {
        var epsgString = "EPSG:4326";
        return int.Parse(epsgString.Split(':')[1]);
    }

    [Benchmark(Description = "CRS: Format WKT coordinate system")]
    public string FormatCoordinateSystemWkt()
    {
        return _wgs84Cs.WKT;
    }

    // =====================================================
    // Bounding Box Transformations
    // =====================================================

    [Benchmark(Description = "CRS: Transform bbox (WGS84 -> Web Mercator)")]
    public (double minX, double minY, double maxX, double maxY) TransformBboxWgs84ToWebMercator()
    {
        var minPoint = new Coordinate(-122.7, 45.5);
        var maxPoint = new Coordinate(-122.6, 45.6);

        var transformedMin = _wgs84ToWebMercator.MathTransform.Transform(minPoint);
        var transformedMax = _wgs84ToWebMercator.MathTransform.Transform(maxPoint);

        return (transformedMin.X, transformedMin.Y, transformedMax.X, transformedMax.Y);
    }

    [Benchmark(Description = "CRS: Transform bbox corners (4 points)")]
    public List<Coordinate> TransformBboxCorners()
    {
        var corners = new[]
        {
            new Coordinate(-122.7, 45.5),  // SW
            new Coordinate(-122.6, 45.5),  // SE
            new Coordinate(-122.6, 45.6),  // NE
            new Coordinate(-122.7, 45.6)   // NW
        };

        var results = new List<Coordinate>(4);
        foreach (var corner in corners)
        {
            results.Add(_wgs84ToWebMercator.MathTransform.Transform(corner));
        }

        return results;
    }

    // =====================================================
    // Distance Calculations in Different CRS
    // =====================================================

    [Benchmark(Description = "CRS: Distance calculation (WGS84)")]
    public double DistanceWgs84()
    {
        var point1 = _wgs84Factory.CreatePoint(new Coordinate(-122.6765, 45.5231));
        var point2 = _wgs84Factory.CreatePoint(new Coordinate(-122.6865, 45.5331));
        return point1.Distance(point2);
    }

    [Benchmark(Description = "CRS: Distance calculation (Web Mercator)")]
    public double DistanceWebMercator()
    {
        var point1Wgs84 = _wgs84Factory.CreatePoint(new Coordinate(-122.6765, 45.5231));
        var point2Wgs84 = _wgs84Factory.CreatePoint(new Coordinate(-122.6865, 45.5331));

        var coord1 = _wgs84ToWebMercator.MathTransform.Transform(point1Wgs84.Coordinate);
        var coord2 = _wgs84ToWebMercator.MathTransform.Transform(point2Wgs84.Coordinate);

        var point1WebMercator = _webMercatorFactory.CreatePoint(coord1);
        var point2WebMercator = _webMercatorFactory.CreatePoint(coord2);

        return point1WebMercator.Distance(point2WebMercator);
    }

    [Benchmark(Description = "CRS: Distance calculation (UTM 10N)")]
    public double DistanceUtm10N()
    {
        var point1Wgs84 = _wgs84Factory.CreatePoint(new Coordinate(-122.6765, 45.5231));
        var point2Wgs84 = _wgs84Factory.CreatePoint(new Coordinate(-122.6865, 45.5331));

        var coord1 = _wgs84ToUtm10N.MathTransform.Transform(point1Wgs84.Coordinate);
        var coord2 = _wgs84ToUtm10N.MathTransform.Transform(point2Wgs84.Coordinate);

        var point1Utm = _wgs84Factory.CreatePoint(coord1);
        var point2Utm = _wgs84Factory.CreatePoint(coord2);

        return point1Utm.Distance(point2Utm);
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private Coordinate[] TransformCoordinates(Coordinate[] coords, ICoordinateTransformation transform)
    {
        var results = new Coordinate[coords.Length];
        for (int i = 0; i < coords.Length; i++)
        {
            results[i] = transform.MathTransform.Transform(coords[i]);
        }
        return results;
    }

    private Polygon CreatePolygonWgs84(double baseLon, double baseLat, double size)
    {
        var coords = new[]
        {
            new Coordinate(baseLon, baseLat),
            new Coordinate(baseLon + size, baseLat),
            new Coordinate(baseLon + size, baseLat + size),
            new Coordinate(baseLon, baseLat + size),
            new Coordinate(baseLon, baseLat)
        };

        var ring = _wgs84Factory.CreateLinearRing(coords);
        return _wgs84Factory.CreatePolygon(ring);
    }

    private List<Point> GeneratePoints(int count)
    {
        var points = new List<Point>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var lon = -122.7 + (random.NextDouble() * 0.2); // Portland area
            var lat = 45.5 + (random.NextDouble() * 0.2);
            points.Add(_wgs84Factory.CreatePoint(new Coordinate(lon, lat)));
        }

        return points;
    }

    private List<Polygon> GeneratePolygons(int count)
    {
        var polygons = new List<Polygon>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var baseLon = -122.7 + (random.NextDouble() * 0.2);
            var baseLat = 45.5 + (random.NextDouble() * 0.2);
            var size = 0.001 + (random.NextDouble() * 0.005);
            polygons.Add(CreatePolygonWgs84(baseLon, baseLat, size));
        }

        return polygons;
    }

    private ProjectedCoordinateSystem CreateStatePlaneOregonNorth()
    {
        // State Plane Oregon North (NAD83) - EPSG:2991
        // Lambert Conformal Conic projection
        var parameters = new List<ProjectionParameter>
        {
            new ProjectionParameter("latitude_of_origin", 43.66666666666666),
            new ProjectionParameter("central_meridian", -120.5),
            new ProjectionParameter("standard_parallel_1", 44.33333333333334),
            new ProjectionParameter("standard_parallel_2", 46.0),
            new ProjectionParameter("false_easting", 2500000.0),
            new ProjectionParameter("false_northing", 0.0)
        };

        var projection = new Projection(
            "Lambert_Conformal_Conic_2SP",
            parameters,
            "Lambert_Conformal_Conic_2SP",
            "EPSG",
            2991,
            "",
            "",
            ""
        );

        return new ProjectedCoordinateSystem(
            HorizontalDatum.NorthAmericanDatum1983,
            _nad83Cs,
            LinearUnit.Metre,
            projection,
            new List<AxisInfo>
            {
                new AxisInfo("X", AxisOrientationEnum.East),
                new AxisInfo("Y", AxisOrientationEnum.North)
            },
            "NAD83 / Oregon North",
            "EPSG",
            2991,
            "",
            "",
            ""
        );
    }
}
