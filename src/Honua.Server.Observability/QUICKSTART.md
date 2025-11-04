# Honua Observability - Quick Start Guide

Get up and running with Honua observability in 5 minutes.

## Step 1: Add Project Reference

```bash
cd src/Honua.Server.Host
dotnet add reference ../Honua.Server.Observability/Honua.Server.Observability.csproj
```

## Step 2: Update Program.cs

```csharp
using Honua.Server.Observability;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Logging.AddHonuaSerilog(
    serviceName: "Honua.Server",
    minimumLevel: LogEventLevel.Information
);

// Add observability
builder.Services.AddHonuaObservability(
    serviceName: "Honua.Server",
    serviceVersion: "1.0.0",
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
);

// ... your other services ...

var app = builder.Build();

// Add observability middleware (BEFORE other middleware)
app.UseHonuaMetrics();
app.UseHonuaHealthChecks();
app.UsePrometheusMetrics();

// ... your other middleware ...

app.Run();
```

## Step 3: Start Monitoring Stack

```bash
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

## Step 4: Start Your Application

```bash
cd src/Honua.Server.Host
dotnet run
```

## Step 5: Verify Everything Works

### Check Metrics Endpoint
```bash
curl http://localhost:5000/metrics
```

You should see Prometheus metrics output.

### Check Health Endpoints
```bash
# Overall health
curl http://localhost:5000/health

# Liveness probe
curl http://localhost:5000/health/live

# Readiness probe
curl http://localhost:5000/health/ready
```

### Open Grafana Dashboard

1. Go to http://localhost:3000
2. Login with `admin` / `admin`
3. Navigate to Dashboards → Honua → Honua Build Orchestrator - Overview

### Open Prometheus

1. Go to http://localhost:9090
2. Try some queries:
   - `builds_in_queue`
   - `rate(http_requests_total[5m])`
   - `cache_entries_total`

## Step 6: Use Metrics in Your Code

### Build Queue Metrics

```csharp
public class MyBuildService
{
    private readonly BuildQueueMetrics _metrics;

    public MyBuildService(BuildQueueMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task ProcessBuild(string tier, string architecture)
    {
        _metrics.RecordBuildEnqueued(tier, architecture);

        var startTime = DateTime.UtcNow;

        try
        {
            await ExecuteBuildAsync();

            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordBuildCompleted(tier, true, false, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordBuildCompleted(tier, false, false, duration, ex.GetType().Name);
            throw;
        }
    }
}
```

### Cache Metrics

```csharp
public class MyCacheService
{
    private readonly CacheMetrics _metrics;

    public MyCacheService(CacheMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task<T?> GetAsync<T>(string key, string tier, string arch)
    {
        var result = await _cache.GetAsync<T>(key);

        _metrics.RecordCacheLookup(
            hit: result != null,
            tier: tier,
            architecture: arch
        );

        return result;
    }
}
```

## Common Prometheus Queries

### Build Queue Performance
```promql
# Current queue depth
builds_in_queue

# Build throughput (builds per second)
rate(builds_enqueued_total[5m])

# Build success rate
rate(build_success_total[5m]) / (rate(build_success_total[5m]) + rate(build_failure_total[5m]))

# Build duration (p95)
histogram_quantile(0.95, sum(rate(build_duration_seconds_bucket[5m])) by (le, tier))
```

### Cache Performance
```promql
# Cache hit rate
rate(cache_lookups_total{result="hit"}[5m]) / rate(cache_lookups_total[5m])

# Cache entries
cache_entries_total

# Time saved by cache (hours per day)
sum(increase(cache_savings_seconds_total[24h])) / 3600
```

### HTTP Performance
```promql
# Request rate
sum(rate(http_requests_total[5m]))

# Error rate
rate(http_requests_total{status_class="5xx"}[5m]) / rate(http_requests_total[5m])

# Request latency p95
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le))
```

## Troubleshooting

### Metrics not appearing?

1. Check the `/metrics` endpoint:
   ```bash
   curl http://localhost:5000/metrics | grep honua
   ```

2. Check Prometheus targets:
   - Go to http://localhost:9090/targets
   - Ensure your app is listed and status is "UP"

3. Check Prometheus config:
   - Edit `prometheus/prometheus.yml`
   - Update the target address if needed

### Dashboard not loading?

1. Verify Prometheus datasource:
   - Go to Configuration → Data Sources
   - Test the Prometheus connection

2. Re-import dashboard:
   - Go to Dashboards → Import
   - Upload `grafana/dashboards/honua-overview.json`

### High memory usage?

Reduce metric cardinality by:
- Using fewer unique labels
- Aggregating high-cardinality dimensions
- Increasing scrape interval in Prometheus

## Next Steps

- **Configure Alerts**: Edit `prometheus/alerts.yml` to customize alert thresholds
- **Set up Notifications**: Configure Alertmanager in `alertmanager/alertmanager.yml`
- **Add Custom Metrics**: Create new metric classes following the existing patterns
- **Enable Distributed Tracing**: Use the `ActivityExtensions` class for custom spans
- **Production Deployment**: See README.md for production deployment guidelines

## Support

For issues or questions:
- Check the main README.md for detailed documentation
- Review the Examples/ directory for code samples
- See the existing metric classes for implementation patterns

## Clean Up

To stop the monitoring stack:

```bash
docker-compose -f docker-compose.monitoring.yml down
```

To remove volumes (WARNING: deletes all metrics data):

```bash
docker-compose -f docker-compose.monitoring.yml down -v
```
