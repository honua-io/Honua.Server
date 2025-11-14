// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Benchmarks.Helpers;
using System.Text;
using Newtonsoft.Json;

namespace Honua.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for GeoJSON serialization and deserialization performance.
/// Tests different geometry types, collection sizes, and serialization methods.
/// </summary>
[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class GeoJsonSerializationBenchmarks
{
    private GeoJsonReader _geoJsonReader = null!;
    private GeoJsonWriter _geoJsonWriter = null!;
    private JsonSerializerSettings _newtonsoftSettings = null!;

    private Point _testPoint = null!;
    private LineString _testLineString = null!;
    private Polygon _testPolygon = null!;
    private MultiPolygon _testMultiPolygon = null!;
    private GeometryCollection _testGeometryCollection = null!;

    private string _pointGeoJson = null!;
    private string _lineStringGeoJson = null!;
    private string _polygonGeoJson = null!;
    private string _multiPolygonGeoJson = null!;
    private string _featureCollectionGeoJson = null!;

    private List<(Geometry Geometry, Dictionary<string, object> Properties)> _featureCollection = null!;

    [Params(1, 100, 1000)]
    public int FeatureCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        // GeoJsonReader parameterless constructor uses default factory
        _geoJsonReader = new GeoJsonReader();
        _geoJsonWriter = new GeoJsonWriter();

        _newtonsoftSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        // Generate test geometries
        _testPoint = GeometryDataGenerator.GeneratePoint();
        _testLineString = GeometryDataGenerator.GenerateLineString(50);
        _testPolygon = GeometryDataGenerator.GeneratePolygon(100);
        _testMultiPolygon = GeometryDataGenerator.GenerateMultiPolygon(5, 20);

        var geometries = new Geometry[]
        {
            _testPoint,
            _testLineString,
            _testPolygon
        };
        _testGeometryCollection = factory.CreateGeometryCollection(geometries);

        // Pre-serialize for deserialization benchmarks
        _pointGeoJson = _geoJsonWriter.Write(_testPoint);
        _lineStringGeoJson = _geoJsonWriter.Write(_testLineString);
        _polygonGeoJson = _geoJsonWriter.Write(_testPolygon);
        _multiPolygonGeoJson = _geoJsonWriter.Write(_testMultiPolygon);

        // Generate feature collection
        _featureCollection = new List<(Geometry, Dictionary<string, object>)>(FeatureCount);
        for (int i = 0; i < FeatureCount; i++)
        {
            _featureCollection.Add(GeometryDataGenerator.GenerateFeature(i));
        }

        // Create a complete GeoJSON FeatureCollection
        _featureCollectionGeoJson = CreateFeatureCollectionGeoJson(_featureCollection);
    }

    // ========== SERIALIZATION BENCHMARKS ==========

    [Benchmark]
    public string Serialize_Point()
    {
        return _geoJsonWriter.Write(_testPoint);
    }

    [Benchmark]
    public string Serialize_LineString()
    {
        return _geoJsonWriter.Write(_testLineString);
    }

    [Benchmark]
    public string Serialize_Polygon()
    {
        return _geoJsonWriter.Write(_testPolygon);
    }

    [Benchmark]
    public string Serialize_MultiPolygon()
    {
        return _geoJsonWriter.Write(_testMultiPolygon);
    }

    [Benchmark]
    public string Serialize_GeometryCollection()
    {
        return _geoJsonWriter.Write(_testGeometryCollection);
    }

    // ========== DESERIALIZATION BENCHMARKS ==========

    [Benchmark]
    public Geometry Deserialize_Point()
    {
        return _geoJsonReader.Read<Geometry>(_pointGeoJson);
    }

    [Benchmark]
    public Geometry Deserialize_LineString()
    {
        return _geoJsonReader.Read<Geometry>(_lineStringGeoJson);
    }

    [Benchmark]
    public Geometry Deserialize_Polygon()
    {
        return _geoJsonReader.Read<Geometry>(_polygonGeoJson);
    }

    [Benchmark]
    public Geometry Deserialize_MultiPolygon()
    {
        return _geoJsonReader.Read<Geometry>(_multiPolygonGeoJson);
    }

    // ========== FEATURE COLLECTION BENCHMARKS ==========

    [Benchmark]
    public string Serialize_FeatureCollection()
    {
        return CreateFeatureCollectionGeoJson(_featureCollection);
    }

    [Benchmark]
    public int Deserialize_FeatureCollection()
    {
        return ParseFeatureCollectionGeoJson(_featureCollectionGeoJson);
    }

    // ========== ROUND-TRIP BENCHMARKS ==========

    [Benchmark]
    public Geometry RoundTrip_Point()
    {
        var json = _geoJsonWriter.Write(_testPoint);
        return _geoJsonReader.Read<Geometry>(json);
    }

    [Benchmark]
    public Geometry RoundTrip_Polygon()
    {
        var json = _geoJsonWriter.Write(_testPolygon);
        return _geoJsonReader.Read<Geometry>(json);
    }

    [Benchmark]
    public Geometry RoundTrip_MultiPolygon()
    {
        var json = _geoJsonWriter.Write(_testMultiPolygon);
        return _geoJsonReader.Read<Geometry>(json);
    }

    // ========== BULK OPERATIONS ==========

    [Benchmark]
    public List<string> BulkSerialize_Features()
    {
        var results = new List<string>(_featureCollection.Count);
        foreach (var (geometry, properties) in _featureCollection)
        {
            var geoJson = CreateFeatureGeoJson(geometry, properties);
            results.Add(geoJson);
        }
        return results;
    }

    [Benchmark]
    public List<Geometry> BulkDeserialize_Features()
    {
        var results = new List<Geometry>(_featureCollection.Count);
        foreach (var (geometry, _) in _featureCollection)
        {
            var geoJson = _geoJsonWriter.Write(geometry);
            var deserializedGeometry = _geoJsonReader.Read<Geometry>(geoJson);
            results.Add(deserializedGeometry);
        }
        return results;
    }

    // ========== STRING BUILDER VS CONCATENATION ==========

    [Benchmark]
    public string FeatureCollection_StringBuilder()
    {
        return CreateFeatureCollectionGeoJson_StringBuilder(_featureCollection);
    }

    [Benchmark]
    public string FeatureCollection_StringConcat()
    {
        return CreateFeatureCollectionGeoJson_StringConcat(_featureCollection);
    }

    // ========== HELPER METHODS ==========

    private string CreateFeatureGeoJson(Geometry geometry, Dictionary<string, object> properties)
    {
        var geometryJson = _geoJsonWriter.Write(geometry);
        var propertiesJson = JsonConvert.SerializeObject(properties, _newtonsoftSettings);

        return $@"{{
  ""type"": ""Feature"",
  ""geometry"": {geometryJson},
  ""properties"": {propertiesJson}
}}";
    }

    private string CreateFeatureCollectionGeoJson(List<(Geometry Geometry, Dictionary<string, object> Properties)> features)
    {
        var sb = new StringBuilder();
        sb.Append(@"{""type"":""FeatureCollection"",""features"":[");

        for (int i = 0; i < features.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (geometry, properties) = features[i];
            var geometryJson = _geoJsonWriter.Write(geometry);
            var propertiesJson = JsonConvert.SerializeObject(properties, _newtonsoftSettings);
            sb.Append($@"{{""type"":""Feature"",""geometry"":{geometryJson},""properties"":{propertiesJson}}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string CreateFeatureCollectionGeoJson_StringBuilder(List<(Geometry Geometry, Dictionary<string, object> Properties)> features)
    {
        var sb = new StringBuilder(features.Count * 500); // Pre-allocate
        sb.Append(@"{""type"":""FeatureCollection"",""features"":[");

        for (int i = 0; i < features.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var (geometry, properties) = features[i];
            var geometryJson = _geoJsonWriter.Write(geometry);
            var propertiesJson = JsonConvert.SerializeObject(properties, _newtonsoftSettings);
            sb.Append($@"{{""type"":""Feature"",""geometry"":{geometryJson},""properties"":{propertiesJson}}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    private string CreateFeatureCollectionGeoJson_StringConcat(List<(Geometry Geometry, Dictionary<string, object> Properties)> features)
    {
        var result = @"{""type"":""FeatureCollection"",""features"":[";

        for (int i = 0; i < features.Count; i++)
        {
            if (i > 0) result += ',';
            var (geometry, properties) = features[i];
            var geometryJson = _geoJsonWriter.Write(geometry);
            var propertiesJson = JsonConvert.SerializeObject(properties, _newtonsoftSettings);
            result += $@"{{""type"":""Feature"",""geometry"":{geometryJson},""properties"":{propertiesJson}}}";
        }

        result += "]}";
        return result;
    }

    private int ParseFeatureCollectionGeoJson(string geoJson)
    {
        // Simple parsing to count features (not full deserialization)
        var featureCount = 0;
        var index = 0;
        while ((index = geoJson.IndexOf(@"""type"":""Feature""", index, StringComparison.Ordinal)) != -1)
        {
            featureCount++;
            index++;
        }
        return featureCount;
    }
}
