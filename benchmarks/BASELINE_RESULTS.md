# Baseline Performance Results

Initial baseline performance metrics for Honua Server.

## Environment

- **Date**: To be established on first run
- **Hardware**: GitHub Actions Standard Runner
- **OS**: Ubuntu Latest
- **Runtime**: .NET 9.0
- **Build**: Release

## How to Establish Baseline

Run the following command to establish initial baseline:

```bash
./scripts/run-benchmarks-baseline.sh
```

This will:
1. Run all benchmarks
2. Save results to `benchmarks/baseline/`
3. Generate this summary

## Expected Performance Characteristics

Based on preliminary testing and industry standards:

### Database Query Benchmarks

| Operation | Expected Mean | Expected Allocation |
|-----------|---------------|---------------------|
| Query all (10K features) | 150-200ms | ~20MB |
| Single feature lookup | 3-5ms | ~10KB |
| Spatial bbox query | 80-120ms | ~15MB |
| Attribute filter | 100-150ms | ~18MB |
| Pagination (100 features) | 30-50ms | ~2MB |

### Raster Processing Benchmarks

| Operation | Expected Mean | Expected Allocation |
|-----------|---------------|---------------------|
| COG tile (256x256) | 30-50ms | ~512KB |
| COG tile (512x512) | 70-100ms | ~1.5MB |
| COG tile (1024x1024) | 150-200ms | ~4MB |
| PNG encode (512x512) | 40-60ms | ~1MB |
| JPEG encode (512x512) | 30-50ms | ~800KB |
| WebP encode (512x512) | 50-70ms | ~1.2MB |

### Vector Tile Benchmarks

| Operation | Expected Mean | Expected Allocation |
|-----------|---------------|---------------------|
| MVT encode (100 features) | 5-10ms | ~500KB |
| MVT encode (1K features) | 15-25ms | ~3MB |
| MVT encode (10K features) | 150-250ms | ~25MB |
| Simplification (1K features) | 20-40ms | ~2MB |
| GeoJSON serialize (1K features) | 40-60ms | ~5MB |

### API Endpoint Benchmarks

| Operation | Expected Mean | Expected Allocation |
|-----------|---------------|---------------------|
| OGC API collection (1K features) | 80-120ms | ~10MB |
| WMS GetCapabilities | 30-50ms | ~500KB |
| WMTS GetTile parsing | 1-2ms | ~5KB |
| STAC search (100 results) | 60-90ms | ~5MB |
| JWT validation | 0.5-1ms | ~1KB |

### Export Benchmarks

| Operation | Expected Mean | Expected Allocation |
|-----------|---------------|---------------------|
| CSV (1K features) | 80-120ms | ~8MB |
| CSV (10K features) | 800-1200ms | ~80MB |
| Shapefile (1K features) | 150-250ms | ~15MB |
| Shapefile (10K features) | 1500-2500ms | ~150MB |
| GeoPackage (1K features) | 120-180ms | ~12MB |
| GeoPackage (10K features) | 1200-1800ms | ~120MB |

## Performance Goals

### Short-term (3 months)

- Establish stable baseline
- Reduce memory allocations by 20%
- Optimize hot paths (database queries, tile encoding)
- Implement caching strategies

### Medium-term (6 months)

- 30% improvement in database query performance
- 25% improvement in raster encoding performance
- Reduce GC pressure (fewer Gen1/Gen2 collections)
- Optimize vector tile generation

### Long-term (12 months)

- 50% overall performance improvement
- Zero-allocation hot paths where possible
- Distributed caching implementation
- Horizontal scaling optimizations

## Optimization Opportunities

Initial profiling suggests these areas for optimization:

1. **Database Queries**
   - Implement query result caching
   - Optimize spatial index usage
   - Use compiled queries for hot paths

2. **Raster Processing**
   - Implement tile caching (memory + distributed)
   - Use ArrayPool for buffer management
   - Parallel tile generation

3. **Vector Tiles**
   - Use database-native MVT generation (PostGIS ST_AsMVT)
   - Implement aggressive overzooming
   - Cache pre-simplified geometries

4. **API Responses**
   - Response caching (ETags, conditional requests)
   - Compression (Brotli/Gzip)
   - Streaming for large responses

5. **General**
   - Reduce allocations in hot paths
   - Use Span<T> and Memory<T>
   - Implement object pooling
   - Async/await optimization

## Next Steps

1. Run baseline benchmarks: `./scripts/run-benchmarks-baseline.sh`
2. Review results and identify bottlenecks
3. Profile hot paths with dotnet-trace
4. Implement optimizations
5. Re-run benchmarks to measure impact
6. Update baseline after significant changes

## Monitoring

Benchmarks are automatically run:
- On every PR (critical benchmarks only)
- On main branch commits (full suite)
- Nightly (full suite with trending)

Results are tracked in:
- GitHub Actions artifacts
- `benchmarks/baseline/` directory
- Performance trend graphs (coming soon)

## Contributing

When making performance-critical changes:
1. Run benchmarks before changes
2. Implement optimizations
3. Run benchmarks after changes
4. Document improvements in PR
5. Update baseline if significant change

Target: >10% improvement or <5% regression
