# Quick Start: Running Tests

This is a quick reference for running tests efficiently in HonuaIO.

## TL;DR - Just Run Tests

```bash
# The simplest way - handles everything automatically
./scripts/test-all.sh

# Or with dotnet test directly (after building)
dotnet build -c Release
dotnet test --no-build
```

## The Problem We Solved

**Before**: Tests were slow, building multiple times, with race conditions and failures.

**Now**: Clean once → Build once → Test in parallel = Fast & Reliable

## Three Ways to Run Tests

### 1. Recommended: Use the unified script

```bash
# Full workflow (clean + build + test)
./scripts/test-all.sh

# Fast iterations (skip clean)
./scripts/test-all.sh --skip-clean

# Just unit tests
./scripts/test-all.sh --filter "Category=Unit"

# Fewer parallel threads (more stable, avoids Docker issues)
./scripts/test-all.sh --max-threads 2
```

### 2. Use dotnet test directly

```bash
# Step 1: Build ONCE (important!)
dotnet build -c Release

# Step 2: Run tests (no rebuild)
dotnet test --no-build

# With filter
dotnet test --no-build --filter "Category!=STAC"
```

### 3. Use the parallel script

```bash
# Build first
dotnet build -c Release

# Run with custom threading
./scripts/run-tests-csharp-parallel.sh --no-build --max-threads 4
```

## Common Scenarios

### I just want to run all tests
```bash
./scripts/test-all.sh
```

### I'm iterating quickly and don't want to clean
```bash
./scripts/test-all.sh --skip-clean
```

### PostgreSQL containers are failing
```bash
# Reduce parallel threads to avoid Docker resource exhaustion
./scripts/test-all.sh --max-threads 2
```

### I don't have STAC schema set up
```bash
./scripts/test-all.sh --filter "Category!=STAC"
```

### I want to debug a specific test
```bash
dotnet build
dotnet test tests/Honua.Server.Core.Tests.Data \
  --no-build \
  --filter "FullyQualifiedName~MySpecificTest" \
  --logger "console;verbosity=detailed"
```

### I want code coverage
```bash
./scripts/test-all.sh --coverage
# Results in: TestResults/CoverageReport/index.html
```

## Key Rules

### ✅ DO

1. **Build before testing**: Always run `dotnet build` before `dotnet test --no-build`
2. **Use --no-build**: Once built, always use `--no-build` flag
3. **Use filters**: Skip tests you don't need with `--filter`
4. **Adjust parallelism**: Use `--max-threads` to tune for your system

### ❌ DON'T

1. **Don't run `dotnet test` without `--no-build`**: Causes race conditions
2. **Don't build during parallel testing**: File locking issues
3. **Don't use too many threads**: Overwhelms Docker (default is 4, safe for most systems)

## Configuration Files

- **tests/.runsettings** - Used by `dotnet test` automatically
- **tests/xunit.runner.json** - xUnit parallel settings (maxParallelThreads: 4)
- **tests/appsettings.Test.json** - Test environment config (QuickStart enabled)

## Environment Variables

The scripts and .runsettings automatically set these:

```bash
ASPNETCORE_ENVIRONMENT=Test
HONUA_ALLOW_QUICKSTART=true
honua__authentication__allowQuickStart=true
MaxParallelThreads=4
```

You don't need to set them manually.

## Troubleshooting

### "PostgreSQL test container is not available"

**Fix**: Reduce threads or skip integration tests
```bash
./scripts/test-all.sh --max-threads 2
# OR
./scripts/test-all.sh --filter "Category!=Integration"
```

### "QuickStart authentication mode is disabled"

**Fix**: Make sure ASPNETCORE_ENVIRONMENT=Test is set
```bash
export ASPNETCORE_ENVIRONMENT=Test
dotnet test --no-build
```

### Tests are slow

**Fix**: Use Release build and filters
```bash
dotnet build -c Release  # 30% faster than Debug
dotnet test --no-build --filter "Category=Unit"  # Run only fast tests
```

### File locking errors

**Fix**: Always use --no-build
```bash
dotnet build first
dotnet test --no-build
```

## Performance

With a 22-core system:

| What | Time | Command |
|------|------|---------|
| All tests (cold) | 3-5 min | `./scripts/test-all.sh` |
| All tests (warm) | 2-3 min | `./scripts/test-all.sh --skip-clean` |
| Unit tests only | 30-60 sec | `./scripts/test-all.sh --filter "Category=Unit"` |

## More Info

For detailed documentation, see:
- **README.TESTING.md** - Complete testing guide
- **PARALLEL_TESTING_SUMMARY.md** - Parallel test infrastructure details
- **REMAINING_TEST_FAILURES.md** - Known issues and workarounds
