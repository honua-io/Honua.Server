# Honua Benchmarks - Quick Start Guide

This guide will help you get started with running and analyzing Honua performance benchmarks.

## Prerequisites

- .NET 9.0 SDK installed
- 5-10 minutes for initial benchmark run
- Release configuration (Debug mode is not supported)

## Quick Start (5 minutes)

### 1. Navigate to Benchmarks

```bash
cd benchmarks/Honua.Benchmarks
```

### 2. Run a Quick Test

Run a single fast benchmark to verify everything works:

```bash
dotnet run -c Release --filter *Intersection_Small*
```

Expected output:
```
BenchmarkDotNet v0.14.0
...
| Method              | Mean     | Error    | StdDev   | Allocated |
|-------------------- |---------:|---------:|---------:|----------:|
| Intersection_Small  | 1.234 μs | 0.024 μs | 0.022 μs |   1.56 KB |
```

### 3. Run All Benchmarks (Optional)

```bash
dotnet run -c Release
```

This will take 15-30 minutes depending on your system.

## Understanding Your First Results

### What to Look For

1. **Mean**: Average execution time (lower is better)
2. **Allocated**: Memory used (lower is better)
3. **Rank**: Relative performance (1 is fastest)

### Example Results

```
| Method                 | Mean      | Rank | Allocated |
|----------------------- |----------:|-----:|----------:|
| ConcurrentDict_Get     | 12.34 μs  |    1 |     480 B |
| MemoryCache_Get        | 15.67 μs  |    2 |   1.2 KB  |
| StandardDict_Get       | 23.45 μs  |    3 |     720 B |
```

**Interpretation**: ConcurrentDictionary is fastest, MemoryCache uses more memory but provides additional features.

## Common Use Cases

### Use Case 1: Test Specific Feature

Testing only geometry operations:

```bash
dotnet run -c Release --filter *GeometryOperations*
```

### Use Case 2: Compare Two Approaches

Run benchmarks, make changes, run again:

```bash
# Before changes
dotnet run -c Release --filter *CachingBenchmarks* > before.txt

# Make your code changes

# After changes
dotnet run -c Release --filter *CachingBenchmarks* > after.txt

# Compare
diff before.txt after.txt
```

### Use Case 3: Verify Performance Requirements

Check if operations meet SLA requirements:

```bash
dotnet run -c Release --filter *Intersection_Medium*

# Expected: < 50 μs (microseconds)
# If actual > 50 μs, investigate optimization opportunities
```

## Benchmark Categories

| Category | Command | Runtime | What It Tests |
|----------|---------|---------|---------------|
| Geometry Ops | `--filter *GeometryOperations*` | 5-10 min | Spatial operations (intersection, union, buffer) |
| Spatial Index | `--filter *SpatialIndex*` | 10-15 min | Index creation and queries |
| Caching | `--filter *Caching*` | 5-10 min | Cache hit/miss performance |
| GeoJSON | `--filter *GeoJson*` | 5-10 min | Serialization performance |

## Results Location

After running benchmarks, results are saved to:

```
BenchmarkDotNet.Artifacts/
├── results/
│   ├── Honua.Benchmarks.*.md       # Markdown tables
│   ├── Honua.Benchmarks.*.html     # Interactive HTML
│   └── Honua.Benchmarks.*.json     # Machine-readable
└── logs/
    └── *.log                        # Detailed logs
```

## Next Steps

### Learn More
- Read [README.md](README.md) for comprehensive documentation
- Review [BenchmarkResults.md](BenchmarkResults.md) for baseline comparisons
- Check [BenchmarkDotNet docs](https://benchmarkdotnet.org/) for advanced features

### Add Custom Benchmarks
1. Create new file in `Benchmarks/` directory
2. Add `[Benchmark]` attribute to methods
3. Run with `dotnet run -c Release`

### CI/CD Integration
- Benchmarks run automatically in GitHub Actions
- Check `.github/workflows/benchmarks.yml`
- Results uploaded as artifacts

## Troubleshooting

### Issue: "dotnet: command not found"

**Solution**: Install .NET 9.0 SDK from https://dotnet.microsoft.com/download

### Issue: Benchmarks take too long

**Solution**: Run specific categories or methods:
```bash
dotnet run -c Release --filter *_Small*
```

### Issue: "Must run in Release mode"

**Solution**: Always use `-c Release` flag:
```bash
dotnet run -c Release  # Correct
dotnet run             # Wrong - uses Debug mode
```

### Issue: Inconsistent results

**Solution**:
- Close other applications
- Run benchmarks 2-3 times
- Check for background processes (antivirus, updates)

## Useful Commands Cheat Sheet

```bash
# Run all benchmarks
dotnet run -c Release

# Run specific class
dotnet run -c Release --filter *CachingBenchmarks*

# Run specific method
dotnet run -c Release --filter *Intersection_Large*

# Run with specific parameters
dotnet run -c Release --filter *CachingBenchmarks* --job short

# List all benchmarks without running
dotnet run -c Release --list flat

# Run and export to CSV
dotnet run -c Release --exporters csv

# Quick job (fewer iterations, faster)
dotnet run -c Release --job short --filter *_Small*
```

## Performance Tips

### When Benchmarking Your Code

1. **Isolate**: Test one thing at a time
2. **Realistic**: Use realistic data sizes
3. **Consistent**: Run on same machine/environment
4. **Repeat**: Run multiple times to confirm
5. **Baseline**: Compare against known good performance

### Common Gotchas

❌ **Don't**:
- Run in Debug mode
- Test with unrealistic data (too small or too large)
- Compare results from different machines
- Benchmark I/O without mocking

✅ **Do**:
- Use Release mode
- Test multiple data sizes
- Run on consistent environment
- Mock external dependencies

## Example Workflow

Here's a typical workflow for optimizing code:

```bash
# 1. Run baseline benchmark
dotnet run -c Release --filter *MyFeature* > baseline.txt

# 2. Review results and identify bottleneck
cat baseline.txt

# 3. Make optimization changes in code

# 4. Run benchmark again
dotnet run -c Release --filter *MyFeature* > optimized.txt

# 5. Compare results
diff baseline.txt optimized.txt

# 6. If improved, commit changes. If worse, revert and try different approach.
```

## Getting Help

- Review full documentation: [README.md](README.md)
- Check BenchmarkDotNet docs: https://benchmarkdotnet.org/
- Open issue: https://github.com/honua-io/Honua.Server/issues

## Summary

You've learned how to:
- ✅ Run your first benchmark
- ✅ Interpret results
- ✅ Filter specific benchmarks
- ✅ Find generated reports
- ✅ Troubleshoot common issues

**Next**: Read the full [README.md](README.md) for advanced features and best practices.
