# Honua Performance Baselines

This document establishes performance baselines and SLOs (Service Level Objectives) for the Honua geospatial server.

## Service Level Objectives (SLOs)

### Availability SLO
- **Target**: 99.9% availability (43.2 minutes downtime per month)
- **Measurement**: Success rate of API requests over 30-day rolling window
- **Definition**: Request is successful if HTTP status code < 500

### Latency SLO
- **Target**: 95% of requests complete within 2000ms
- **Measurement**: P95 latency of all API requests over 30-day rolling window
- **Critical Threshold**: P95 > 5000ms indicates critical performance degradation

### Raster Cache SLO
- **Target**: 70% cache hit rate for raster tiles
- **Measurement**: Ratio of cache hits to total cache operations
- **Optimal**: > 90% hit rate indicates well-tuned cache

## Performance Baselines by API Protocol

### WFS (Web Feature Service)
- **Expected P50 Latency**: 100-300ms
- **Expected P95 Latency**: 500-1000ms
- **Expected P99 Latency**: 1000-2000ms
- **Typical Request Rate**: 10-50 req/s
- **Error Rate Threshold**: < 1%

**Baseline Assumptions:**
- Simple feature queries (< 1000 features)
- Indexed spatial queries
- Database response time < 50ms

### WMS (Web Map Service)
- **Expected P50 Latency**: 200-500ms
- **Expected P95 Latency**: 1000-2000ms
- **Expected P99 Latency**: 2000-4000ms
- **Typical Request Rate**: 50-200 req/s (tile-based)
- **Error Rate Threshold**: < 0.5%

**Baseline Assumptions:**
- 256x256 or 512x512 tiles
- Raster cache hit rate > 70%
- Uncached tile render time < 500ms

### WMTS (Web Map Tile Service)
- **Expected P50 Latency**: 10-50ms (cached), 200-500ms (uncached)
- **Expected P95 Latency**: 100ms (cached), 1000ms (uncached)
- **Expected P99 Latency**: 200ms (cached), 2000ms (uncached)
- **Typical Request Rate**: 100-500 req/s
- **Error Rate Threshold**: < 0.1%

**Baseline Assumptions:**
- High cache hit rate (> 90%)
- Pre-seeded tile cache for popular zoom levels
- Direct file serving for cached tiles

### OGC API Features
- **Expected P50 Latency**: 150-400ms
- **Expected P95 Latency**: 600-1200ms
- **Expected P99 Latency**: 1200-2500ms
- **Typical Request Rate**: 20-100 req/s
- **Error Rate Threshold**: < 1%

**Baseline Assumptions:**
- Pagination with max 1000 features per page
- GeoJSON output format
- Spatial and attribute filtering

### STAC (SpatioTemporal Asset Catalog)
- **Expected P50 Latency**: 50-200ms
- **Expected P95 Latency**: 300-600ms
- **Expected P99 Latency**: 600-1000ms
- **Typical Request Rate**: 5-50 req/s
- **Error Rate Threshold**: < 0.5%

**Baseline Assumptions:**
- Catalog queries with < 1000 items
- Static catalog (infrequent updates)
- JSON output format

## Resource Baselines

### Memory Usage
- **Baseline**: 2-4 GB resident memory
- **Warning Threshold**: 8 GB
- **Critical Threshold**: 12 GB
- **Expected Growth**: ~100-200 MB per active dataset

**Factors Affecting Memory:**
- Number of active datasets
- Raster cache size configuration
- Query result caching
- Database connection pooling

### CPU Usage
- **Baseline**: 10-30% average utilization
- **Peak**: 40-60% during tile rendering bursts
- **Warning Threshold**: 80% sustained for > 10 minutes
- **Critical Threshold**: 95% sustained for > 5 minutes

**CPU-Intensive Operations:**
- On-demand raster tile rendering
- COG/Zarr data decompression
- Spatial query processing
- Coordinate transformations

### Disk I/O
- **Read IOPS**: 100-500 IOPS (typical)
- **Read Throughput**: 50-200 MB/s
- **Write IOPS**: 10-50 IOPS (cache writes, logging)
- **Write Throughput**: 10-50 MB/s

**High I/O Scenarios:**
- Cold cache startup
- Large dataset ingestion
- Raster tile pre-seeding
- Database queries with large result sets

### Network I/O
- **Inbound**: 10-50 Mbps (typical)
- **Outbound**: 50-200 Mbps (typical for tile serving)
- **Peak Outbound**: 500-1000 Mbps (burst for raster tiles)

## Raster Cache Performance

### Cache Hit Rate Expectations
- **Cold Start**: 0-20% (first hour)
- **Warm Cache**: 70-85% (after 4 hours)
- **Hot Cache**: 85-95% (after 24 hours)

### Tile Render Performance
- **COG Tile Render (uncached)**: 100-500ms
- **Zarr Chunk Read (uncached)**: 50-200ms
- **Cached Tile Serve**: 1-10ms

### Pre-seed Job Performance
- **Tiles per Second**: 5-20 tiles/s (depends on format and complexity)
- **Expected Job Duration**:
  - Small dataset (z0-z12): 1-5 minutes
  - Medium dataset (z0-z14): 10-30 minutes
  - Large dataset (z0-z16): 1-4 hours

## Database Performance

### Query Latency
- **Simple Point Query**: < 10ms
- **Spatial Intersection (< 100 features)**: 10-50ms
- **Spatial Intersection (100-1000 features)**: 50-200ms
- **Complex Spatial Join**: 200-1000ms

### Connection Pool
- **Minimum Connections**: 5
- **Maximum Connections**: 50
- **Typical Active Connections**: 10-20

## Error Rate Baselines

### Acceptable Error Rates by Category
- **Validation Errors (4xx)**: < 5% (user input errors)
- **Server Errors (5xx)**: < 0.1% (system failures)
- **Database Errors**: < 0.05%
- **Timeout Errors**: < 0.5%
- **Authentication Errors**: < 2% (includes failed login attempts)

## Load Testing Benchmarks

### Standard Load Profile
- **Concurrent Users**: 100
- **Request Rate**: 200 req/s
- **Test Duration**: 30 minutes
- **Expected P95 Latency**: < 1500ms
- **Expected Error Rate**: < 0.5%

### Peak Load Profile
- **Concurrent Users**: 500
- **Request Rate**: 1000 req/s
- **Test Duration**: 10 minutes
- **Expected P95 Latency**: < 3000ms
- **Expected Error Rate**: < 2%

### Stress Test Thresholds
- **Breaking Point**: 2000+ req/s
- **Expected Degradation**: Graceful (queue requests, return 429)
- **Recovery Time**: < 5 minutes after load reduction

## Monitoring Recommendations

### Critical Metrics (P0 - Page Immediately)
1. Service availability < 99%
2. P95 latency > 5000ms
3. Error rate > 5%
4. Memory usage > 12 GB
5. Out of memory errors

### Important Metrics (P1 - Alert During Business Hours)
1. P95 latency > 2000ms
2. Error rate > 1%
3. Cache hit rate < 70%
4. CPU usage > 80%
5. Database connection pool exhaustion

### Warning Metrics (P2 - Review Daily)
1. P95 latency > 1000ms
2. Error rate > 0.5%
3. Cache hit rate < 85%
4. Memory growth rate > 100 MB/hour
5. Disk usage > 80%

## Capacity Planning Guidelines

### Scaling Triggers
- **Horizontal Scaling**: Trigger when sustained > 60% CPU or > 500 req/s per instance
- **Vertical Scaling**: Trigger when memory usage consistently > 75% of available
- **Database Scaling**: Trigger when query latency P95 > 100ms or connection pool > 80% utilized

### Expected Capacity per Instance
- **Small Instance** (2 vCPU, 4 GB RAM): 100-200 req/s
- **Medium Instance** (4 vCPU, 8 GB RAM): 200-500 req/s
- **Large Instance** (8 vCPU, 16 GB RAM): 500-1000 req/s

### Dataset Capacity
- **Datasets per Instance**: 10-50 (depending on size and access patterns)
- **Total Raster Size**: Up to 1 TB per instance (with appropriate cache configuration)
- **Vector Features**: Up to 10M features per dataset (with spatial indexing)

## Baseline Revision Schedule

- **Initial Baselines**: Set during initial deployment with 1 week of production traffic
- **Review Frequency**: Monthly for first 6 months, then quarterly
- **Major Revision Triggers**:
  - Infrastructure changes (new hardware, cloud migration)
  - Major version upgrades
  - Significant feature additions
  - Sustained 30-day SLO breach

## Performance Testing Commands

### Load Testing with k6
```bash
# Standard load test
k6 run --vus 100 --duration 30m tests/performance/standard-load.js

# Peak load test
k6 run --vus 500 --duration 10m tests/performance/peak-load.js

# Ramp-up stress test
k6 run --vus 1000 --duration 20m --rps 2000 tests/performance/stress-test.js
```

### Benchmarking with BenchmarkDotNet
```bash
# Run all raster benchmarks
dotnet run --project benchmarks/Honua.Benchmarks -c Release -- --filter *Raster*

# Run reader comparison benchmarks
dotnet run --project benchmarks/Honua.Benchmarks -c Release -- --filter *ReaderComparison*
```

### Database Query Performance
```bash
# Run query profiling
honua benchmark db --dataset test-dataset --iterations 1000

# Analyze slow queries
honua admin db slow-queries --threshold 100ms --last 24h
```

## Related Documentation

- [Grafana Dashboards](./grafana-dashboards.md)
- [Alert Runbooks](./alert-runbooks.md)
- [Monitoring Architecture](./monitoring-architecture.md)
- [Performance Tuning Guide](./performance-tuning.md)
