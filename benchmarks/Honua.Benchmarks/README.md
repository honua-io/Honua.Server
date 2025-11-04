# Honua Raster Performance Benchmarks

Performance benchmarks for raster reading capabilities (COG and Zarr).

## Running Benchmarks

### All Benchmarks
```bash
dotnet run -c Release --project benchmarks/Honua.Benchmarks
```

### Specific Benchmark Class
```bash
dotnet run -c Release --project benchmarks/Honua.Benchmarks --filter "*RasterReaderBenchmarks*"
```

### List Available Benchmarks
```bash
dotnet run -c Release --project benchmarks/Honua.Benchmarks --list flat
```

## Benchmark Categories

### RasterReaderBenchmarks
Tests for COG and Zarr reader performance:
- COG file opening and metadata extraction
- Tile reading performance
- Window reading performance
- GeoTIFF tag parsing
- Zarr chunk decompression (GZIP, ZSTD, Blosc)
- Cache hit/miss performance

### HttpRangeRequestBenchmarks
Tests for HTTP range request optimization:
- Single range request performance
- Sequential access patterns
- Random access patterns
- Read-ahead buffer efficiency

### ReaderComparisonBenchmarks
Comparison between different reader implementations:
- Pure .NET (LibTiff) vs GDAL
- Performance vs resource usage

## Test Data

Place test files in `benchmarks/Honua.Benchmarks/test-data/`:

```
test-data/
├── sample.tif          # Sample GeoTIFF/COG file
├── sample-zarr/        # Sample Zarr store
└── README.txt          # Test data description
```

### Generating Test Data

You can generate test COG files using GDAL:

```bash
# Create a simple test GeoTIFF
gdal_create -of GTiff -outsize 1024 1024 -bands 3 test.tif

# Convert to COG
gdal_translate test.tif sample.tif \
  -of COG \
  -co COMPRESS=DEFLATE \
  -co BLOCKSIZE=512 \
  -co OVERVIEW_RESAMPLING=BILINEAR
```

## Expected Results

Typical performance metrics (on modern hardware):

| Operation | Mean Time | Allocation |
|-----------|-----------|------------|
| COG: Read metadata | ~1 ms | ~50 KB |
| COG: Read tile (512x512) | ~5 ms | ~1 MB |
| Zarr: Decompress GZIP (1MB) | ~10 ms | ~1 MB |
| Zarr: Decompress ZSTD (1MB) | ~5 ms | ~1 MB |
| HTTP: Range request (16KB) | ~50 ms* | ~16 KB |

*Network latency dependent

## Optimization Tips

1. **COG Reading**
   - Use tiled access for large files
   - Read overviews for zoomed-out views
   - Enable HTTP range requests for remote files

2. **Zarr Reading**
   - Enable chunk caching
   - Use ZSTD compression (faster than GZIP)
   - Batch chunk reads when possible

3. **HTTP Requests**
   - Increase read-ahead buffer for sequential access
   - Use persistent HTTP connections
   - Consider CDN for static datasets

## Profiling

### Memory Profiling
```bash
dotnet run -c Release --project benchmarks/Honua.Benchmarks \
  --filter "*RasterReaderBenchmarks*" \
  --memory
```

### CPU Profiling (Windows)
```bash
dotnet run -c Release --project benchmarks/Honua.Benchmarks \
  --filter "*RasterReaderBenchmarks*" \
  --profiler ETW
```

## Continuous Benchmarking

Track performance over time:

```bash
# Run benchmarks and save results
dotnet run -c Release --project benchmarks/Honua.Benchmarks \
  --exporters json \
  --artifacts ./benchmark-results

# Compare with baseline
dotnet run -c Release --project benchmarks/Honua.Benchmarks \
  --filter "*RasterReaderBenchmarks*" \
  --baseline baseline-results.json
```

## Troubleshooting

### "Test file not found" warnings
- Benchmarks skip tests when data files are missing
- Add test files to `test-data/` directory
- See "Generating Test Data" section above

### Out of memory errors
- Reduce benchmark iteration count
- Decrease test data size
- Increase available memory

### Network timeout errors (HTTP benchmarks)
- Check internet connection
- Increase HttpClient timeout
- Use local test server instead
