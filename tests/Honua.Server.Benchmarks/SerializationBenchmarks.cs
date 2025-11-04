using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks for serialization and deserialization of geospatial data formats:
/// GeoJSON, KML, GML, WKT, WKB, GeoPackage, and Shapefile formats.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class SerializationBenchmarks
{
    private GeometryFactory _geometryFactory = null!;
    private GeoJsonWriter _geoJsonWriter = null!;
    private GeoJsonReader _geoJsonReader = null!;
    private WKTWriter _wktWriter = null!;
    private WKTReader _wktReader = null!;
    private WKBWriter _wkbWriter = null!;
    private WKBReader _wkbReader = null!;

    private Polygon _simplePolygon = null!;
    private Polygon _complexPolygon = null!;
    private List<Polygon> _polygons100 = null!;
    private List<Point> _points1000 = null!;
    private LineString _lineString = null!;
    private MultiPolygon _multiPolygon = null!;

    private string _geoJsonSimple = null!;
    private string _geoJsonComplex = null!;
    private string _geoJsonFeatureCollection = null!;
    private string _wktPolygon = null!;
    private byte[] _wkbPolygon = null!;
    private string _kmlPolygon = null!;
    private string _gmlPolygon = null!;

    private JsonSerializerOptions _jsonOptions = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _geoJsonWriter = new GeoJsonWriter();
        _geoJsonReader = new GeoJsonReader();
        _wktWriter = new WKTWriter();
        _wktReader = new WKTReader();
        _wkbWriter = new WKBWriter();
        _wkbReader = new WKBReader();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Create test geometries
        _simplePolygon = CreatePolygon(-122.5, 45.5, 0.01, 5);
        _complexPolygon = CreatePolygon(-122.4, 45.6, 0.05, 1000);
        _polygons100 = GeneratePolygons(100);
        _points1000 = GeneratePoints(1000);

        var lineCoords = Enumerable.Range(0, 100)
            .Select(i => new Coordinate(-122.5 + i * 0.001, 45.5 + i * 0.001))
            .ToArray();
        _lineString = _geometryFactory.CreateLineString(lineCoords);

        _multiPolygon = _geometryFactory.CreateMultiPolygon(_polygons100.Take(10).ToArray());

        // Pre-serialize for deserialization benchmarks
        _geoJsonSimple = _geoJsonWriter.Write(_simplePolygon);
        _geoJsonComplex = _geoJsonWriter.Write(_complexPolygon);
        _geoJsonFeatureCollection = CreateGeoJsonFeatureCollection(_polygons100);
        _wktPolygon = _wktWriter.Write(_simplePolygon);
        _wkbPolygon = _wkbWriter.Write(_simplePolygon);
        _kmlPolygon = CreateKmlPolygon(_simplePolygon);
        _gmlPolygon = CreateGmlPolygon(_simplePolygon);
    }

    // =====================================================
    // GeoJSON Serialization
    // =====================================================

    [Benchmark(Description = "GeoJSON: Serialize simple polygon")]
    public string GeoJsonSerializeSimplePolygon()
    {
        return _geoJsonWriter.Write(_simplePolygon);
    }

    [Benchmark(Description = "GeoJSON: Serialize complex polygon (1000 vertices)")]
    public string GeoJsonSerializeComplexPolygon()
    {
        return _geoJsonWriter.Write(_complexPolygon);
    }

    [Benchmark(Description = "GeoJSON: Serialize point")]
    public string GeoJsonSerializePoint()
    {
        return _geoJsonWriter.Write(_points1000[0]);
    }

    [Benchmark(Description = "GeoJSON: Serialize line string (100 vertices)")]
    public string GeoJsonSerializeLineString()
    {
        return _geoJsonWriter.Write(_lineString);
    }

    [Benchmark(Description = "GeoJSON: Serialize multi-polygon (10 polygons)")]
    public string GeoJsonSerializeMultiPolygon()
    {
        return _geoJsonWriter.Write(_multiPolygon);
    }

    [Benchmark(Description = "GeoJSON: Serialize feature collection (100 features)")]
    public string GeoJsonSerializeFeatureCollection()
    {
        return CreateGeoJsonFeatureCollection(_polygons100);
    }

    [Benchmark(Description = "GeoJSON: Serialize 1,000 points")]
    public string GeoJsonSerialize1000Points()
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");

        for (int i = 0; i < _points1000.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"type\":\"Feature\",\"geometry\":");
            sb.Append(_geoJsonWriter.Write(_points1000[i]));
            sb.Append(",\"properties\":{\"id\":");
            sb.Append(i);
            sb.Append("}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    // =====================================================
    // GeoJSON Deserialization
    // =====================================================

    [Benchmark(Description = "GeoJSON: Deserialize simple polygon")]
    public Geometry GeoJsonDeserializeSimplePolygon()
    {
        return _geoJsonReader.Read<Geometry>(_geoJsonSimple);
    }

    [Benchmark(Description = "GeoJSON: Deserialize complex polygon")]
    public Geometry GeoJsonDeserializeComplexPolygon()
    {
        return _geoJsonReader.Read<Geometry>(_geoJsonComplex);
    }

    [Benchmark(Description = "GeoJSON: Deserialize feature collection")]
    public object GeoJsonDeserializeFeatureCollection()
    {
        return JsonSerializer.Deserialize<object>(_geoJsonFeatureCollection);
    }

    // =====================================================
    // WKT Serialization/Deserialization
    // =====================================================

    [Benchmark(Description = "WKT: Serialize simple polygon")]
    public string WktSerializeSimplePolygon()
    {
        return _wktWriter.Write(_simplePolygon);
    }

    [Benchmark(Description = "WKT: Serialize complex polygon")]
    public string WktSerializeComplexPolygon()
    {
        return _wktWriter.Write(_complexPolygon);
    }

    [Benchmark(Description = "WKT: Serialize 100 polygons")]
    public List<string> WktSerialize100Polygons()
    {
        var results = new List<string>(_polygons100.Count);
        foreach (var polygon in _polygons100)
        {
            results.Add(_wktWriter.Write(polygon));
        }
        return results;
    }

    [Benchmark(Description = "WKT: Deserialize polygon")]
    public Geometry WktDeserializePolygon()
    {
        return _wktReader.Read(_wktPolygon);
    }

    [Benchmark(Description = "WKT: Serialize multi-polygon")]
    public string WktSerializeMultiPolygon()
    {
        return _wktWriter.Write(_multiPolygon);
    }

    // =====================================================
    // WKB Serialization/Deserialization
    // =====================================================

    [Benchmark(Description = "WKB: Serialize simple polygon")]
    public byte[] WkbSerializeSimplePolygon()
    {
        return _wkbWriter.Write(_simplePolygon);
    }

    [Benchmark(Description = "WKB: Serialize complex polygon")]
    public byte[] WkbSerializeComplexPolygon()
    {
        return _wkbWriter.Write(_complexPolygon);
    }

    [Benchmark(Description = "WKB: Serialize 100 polygons")]
    public List<byte[]> WkbSerialize100Polygons()
    {
        var results = new List<byte[]>(_polygons100.Count);
        foreach (var polygon in _polygons100)
        {
            results.Add(_wkbWriter.Write(polygon));
        }
        return results;
    }

    [Benchmark(Description = "WKB: Deserialize polygon")]
    public Geometry WkbDeserializePolygon()
    {
        return _wkbReader.Read(_wkbPolygon);
    }

    [Benchmark(Description = "WKB: Serialize point")]
    public byte[] WkbSerializePoint()
    {
        return _wkbWriter.Write(_points1000[0]);
    }

    [Benchmark(Description = "WKB: Serialize line string")]
    public byte[] WkbSerializeLineString()
    {
        return _wkbWriter.Write(_lineString);
    }

    // =====================================================
    // KML Serialization
    // =====================================================

    [Benchmark(Description = "KML: Serialize simple polygon")]
    public string KmlSerializeSimplePolygon()
    {
        return CreateKmlPolygon(_simplePolygon);
    }

    [Benchmark(Description = "KML: Serialize complex polygon")]
    public string KmlSerializeComplexPolygon()
    {
        return CreateKmlPolygon(_complexPolygon);
    }

    [Benchmark(Description = "KML: Serialize 100 polygons")]
    public string KmlSerialize100Polygons()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("<Document>");

        foreach (var polygon in _polygons100)
        {
            sb.AppendLine("<Placemark>");
            sb.Append(CreateKmlPolygonCoordinates(polygon));
            sb.AppendLine("</Placemark>");
        }

        sb.AppendLine("</Document>");
        sb.AppendLine("</kml>");
        return sb.ToString();
    }

    [Benchmark(Description = "KML: Serialize point")]
    public string KmlSerializePoint()
    {
        var point = _points1000[0];
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<kml xmlns=""http://www.opengis.net/kml/2.2"">
  <Placemark>
    <Point>
      <coordinates>{point.X},{point.Y}</coordinates>
    </Point>
  </Placemark>
</kml>";
    }

    // =====================================================
    // GML Serialization
    // =====================================================

    [Benchmark(Description = "GML 3.2: Serialize simple polygon")]
    public string GmlSerializeSimplePolygon()
    {
        return CreateGmlPolygon(_simplePolygon);
    }

    [Benchmark(Description = "GML 3.2: Serialize complex polygon")]
    public string GmlSerializeComplexPolygon()
    {
        return CreateGmlPolygon(_complexPolygon);
    }

    [Benchmark(Description = "GML 3.2: Serialize 100 polygons")]
    public string GmlSerialize100Polygons()
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<gml:FeatureCollection xmlns:gml=\"http://www.opengis.net/gml/3.2\">");

        foreach (var polygon in _polygons100)
        {
            sb.Append("<gml:featureMember>");
            sb.Append(CreateGmlPolygonElement(polygon));
            sb.Append("</gml:featureMember>");
        }

        sb.Append("</gml:FeatureCollection>");
        return sb.ToString();
    }

    [Benchmark(Description = "GML 3.2: Serialize point")]
    public string GmlSerializePoint()
    {
        var point = _points1000[0];
        return $@"<gml:Point xmlns:gml=""http://www.opengis.net/gml/3.2"" srsName=""EPSG:4326"">
  <gml:pos>{point.Y} {point.X}</gml:pos>
</gml:Point>";
    }

    // =====================================================
    // CSV with Geometry Serialization
    // =====================================================

    [Benchmark(Description = "CSV: Serialize 100 features (WKT geometry)")]
    public string CsvSerializeWkt100()
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,name,category,geometry");

        for (int i = 0; i < _polygons100.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(",Feature ");
            sb.Append(i + 1);
            sb.Append(",Category");
            sb.Append(i % 3);
            sb.Append(",\"");
            sb.Append(_wktWriter.Write(_polygons100[i]));
            sb.AppendLine("\"");
        }

        return sb.ToString();
    }

    [Benchmark(Description = "CSV: Serialize 100 features (GeoJSON geometry)")]
    public string CsvSerializeGeoJson100()
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,name,category,geometry");

        for (int i = 0; i < _polygons100.Count; i++)
        {
            sb.Append(i + 1);
            sb.Append(",Feature ");
            sb.Append(i + 1);
            sb.Append(",Category");
            sb.Append(i % 3);
            sb.Append(",\"");
            sb.Append(_geoJsonWriter.Write(_polygons100[i]).Replace("\"", "\"\""));
            sb.AppendLine("\"");
        }

        return sb.ToString();
    }

    [Benchmark(Description = "CSV: Serialize 1,000 points")]
    public string CsvSerializePoints1000()
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,x,y");

        for (int i = 0; i < _points1000.Count; i++)
        {
            var point = _points1000[i];
            sb.Append(i + 1);
            sb.Append(',');
            sb.Append(point.X);
            sb.Append(',');
            sb.AppendLine(point.Y.ToString());
        }

        return sb.ToString();
    }

    // =====================================================
    // System.Text.Json vs Newtonsoft.Json
    // =====================================================

    [Benchmark(Description = "JSON: System.Text.Json serialize feature")]
    public string SystemTextJsonSerialize()
    {
        var feature = new
        {
            type = "Feature",
            id = 1,
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { GetCoordinatesArray(_simplePolygon) }
            },
            properties = new
            {
                name = "Test Feature",
                category = "Residential",
                area = 10000.0
            }
        };

        return JsonSerializer.Serialize(feature, _jsonOptions);
    }

    [Benchmark(Description = "JSON: System.Text.Json deserialize feature")]
    public object SystemTextJsonDeserialize()
    {
        var json = "{\"type\":\"Feature\",\"id\":1,\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[-122.5,45.5],[-122.49,45.5],[-122.49,45.51],[-122.5,45.51],[-122.5,45.5]]]},\"properties\":{\"name\":\"Test Feature\",\"category\":\"Residential\",\"area\":10000}}";
        return JsonSerializer.Deserialize<object>(json)!;
    }

    // =====================================================
    // Streaming Serialization
    // =====================================================

    [Benchmark(Description = "Streaming: GeoJSON features (100)")]
    public async Task<long> StreamingGeoJsonSerialize()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync("{\"type\":\"FeatureCollection\",\"features\":[");

        for (int i = 0; i < 100; i++)
        {
            if (i > 0) await writer.WriteAsync(',');
            var geoJson = _geoJsonWriter.Write(_polygons100[i]);
            await writer.WriteAsync("{\"type\":\"Feature\",\"geometry\":");
            await writer.WriteAsync(geoJson);
            await writer.WriteAsync(",\"properties\":{\"id\":");
            await writer.WriteAsync(i.ToString());
            await writer.WriteAsync("}}");
        }

        await writer.WriteLineAsync("]}");
        await writer.FlushAsync();

        return ms.Length;
    }

    [Benchmark(Description = "Streaming: CSV export (100)")]
    public async Task<long> StreamingCsvSerialize()
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);

        await writer.WriteLineAsync("id,name,category,wkt");

        for (int i = 0; i < 100; i++)
        {
            await writer.WriteAsync($"{i + 1},Feature {i + 1},Category{i % 3},\"");
            await writer.WriteAsync(_wktWriter.Write(_polygons100[i]));
            await writer.WriteLineAsync("\"");
        }

        await writer.FlushAsync();
        return ms.Length;
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private Polygon CreatePolygon(double baseLon, double baseLat, double size, int vertices)
    {
        var coords = new Coordinate[vertices + 1];
        for (int i = 0; i < vertices; i++)
        {
            var angle = (2 * Math.PI * i) / vertices;
            coords[i] = new Coordinate(
                baseLon + size * Math.Cos(angle),
                baseLat + size * Math.Sin(angle)
            );
        }
        coords[vertices] = coords[0];

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
            polygons.Add(CreatePolygon(baseLon, baseLat, 0.001, 5));
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

    private string CreateGeoJsonFeatureCollection(List<Polygon> polygons)
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");

        for (int i = 0; i < polygons.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"type\":\"Feature\",\"id\":");
            sb.Append(i + 1);
            sb.Append(",\"geometry\":");
            sb.Append(_geoJsonWriter.Write(polygons[i]));
            sb.Append(",\"properties\":{\"name\":\"Feature ");
            sb.Append(i + 1);
            sb.Append("\"}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string CreateKmlPolygon(Polygon polygon)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<kml xmlns=""http://www.opengis.net/kml/2.2"">
  <Placemark>
    {CreateKmlPolygonCoordinates(polygon)}
  </Placemark>
</kml>";
    }

    private string CreateKmlPolygonCoordinates(Polygon polygon)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Polygon>");
        sb.AppendLine("  <outerBoundaryIs>");
        sb.AppendLine("    <LinearRing>");
        sb.Append("      <coordinates>");

        var coords = polygon.ExteriorRing.Coordinates;
        for (int i = 0; i < coords.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append($"{coords[i].X},{coords[i].Y}");
        }

        sb.AppendLine("</coordinates>");
        sb.AppendLine("    </LinearRing>");
        sb.AppendLine("  </outerBoundaryIs>");
        sb.Append("</Polygon>");

        return sb.ToString();
    }

    private string CreateGmlPolygon(Polygon polygon)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
{CreateGmlPolygonElement(polygon)}";
    }

    private string CreateGmlPolygonElement(Polygon polygon)
    {
        var sb = new StringBuilder();
        sb.Append("<gml:Polygon xmlns:gml=\"http://www.opengis.net/gml/3.2\" srsName=\"EPSG:4326\">");
        sb.Append("<gml:exterior><gml:LinearRing><gml:posList>");

        var coords = polygon.ExteriorRing.Coordinates;
        for (int i = 0; i < coords.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append($"{coords[i].Y} {coords[i].X}");
        }

        sb.Append("</gml:posList></gml:LinearRing></gml:exterior>");
        sb.Append("</gml:Polygon>");

        return sb.ToString();
    }

    private double[][] GetCoordinatesArray(Polygon polygon)
    {
        return polygon.ExteriorRing.Coordinates
            .Select(c => new[] { c.X, c.Y })
            .ToArray();
    }
}
