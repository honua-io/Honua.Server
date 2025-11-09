# Performance Benchmarks

Comprehensive performance benchmarking system for Honua using BenchmarkDotNet.

## Overview

Performance benchmarks help us:
1. Establish baseline performance metrics
2. Detect performance regressions early
3. Guide optimization efforts
4. Track performance trends over time
5. Make informed architectural decisions

## Quick Start

```bash
# Run all benchmarks
dotnet run -c Release --project tests/Honua.Server.Benchmarks

# Run specific category
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*DatabaseQueryBenchmarks*"

# List available benchmarks
dotnet run -c Release --project tests/Honua.Server.Benchmarks --list flat
```

## Benchmark Categories

### 1. Database Query Benchmarks

**Location**: `tests/Honua.Server.Benchmarks/DatabaseQueryBenchmarks.cs`

Tests database query performance across different query patterns and dataset sizes.

**Key Benchmarks**:
- `QueryAllFeatures` - Full table scan performance
- `QuerySingleFeature` - Single feature lookup by ID
- `QuerySpatialBoundingBox` - Spatial filter with bounding box
- `QuerySpatialIntersection` - Spatial intersection queries
- `QueryAttributeEquality` - Attribute-based filtering
- `QueryPaginationFirst100` - First page retrieval
- `QueryPaginationOffset5000` - Deep pagination performance
- `QueryCombinedFilters` - Complex multi-filter queries
- `QueryCrsTransformation` - On-the-fly CRS transformations

**Performance Targets**:
| Operation | 10K Features | 100K Features |
|-----------|--------------|---------------|
| Query all | <200ms | <2s |
| Single feature | <5ms | <10ms |
| Spatial bbox | <100ms | <500ms |
| Pagination (first page) | <50ms | <100ms |

### 2. Raster Processing Benchmarks

**Location**: `tests/Honua.Server.Benchmarks/RasterProcessingBenchmarks.cs`

Tests raster tile extraction, encoding, and transformation performance.

**Key Benchmarks**:
- `CogReadTileLocal` - COG tile reading (256x256, 512x512, 1024x1024)
- `CogReadWindow` - Window-based reading
- `CogReadMetadata` - Metadata extraction and GeoTIFF tag parsing
- `EncodePngDefault` - PNG encoding (default compression)
- `EncodeJpeg85` - JPEG encoding (quality 85)
- `EncodeWebPLossless` - WebP lossless encoding
- `ReprojectToWebMercator` - Coordinate transformation
- `MosaicCombine4Tiles` - Mosaic generation

**Performance Targets**:
| Operation | 256x256 | 512x512 | 1024x1024 |
|-----------|---------|---------|-----------|
| COG tile read | <50ms | <100ms | <200ms |
| PNG encode | <20ms | <50ms | <150ms |
| JPEG encode | <15ms | <40ms | <120ms |
| WebP encode | <25ms | <60ms | <180ms |

### 3. Vector Tile Benchmarks

**Location**: `tests/Honua.Server.Benchmarks/VectorTileBenchmarks.cs`

Tests MVT encoding, geometry simplification, and vector tile generation.

**Key Benchmarks**:
- `MvtEncode100Polygons` - MVT encoding for small datasets
- `MvtEncode1000Polygons` - MVT encoding for medium datasets
- `MvtEncode10000Polygons` - MVT encoding for large datasets
- `SimplifyPolygonsTolerance0001` - Geometry simplification
- `GeoJsonSerialize1000Polygons` - GeoJSON serialization
- `GenerateSimpleMvtQuery` - PostGIS MVT query generation

**Performance Targets**:
| Operation | 1K Features | 10K Features |
|-----------|-------------|--------------|
| MVT encode | <20ms | <200ms |
| Simplification | <30ms | <300ms |
| GeoJSON serialize | <50ms | <500ms |

### 4. API Endpoint Benchmarks

**Location**: `tests/Honua.Server.Benchmarks/ApiEndpointBenchmarks.cs`

Tests request/response performance for OGC APIs, WMS, WMTS, and STAC.

**Key Benchmarks**:
- `OgcApiFeatureCollection1000` - OGC API Features response
- `WmsGetMapRequest` - WMS GetMap parameter parsing
- `WmtsGetTileRequest` - WMTS GetTile request handling
- `StacCatalogSerialization` - STAC catalog serialization
- `ValidateJwtToken` - Authentication overhead

**Performance Targets**:
| Operation | Target |
|-----------|--------|
| OGC API response (1K features) | <100ms |
| WMS GetCapabilities | <50ms |
| WMTS GetTile (cached) | <5ms |
| STAC search (100 results) | <75ms |
| JWT validation | <1ms |

### 5. Export Benchmarks

**Location**: `tests/Honua.Server.Benchmarks/ExportBenchmarks.cs`

Tests export performance for CSV, Shapefile, and GeoPackage formats.

**Key Benchmarks**:
- `CsvWkt_Medium` - CSV export with WKT geometry (1,000 features)
- `Shapefile_Large` - Shapefile export (10,000 features)
- `GeoPackage_Medium` - GeoPackage export (1,000 features)

**Performance Targets**:
| Format | 1K Features | 10K Features |
|--------|-------------|--------------|
| CSV | <100ms | <1,000ms |
| Shapefile | <200ms | <2,000ms |
| GeoPackage | <150ms | <1,500ms |

## Baseline Results

Baseline performance results are stored in `benchmarks/baseline/` and updated with each major release.

### Viewing Baseline Results

```bash
# View latest baseline
cat benchmarks/baseline/latest.json

# View specific release baseline
cat benchmarks/baseline/v1.0.0.json
```

### Updating Baseline

```bash
# Run benchmarks and save results
./scripts/run-benchmarks-baseline.sh

# This will:
# 1. Run all benchmarks
# 2. Save results with timestamp
# 3. Update 'latest.json' symlink
# 4. Generate markdown summary
```

## Continuous Benchmarking

### CI/CD Integration

Benchmarks are automatically run in CI/CD on:
- Pull requests (subset of critical benchmarks)
- Main branch commits (full benchmark suite)
- Nightly builds (comprehensive benchmarking with trending)

### GitHub Actions Workflow

See `.github/workflows/benchmarks.yml` for the complete workflow.

Key steps:
1. Run benchmarks against PR code
2. Compare with baseline from target branch
3. Detect regressions (>10% slower)
4. Comment on PR with results
5. Fail CI if critical regression detected

### Regression Detection

Performance regression thresholds:
- **Critical operations** (database queries, tile generation): >10% regression = ❌ FAIL
- **Secondary operations** (serialization, parsing): >20% regression = ⚠️ WARNING
- **Minor operations**: >30% regression = 📊 INFO

## Running Benchmarks Locally

### Prerequisites

```bash
# Ensure Release mode for accurate results
dotnet build -c Release

# Ensure no background processes consuming resources
# Close unnecessary applications
# Disable CPU throttling if possible
```

### Running Full Suite

```bash
# Run all benchmarks (takes 1-2 hours)
dotnet run -c Release --project tests/Honua.Server.Benchmarks

# Results will be in:
# - tests/Honua.Server.Benchmarks/BenchmarkDotNet.Artifacts/results/
```

### Running Specific Benchmarks

```bash
# Database benchmarks only
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*Database*"

# Raster benchmarks with 512x512 tiles
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*Raster*" --params TileSize=512

# Export benchmarks for medium datasets
dotnet run -c Release --project tests/Honua.Server.Benchmarks --filter "*_Medium"
```

### Comparing Results

```bash
# Run benchmarks and save results
dotnet run -c Release --project tests/Honua.Server.Benchmarks --exporters json

# Compare with previous run
./scripts/compare-benchmark-results.sh \
  tests/Honua.Server.Benchmarks/BenchmarkDotNet.Artifacts/results/previous.json \
  tests/Honua.Server.Benchmarks/BenchmarkDotNet.Artifacts/results/latest.json
```

## Interpreting Results

### Understanding Metrics

- **Mean**: Average execution time across all iterations
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation (lower is more consistent)
- **Median**: Middle value (less affected by outliers)
- **Min/Max**: Fastest/slowest individual runs
- **Gen0/1/2**: Garbage collection counts (lower is better)
- **Allocated**: Total memory allocated (lower is better)

### Example Output

```
| Method                           | Mean      | Error    | StdDev   | Gen0   | Gen1  | Allocated |
|--------------------------------- |----------:|---------:|---------:|-------:|------:|----------:|
| QueryAllFeatures (10000)         | 145.2 ms  | 2.1 ms   | 1.9 ms   | 2000.0 | 500.0 |  21.5 MB  |
| CogReadTileLocal (512)           |  47.3 ms  | 0.8 ms   | 0.7 ms   |  125.0 |  62.5 |   1.2 MB  |
| MvtEncode1000Polygons            |  18.7 ms  | 0.3 ms   | 0.2 ms   |  312.5 | 156.2 |   3.4 MB  |
```

### What to Look For

✅ **Good Performance**:
- Low Mean time
- Low StdDev (consistent)
- Low memory allocation
- Few GC collections

❌ **Performance Issues**:
- High Mean time
- High StdDev (inconsistent)
- Excessive memory allocation
- Frequent Gen1/Gen2 collections

## Performance Optimization Guide

### Database Queries

1. **Index spatial columns**:
   ```sql
   CREATE INDEX idx_parcels_geom ON parcels USING GIST(geom);
   ```

2. **Use connection pooling**:
   ```csharp
   services.AddDbContext<HonuaDbContext>(options =>
       options.UseNpgsql(connectionString, o => o.UseNetTopologySuite())
              .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
   ```

3. **Optimize queries**:
   - Use `AsNoTracking()` for read-only queries
   - Project only needed columns
   - Use compiled queries for hot paths

### Raster Processing

1. **Reuse buffers**:
   ```csharp
   var buffer = ArrayPool<byte>.Shared.Rent(tileSize);
   try {
       // Use buffer
   } finally {
       ArrayPool<byte>.Shared.Return(buffer);
   }
   ```

2. **Parallel tile generation**:
   ```csharp
   await Parallel.ForEachAsync(tiles, async (tile, ct) => {
       await GenerateTileAsync(tile, ct);
   });
   ```

3. **Cache encoded tiles**:
   - Use distributed cache (Redis) for production
   - Memory cache for development
   - Consider CDN for static tiles

### Vector Tiles

1. **Use database-native MVT generation**:
   ```sql
   -- PostGIS MVT is faster than application-level encoding
   SELECT ST_AsMVT(mvtgeom.*, 'layer_name')
   FROM (...) AS mvtgeom;
   ```

2. **Simplify geometries at appropriate zoom levels**:
   ```csharp
   var tolerance = GetSimplificationTolerance(zoom);
   var simplified = TopologyPreservingSimplifier.Simplify(geom, tolerance);
   ```

3. **Enable overzooming**:
   - Generate tiles up to zoom 14
   - Use client-side overzooming for zoom 15-22

### API Responses

1. **Use streaming for large responses**:
   ```csharp
   public async IAsyncEnumerable<Feature> GetFeaturesAsync() {
       await foreach (var feature in _repository.QueryAsync()) {
           yield return feature;
       }
   }
   ```

2. **Implement response caching**:
   ```csharp
   [ResponseCache(Duration = 3600, VaryByQueryKeys = new[] { "*" })]
   public async Task<IActionResult> GetCollection() { ... }
   ```

3. **Use compression**:
   ```csharp
   services.AddResponseCompression(options => {
       options.EnableForHttps = true;
       options.Providers.Add<BrotliCompressionProvider>();
       options.Providers.Add<GzipCompressionProvider>();
   });
   ```

## Profiling

### CPU Profiling

```bash
# Using dotnet-trace
dotnet trace collect --process-id <PID> --providers Microsoft-DotNETCore-SampleProfiler

# Using PerfView (Windows)
PerfView.exe collect -ThreadTime -CircularMB:1024
```

### Memory Profiling

```bash
# Using dotnet-gcdump
dotnet gcdump collect --process-id <PID>

# Analyze with PerfView
PerfView.exe /GCOnly <dumpfile.gcdump>
```

### Allocation Profiling

```bash
# Using dotnet-trace with allocation tracking
dotnet trace collect --process-id <PID> \
  --providers Microsoft-Windows-DotNETRuntime:0xC000000000000001

# Analyze with PerfView or speedscope
```

## Best Practices

1. **Run benchmarks in Release mode** - Debug mode has different performance characteristics
2. **Warm up the system** - Run a few iterations before measuring
3. **Minimize background processes** - Close unnecessary applications
4. **Use consistent hardware** - Compare results on the same machine
5. **Test with realistic data** - Use production-like datasets
6. **Measure both throughput and latency** - Both matter for user experience
7. **Track trends over time** - Single measurements can be misleading
8. **Document changes** - Note what changed when performance improves/degrades

## Troubleshooting

### Benchmarks Running Slowly

- Ensure running in Release mode: `-c Release`
- Check for background processes consuming CPU
- Verify sufficient memory available
- Check for thermal throttling on laptops

### Inconsistent Results

- High StdDev indicates inconsistency
- Try increasing iteration count: `--iterationCount 20`
- Check for background services/cron jobs
- Ensure stable network for remote resource benchmarks

### Out of Memory

- Reduce dataset size: `--params FeatureCount=1000`
- Run specific benchmarks instead of full suite
- Increase available memory
- Check for memory leaks in benchmarked code

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [.NET Performance Best Practices](https://docs.microsoft.com/en-us/dotnet/core/performance/)
- [Honua Performance Baselines](../benchmarks/baseline/)
- [GitHub Actions Benchmark Workflow](../.github/workflows/benchmarks.yml)

## Contributing

When adding new benchmarks:

1. Follow existing naming conventions
2. Add `[MemoryDiagnoser]` attribute
3. Use parameterized tests for variations
4. Document expected performance targets
5. Update this documentation
6. Run benchmarks locally before committing
7. Include baseline results in PR

## Support

For questions about benchmarks:
- Open an issue with the `performance` label
- Check existing benchmark discussions
- Review previous performance optimization PRs
