using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Performance benchmarks for export operations across all formats and geometry types.
/// Tracks performance regressions to ensure Honua remains fast at scale.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
public class ExportBenchmarks
{
    private LayerDefinition _layer = null!;
    private List<FeatureRecord> _smallDataset = null!;  // 100 features
    private List<FeatureRecord> _mediumDataset = null!; // 1,000 features
    private List<FeatureRecord> _largeDataset = null!;  // 10,000 features
    private GeometryFactory _geometryFactory = null!;
    private GeoJsonWriter _geoJsonWriter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _geoJsonWriter = new GeoJsonWriter();

        _layer = new LayerDefinition
        {
            Id = "benchmark-layer",
            ServiceId = "benchmark-service",
            Title = "Benchmark Layer",
            GeometryType = "Polygon",
            IdField = "feature_id",
            GeometryField = "geom",
            Fields = new[]
            {
                new FieldDefinition { Name = "feature_id", DataType = "int64" },
                new FieldDefinition { Name = "name", DataType = "string" },
                new FieldDefinition { Name = "category", DataType = "string" },
                new FieldDefinition { Name = "area_sqm", DataType = "double" },
                new FieldDefinition { Name = "created_at", DataType = "datetime" }
            }
        };

        _smallDataset = GenerateDataset(100);
        _mediumDataset = GenerateDataset(1000);
        _largeDataset = GenerateDataset(10000);
    }

    private List<FeatureRecord> GenerateDataset(int featureCount)
    {
        var records = new List<FeatureRecord>(featureCount);
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < featureCount; i++)
        {
            // Generate realistic polygon (property boundary)
            var baseLon = -122.4 + (random.NextDouble() * 0.2);
            var baseLat = 45.5 + (random.NextDouble() * 0.2);
            var size = 0.001; // ~100m parcels

            var coords = new[]
            {
                new Coordinate(baseLon, baseLat),
                new Coordinate(baseLon + size, baseLat),
                new Coordinate(baseLon + size, baseLat + size),
                new Coordinate(baseLon, baseLat + size),
                new Coordinate(baseLon, baseLat) // Close ring
            };

            var ring = _geometryFactory.CreateLinearRing(coords);
            var polygon = _geometryFactory.CreatePolygon(ring);
            var geoJson = _geoJsonWriter.Write(polygon);

            var attributes = new Dictionary<string, object?>
            {
                ["feature_id"] = i + 1,
                ["name"] = $"Property {i + 1}",
                ["category"] = i % 3 == 0 ? "Residential" : i % 3 == 1 ? "Commercial" : "Industrial",
                ["area_sqm"] = 10000.0 + (random.NextDouble() * 5000.0),
                ["created_at"] = new DateTime(2024, 1, 1).AddDays(i),
                ["geom"] = JsonNode.Parse(geoJson)
            };

            records.Add(new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes)));
        }

        return records;
    }

    private static async IAsyncEnumerable<FeatureRecord> ToAsyncEnumerable(List<FeatureRecord> records)
    {
        foreach (var record in records)
        {
            yield return record;
            await Task.CompletedTask;
        }
    }

    // =====================================================
    // CSV Export Benchmarks
    // =====================================================

    [Benchmark(Description = "CSV (WKT) - 100 features")]
    public async Task<ExportResult> CsvWkt_Small()
    {
        var exporter = new CsvExporter(new CsvExportOptions { GeometryFormat = "wkt" });
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, ToAsyncEnumerable(_smallDataset));
    }

    [Benchmark(Description = "CSV (WKT) - 1,000 features")]
    public async Task<ExportResult> CsvWkt_Medium()
    {
        var exporter = new CsvExporter(new CsvExportOptions { GeometryFormat = "wkt" });
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, ToAsyncEnumerable(_mediumDataset));
    }

    [Benchmark(Description = "CSV (WKT) - 10,000 features")]
    public async Task<ExportResult> CsvWkt_Large()
    {
        var exporter = new CsvExporter(new CsvExportOptions { GeometryFormat = "wkt" });
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, ToAsyncEnumerable(_largeDataset));
    }

    [Benchmark(Description = "CSV (GeoJSON) - 1,000 features")]
    public async Task<ExportResult> CsvGeoJson_Medium()
    {
        var exporter = new CsvExporter(new CsvExportOptions { GeometryFormat = "geojson" });
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, ToAsyncEnumerable(_mediumDataset));
    }

    // =====================================================
    // Shapefile Export Benchmarks
    // =====================================================

    [Benchmark(Description = "Shapefile - 100 features")]
    public async Task<ExportResult> Shapefile_Small()
    {
        var exporter = new ShapefileExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_smallDataset));
    }

    [Benchmark(Description = "Shapefile - 1,000 features")]
    public async Task<ExportResult> Shapefile_Medium()
    {
        var exporter = new ShapefileExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_mediumDataset));
    }

    [Benchmark(Description = "Shapefile - 10,000 features")]
    public async Task<ExportResult> Shapefile_Large()
    {
        var exporter = new ShapefileExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_largeDataset));
    }

    [Benchmark(Description = "Shapefile with CRS Transform - 1,000 features")]
    public async Task<ExportResult> Shapefile_CrsTransform_Medium()
    {
        var exporter = new ShapefileExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        // Transform to Web Mercator (common operation)
        return await exporter.ExportAsync(_layer, query, "EPSG:3857", ToAsyncEnumerable(_mediumDataset));
    }

    // =====================================================
    // GeoPackage Export Benchmarks
    // =====================================================

    [Benchmark(Description = "GeoPackage - 100 features")]
    public async Task<ExportResult> GeoPackage_Small()
    {
        var exporter = new GeoPackageExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_smallDataset));
    }

    [Benchmark(Description = "GeoPackage - 1,000 features")]
    public async Task<ExportResult> GeoPackage_Medium()
    {
        var exporter = new GeoPackageExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_mediumDataset));
    }

    [Benchmark(Description = "GeoPackage - 10,000 features")]
    public async Task<ExportResult> GeoPackage_Large()
    {
        var exporter = new GeoPackageExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:4326", ToAsyncEnumerable(_largeDataset));
    }

    [Benchmark(Description = "GeoPackage with CRS Transform - 1,000 features")]
    public async Task<ExportResult> GeoPackage_CrsTransform_Medium()
    {
        var exporter = new GeoPackageExporter();
        var query = new FeatureQuery(Crs: "EPSG:4326");
        return await exporter.ExportAsync(_layer, query, "EPSG:3857", ToAsyncEnumerable(_mediumDataset));
    }
}

/// <summary>
/// Baseline benchmarks for fundamental geospatial operations.
/// Used to identify bottlenecks in core geometry processing.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
public class GeometryProcessingBenchmarks
{
    private GeometryFactory _geometryFactory = null!;
    private WKTWriter _wktWriter = null!;
    private GeoJsonWriter _geoJsonWriter = null!;
    private Polygon _testPolygon = null!;
    private List<Polygon> _polygons = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _wktWriter = new WKTWriter();
        _geoJsonWriter = new GeoJsonWriter();

        // Create test polygon
        var coords = new[]
        {
            new Coordinate(-122.5, 45.5),
            new Coordinate(-122.3, 45.5),
            new Coordinate(-122.3, 45.7),
            new Coordinate(-122.5, 45.7),
            new Coordinate(-122.5, 45.5)
        };

        var ring = _geometryFactory.CreateLinearRing(coords);
        _testPolygon = _geometryFactory.CreatePolygon(ring);

        // Create batch of polygons
        _polygons = new List<Polygon>(1000);
        for (int i = 0; i < 1000; i++)
        {
            var baseLon = -122.5 + (i * 0.001);
            var baseLat = 45.5 + (i * 0.001);
            var polyCoords = new[]
            {
                new Coordinate(baseLon, baseLat),
                new Coordinate(baseLon + 0.001, baseLat),
                new Coordinate(baseLon + 0.001, baseLat + 0.001),
                new Coordinate(baseLon, baseLat + 0.001),
                new Coordinate(baseLon, baseLat)
            };
            var polyRing = _geometryFactory.CreateLinearRing(polyCoords);
            _polygons.Add(_geometryFactory.CreatePolygon(polyRing));
        }
    }

    [Benchmark(Description = "WKT serialization (single polygon)")]
    public string WktSerialization()
    {
        return _wktWriter.Write(_testPolygon);
    }

    [Benchmark(Description = "GeoJSON serialization (single polygon)")]
    public string GeoJsonSerialization()
    {
        return _geoJsonWriter.Write(_testPolygon);
    }

    [Benchmark(Description = "WKT serialization (1,000 polygons)")]
    public List<string> WktSerializationBatch()
    {
        var results = new List<string>(_polygons.Count);
        foreach (var polygon in _polygons)
        {
            results.Add(_wktWriter.Write(polygon));
        }
        return results;
    }

    [Benchmark(Description = "GeoJSON serialization (1,000 polygons)")]
    public List<string> GeoJsonSerializationBatch()
    {
        var results = new List<string>(_polygons.Count);
        foreach (var polygon in _polygons)
        {
            results.Add(_geoJsonWriter.Write(polygon));
        }
        return results;
    }
}
