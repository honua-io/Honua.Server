using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.VectorTiles;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Simplify;
using System.Text;
using System.Text.Json;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Vector tile benchmarks covering MVT encoding, geometry simplification, and serialization.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class VectorTileBenchmarks
{
    private GeometryFactory _geometryFactory = null!;
    private List<Polygon> _smallDataset = null!;   // 100 polygons
    private List<Polygon> _mediumDataset = null!;  // 1,000 polygons
    private List<Polygon> _largeDataset = null!;   // 10,000 polygons
    private List<Point> _pointDataset = null!;     // 1,000 points
    private GeoJsonWriter _geoJsonWriter = null!;
    private VectorTileProcessor _tileProcessor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _geoJsonWriter = new GeoJsonWriter();

        _tileProcessor = new VectorTileProcessor(new VectorTileOptions
        {
            Extent = 4096,
            Buffer = 64,
            MaxZoom = 20,
            MaxDataZoom = 14,
            EnableSimplification = true,
            EnableOverzooming = true,
            EnableFeatureReduction = true,
            EnableClustering = false,
            SimplificationTolerance = 1.0,
            MinFeatureArea = 4.0,
            ClusterMinZoom = 0,
            ClusterMaxZoom = 10
        });

        // Generate test datasets
        _smallDataset = GeneratePolygonDataset(100);
        _mediumDataset = GeneratePolygonDataset(1000);
        _largeDataset = GeneratePolygonDataset(10000);
        _pointDataset = GeneratePointDataset(1000);
    }

    // =====================================================
    // MVT Encoding Benchmarks
    // =====================================================

    [Benchmark(Description = "MVT: Encode 100 polygons")]
    public byte[] MvtEncode100Polygons()
    {
        return EncodeMvt(_smallDataset);
    }

    [Benchmark(Description = "MVT: Encode 1,000 polygons")]
    public byte[] MvtEncode1000Polygons()
    {
        return EncodeMvt(_mediumDataset);
    }

    [Benchmark(Description = "MVT: Encode 10,000 polygons")]
    public byte[] MvtEncode10000Polygons()
    {
        return EncodeMvt(_largeDataset);
    }

    [Benchmark(Description = "MVT: Encode 1,000 points")]
    public byte[] MvtEncode1000Points()
    {
        // Simulate MVT encoding for points
        var features = new List<Dictionary<string, object>>();

        foreach (var point in _pointDataset)
        {
            features.Add(new Dictionary<string, object>
            {
                ["geometry"] = point,
                ["properties"] = new Dictionary<string, object>
                {
                    ["id"] = point.UserData ?? 0
                }
            });
        }

        return EncodeToProtobuf(features);
    }

    // =====================================================
    // Geometry Simplification Benchmarks
    // =====================================================

    [Benchmark(Description = "Simplify: 1,000 polygons (tolerance 0.001)")]
    public List<Geometry> SimplifyPolygonsTolerance0001()
    {
        var simplified = new List<Geometry>(_mediumDataset.Count);
        var simplifier = new DouglasPeuckerSimplifier(_mediumDataset[0]);
        simplifier.DistanceTolerance = 0.001;

        foreach (var polygon in _mediumDataset)
        {
            simplifier.InputGeometry = polygon;
            simplified.Add(simplifier.GetResultGeometry());
        }

        return simplified;
    }

    [Benchmark(Description = "Simplify: 1,000 polygons (tolerance 0.01)")]
    public List<Geometry> SimplifyPolygonsTolerance001()
    {
        var simplified = new List<Geometry>(_mediumDataset.Count);
        var simplifier = new DouglasPeuckerSimplifier(_mediumDataset[0]);
        simplifier.DistanceTolerance = 0.01;

        foreach (var polygon in _mediumDataset)
        {
            simplifier.InputGeometry = polygon;
            simplified.Add(simplifier.GetResultGeometry());
        }

        return simplified;
    }

    [Benchmark(Description = "Simplify: TopologyPreservingSimplifier (1,000 polygons)")]
    public List<Geometry> SimplifyTopologyPreserving()
    {
        var simplified = new List<Geometry>(_mediumDataset.Count);

        foreach (var polygon in _mediumDataset)
        {
            simplified.Add(TopologyPreservingSimplifier.Simplify(polygon, 0.001));
        }

        return simplified;
    }

    // =====================================================
    // GeoJSON Serialization Benchmarks
    // =====================================================

    [Benchmark(Description = "GeoJSON: Serialize 100 polygons")]
    public string GeoJsonSerialize100Polygons()
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");

        for (int i = 0; i < _smallDataset.Count; i++)
        {
            if (i > 0) sb.Append(',');

            var geomJson = _geoJsonWriter.Write(_smallDataset[i]);
            sb.Append("{\"type\":\"Feature\",\"geometry\":");
            sb.Append(geomJson);
            sb.Append(",\"properties\":{\"id\":");
            sb.Append(i);
            sb.Append("}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    [Benchmark(Description = "GeoJSON: Serialize 1,000 polygons")]
    public string GeoJsonSerialize1000Polygons()
    {
        var sb = new StringBuilder();
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");

        for (int i = 0; i < _mediumDataset.Count; i++)
        {
            if (i > 0) sb.Append(',');

            var geomJson = _geoJsonWriter.Write(_mediumDataset[i]);
            sb.Append("{\"type\":\"Feature\",\"geometry\":");
            sb.Append(geomJson);
            sb.Append(",\"properties\":{\"id\":");
            sb.Append(i);
            sb.Append("}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }

    [Benchmark(Description = "GeoJSON: Parse 1,000 polygons")]
    public List<Geometry> GeoJsonParse1000Polygons()
    {
        var reader = new GeoJsonReader();
        var parsed = new List<Geometry>(_mediumDataset.Count);

        foreach (var polygon in _mediumDataset)
        {
            var json = _geoJsonWriter.Write(polygon);
            parsed.Add(reader.Read<Geometry>(json));
        }

        return parsed;
    }

    // =====================================================
    // Vector Tile Query Generation Benchmarks
    // =====================================================

    [Benchmark(Description = "VectorTile: Generate PostGIS MVT query (simple)")]
    public string GenerateSimpleMvtQuery()
    {
        return _tileProcessor.BuildPostgisMvtQuery(
            tableName: "public.parcels",
            geometryColumn: "geom",
            storageSrid: 4326,
            requestedZoom: 10,
            layerName: "parcels"
        );
    }

    [Benchmark(Description = "VectorTile: Generate PostGIS MVT query (with attributes)")]
    public string GenerateMvtQueryWithAttributes()
    {
        return _tileProcessor.BuildPostgisMvtQuery(
            tableName: "public.parcels",
            geometryColumn: "geom",
            storageSrid: 4326,
            requestedZoom: 10,
            layerName: "parcels",
            attributeColumns: new[] { "id", "name", "category", "area_sqm", "created_at" }
        );
    }

    [Benchmark(Description = "VectorTile: Generate clustering query")]
    public string GenerateClusteringQuery()
    {
        return _tileProcessor.BuildClusteringQuery(
            tableName: "public.points_of_interest",
            geometryColumn: "geom",
            storageSrid: 4326,
            zoom: 8,
            layerName: "poi",
            attributeColumns: new[] { "id", "name", "type" }
        );
    }

    // =====================================================
    // Tile Optimization Benchmarks
    // =====================================================

    [Benchmark(Description = "VectorTile: Calculate simplification tolerance (zoom 5)")]
    public double CalculateSimplificationToleranceZoom5()
    {
        return _tileProcessor.GetSimplificationTolerance(5);
    }

    [Benchmark(Description = "VectorTile: Calculate simplification tolerance (zoom 15)")]
    public double CalculateSimplificationToleranceZoom15()
    {
        return _tileProcessor.GetSimplificationTolerance(15);
    }

    [Benchmark(Description = "VectorTile: Calculate min feature area (zoom 5)")]
    public double CalculateMinFeatureAreaZoom5()
    {
        return _tileProcessor.GetMinFeatureArea(5);
    }

    [Benchmark(Description = "VectorTile: Determine overzooming")]
    public bool DetermineOverzooming()
    {
        return _tileProcessor.ShouldOverzoom(18, out var dataZoom);
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private List<Polygon> GeneratePolygonDataset(int count)
    {
        var polygons = new List<Polygon>(count);
        var random = new Random(42); // Fixed seed for reproducibility

        for (int i = 0; i < count; i++)
        {
            var baseLon = -122.5 + (random.NextDouble() * 0.5);
            var baseLat = 45.5 + (random.NextDouble() * 0.5);
            var size = 0.001 + (random.NextDouble() * 0.005);

            // Generate polygon with varying complexity
            var numPoints = 5 + random.Next(15); // 5-20 vertices
            var coords = new Coordinate[numPoints];

            for (int j = 0; j < numPoints - 1; j++)
            {
                var angle = (2 * Math.PI * j) / (numPoints - 1);
                var radius = size * (0.8 + random.NextDouble() * 0.4);
                coords[j] = new Coordinate(
                    baseLon + radius * Math.Cos(angle),
                    baseLat + radius * Math.Sin(angle)
                );
            }

            coords[numPoints - 1] = coords[0]; // Close the ring

            var ring = _geometryFactory.CreateLinearRing(coords);
            var polygon = _geometryFactory.CreatePolygon(ring);
            polygon.UserData = i; // Store ID
            polygons.Add(polygon);
        }

        return polygons;
    }

    private List<Point> GeneratePointDataset(int count)
    {
        var points = new List<Point>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var lon = -122.5 + (random.NextDouble() * 0.5);
            var lat = 45.5 + (random.NextDouble() * 0.5);
            var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
            point.UserData = i;
            points.Add(point);
        }

        return points;
    }

    private byte[] EncodeMvt(List<Polygon> polygons)
    {
        // Simplified MVT encoding simulation
        // In a real implementation, this would use Mapbox.Vector.Tile or similar
        var features = new List<Dictionary<string, object>>();

        foreach (var polygon in polygons)
        {
            features.Add(new Dictionary<string, object>
            {
                ["geometry"] = polygon,
                ["properties"] = new Dictionary<string, object>
                {
                    ["id"] = polygon.UserData ?? 0
                }
            });
        }

        return EncodeToProtobuf(features);
    }

    private byte[] EncodeToProtobuf(List<Dictionary<string, object>> features)
    {
        // Simulate protobuf encoding
        // In production, this would use actual MVT encoding library
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Write feature count
        writer.Write(features.Count);

        foreach (var feature in features)
        {
            // Simplified encoding: write geometry type and coordinates count
            if (feature["geometry"] is Geometry geom)
            {
                writer.Write(geom.GeometryType);
                writer.Write(geom.Coordinates.Length);

                foreach (var coord in geom.Coordinates)
                {
                    writer.Write(coord.X);
                    writer.Write(coord.Y);
                }
            }

            // Write properties
            if (feature["properties"] is Dictionary<string, object> props)
            {
                writer.Write(props.Count);
                foreach (var prop in props)
                {
                    writer.Write(prop.Key);
                    writer.Write(prop.Value?.ToString() ?? "");
                }
            }
        }

        return ms.ToArray();
    }
}

/// <summary>
/// Benchmarks for tile caching strategies.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class TileCachingBenchmarks
{
    private Dictionary<string, byte[]> _memoryCache = null!;
    private byte[] _sampleTileData = null!;

    [GlobalSetup]
    public void Setup()
    {
        _memoryCache = new Dictionary<string, byte[]>();

        // Generate sample tile data (256x256 PNG)
        _sampleTileData = new byte[65536]; // ~64KB typical tile size
        new Random(42).NextBytes(_sampleTileData);
    }

    [Benchmark(Description = "Cache: In-memory lookup (hit)")]
    public byte[]? CacheLookupHit()
    {
        var key = "tile:10:512:384";
        _memoryCache[key] = _sampleTileData;
        return _memoryCache.TryGetValue(key, out var data) ? data : null;
    }

    [Benchmark(Description = "Cache: In-memory lookup (miss)")]
    public byte[]? CacheLookupMiss()
    {
        var key = "tile:10:512:999"; // Non-existent
        return _memoryCache.TryGetValue(key, out var data) ? data : null;
    }

    [Benchmark(Description = "Cache: Store tile (64KB)")]
    public void CacheStoreTile()
    {
        var key = $"tile:10:{Random.Shared.Next(1000)}:{Random.Shared.Next(1000)}";
        _memoryCache[key] = _sampleTileData;
    }

    [Benchmark(Description = "Cache: Generate cache key")]
    public string GenerateCacheKey()
    {
        return $"tile:{10}:{512}:{384}:EPSG:3857";
    }

    [Benchmark(Description = "Cache: Generate cache key with hash")]
    public string GenerateCacheKeyWithHash()
    {
        var input = $"tile:10:512:384:EPSG:3857";
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(hash);
    }
}
