using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetTopologySuite.Geometries;
using System.IO.Compression;
using System.Text;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Benchmarks for tile generation including vector tiles (MVT), raster tiles (PNG/JPEG/WebP),
/// tile caching, tile grid calculations, and tile compression.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class TileBenchmarks
{
    private GeometryFactory _geometryFactory = null!;
    private List<Polygon> _smallDataset = null!;   // 100 features
    private List<Polygon> _mediumDataset = null!;  // 1,000 features
    private List<Polygon> _largeDataset = null!;   // 10,000 features
    private List<Point> _pointsDataset = null!;    // 5,000 points
    private byte[] _sampleTileData = null!;
    private byte[] _samplePngTile = null!;
    private TileCoordinate[] _tileCoordinates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);

        // Generate test datasets
        _smallDataset = GeneratePolygons(100);
        _mediumDataset = GeneratePolygons(1000);
        _largeDataset = GeneratePolygons(10000);
        _pointsDataset = GeneratePoints(5000);

        // Generate sample tile data
        _sampleTileData = new byte[65536]; // 64KB
        new Random(42).NextBytes(_sampleTileData);

        // Generate sample PNG tile (simplified)
        _samplePngTile = GeneratePngTileData(256, 256);

        // Pre-calculate tile coordinates for benchmarking
        _tileCoordinates = GenerateTileCoordinates(100);
    }

    // =====================================================
    // Tile Grid Calculations
    // =====================================================

    [Benchmark(Description = "TileGrid: Calculate tile bounds (Web Mercator)")]
    public (double minX, double minY, double maxX, double maxY) CalculateTileBoundsWebMercator()
    {
        // Tile z=10, x=512, y=384
        return GetTileBounds(10, 512, 384);
    }

    [Benchmark(Description = "TileGrid: Calculate tile coordinates from lat/lon")]
    public (int z, int x, int y) LatLonToTileCoordinates()
    {
        // Portland, Oregon at zoom 10
        return LatLonToTile(45.5231, -122.6765, 10);
    }

    [Benchmark(Description = "TileGrid: Batch calculate 100 tile bounds")]
    public List<(double, double, double, double)> BatchCalculateTileBounds()
    {
        var results = new List<(double, double, double, double)>(100);
        foreach (var tile in _tileCoordinates)
        {
            results.Add(GetTileBounds(tile.Z, tile.X, tile.Y));
        }
        return results;
    }

    [Benchmark(Description = "TileGrid: Get tiles for bbox (zoom 10)")]
    public List<(int x, int y)> GetTilesForBbox()
    {
        // Portland metro area
        var minLon = -122.8;
        var minLat = 45.4;
        var maxLon = -122.5;
        var maxLat = 45.6;
        var zoom = 10;

        return GetTilesInBbox(minLon, minLat, maxLon, maxLat, zoom);
    }

    [Benchmark(Description = "TileGrid: Get parent tile")]
    public (int z, int x, int y) GetParentTile()
    {
        // Get parent of tile z=10, x=512, y=384
        return (9, 512 / 2, 384 / 2);
    }

    [Benchmark(Description = "TileGrid: Get child tiles")]
    public List<(int z, int x, int y)> GetChildTiles()
    {
        // Get 4 children of tile z=10, x=512, y=384
        var z = 10;
        var x = 512;
        var y = 384;

        return new List<(int, int, int)>
        {
            (z + 1, x * 2, y * 2),
            (z + 1, x * 2 + 1, y * 2),
            (z + 1, x * 2, y * 2 + 1),
            (z + 1, x * 2 + 1, y * 2 + 1)
        };
    }

    // =====================================================
    // Vector Tile (MVT) Generation
    // =====================================================

    [Benchmark(Description = "MVT: Encode 100 polygons")]
    public byte[] MvtEncode100Polygons()
    {
        return EncodeMvt(_smallDataset, extent: 4096);
    }

    [Benchmark(Description = "MVT: Encode 1,000 polygons")]
    public byte[] MvtEncode1000Polygons()
    {
        return EncodeMvt(_mediumDataset, extent: 4096);
    }

    [Benchmark(Description = "MVT: Encode 10,000 polygons")]
    public byte[] MvtEncode10000Polygons()
    {
        return EncodeMvt(_largeDataset, extent: 4096);
    }

    [Benchmark(Description = "MVT: Encode 5,000 points")]
    public byte[] MvtEncode5000Points()
    {
        return EncodeMvtPoints(_pointsDataset, extent: 4096);
    }

    [Benchmark(Description = "MVT: Clip features to tile bounds")]
    public List<Polygon> ClipFeaturesToTile()
    {
        var tileBounds = GetTileBounds(10, 512, 384);
        var envelope = new Envelope(tileBounds.minX, tileBounds.maxX, tileBounds.minY, tileBounds.maxY);
        var tileGeom = envelope.ToGeometry(_geometryFactory);

        var clipped = new List<Polygon>();
        foreach (var polygon in _mediumDataset)
        {
            if (envelope.Intersects(polygon.EnvelopeInternal))
            {
                var intersection = polygon.Intersection(tileGeom);
                if (intersection is Polygon p)
                {
                    clipped.Add(p);
                }
            }
        }

        return clipped;
    }

    [Benchmark(Description = "MVT: Transform to tile coordinates")]
    public List<Coordinate> TransformToTileCoordinates()
    {
        var coords = _smallDataset[0].Coordinates;
        var tileBounds = GetTileBounds(10, 512, 384);
        var extent = 4096;

        var transformed = new List<Coordinate>(coords.Length);
        foreach (var coord in coords)
        {
            var tileX = (int)((coord.X - tileBounds.minX) / (tileBounds.maxX - tileBounds.minX) * extent);
            var tileY = (int)((coord.Y - tileBounds.minY) / (tileBounds.maxY - tileBounds.minY) * extent);
            transformed.Add(new Coordinate(tileX, tileY));
        }

        return transformed;
    }

    // =====================================================
    // Raster Tile Generation
    // =====================================================

    [Benchmark(Description = "Raster: Create blank tile (256x256)")]
    public byte[] CreateBlankTile256()
    {
        return new byte[256 * 256 * 4]; // RGBA
    }

    [Benchmark(Description = "Raster: Create blank tile (512x512)")]
    public byte[] CreateBlankTile512()
    {
        return new byte[512 * 512 * 4]; // RGBA
    }

    [Benchmark(Description = "Raster: Simulate PNG encoding (256x256)")]
    public byte[] SimulatePngEncoding256()
    {
        // In real implementation, this would use SixLabors.ImageSharp or similar
        var pixels = new byte[256 * 256 * 4];
        new Random(42).NextBytes(pixels);

        // Simulate compression
        using var ms = new MemoryStream();
        using var gzip = new GZipStream(ms, CompressionLevel.Fastest);
        gzip.Write(pixels, 0, pixels.Length);
        gzip.Flush();

        return ms.ToArray();
    }

    [Benchmark(Description = "Raster: Simulate JPEG encoding (256x256)")]
    public byte[] SimulateJpegEncoding256()
    {
        // Simulate JPEG encoding (no alpha channel)
        var pixels = new byte[256 * 256 * 3]; // RGB
        new Random(42).NextBytes(pixels);

        // JPEG is lossy but faster
        using var ms = new MemoryStream();
        ms.Write(pixels, 0, pixels.Length);
        return ms.ToArray();
    }

    [Benchmark(Description = "Raster: Tile resampling (512x512 to 256x256)")]
    public byte[] ResampleTile()
    {
        var sourceTile = new byte[512 * 512 * 4];
        var targetTile = new byte[256 * 256 * 4];

        // Simplified bilinear resampling simulation
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                var srcX = x * 2;
                var srcY = y * 2;
                var srcIndex = (srcY * 512 + srcX) * 4;
                var dstIndex = (y * 256 + x) * 4;

                // Copy pixel (simplified)
                targetTile[dstIndex] = sourceTile[srcIndex];
                targetTile[dstIndex + 1] = sourceTile[srcIndex + 1];
                targetTile[dstIndex + 2] = sourceTile[srcIndex + 2];
                targetTile[dstIndex + 3] = sourceTile[srcIndex + 3];
            }
        }

        return targetTile;
    }

    // =====================================================
    // Tile Compression
    // =====================================================

    [Benchmark(Description = "Compression: GZip compress tile (64KB)")]
    public byte[] GZipCompressTile()
    {
        using var ms = new MemoryStream();
        using var gzip = new GZipStream(ms, CompressionLevel.Fastest);
        gzip.Write(_sampleTileData, 0, _sampleTileData.Length);
        gzip.Flush();
        return ms.ToArray();
    }

    [Benchmark(Description = "Compression: GZip decompress tile")]
    public byte[] GZipDecompressTile()
    {
        // First compress
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using var gzip = new GZipStream(ms, CompressionLevel.Fastest);
            gzip.Write(_sampleTileData, 0, _sampleTileData.Length);
            gzip.Flush();
            compressed = ms.ToArray();
        }

        // Then decompress
        using var compressedStream = new MemoryStream(compressed);
        using var gzipDecompress = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        gzipDecompress.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    [Benchmark(Description = "Compression: Brotli compress tile")]
    public byte[] BrotliCompressTile()
    {
        using var ms = new MemoryStream();
        using var brotli = new BrotliStream(ms, CompressionLevel.Fastest);
        brotli.Write(_sampleTileData, 0, _sampleTileData.Length);
        brotli.Flush();
        return ms.ToArray();
    }

    [Benchmark(Description = "Compression: Brotli decompress tile")]
    public byte[] BrotliDecompressTile()
    {
        // First compress
        byte[] compressed;
        using (var ms = new MemoryStream())
        {
            using var brotli = new BrotliStream(ms, CompressionLevel.Fastest);
            brotli.Write(_sampleTileData, 0, _sampleTileData.Length);
            brotli.Flush();
            compressed = ms.ToArray();
        }

        // Then decompress
        using var compressedStream = new MemoryStream(compressed);
        using var brotliDecompress = new BrotliStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        brotliDecompress.CopyTo(resultStream);
        return resultStream.ToArray();
    }

    // =====================================================
    // Tile Caching
    // =====================================================

    [Benchmark(Description = "Cache: Generate tile cache key")]
    public string GenerateTileCacheKey()
    {
        return $"tile:parcels:WebMercatorQuad:10:512:384:mvt";
    }

    [Benchmark(Description = "Cache: Generate tile cache key with hash")]
    public string GenerateTileCacheKeyWithHash()
    {
        var key = "tile:parcels:WebMercatorQuad:10:512:384:mvt";
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash);
    }

    [Benchmark(Description = "Cache: Tile ETag generation")]
    public string GenerateTileETag()
    {
        var data = _sampleTileData;
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(data);
        return $"\"{BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}\"";
    }

    [Benchmark(Description = "Cache: Tile metadata serialization")]
    public string SerializeTileMetadata()
    {
        var metadata = new
        {
            collection = "parcels",
            tileMatrixSet = "WebMercatorQuad",
            z = 10,
            x = 512,
            y = 384,
            format = "application/vnd.mapbox-vector-tile",
            generated = DateTime.UtcNow,
            featureCount = 1234,
            sizeBytes = 65536
        };

        return System.Text.Json.JsonSerializer.Serialize(metadata);
    }

    // =====================================================
    // Tile Seeding/Warming
    // =====================================================

    [Benchmark(Description = "Seeding: Calculate tiles to seed (zoom 8-12)")]
    public int CalculateTilesToSeed()
    {
        // Calculate number of tiles in bbox for zoom levels 8-12
        var minLon = -122.8;
        var minLat = 45.4;
        var maxLon = -122.5;
        var maxLat = 45.6;

        int totalTiles = 0;
        for (int zoom = 8; zoom <= 12; zoom++)
        {
            var tiles = GetTilesInBbox(minLon, minLat, maxLon, maxLat, zoom);
            totalTiles += tiles.Count;
        }

        return totalTiles;
    }

    [Benchmark(Description = "Seeding: Generate tile request queue (1,000 tiles)")]
    public List<TileRequest> GenerateTileRequestQueue()
    {
        var requests = new List<TileRequest>(1000);
        for (int i = 0; i < 1000; i++)
        {
            requests.Add(new TileRequest
            {
                Collection = "parcels",
                TileMatrixSet = "WebMercatorQuad",
                Z = 10,
                X = i % 100,
                Y = i / 100,
                Format = "mvt"
            });
        }
        return requests;
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private List<Polygon> GeneratePolygons(int count)
    {
        var polygons = new List<Polygon>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var baseLon = -122.5 + (random.NextDouble() * 0.5);
            var baseLat = 45.5 + (random.NextDouble() * 0.5);
            var size = 0.001 + (random.NextDouble() * 0.005);

            var coords = new[]
            {
                new Coordinate(baseLon, baseLat),
                new Coordinate(baseLon + size, baseLat),
                new Coordinate(baseLon + size, baseLat + size),
                new Coordinate(baseLon, baseLat + size),
                new Coordinate(baseLon, baseLat)
            };

            var ring = _geometryFactory.CreateLinearRing(coords);
            polygons.Add(_geometryFactory.CreatePolygon(ring));
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

    private byte[] EncodeMvt(List<Polygon> polygons, int extent)
    {
        // Simplified MVT encoding
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(polygons.Count);
        writer.Write(extent);

        foreach (var polygon in polygons)
        {
            var coords = polygon.Coordinates;
            writer.Write(coords.Length);
            foreach (var coord in coords)
            {
                writer.Write(coord.X);
                writer.Write(coord.Y);
            }
        }

        return ms.ToArray();
    }

    private byte[] EncodeMvtPoints(List<Point> points, int extent)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(points.Count);
        writer.Write(extent);

        foreach (var point in points)
        {
            writer.Write(point.X);
            writer.Write(point.Y);
        }

        return ms.ToArray();
    }

    private (double minX, double minY, double maxX, double maxY) GetTileBounds(int z, int x, int y)
    {
        // Web Mercator tile bounds calculation
        var n = Math.Pow(2, z);
        var minLon = x / n * 360.0 - 180.0;
        var maxLon = (x + 1) / n * 360.0 - 180.0;

        var latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / n)));
        var maxLat = latRad * 180.0 / Math.PI;

        latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (y + 1) / n)));
        var minLat = latRad * 180.0 / Math.PI;

        return (minLon, minLat, maxLon, maxLat);
    }

    private (int z, int x, int y) LatLonToTile(double lat, double lon, int zoom)
    {
        var n = Math.Pow(2, zoom);
        var x = (int)((lon + 180.0) / 360.0 * n);
        var latRad = lat * Math.PI / 180.0;
        var y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (zoom, x, y);
    }

    private List<(int x, int y)> GetTilesInBbox(double minLon, double minLat, double maxLon, double maxLat, int zoom)
    {
        var min = LatLonToTile(minLat, minLon, zoom);
        var max = LatLonToTile(maxLat, maxLon, zoom);

        var tiles = new List<(int x, int y)>();
        for (int x = Math.Min(min.x, max.x); x <= Math.Max(min.x, max.x); x++)
        {
            for (int y = Math.Min(min.y, max.y); y <= Math.Max(min.y, max.y); y++)
            {
                tiles.Add((x, y));
            }
        }

        return tiles;
    }

    private TileCoordinate[] GenerateTileCoordinates(int count)
    {
        var tiles = new TileCoordinate[count];
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            tiles[i] = new TileCoordinate
            {
                Z = 10,
                X = random.Next(0, 1024),
                Y = random.Next(0, 1024)
            };
        }

        return tiles;
    }

    private byte[] GeneratePngTileData(int width, int height)
    {
        // Simplified PNG tile data generation
        var data = new byte[width * height * 4]; // RGBA
        new Random(42).NextBytes(data);
        return data;
    }

    private struct TileCoordinate
    {
        public int Z { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class TileRequest
    {
        public string Collection { get; set; } = "";
        public string TileMatrixSet { get; set; } = "";
        public int Z { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Format { get; set; } = "";
    }
}
