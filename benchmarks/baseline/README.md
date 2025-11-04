# Performance Baselines

This directory contains baseline performance metrics for Honua.

## Structure

```
baseline/
├── README.md                    # This file
├── latest.json                  # Symlink to most recent baseline
├── baseline-YYYYMMDD-HHMMSS.json # Historical baselines
├── baseline-YYYYMMDD-HHMMSS.md   # Markdown summaries
└── baseline-YYYYMMDD-HHMMSS-summary.txt # System info
```

## Usage

### View Latest Baseline

```bash
cat benchmarks/baseline/latest.json | jq '.Benchmarks[] | {Name: .FullName, Mean: .Statistics.Mean}'
```

### Establish New Baseline

```bash
./scripts/run-benchmarks-baseline.sh
```

This will:
1. Run all benchmarks in Release mode
2. Save results with timestamp
3. Update `latest.json` symlink
4. Generate summary report

### Compare Results

```bash
./scripts/compare-benchmark-results.sh \
  benchmarks/baseline/latest.json \
  tests/Honua.Server.Benchmarks/BenchmarkDotNet.Artifacts/results/latest.json
```

## Baseline History

Baselines are created:
- **On major releases** - Establish performance characteristics for the release
- **After significant optimizations** - Capture improvements
- **Monthly** - Track long-term trends

## Performance Targets

### Database Queries (10K features)

| Operation | Target | Notes |
|-----------|--------|-------|
| Query all | <200ms | Full table scan |
| Single feature | <5ms | ID-based lookup |
| Spatial bbox | <100ms | With spatial index |
| Pagination | <50ms | First page only |

### Raster Processing (512x512 tile)

| Operation | Target | Notes |
|-----------|--------|-------|
| COG read | <100ms | Local file |
| PNG encode | <50ms | Default compression |
| JPEG encode | <40ms | Quality 85 |
| WebP encode | <60ms | Lossless |

### Vector Tiles (1K features)

| Operation | Target | Notes |
|-----------|--------|-------|
| MVT encode | <20ms | Including serialization |
| Simplification | <30ms | Douglas-Peucker |
| GeoJSON serialize | <50ms | Full feature collection |

### API Responses

| Operation | Target | Notes |
|-----------|--------|-------|
| OGC API collection | <100ms | 1K features |
| WMS GetCapabilities | <50ms | 100 layers |
| STAC search | <75ms | 100 results |
| JWT validation | <1ms | Per request |

## Regression Thresholds

- **Critical** (>10% slower): ❌ Block merge
- **Warning** (>20% slower): ⚠️ Requires review
- **Minor** (>30% slower): 📊 Track but allow

## Baseline Retention

- Latest baseline: Permanent
- Release baselines: Permanent
- Monthly baselines: 1 year
- Development baselines: 90 days

## Notes

- All benchmarks run in Release mode
- Results from CI environment (GitHub Actions runners)
- Local results may vary due to hardware differences
- Compare against same hardware for accurate regression detection
