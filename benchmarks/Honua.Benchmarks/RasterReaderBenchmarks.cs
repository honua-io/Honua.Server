using System.Net.Http;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Honua.Benchmarks;

/// <summary>
/// Benchmarks for raster readers (COG and Zarr).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class RasterReaderBenchmarks
{
    private HttpClient _httpClient = null!;
    private ILogger<LibTiffCogReader> _cogLogger = null!;
    private ILogger<HttpZarrReader> _zarrLogger = null!;
    private ILogger<ZarrChunkCache> _cacheLogger = null!;
    private ZarrChunkCache _chunkCache = null!;

    // Sample test files (replace with actual test data paths)
    private const string SampleCogPath = "test-data/sample.tif";
    private const string SampleZarrUri = "https://example.com/sample.zarr";

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient();
        _cogLogger = NullLogger<LibTiffCogReader>.Instance;
        _zarrLogger = NullLogger<HttpZarrReader>.Instance;
        _cacheLogger = NullLogger<ZarrChunkCache>.Instance;

        _chunkCache = new ZarrChunkCache(_cacheLogger, new ZarrChunkCacheOptions
        {
            MaxCacheSizeBytes = 256 * 1024 * 1024,  // 256 MB
            ChunkTtlMinutes = 60
        });
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _chunkCache?.Dispose();
        _httpClient?.Dispose();
    }

    [Benchmark(Description = "COG: Open local file and read metadata")]
    public async Task CogOpenAndReadMetadata()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;  // Skip if test file doesn't exist
        }

        var reader = new LibTiffCogReader(_cogLogger, _httpClient);
        using var dataset = await reader.OpenAsync(SampleCogPath);
        var metadata = dataset.Metadata;
    }

    [Benchmark(Description = "COG: Read single tile from local file")]
    public async Task CogReadTile()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;
        }

        var reader = new LibTiffCogReader(_cogLogger, _httpClient);
        using var dataset = await reader.OpenAsync(SampleCogPath);

        if (dataset.Metadata.IsTiled)
        {
            var tile = await reader.ReadTileAsync(dataset, 0, 0);
        }
    }

    [Benchmark(Description = "COG: Read 512x512 window")]
    public async Task CogReadWindow()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;
        }

        var reader = new LibTiffCogReader(_cogLogger, _httpClient);
        using var dataset = await reader.OpenAsync(SampleCogPath);
        var window = await reader.ReadWindowAsync(dataset, 0, 0, 512, 512);
    }

    [Benchmark(Description = "GeoTIFF: Parse geospatial tags")]
    public async Task GeoTiffParseGeospatialTags()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;
        }

        var reader = new LibTiffCogReader(_cogLogger, _httpClient);
        using var dataset = await reader.OpenAsync(SampleCogPath);

        var geoTransform = dataset.Metadata.GeoTransform;
        var projection = dataset.Metadata.ProjectionWkt;
    }

    // Note: Zarr benchmarks require actual remote Zarr store
    // The following are templates that would run with real data

    /*
    [Benchmark(Description = "Zarr: Read chunk without cache")]
    public async Task ZarrReadChunkNoCache()
    {
        var reader = new HttpZarrReader(_zarrLogger, _httpClient, chunkCache: null);
        using var array = await reader.OpenArrayAsync(SampleZarrUri, "temperature");
        var chunk = await reader.ReadChunkAsync(array, new[] { 0, 0, 0 });
    }

    [Benchmark(Description = "Zarr: Read chunk with cache (cold)")]
    public async Task ZarrReadChunkCacheCold()
    {
        _chunkCache.Clear();  // Ensure cold cache

        var reader = new HttpZarrReader(_zarrLogger, _httpClient, _chunkCache);
        using var array = await reader.OpenArrayAsync(SampleZarrUri, "temperature");
        var chunk = await reader.ReadChunkAsync(array, new[] { 0, 0, 0 });
    }

    [Benchmark(Description = "Zarr: Read chunk with cache (warm)")]
    public async Task ZarrReadChunkCacheWarm()
    {
        var reader = new HttpZarrReader(_zarrLogger, _httpClient, _chunkCache);
        using var array = await reader.OpenArrayAsync(SampleZarrUri, "temperature");

        // First read to warm cache
        await reader.ReadChunkAsync(array, new[] { 0, 0, 0 });

        // Benchmarked read
        var chunk = await reader.ReadChunkAsync(array, new[] { 0, 0, 0 });
    }

    [Benchmark(Description = "Zarr: Decompress GZIP chunk")]
    public void ZarrDecompressGzip()
    {
        var decompressor = new ZarrDecompressor(_zarrLogger);
        var testData = GenerateCompressedTestData("gzip", 1024 * 1024);  // 1 MB
        var decompressed = decompressor.Decompress(testData, "gzip");
    }

    [Benchmark(Description = "Zarr: Decompress ZSTD chunk")]
    public void ZarrDecompressZstd()
    {
        var decompressor = new ZarrDecompressor(_zarrLogger);
        var testData = GenerateCompressedTestData("zstd", 1024 * 1024);
        var decompressed = decompressor.Decompress(testData, "zstd");
    }
    */
}

/// <summary>
/// Benchmarks for HTTP range request optimizations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class HttpRangeRequestBenchmarks
{
    private HttpClient _httpClient = null!;
    private ILogger<HttpRangeStream> _logger = null!;

    // Sample remote COG URL (replace with actual test URL)
    private const string RemoteCogUrl = "https://example.com/sample-cog.tif";

    [GlobalSetup]
    public void Setup()
    {
        _httpClient = new HttpClient();
        _logger = NullLogger<HttpRangeStream>.Instance;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
    }

    /*
    [Benchmark(Description = "HTTP: Single range request (16 KB)")]
    public async Task HttpSingleRangeRequest16KB()
    {
        using var stream = await HttpRangeStream.CreateAsync(_httpClient, RemoteCogUrl, _logger);
        var buffer = new byte[16 * 1024];
        await stream.ReadAsync(buffer, 0, buffer.Length);
    }

    [Benchmark(Description = "HTTP: Sequential range requests (4x16 KB)")]
    public async Task HttpSequentialRangeRequests()
    {
        using var stream = await HttpRangeStream.CreateAsync(_httpClient, RemoteCogUrl, _logger);
        var buffer = new byte[16 * 1024];

        for (int i = 0; i < 4; i++)
        {
            await stream.ReadAsync(buffer, 0, buffer.Length);
        }
    }

    [Benchmark(Description = "HTTP: Random access range requests")]
    public async Task HttpRandomAccessRangeRequests()
    {
        using var stream = await HttpRangeStream.CreateAsync(_httpClient, RemoteCogUrl, _logger);
        var buffer = new byte[16 * 1024];

        // Simulate random tile access
        var positions = new[] { 0L, 1024 * 1024, 2 * 1024 * 1024, 500 * 1024 };

        foreach (var pos in positions)
        {
            stream.Seek(pos, SeekOrigin.Begin);
            await stream.ReadAsync(buffer, 0, buffer.Length);
        }
    }
    */
}

/// <summary>
/// Comparison benchmarks: GDAL vs Pure .NET readers.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ReaderComparisonBenchmarks
{
    private const string SampleCogPath = "test-data/sample.tif";

    [Benchmark(Baseline = true, Description = "Pure .NET: LibTiff COG reader")]
    public async Task PureNetCogReader()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;
        }

        var logger = NullLogger<LibTiffCogReader>.Instance;
        var reader = new LibTiffCogReader(logger);
        using var dataset = await reader.OpenAsync(SampleCogPath);
        var metadata = dataset.Metadata;
    }

    /*
    [Benchmark(Description = "GDAL: Traditional COG reader")]
    public void GdalCogReader()
    {
        if (!File.Exists(SampleCogPath))
        {
            return;
        }

        // GDAL-based implementation (if available)
        // using var dataset = Gdal.Open(SampleCogPath, Access.GA_ReadOnly);
        // var width = dataset.RasterXSize;
        // var height = dataset.RasterYSize;
    }
    */
}
