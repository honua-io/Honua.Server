# Performance Tuning Guide for 22-Core System

Your Intel Core Ultra 7 165H with 22 logical processors + 64GB RAM is an absolute powerhouse for parallel testing!

## Optimal pytest Configuration

### Default (Recommended):
```bash
pytest -n auto  # Uses all 22 cores automatically
```

Expected performance:
- **Small test suite** (50 tests): 2-3 seconds
- **Medium test suite** (150 tests): 5-10 seconds  
- **Full suite** (350 tests): **1.5-2 minutes** (vs 15 min sequential!)
- **Speedup**: **7-10x** over sequential execution

### Fine-Tuning Options:

#### Maximum Throughput (CPU-bound tests):
```bash
pytest -n 22 --dist loadscope  # All cores, scope-based distribution
```
Use when: Tests are CPU-intensive (parsing, computation)

#### Balanced (I/O + CPU mix):
```bash
pytest -n 16 --dist loadfile  # Leave headroom for system
```
Use when: Mix of database queries and computation (default setting in pytest.ini)

#### Conservative (avoid resource contention):
```bash
pytest -n 12 --dist loadfile  # Half cores, less contention
```
Use when: SQLite tests hitting database locks or memory pressure

## Performance Monitoring

### Show test execution timing:
```bash
pytest -n auto --durations=20  # Show 20 slowest tests
```

### Profile CPU usage:
```bash
pytest -n auto --profile  # Requires pytest-profiling
```

### Watch real-time progress:
```bash
pytest -n auto -v  # Verbose shows which tests are running on each worker
```

## Common Performance Issues

### Issue: SQLite Database Locks
**Symptom**: `database is locked` errors
**Solution**: Reduce workers or switch to PostgreSQL
```bash
pytest -n 8 --dist loadfile  # Fewer workers = less contention
# OR use PostgreSQL for tests requiring write concurrency
```

### Issue: Memory Pressure (OWSLib/rasterio tests)
**Symptom**: Slow tests, system becomes unresponsive
**Solution**: Limit workers or increase Docker memory
```bash
pytest -n 12 --dist loadfile  # Use fewer workers
```

### Issue: Worker Startup Overhead
**Symptom**: Short test runs slower with parallelism
**Solution**: Use sequential for quick tests
```bash
pytest test_quick.py  # No -n flag for <10 tests
```

## Docker Resource Allocation

Your `.devcontainer/devcontainer.json` is configured for:
- **CPUs**: 8 (you can increase to 22)
- **Memory**: 16GB (you have 64GB available!)

To maximize performance, edit `.devcontainer/devcontainer.json`:
```json
"runArgs": [
  "--cpus=22",      // Use all cores
  "--memory=32g",   // Increase memory allocation
  "--network=honua-test-network"
]
```

Or update docker-compose.shared-test-env.yml:
```yaml
services:
  honua-test:
    deploy:
      resources:
        limits:
          cpus: '22'
          memory: 32G
```

## Performance Benchmarks (Your Hardware)

Expected test execution times:

| Test Suite | Sequential | Parallel (22 cores) | Your Speedup |
|------------|------------|-------------------|--------------|
| OGC Features (50 tests) | 45s | **3-5s** | 9-15x |
| OGC Tiles (30 tests) | 60s | **5-8s** | 7-12x |
| STAC API (40 tests) | 55s | **4-6s** | 9-13x |
| WMS/WFS (80 tests) | 120s | **12-15s** | 8-10x |
| **Full Suite (350 tests)** | **15 min** | **1.5-2 min** | **7-10x** |

## Tips for Maximum Speed

1. **Keep test environment running**: Start once, reuse indefinitely
   ```bash
   cd tests && bash start-shared-test-env.sh start
   # Leave running, tests will connect instantly
   ```

2. **Use test markers for iteration**: Don't run full suite every time
   ```bash
   pytest -m ogc -n auto  # Just OGC tests while working on OGC features
   ```

3. **Run tests in watch mode** (with pytest-watch):
   ```bash
   ptw -n auto  # Auto-runs tests when files change
   ```

4. **Parallelize Docker builds** too:
   ```bash
   DOCKER_BUILDKIT=1 docker build --build-arg BUILDKIT_INLINE_CACHE=1 \
     -t honua-server:test .
   # BuildKit enables parallel layer building
   ```

5. **Pre-warm Docker layers**: Keep recent build in cache
   ```bash
   # Rebuild only when dependencies change, not on every code edit
   ```

## Comparison: Your Laptop vs CI/GitHub Actions

| Environment | Cores | Full Suite Time | Cost |
|-------------|-------|----------------|------|
| **Your Laptop** | 22 | **1.5-2 min** | $0 |
| GitHub Actions (ubuntu-latest) | 2 | 15-20 min | Counted against quota |
| GitHub Actions (ubuntu-latest-8-cores) | 8 | 4-5 min | 2x quota usage |

**Your laptop is 7-10x faster than CI and costs nothing!** Use it for rapid iteration, run CI only for final validation.

## Advanced: Custom Worker Distribution

For very uneven test durations, create a custom distribution:

```python
# conftest.py
def pytest_xdist_auto_num_workers():
    """Override auto worker count based on test type"""
    return 22  # Force 22 workers
```

Or use pytest-xdist's load balancing:
```bash
pytest -n auto --dist loadgroup  # Group tests by markers
```

## Monitor Real Resource Usage

While tests run:
```bash
# Terminal 1: Run tests
pytest -n auto

# Terminal 2: Watch resource usage
htop  # or 'top' to see CPU/memory per worker
```

You should see 22 Python processes maxing out all cores!

## Bottom Line

With your hardware:
- **Always use `-n auto`** unless you have specific reasons not to
- Full test suite should complete in **under 2 minutes**
- This enables **true TDD with AI** - instant feedback on every change
- Your $800 in AI credits will go much further with 10x faster validation!

Happy testing! 🚀
