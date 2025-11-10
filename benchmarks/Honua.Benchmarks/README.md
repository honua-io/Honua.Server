# Honua Performance Benchmarks

This project contains comprehensive performance benchmarks for the Honua geospatial server using [BenchmarkDotNet](https://benchmarkdotnet.org/).

## Overview

The benchmark suite covers critical performance areas:

- **Geometry Operations**: NetTopologySuite spatial operations (intersection, union, buffer, contains, etc.)
- **Spatial Indexing**: STRtree and Quadtree index creation and query performance
- **Caching**: Memory cache hit/miss ratios and eviction strategies
- **GeoJSON Serialization**: Serialization and deserialization of various geometry types

## Prerequisites

- .NET 9.0 SDK or later
- Recommended: Run on a dedicated machine or in a controlled environment for consistent results
- Administrator/root privileges (required for some BenchmarkDotNet diagnostics)

## Running Benchmarks

### Run All Benchmarks

```bash
cd benchmarks/Honua.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark Class

```bash
# Run only geometry operations benchmarks
dotnet run -c Release --filter *GeometryOperationsBenchmarks*

# Run only spatial indexing benchmarks
dotnet run -c Release --filter *SpatialIndexBenchmarks*

# Run only caching benchmarks
dotnet run -c Release --filter *CachingBenchmarks*

# Run only GeoJSON serialization benchmarks
dotnet run -c Release --filter *GeoJsonSerializationBenchmarks*
```

### Run Specific Benchmark Method

```bash
# Run a specific benchmark method
dotnet run -c Release --filter *GeometryOperationsBenchmarks.Intersection_Large*
```

### Export Results

BenchmarkDotNet automatically exports results to the `BenchmarkDotNet.Artifacts` directory in multiple formats:

- **Markdown**: GitHub-flavored markdown tables
- **HTML**: Interactive HTML reports
- **JSON**: Machine-readable results for CI/CD integration

## Benchmark Categories

### 1. Geometry Operations (`GeometryOperationsBenchmarks.cs`)

Tests NetTopologySuite spatial operations with varying complexity:

- **Intersection, Union, Difference**: Polygon overlay operations
- **Buffer**: Point, line, and polygon buffering
- **Contains/Intersects**: Spatial relationship tests
- **ConvexHull**: Computational geometry algorithms
- **Simplification**: Douglas-Peucker simplification
- **Distance Calculations**: Point-to-polygon and polygon-to-polygon distances

**Dataset Sizes**:
- Small: 10 vertices
- Medium: 100 vertices
- Large: 1,000 vertices

### 2. Spatial Indexing (`SpatialIndexBenchmarks.cs`)

Compares spatial index structures for feature queries:

- **Index Creation**: Build time for STRtree vs Quadtree
- **Query Performance**: Envelope queries with various search areas
- **Nearest Neighbor**: K-nearest neighbor searches
- **Linear vs Indexed**: Comparison of brute-force vs spatial index queries

**Dataset Sizes**:
- Small: 100 features
- Medium: 1,000 features
- Large: 10,000 features

### 3. Caching (`CachingBenchmarks.cs`)

Evaluates caching strategies and implementations:

- **Cache Implementations**: MemoryCache, ConcurrentDictionary, Dictionary
- **Hit Ratios**: Parameterized tests with 20%, 50%, 80% hit rates
- **Eviction Policies**: Memory limits and manual eviction
- **Get/Set Performance**: Read and write operations
- **GetOrCreate Pattern**: Cache-aside pattern performance

**Parameters**:
- `HitRatio`: 0.2, 0.5, 0.8 (cache hit probability)

### 4. GeoJSON Serialization (`GeoJsonSerializationBenchmarks.cs`)

Measures GeoJSON serialization/deserialization performance:

- **Geometry Types**: Point, LineString, Polygon, MultiPolygon, GeometryCollection
- **Feature Collections**: Bulk serialization with 1, 100, 1,000 features
- **Round-trip**: Serialize + deserialize operations
- **String Building**: StringBuilder vs string concatenation

**Parameters**:
- `FeatureCount`: 1, 100, 1,000 (features in collection)

## Understanding Results

### Key Metrics

- **Mean**: Average execution time
- **Error**: Standard error (99.9% confidence interval)
- **StdDev**: Standard deviation of measurements
- **Median**: Middle value of all measurements
- **P95/P99**: 95th and 99th percentile latencies
- **Gen0/Gen1/Gen2**: Garbage collection counts
- **Allocated**: Memory allocated per operation

### Example Output

```
BenchmarkDotNet v0.14.0, Windows 11
AMD EPYC 9654 96-Core Processor, 2 CPU, 192 logical and 96 physical cores

| Method                    | Mean       | Error    | StdDev   | Median     | Allocated |
|-------------------------- |-----------:|---------:|---------:|-----------:|----------:|
| Intersection_Small        |   1.234 μs | 0.024 μs | 0.022 μs |   1.235 μs |   1.56 KB |
| Intersection_Medium       |  12.456 μs | 0.234 μs | 0.219 μs |  12.450 μs |  15.23 KB |
| Intersection_Large        | 124.567 μs | 2.345 μs | 2.193 μs | 124.500 μs | 152.34 KB |
```

### Interpreting Results

- **Faster is Better**: Lower Mean, Median, and percentile values indicate better performance
- **Consistency**: Lower StdDev and Error indicate more predictable performance
- **Memory**: Lower Allocated values reduce GC pressure and improve throughput
- **GC**: Fewer Gen0/Gen1/Gen2 collections improve responsiveness

## Performance Quick Reference

### Expected Performance Ranges

| Operation | Size | Expected Time | Status |
|-----------|------|---|---|
| **Geometry Intersection** | 10 vertices | < 2 μs | ✅ |
| **Geometry Intersection** | 100 vertices | < 20 μs | ✅ |
| **Geometry Intersection** | 1000 vertices | < 150 μs | ✅ |
| **Geometry Union** | 10 vertices | < 2 μs | ✅ |
| **Geometry Union** | 100 vertices | < 20 μs | ✅ |
| **Geometry Union** | 1000 vertices | < 150 μs | ✅ |
| **Geometry Buffer** | Point | < 1 μs | ✅ |
| **Geometry Buffer** | LineString | < 250 μs | ✅ |
| **Geometry Buffer** | Polygon (1000 v) | < 250 μs | ✅ |
| **Spatial Index Creation** | 100 features | < 0.5 ms | ✅ |
| **Spatial Index Creation** | 1000 features | < 5 ms | ✅ |
| **Spatial Index Creation** | 10000 features | < 40 ms | ✅ |
| **Spatial Index Query** | Any size | < 2 μs | ✅ 36x vs linear |
| **Cache Get (80% hit)** | N/A | < 20 μs | ✅ |
| **Cache Get (50% hit)** | N/A | < 20 μs | ✅ |
| **Cache Set** | 1000 items | < 4 ms | ✅ |
| **GeoJSON Serialize** | Point | < 2 μs | ✅ |
| **GeoJSON Serialize** | Polygon | < 15 μs | ✅ |
| **GeoJSON Serialize** | 100 features | < 25 ms | ✅ |
| **GeoJSON Serialize** | 1000 features | < 250 ms | ✅ |

### Key Performance Insights

#### 1. Geometry Operations
- **Topological tests scale well**: Contains/Intersects < 2 μs regardless of complexity
- **Overlay operations scale with complexity**: 10x slower for 10x vertices
- **Buffer operations are expensive**: 100-250 μs for complex geometries
- **Recommendation**: Use spatial indexes to filter before expensive operations

#### 2. Spatial Indexing
- **STRtree 30% faster than Quadtree**
- **Query speedup: 36x vs linear search** (10K features)
- **Index breakeven: ~20 queries**
- **Recommendation**: Always use index for > 1000 features

#### 3. Caching
- **ConcurrentDictionary fastest** (~12 μs)
- **Hit ratio directly impacts performance** (4-5 μs difference)
- **Recommendation**: Target > 70% hit rate

#### 4. Serialization
- **StringBuilder 2x faster than concatenation**
- **Throughput: ~2,900 features/sec**
- **Recommendation**: Pre-allocate StringBuilder with size estimate

### Representative Benchmark Runs

```bash
# Quick validation (2-3 minutes)
dotnet run -c Release --filter *Intersection_Small*

# Geometry operations (5-10 minutes)
dotnet run -c Release --filter "*GeometryOperationsBenchmarks*" --filter "*_Small*|*_Medium*"

# Spatial indexing (10-15 minutes)
dotnet run -c Release --filter "*SpatialIndexBenchmarks*" --filter "*_Small*|*_Medium*"

# Caching at 50% hit rate (5-10 minutes)
dotnet run -c Release --filter "*CachingBenchmarks*" --filter "*VariableHitRatio*"

# GeoJSON with 100 features (5-10 minutes)
dotnet run -c Release --filter "*GeoJsonSerializationBenchmarks*" --filter "*100*"

# Full benchmark suite (30-45 minutes)
dotnet run -c Release
```

### Baseline Results

See **[BASELINE-RESULTS.md](BASELINE-RESULTS.md)** for:
- Complete baseline measurements
- Detailed performance analysis
- Optimization recommendations
- SLA targets and thresholds
- How to detect performance regressions

## Best Practices

### Running Reliable Benchmarks

1. **Release Configuration**: Always use `-c Release` to enable optimizations
2. **Close Applications**: Close unnecessary applications and background processes
3. **Consistent Environment**: Run on the same machine with similar system load
4. **Multiple Runs**: Run benchmarks multiple times to verify consistency
5. **Warm-up**: BenchmarkDotNet automatically includes warm-up iterations

### Avoiding Common Pitfalls

- ❌ **Don't** run in Debug mode
- ❌ **Don't** run while debugging or profiling
- ❌ **Don't** run in a VM if comparing against bare metal results
- ❌ **Don't** compare results across different machines
- ✅ **Do** run with administrator/root privileges for accurate diagnostics
- ✅ **Do** use the same .NET runtime version for comparisons
- ✅ **Do** commit baseline results to track performance over time

## Adding New Benchmarks

### 1. Create a New Benchmark Class

```csharp
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class MyNewBenchmarks
{
    private object _testData = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize test data once
        _testData = CreateTestData();
    }

    [Benchmark]
    public void MyBenchmarkMethod()
    {
        // Code to benchmark
    }
}
```

### 2. Key Attributes

- `[MemoryDiagnoser]`: Enables memory allocation tracking
- `[GlobalSetup]`: Runs once before all benchmarks
- `[Benchmark]`: Marks a method as a benchmark
- `[Params(...)]`: Parameterizes benchmarks with multiple values
- `[Arguments(...)]`: Passes arguments to benchmark methods

### 3. Best Practices for New Benchmarks

- Use realistic data sizes (1, 100, 1,000, 10,000)
- Include setup/teardown with `[GlobalSetup]`/`[GlobalCleanup]`
- Avoid I/O operations in benchmark methods (mock or setup data beforehand)
- Return results from benchmark methods to prevent dead code elimination
- Use `[Params]` to test multiple scenarios in one benchmark

## Baseline Results

Baseline results are stored in `BenchmarkDotNet.Artifacts/results/` and should be committed to version control.

### Creating a Baseline

```bash
# Run benchmarks and save results
dotnet run -c Release

# Results are saved to BenchmarkDotNet.Artifacts/results/
# Commit the markdown files to track performance over time
git add BenchmarkDotNet.Artifacts/results/*.md
git commit -m "Add baseline benchmark results"
```

### Comparing with Baseline

```bash
# Run benchmarks and compare with previous results
dotnet run -c Release

# View the difference in generated reports
```

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Performance Benchmarks

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 9.0.x
      - name: Run Benchmarks
        run: |
          cd benchmarks/Honua.Benchmarks
          dotnet run -c Release --filter *GeometryOperationsBenchmarks*
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results
          path: benchmarks/Honua.Benchmarks/BenchmarkDotNet.Artifacts/results/
```

### Azure Pipelines Example

```yaml
- task: DotNetCoreCLI@2
  displayName: 'Run Performance Benchmarks'
  inputs:
    command: 'run'
    projects: 'benchmarks/Honua.Benchmarks/Honua.Benchmarks.csproj'
    arguments: '-c Release'

- task: PublishBuildArtifacts@1
  displayName: 'Publish Benchmark Results'
  inputs:
    pathToPublish: 'benchmarks/Honua.Benchmarks/BenchmarkDotNet.Artifacts/results'
    artifactName: 'benchmark-results'
```

## Performance Optimization Guidelines

### When to Optimize

1. **Identify Hotspots**: Use benchmarks to identify performance bottlenecks
2. **Set Targets**: Define acceptable performance thresholds
3. **Measure Impact**: Re-run benchmarks after optimizations
4. **Track Regressions**: Monitor benchmarks in CI/CD to catch regressions early

### Common Optimizations

- **Spatial Operations**: Use spatial indexes for large datasets
- **Geometry Simplification**: Reduce vertex count before expensive operations
- **Caching**: Cache frequently accessed geometries and query results
- **Batch Operations**: Process features in batches to amortize overhead
- **Memory Pooling**: Reuse buffers and objects to reduce allocations

## Troubleshooting

### Issue: Benchmarks take too long

**Solution**: Run specific benchmarks instead of the full suite:
```bash
dotnet run -c Release --filter *Intersection_Small*
```

### Issue: Results are inconsistent

**Solution**:
- Ensure no other applications are running
- Run with elevated privileges
- Increase iteration count: Add `[SimpleJob(iterationCount: 20)]` attribute

### Issue: Out of memory

**Solution**:
- Reduce dataset sizes in benchmark parameters
- Run benchmarks one class at a time
- Increase system memory or use a machine with more RAM

## Resources

- [BenchmarkDotNet Documentation](https://benchmarkdotnet.org/)
- [NetTopologySuite Documentation](https://nettopologysuite.github.io/NetTopologySuite/)
- [Honua Server Documentation](../../README.md)

## Contributing

When adding benchmarks:

1. Follow existing naming conventions
2. Include memory diagnostics with `[MemoryDiagnoser]`
3. Test with realistic data sizes
4. Document what the benchmark measures
5. Add the benchmark category to this README

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
