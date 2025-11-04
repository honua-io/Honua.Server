using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Serialization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Honua.Benchmarks;

/// <summary>
/// Benchmarks for GeoJSON serialization and geometry processing including WKT to GeoJSON conversion.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class GeoJsonSerializationBenchmarks
{
    private LayerDefinition _layer = null!;
    private FeatureQuery _query = null!;
    private FeatureRecord _smallRecord = null!;
    private FeatureRecord _mediumRecord = null!;
    private FeatureRecord _largeRecord = null!;
    private string _pointWkt = null!;
    private string _lineStringWkt = null!;
    private string _polygonWkt = null!;
    private string _complexPolygonWkt = null!;
    private string _pointGeoJson = null!;
    private string _polygonGeoJson = null!;
    private GeoJsonWriter _geoJsonWriter = null!;
    private WKTReader _wktReader = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup layer definition
        _layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            GeometryField = "geometry",
            IdField = "id",
            DisplayField = "name",
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "id",
                Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
            }
        };

        // Setup query
        _query = new FeatureQuery
        {
            Limit = 100
        };

        // Setup WKT geometries
        _pointWkt = "POINT(-122.4194 37.7749)";
        _lineStringWkt = "LINESTRING(-122.4194 37.7749, -122.4184 37.7759, -122.4174 37.7769)";
        _polygonWkt = "POLYGON((-122.4194 37.7749, -122.4184 37.7749, -122.4184 37.7739, -122.4194 37.7739, -122.4194 37.7749))";

        // Complex polygon with hole
        _complexPolygonWkt = "POLYGON((" +
            "-122.52 37.80, -122.35 37.80, -122.35 37.70, -122.52 37.70, -122.52 37.80), " +
            "(-122.48 37.77, -122.45 37.77, -122.45 37.74, -122.48 37.74, -122.48 37.77))";

        // Setup GeoJSON strings
        _pointGeoJson = "{\"type\":\"Point\",\"coordinates\":[-122.4194,37.7749]}";
        _polygonGeoJson = "{\"type\":\"Polygon\",\"coordinates\":[[[-122.4194,37.7749],[-122.4184,37.7749],[-122.4184,37.7739],[-122.4194,37.7739],[-122.4194,37.7749]]]}";

        // Setup NTS readers/writers
        _wktReader = new WKTReader();
        _geoJsonWriter = new GeoJsonWriter();

        // Setup feature records with different complexity levels
        _smallRecord = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["name"] = "Small Feature",
            ["geometry"] = _pointGeoJson,
            ["category"] = "test",
            ["value"] = 42.5
        });

        _mediumRecord = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 2,
            ["name"] = "Medium Feature",
            ["geometry"] = _polygonGeoJson,
            ["category"] = "test",
            ["description"] = "This is a medium-sized feature with more properties",
            ["value"] = 123.45,
            ["count"] = 100,
            ["active"] = true,
            ["created_date"] = DateTime.UtcNow,
            ["tags"] = "tag1,tag2,tag3"
        });

        _largeRecord = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 3,
            ["name"] = "Large Feature",
            ["geometry"] = _polygonGeoJson,
            ["category"] = "complex",
            ["description"] = "This is a large feature with many properties for benchmarking purposes. " +
                             "It contains extended text content and multiple data fields to simulate real-world usage.",
            ["value"] = 999.99,
            ["count"] = 1000,
            ["active"] = true,
            ["created_date"] = DateTime.UtcNow,
            ["modified_date"] = DateTime.UtcNow,
            ["tags"] = "tag1,tag2,tag3,tag4,tag5,tag6,tag7,tag8,tag9,tag10",
            ["status"] = "active",
            ["priority"] = "high",
            ["owner"] = "user@example.com",
            ["metadata_field_1"] = "value1",
            ["metadata_field_2"] = "value2",
            ["metadata_field_3"] = "value3",
            ["metadata_field_4"] = "value4",
            ["metadata_field_5"] = "value5",
            ["custom_data"] = "{\"nested\":\"json\",\"values\":[1,2,3]}"
        });
    }

    #region Feature Component Building Benchmarks

    [Benchmark]
    public FeatureComponents BuildComponents_SmallFeature()
    {
        return FeatureComponentBuilder.BuildComponents(_layer, _smallRecord, _query);
    }

    [Benchmark]
    public FeatureComponents BuildComponents_MediumFeature()
    {
        return FeatureComponentBuilder.BuildComponents(_layer, _mediumRecord, _query);
    }

    [Benchmark]
    public FeatureComponents BuildComponents_LargeFeature()
    {
        return FeatureComponentBuilder.BuildComponents(_layer, _largeRecord, _query);
    }

    #endregion

    #region WKT to GeoJSON Conversion Benchmarks

    [Benchmark]
    public string WktToGeoJson_Point()
    {
        var geometry = _wktReader.Read(_pointWkt);
        return _geoJsonWriter.Write(geometry);
    }

    [Benchmark]
    public string WktToGeoJson_LineString()
    {
        var geometry = _wktReader.Read(_lineStringWkt);
        return _geoJsonWriter.Write(geometry);
    }

    [Benchmark]
    public string WktToGeoJson_Polygon()
    {
        var geometry = _wktReader.Read(_polygonWkt);
        return _geoJsonWriter.Write(geometry);
    }

    [Benchmark]
    public string WktToGeoJson_ComplexPolygon()
    {
        var geometry = _wktReader.Read(_complexPolygonWkt);
        return _geoJsonWriter.Write(geometry);
    }

    #endregion

    #region GeoJSON Parsing Benchmarks

    [Benchmark]
    public JsonNode? ParseGeoJson_Point()
    {
        return JsonNode.Parse(_pointGeoJson);
    }

    [Benchmark]
    public JsonNode? ParseGeoJson_Polygon()
    {
        return JsonNode.Parse(_polygonGeoJson);
    }

    #endregion

    #region Combined WKT Read and Parse Benchmarks

    [Benchmark]
    public JsonNode? WktReadAndParse_Point()
    {
        var geometry = _wktReader.Read(_pointWkt);
        var geoJson = _geoJsonWriter.Write(geometry);
        return JsonNode.Parse(geoJson);
    }

    [Benchmark]
    public JsonNode? WktReadAndParse_Polygon()
    {
        var geometry = _wktReader.Read(_polygonWkt);
        var geoJson = _geoJsonWriter.Write(geometry);
        return JsonNode.Parse(geoJson);
    }

    [Benchmark]
    public JsonNode? WktReadAndParse_ComplexPolygon()
    {
        var geometry = _wktReader.Read(_complexPolygonWkt);
        var geoJson = _geoJsonWriter.Write(geometry);
        return JsonNode.Parse(geoJson);
    }

    #endregion

    #region Feature Record Processing with WKT Benchmarks

    [Benchmark]
    public FeatureComponents BuildComponents_WithWktPoint()
    {
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 1,
            ["name"] = "WKT Point Feature",
            ["geometry"] = _pointWkt,
            ["category"] = "test"
        });
        return FeatureComponentBuilder.BuildComponents(_layer, record, _query);
    }

    [Benchmark]
    public FeatureComponents BuildComponents_WithWktPolygon()
    {
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = 2,
            ["name"] = "WKT Polygon Feature",
            ["geometry"] = _polygonWkt,
            ["category"] = "test"
        });
        return FeatureComponentBuilder.BuildComponents(_layer, record, _query);
    }

    #endregion

    #region Batch Processing Benchmarks

    [Benchmark]
    public List<FeatureComponents> BuildComponents_Batch_10Features()
    {
        var results = new List<FeatureComponents>(10);
        for (int i = 0; i < 10; i++)
        {
            results.Add(FeatureComponentBuilder.BuildComponents(_layer, _mediumRecord, _query));
        }
        return results;
    }

    [Benchmark]
    public List<FeatureComponents> BuildComponents_Batch_100Features()
    {
        var results = new List<FeatureComponents>(100);
        for (int i = 0; i < 100; i++)
        {
            results.Add(FeatureComponentBuilder.BuildComponents(_layer, _mediumRecord, _query));
        }
        return results;
    }

    [Benchmark]
    public List<FeatureComponents> BuildComponents_Batch_1000Features()
    {
        var results = new List<FeatureComponents>(1000);
        for (int i = 0; i < 1000; i++)
        {
            results.Add(FeatureComponentBuilder.BuildComponents(_layer, _smallRecord, _query));
        }
        return results;
    }

    #endregion

    [GlobalCleanup]
    public void Cleanup()
    {
        // Cleanup resources if needed
    }
}
