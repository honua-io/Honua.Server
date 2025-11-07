# GeoETL Performance Quick Reference

Quick reference guide for common performance optimization tasks.

## Table of Contents

- [Enable Parallel Execution](#enable-parallel-execution)
- [Enable Caching](#enable-caching)
- [Optimize Database](#optimize-database)
- [Enable Streaming](#enable-streaming)
- [Monitor Performance](#monitor-performance)
- [Run Benchmarks](#run-benchmarks)
- [Run Load Tests](#run-load-tests)
- [Configuration Templates](#configuration-templates)
- [Common Commands](#common-commands)

## Enable Parallel Execution

**appsettings.json:**
```json
{
  "GeoETL": {
    "Performance": {
      "EngineType": "Parallel",
      "MaxParallelNodes": 4
    }
  }
}
```

**Program.cs:**
```csharp
services.AddGeoEtlPerformanceOptimizations(configuration);
```

**Expected Improvement:** 40-60% faster for workflows with parallel branches

## Enable Caching

### Memory Cache (Single Server)

```json
{
  "GeoETL": {
    "Performance": {
      "Cache": {
        "Enabled": true,
        "Provider": "Memory",
        "DefaultTtlMinutes": 5,
        "MaxMemorySizeMB": 512
      }
    }
  }
}
```

### Redis Cache (Distributed)

```json
{
  "GeoETL": {
    "Performance": {
      "Cache": {
        "Enabled": true,
        "Provider": "Redis",
        "RedisConnectionString": "localhost:6379",
        "DefaultTtlMinutes": 5
      }
    }
  }
}
```

**Expected Improvement:** 80%+ cache hit rate, faster repeated operations

## Optimize Database

### 1. Create Indexes

```bash
psql -d honua -f scripts/geoetl/optimize-database.sql
```

### 2. Configure Connection String

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=honua;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=100;Connection Idle Lifetime=300"
  }
}
```

### 3. Enable Batch Operations

```json
{
  "GeoETL": {
    "Performance": {
      "Database": {
        "UseConnectionPooling": true,
        "DefaultBatchSize": 1000,
        "UseCopyForBulkInsert": true,
        "UsePreparedStatements": true
      }
    }
  }
}
```

**Expected Improvement:** 70-75% faster database operations

## Enable Streaming

**Node Implementation:**
```csharp
public class MyNode : IStreamingWorkflowNode
{
    public async IAsyncEnumerable<IFeature> ProcessStreamAsync(
        IAsyncEnumerable<IFeature> inputFeatures,
        Dictionary<string, object> parameters,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var feature in inputFeatures.WithCancellation(cancellationToken))
        {
            // Process feature
            yield return ProcessFeature(feature);
        }
    }
}
```

**Expected Improvement:** Constant memory usage, handles millions of features

## Monitor Performance

### Enable OpenTelemetry + Prometheus

```json
{
  "GeoETL": {
    "Performance": {
      "Monitoring": {
        "EnableMetrics": true,
        "Prometheus": {
          "Enabled": true,
          "Port": 9090,
          "Path": "/metrics"
        }
      }
    }
  }
}
```

**Program.cs:**
```csharp
services.AddGeoEtlOpenTelemetry(configuration);
```

### Enable Application Insights

```json
{
  "GeoETL": {
    "Performance": {
      "Monitoring": {
        "ApplicationInsights": {
          "Enabled": true,
          "ConnectionString": "InstrumentationKey=...;..."
        }
      }
    }
  }
}
```

**Program.cs:**
```csharp
services.AddGeoEtlApplicationInsights(configuration);
```

### Key Metrics to Track

```promql
# Workflow duration (p95)
histogram_quantile(0.95, geoetl_workflow_duration_bucket)

# Cache hit rate
rate(geoetl_cache_hits_total[5m]) /
(rate(geoetl_cache_hits_total[5m]) + rate(geoetl_cache_misses_total[5m]))

# Throughput (features/sec)
rate(geoetl_features_processed_total[5m])

# Error rate
rate(geoetl_workflows_failed_total[5m]) /
rate(geoetl_workflows_started_total[5m])
```

## Run Benchmarks

```bash
# Navigate to tests
cd tests/Honua.Server.Enterprise.Tests

# Run all GeoETL benchmarks
dotnet run -c Release --filter "*GeoEtlBenchmarks*"

# Run specific benchmark
dotnet run -c Release --filter "*ParallelExecution*"

# Export results
dotnet run -c Release --exporters json html
```

## Run Load Tests

```bash
# Install k6
# macOS: brew install k6
# Linux: sudo apt-get install k6
# Windows: choco install k6

# Run load test
k6 run scripts/geoetl/load-test.js

# With custom settings
k6 run scripts/geoetl/load-test.js \
  --env BASE_URL=http://localhost:5000 \
  --env API_KEY=your-api-key \
  --env TENANT_ID=your-tenant-id

# Run specific scenario
k6 run scripts/geoetl/load-test.js \
  --scenario concurrent_workflows

# Save results
k6 run scripts/geoetl/load-test.js \
  --out json=results.json \
  --out csv=results.csv
```

## Configuration Templates

### Development Environment

```json
{
  "GeoETL": {
    "Performance": {
      "EngineType": "Sequential",
      "Cache": {
        "Enabled": true,
        "Provider": "Memory",
        "MaxMemorySizeMB": 256
      },
      "Database": {
        "DefaultBatchSize": 100
      },
      "Monitoring": {
        "EnableDetailedMetrics": true
      }
    }
  }
}
```

### Staging Environment

```json
{
  "GeoETL": {
    "Performance": {
      "EngineType": "Parallel",
      "MaxParallelNodes": 4,
      "Cache": {
        "Enabled": true,
        "Provider": "Redis",
        "RedisConnectionString": "redis:6379"
      },
      "Database": {
        "UseConnectionPooling": true,
        "DefaultBatchSize": 1000,
        "UseCopyForBulkInsert": true
      },
      "Monitoring": {
        "EnableMetrics": true,
        "Prometheus": { "Enabled": true }
      }
    }
  }
}
```

### Production Environment

```json
{
  "GeoETL": {
    "Performance": {
      "EngineType": "Parallel",
      "MaxParallelNodes": 8,
      "StreamingEnabled": true,
      "MaxMemoryPerWorkflowMB": 1024,
      "Cache": {
        "Enabled": true,
        "Provider": "Redis",
        "RedisConnectionString": "redis-cluster:6379",
        "MaxMemorySizeMB": 2048
      },
      "Database": {
        "UseConnectionPooling": true,
        "MinPoolSize": 10,
        "MaxPoolSize": 100,
        "DefaultBatchSize": 1000,
        "UseCopyForBulkInsert": true,
        "UsePreparedStatements": true
      },
      "Memory": {
        "EnableObjectPooling": true,
        "MaxMemoryMB": 4096
      },
      "Monitoring": {
        "EnableMetrics": true,
        "Prometheus": { "Enabled": true },
        "ApplicationInsights": {
          "Enabled": true,
          "ConnectionString": "..."
        }
      }
    }
  }
}
```

## Common Commands

### PostgreSQL

```sql
-- View slow queries
SELECT query, calls, total_exec_time / 1000 as total_seconds
FROM pg_stat_statements
WHERE query LIKE '%geoetl%'
ORDER BY total_exec_time DESC
LIMIT 20;

-- View table sizes
SELECT tablename, pg_size_pretty(pg_total_relation_size(tablename::regclass))
FROM pg_tables
WHERE tablename LIKE 'geoetl%'
ORDER BY pg_total_relation_size(tablename::regclass) DESC;

-- View index usage
SELECT tablename, indexname, idx_scan, pg_size_pretty(pg_relation_size(indexrelid))
FROM pg_stat_user_indexes
WHERE tablename LIKE 'geoetl%'
ORDER BY idx_scan ASC;

-- Refresh statistics
SELECT geoetl_refresh_stats();

-- Archive old runs
SELECT geoetl_archive_old_runs(90);

-- Manual vacuum
VACUUM ANALYZE geoetl_workflow_runs;
```

### Redis

```bash
# Check cache usage
redis-cli INFO memory

# View keys
redis-cli KEYS "geoetl:*"

# Clear cache
redis-cli FLUSHDB

# Monitor cache operations
redis-cli MONITOR | grep geoetl
```

### Docker

```bash
# View logs
docker logs geoetl-server --tail 100 -f

# Check metrics
curl http://localhost:9090/metrics | grep geoetl

# Container stats
docker stats geoetl-server
```

### .NET

```bash
# Memory profiling
dotnet-counters monitor --process-id $(pidof Honua.Server.Host)

# Event tracing
dotnet-trace collect --process-id $(pidof Honua.Server.Host)

# Memory dump
dotnet-dump collect --process-id $(pidof Honua.Server.Host)

# Analyze dump
dotnet-dump analyze dump.dmp
```

## Troubleshooting Checklist

### Slow Execution

- [ ] Enable parallel execution
- [ ] Check workflow DAG for sequential bottlenecks
- [ ] Review node execution times in logs
- [ ] Verify database indexes exist
- [ ] Check cache hit rate
- [ ] Monitor CPU and memory usage

### High Memory Usage

- [ ] Enable streaming for large datasets
- [ ] Reduce batch sizes
- [ ] Enable object pooling
- [ ] Set memory limits per workflow
- [ ] Check for memory leaks
- [ ] Monitor GC frequency

### Database Issues

- [ ] Enable connection pooling
- [ ] Use batch operations
- [ ] Create recommended indexes
- [ ] Use prepared statements
- [ ] Check slow query log
- [ ] Monitor connection pool usage

### Low Cache Hit Rate

- [ ] Increase cache TTL
- [ ] Pre-warm cache
- [ ] Review cache key strategy
- [ ] Consider Redis for distributed cache
- [ ] Check cache eviction rate

## Performance Targets

| Metric | Target | Critical |
|--------|--------|----------|
| Workflow Duration (p95) | < 60s | < 120s |
| Node Duration (p95) | < 10s | < 30s |
| Throughput | > 1000 features/s | > 500 features/s |
| Memory Usage | < 512 MB | < 1024 MB |
| Cache Hit Rate | > 80% | > 60% |
| Database Query Time (p95) | < 100ms | < 500ms |
| Error Rate | < 1% | < 5% |

## Quick Wins (10-Minute Setup)

1. **Enable Parallel Execution**
   ```json
   { "GeoETL": { "Performance": { "EngineType": "Parallel" } } }
   ```

2. **Enable Memory Cache**
   ```json
   { "GeoETL": { "Performance": { "Cache": { "Enabled": true } } } }
   ```

3. **Create Database Indexes**
   ```bash
   psql -d honua -f scripts/geoetl/optimize-database.sql
   ```

4. **Register Services**
   ```csharp
   services.AddGeoEtlPerformanceOptimizations(configuration);
   ```

**Expected Results:**
- 30-50% performance improvement
- Reduced database load
- Better response times

## Related Documentation

- [Performance Guide](./PERFORMANCE_GUIDE.md) - Comprehensive guide
- [Performance Optimizations](./PERFORMANCE_OPTIMIZATIONS.md) - Implementation details
- [GeoETL Overview](./README.md) - System overview

## Support

For performance-related issues:
1. Check logs for errors and warnings
2. Review metrics dashboard
3. Run benchmarks to establish baseline
4. Consult troubleshooting guide
5. Contact support with metrics and logs
