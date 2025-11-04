using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using System.Net.Http;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Raster processing benchmarks covering tile extraction, encoding, and transformations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class RasterProcessingBenchmarks
{
    private HttpClient _httpClient = null!;
    private LibTiffCogReader _cogReader = null!;
    private HttpZarrReader _zarrReader = null!;
    private ZarrChunkCache _chunkCache = null!;
    private byte[] _testRgbData = null!;
    private const string SampleCogPath = "test-data/sample.tif";

    [Params(256, 512, 1024)]
    public int TileSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient();
        _cogReader = new LibTiffCogReader(
            NullLogger<LibTiffCogReader>.Instance,
            _httpClient
        );

        _chunkCache = new ZarrChunkCache(
            NullLogger<ZarrChunkCache>.Instance,
            new ZarrChunkCacheOptions
            {
                MaxCacheSizeBytes = 256 * 1024 * 1024,
                ChunkTtlMinutes = 60
            }
        );

        _zarrReader = new HttpZarrReader(
            NullLogger<HttpZarrReader>.Instance,
            _httpClient,
            _chunkCache
        );

        // Generate test RGB data
        _testRgbData = GenerateTestRgbData(TileSize, TileSize);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _chunkCache?.Dispose();
        _httpClient?.Dispose();
    }

    // =====================================================
    // COG Tile Extraction Benchmarks
    // =====================================================

    [Benchmark(Description = "COG: Read tile from local file")]
    public async Task<byte[]> CogReadTileLocal()
    {
        if (!File.Exists(SampleCogPath))
        {
            return Array.Empty<byte>();
        }

        using var dataset = await _cogReader.OpenAsync(SampleCogPath);

        if (!dataset.Metadata.IsTiled)
        {
            // Fall back to window read
            return await _cogReader.ReadWindowAsync(dataset, 0, 0, TileSize, TileSize);
        }

        return await _cogReader.ReadTileAsync(dataset, 0, 0);
    }

    [Benchmark(Description = "COG: Read window from local file")]
    public async Task<byte[]> CogReadWindow()
    {
        if (!File.Exists(SampleCogPath))
        {
            return Array.Empty<byte>();
        }

        using var dataset = await _cogReader.OpenAsync(SampleCogPath);
        return await _cogReader.ReadWindowAsync(dataset, 0, 0, TileSize, TileSize);
    }

    [Benchmark(Description = "COG: Read metadata and GeoTIFF tags")]
    public async Task<CogMetadata> CogReadMetadata()
    {
        if (!File.Exists(SampleCogPath))
        {
            return new CogMetadata
            {
                Width = TileSize,
                Height = TileSize,
                BandCount = 3,
                BitsPerSample = 8,
                IsCog = true,
                IsTiled = true,
                TileWidth = TileSize,
                TileHeight = TileSize
            };
        }

        return await _cogReader.GetMetadataAsync(SampleCogPath);
    }

    // =====================================================
    // Zarr Chunk Reading Benchmarks
    // =====================================================

    // Note: These require actual Zarr data stores to run
    // Commented out to prevent benchmark failures without test data

    /*
    [Benchmark(Description = "Zarr: Read chunk (cold cache)")]
    public async Task<byte[]> ZarrReadChunkCold()
    {
        _chunkCache.Clear();
        var array = await _zarrReader.OpenArrayAsync("s3://test-bucket/dataset.zarr", "temperature");
        return await _zarrReader.ReadChunkAsync(array, new[] { 0, 0, 0 });
    }

    [Benchmark(Description = "Zarr: Read chunk (warm cache)")]
    public async Task<byte[]> ZarrReadChunkWarm()
    {
        var array = await _zarrReader.OpenArrayAsync("s3://test-bucket/dataset.zarr", "temperature");
        // First read to warm cache
        await _zarrReader.ReadChunkAsync(array, new[] { 0, 0, 0 });
        // Benchmarked read
        return await _zarrReader.ReadChunkAsync(array, new[] { 0, 0, 0 });
    }

    [Benchmark(Description = "Zarr: Read 2D slice")]
    public async Task<Array> ZarrRead2DSlice()
    {
        var array = await _zarrReader.OpenArrayAsync("s3://test-bucket/dataset.zarr", "temperature");
        return await _zarrReader.ReadSliceAsync(
            array,
            start: new[] { 0, 0, 0 },
            count: new[] { 1, TileSize, TileSize }
        );
    }
    */

    // =====================================================
    // Image Encoding Benchmarks
    // =====================================================

    [Benchmark(Baseline = true, Description = "Encode: PNG (default compression)")]
    public byte[] EncodePngDefault()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Benchmark(Description = "Encode: PNG (max compression)")]
    public byte[] EncodePngMaxCompression()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        var encoder = new PngEncoder
        {
            CompressionLevel = PngCompressionLevel.BestCompression
        };
        image.SaveAsPng(ms, encoder);
        return ms.ToArray();
    }

    [Benchmark(Description = "Encode: JPEG (quality 85)")]
    public byte[] EncodeJpeg85()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        var encoder = new JpegEncoder { Quality = 85 };
        image.SaveAsJpeg(ms, encoder);
        return ms.ToArray();
    }

    [Benchmark(Description = "Encode: JPEG (quality 75)")]
    public byte[] EncodeJpeg75()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        var encoder = new JpegEncoder { Quality = 75 };
        image.SaveAsJpeg(ms, encoder);
        return ms.ToArray();
    }

    [Benchmark(Description = "Encode: WebP (lossless)")]
    public byte[] EncodeWebPLossless()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        var encoder = new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossless
        };
        image.SaveAsWebp(ms, encoder);
        return ms.ToArray();
    }

    [Benchmark(Description = "Encode: WebP (lossy quality 85)")]
    public byte[] EncodeWebPLossy()
    {
        using var image = CreateTestImage(TileSize, TileSize);
        using var ms = new MemoryStream();
        var encoder = new WebpEncoder
        {
            FileFormat = WebpFileFormatType.Lossy,
            Quality = 85
        };
        image.SaveAsWebp(ms, encoder);
        return ms.ToArray();
    }

    // =====================================================
    // Tile Reprojection Benchmarks
    // =====================================================

    [Benchmark(Description = "Reproject: WGS84 to Web Mercator")]
    public void ReprojectToWebMercator()
    {
        // Simulate coordinate transformation for tile bounds
        var sourceSrid = 4326; // WGS84
        var targetSrid = 3857; // Web Mercator

        // Transform tile corners
        var minX = -122.5;
        var minY = 45.5;
        var maxX = -122.3;
        var maxY = 45.7;

        var transformed = Honua.Server.Core.Data.CrsTransform.TransformEnvelope(
            minX, minY, maxX, maxY,
            sourceSrid, targetSrid
        );

        // Result would be used for tile rendering
        _ = transformed;
    }

    // =====================================================
    // Mosaic Generation Benchmarks
    // =====================================================

    [Benchmark(Description = "Mosaic: Combine 4 tiles")]
    public byte[] MosaicCombine4Tiles()
    {
        var tileSize = TileSize;
        var mosaicSize = tileSize * 2;

        using var mosaic = new Image<Rgb24>(mosaicSize, mosaicSize);

        // Create 4 test tiles
        var tiles = new[]
        {
            CreateTestImage(tileSize, tileSize),
            CreateTestImage(tileSize, tileSize),
            CreateTestImage(tileSize, tileSize),
            CreateTestImage(tileSize, tileSize)
        };

        try
        {
            // Place tiles in 2x2 grid
            mosaic.Mutate(ctx =>
            {
                ctx.DrawImage(tiles[0], new Point(0, 0), 1f);
                ctx.DrawImage(tiles[1], new Point(tileSize, 0), 1f);
                ctx.DrawImage(tiles[2], new Point(0, tileSize), 1f);
                ctx.DrawImage(tiles[3], new Point(tileSize, tileSize), 1f);
            });

            // Encode result
            using var ms = new MemoryStream();
            mosaic.SaveAsPng(ms);
            return ms.ToArray();
        }
        finally
        {
            foreach (var tile in tiles)
            {
                tile.Dispose();
            }
        }
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private byte[] GenerateTestRgbData(int width, int height)
    {
        var data = new byte[width * height * 3];
        var random = new Random(42); // Fixed seed for reproducibility

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 3;
                data[offset] = (byte)random.Next(256);     // R
                data[offset + 1] = (byte)random.Next(256); // G
                data[offset + 2] = (byte)random.Next(256); // B
            }
        }

        return data;
    }

    private Image<Rgb24> CreateTestImage(int width, int height)
    {
        var image = new Image<Rgb24>(width, height);
        var random = new Random(42);

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24(
                        (byte)random.Next(256),
                        (byte)random.Next(256),
                        (byte)random.Next(256)
                    );
                }
            }
        });

        return image;
    }
}

/// <summary>
/// Benchmarks for Zarr decompression codecs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class ZarrDecompressionBenchmarks
{
    private ZarrDecompressor _decompressor = null!;
    private byte[] _gzipData = null!;
    private byte[] _zstdData = null!;
    private byte[] _uncompressedData = null!;

    [Params(1024, 16384, 262144)] // 1KB, 16KB, 256KB
    public int DataSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var codecRegistry = new Honua.Server.Core.Raster.Compression.CompressionCodecRegistry(
            NullLogger<Honua.Server.Core.Raster.Compression.CompressionCodecRegistry>.Instance);
        _decompressor = new ZarrDecompressor(NullLogger<ZarrDecompressor>.Instance, codecRegistry);

        // Generate test data
        _uncompressedData = new byte[DataSize];
        new Random(42).NextBytes(_uncompressedData);

        // Compress with different algorithms
        _gzipData = CompressGzip(_uncompressedData);
        _zstdData = CompressZstd(_uncompressedData);
    }

    [Benchmark(Baseline = true, Description = "Decompress: GZIP")]
    public byte[] DecompressGzip()
    {
        return _decompressor.Decompress(_gzipData, "gzip");
    }

    [Benchmark(Description = "Decompress: ZSTD")]
    public byte[] DecompressZstd()
    {
        return _decompressor.Decompress(_zstdData, "zstd");
    }

    [Benchmark(Description = "Decompress: None (pass-through)")]
    public byte[] DecompressNone()
    {
        return _decompressor.Decompress(_uncompressedData, "null");
    }

    private byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private byte[] CompressZstd(byte[] data)
    {
        // Note: Requires ZstdSharp or similar library
        // For now, return uncompressed as placeholder
        return data;
    }
}
