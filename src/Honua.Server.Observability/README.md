# Honua Server Observability

Comprehensive monitoring and observability infrastructure for the Honua Build Orchestrator system using OpenTelemetry, Prometheus, and Grafana.

## Production Defaults

**Important:** As of the latest version, observability is **enabled by default in production** configurations:

- **Metrics:** ENABLED in `appsettings.Production.json` (Prometheus format at `/metrics`)
- **Request Logging:** ENABLED in production for audit trails and performance monitoring
- **Distributed Tracing:** Set to `none` by default (configure OTLP endpoint to enable)

To disable metrics in production:
```bash
export observability__metrics__enabled=false
```

To enable distributed tracing in production:
```bash
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

## Features

- **Metrics Collection**: OpenTelemetry-based metrics for all system components
- **Cloud Provider Support**: Native integration with Azure, AWS, GCP, or self-hosted (Prometheus/Grafana)
- **Health Checks**: Custom health checks for database, queue, license, and registry with Kubernetes-ready endpoints
- **SLI/SLO Monitoring**: Pre-configured recording rules for tracking availability, latency, and error budgets
- **Structured Logging**: Serilog with JSON formatting for log aggregation
- **Prometheus Integration**: Metrics export, alert rules, and recording rules
- **Grafana Dashboards**: Pre-built dashboards for system monitoring
- **Distributed Tracing**: OpenTelemetry-based distributed tracing with OTLP support

## Quick Start

### 1. Add to Your Project

```bash
dotnet add reference ../Honua.Server.Observability/Honua.Server.Observability.csproj
```

### 2. Configure Services

In your `Program.cs` or `Startup.cs`:

```csharp
using Honua.Server.Observability;

// Add observability services
builder.Services.AddHonuaObservability(
    serviceName: "Honua.Server",
    serviceVersion: "1.0.0",
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
);

// Configure Serilog
builder.Logging.AddHonuaSerilog(
    serviceName: "Honua.Server",
    minimumLevel: LogEventLevel.Information
);

// Configure the HTTP request pipeline
var app = builder.Build();

// Add metrics middleware (before other middleware)
app.UseHonuaMetrics();

// Add health check endpoints
app.UseHonuaHealthChecks();

// Add Prometheus metrics endpoint
app.UsePrometheusMetrics();
```

### 3. Start Monitoring Stack

```bash
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

This will start:
- **Prometheus** on http://localhost:9090
- **Grafana** on http://localhost:3000 (admin/admin)
- **Alertmanager** on http://localhost:9093

### 4. Access Dashboards

1. Open Grafana at http://localhost:3000
2. Login with admin/admin
3. Navigate to Dashboards → Honua → Honua Build Orchestrator - Overview

## Metrics

### Build Queue Metrics

```csharp
public class MyBuildService
{
    private readonly BuildQueueMetrics _metrics;

    public MyBuildService(BuildQueueMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task EnqueueBuild(string tier, string architecture)
    {
        _metrics.RecordBuildEnqueued(tier, architecture);
        _metrics.UpdateQueueDepth(await GetQueueDepthAsync());
    }

    public async Task ProcessBuild(Build build)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await ExecuteBuildAsync(build);

            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordBuildCompleted(
                tier: build.Tier,
                success: true,
                fromCache: build.UsedCache,
                duration: duration
            );
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordBuildCompleted(
                tier: build.Tier,
                success: false,
                fromCache: false,
                duration: duration,
                errorType: ex.GetType().Name
            );
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

    public async Task<CachedBuild?> LookupAsync(string key, string tier, string arch)
    {
        var result = await _cache.GetAsync(key);

        _metrics.RecordCacheLookup(
            hit: result != null,
            tier: tier,
            architecture: arch
        );

        if (result != null)
        {
            _metrics.RecordCacheSavings(
                savedTime: result.EstimatedBuildTime,
                tier: tier
            );
        }

        return result;
    }
}
```

### License Metrics

```csharp
public class MyLicenseService
{
    private readonly LicenseMetrics _metrics;

    public MyLicenseService(LicenseMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task UpdateLicenseCountsAsync()
    {
        var coreLicenses = await CountActiveLicensesAsync("core");
        var proLicenses = await CountActiveLicensesAsync("pro");
        var entLicenses = await CountActiveLicensesAsync("enterprise");

        _metrics.UpdateActiveLicenses("core", coreLicenses);
        _metrics.UpdateActiveLicenses("pro", proLicenses);
        _metrics.UpdateActiveLicenses("enterprise", entLicenses);
    }

    public async Task ValidateLicenseAsync(string customerId, string tier)
    {
        var isValid = await CheckLicenseValidityAsync(customerId);

        _metrics.RecordValidation(
            success: isValid,
            tier: tier
        );

        // Track quota usage
        var usage = await GetQuotaUsageAsync(customerId);
        _metrics.UpdateQuotaUsage(
            customerId: customerId,
            quotaType: "builds",
            usagePercent: usage.PercentageUsed
        );
    }
}
```

### Registry Metrics

```csharp
public class MyRegistryService
{
    private readonly RegistryMetrics _metrics;

    public MyRegistryService(RegistryMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task ProvisionRegistryAsync(string provider)
    {
        var startTime = DateTime.UtcNow;
        var success = false;

        try
        {
            await CreateRegistryAsync(provider);
            success = true;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordProvisioning(provider, success, duration);
        }
    }
}
```

### AI Intake Metrics

```csharp
public class MyIntakeService
{
    private readonly IntakeMetrics _metrics;

    public MyIntakeService(IntakeMetrics metrics)
    {
        _metrics = metrics;
    }

    public async Task ProcessConversationAsync(string model)
    {
        _metrics.RecordConversationStarted(model);

        var startTime = DateTime.UtcNow;
        var success = false;

        try
        {
            var response = await CallAIAsync(model);
            success = true;

            // Record token usage
            _metrics.RecordTokenUsage(
                model: model,
                promptTokens: response.PromptTokens,
                completionTokens: response.CompletionTokens
            );

            // Record cost
            _metrics.RecordCost(
                model: model,
                costUsd: response.CostUsd
            );
        }
        catch (Exception ex)
        {
            _metrics.RecordError(model, ex.GetType().Name);
            throw;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            _metrics.RecordConversationCompleted(model, success, duration);
        }
    }
}
```

## Cloud Provider Support

Honua Server supports multiple observability backends:

- **Self-Hosted (default)**: Prometheus, Grafana, Jaeger/Tempo
- **Azure**: Application Insights for metrics, traces, and logs
- **AWS**: CloudWatch + X-Ray via AWS Distro for OpenTelemetry (ADOT)
- **GCP**: Cloud Monitoring + Cloud Trace via OpenTelemetry Collector

### Configuration

```json
{
  "observability": {
    "cloudProvider": "none",  // Options: "none", "azure", "aws", "gcp"
    "azure": {
      "connectionString": "InstrumentationKey=xxx;..."
    },
    "aws": {
      "region": "us-east-1",
      "otlpEndpoint": "http://localhost:4317"
    },
    "gcp": {
      "projectId": "my-project-123456",
      "otlpEndpoint": "http://localhost:4317"
    }
  }
}
```

See [Cloud Provider Setup Guide](../../docs/observability/cloud-provider-setup.md) for detailed configuration.

## Health Checks

Health check endpoints are available at:

- `/health` - Overall health status
- `/health/live` - Liveness probe (always healthy if running)
- `/health/ready` - Readiness probe (checks database and queue)

### Health Check Components

1. **DatabaseHealthCheck**: PostgreSQL connectivity, migrations status
2. **QueueHealthCheck**: Build queue depth and processing status
3. **LicenseHealthCheck**: Active licenses and quota tracking
4. **RegistryHealthCheck**: Container registry availability

### Kubernetes Configuration

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: honua-server
spec:
  containers:
  - name: honua-server
    image: honua/server:latest
    livenessProbe:
      httpGet:
        path: /health/live
        port: 5000
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 5000
      initialDelaySeconds: 10
      periodSeconds: 5
```

Example response:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "database": {
      "status": "Healthy",
      "description": "Database connected successfully. 42 migrations applied.",
      "data": {
        "version": "PostgreSQL 14.5",
        "migrations_applied": 42
      }
    },
    "queue": {
      "status": "Healthy",
      "description": "Queue processing normally. 5 pending, 2 processing",
      "data": {
        "pending_count": 5,
        "processing_count": 2
      }
    },
    "license": {
      "status": "Healthy",
      "description": "All licenses valid",
      "data": {
        "active_licenses": 10,
        "expiring_soon": 0
      }
    },
    "registry": {
      "status": "Healthy",
      "description": "All registries accessible",
      "data": {
        "active_registries": 3,
        "credentials_expiring_soon": 0
      }
    }
  }
}
```

## SLI/SLO Monitoring

Honua includes pre-configured Service Level Indicators (SLIs) and Service Level Objectives (SLOs) for tracking service reliability.

### Default SLO Targets

- **Availability**: 99.9% (error budget: 0.1%)
- **Latency**: 95th percentile < 5 seconds
- **Database**: 99.95% query success rate
- **Build Queue**: 99% build success rate

### Recording Rules

Recording rules pre-compute expensive queries and create SLI metrics:

```promql
# HTTP Availability (5-minute window)
honua:availability:ratio_5m

# Error Budget Remaining
honua:error_budget:remaining_5m

# Error Budget Burn Rate
honua:error_budget:burn_rate_5m

# P95 Latency (5-minute window)
honua:latency:p95_5m

# Service Health Score (composite metric)
honua:service:health_score_5m
```

### Deployment

1. Copy recording rules to Prometheus:
   ```bash
   cp prometheus/recording-rules.yml /etc/prometheus/
   ```

2. Update Prometheus config:
   ```yaml
   rule_files:
     - "alerts.yml"
     - "recording-rules.yml"
   ```

3. Reload Prometheus:
   ```bash
   curl -X POST http://localhost:9090/-/reload
   ```

See [SLI/SLO Monitoring Guide](../../docs/observability/sli-slo-monitoring.md) for detailed setup and interpretation.

## Prometheus Queries

### Build Queue Performance

```promql
# Queue depth
builds_in_queue

# Build throughput (builds/sec)
rate(builds_enqueued_total[5m])

# Build success rate
rate(build_success_total[5m]) / (rate(build_success_total[5m]) + rate(build_failure_total[5m]))

# Average build duration (p50, p95, p99)
histogram_quantile(0.50, sum(rate(build_duration_seconds_bucket[5m])) by (le, tier))
histogram_quantile(0.95, sum(rate(build_duration_seconds_bucket[5m])) by (le, tier))
histogram_quantile(0.99, sum(rate(build_duration_seconds_bucket[5m])) by (le, tier))
```

### Cache Performance

```promql
# Cache hit rate
rate(cache_lookups_total{result="hit"}[5m]) / rate(cache_lookups_total[5m])

# Cache entries
cache_entries_total

# Time saved by cache (hours)
sum(rate(cache_savings_seconds_total[1h])) / 3600
```

### HTTP Performance

```promql
# Request rate by status
sum by(status_class) (rate(http_requests_total[5m]))

# Error rate
rate(http_requests_total{status_class="5xx"}[5m]) / rate(http_requests_total[5m])

# Request latency (p95)
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, path))
```

## Alerts

Alert rules are defined in `prometheus/alerts.yml`. The system includes 35+ pre-configured alerts:

### Critical Alerts
- **ServiceDown**: Service unreachable for 2 minutes
- **HighErrorBudgetBurn**: Burning error budget 10x faster than allowed (SLO violation)
- **NoActiveRegistries**: No registries available
- **QuotaExceeded**: Customer quota exceeded
- **DatabaseConnectionFailures**: Database connection failures detected

### Warning Alerts
- **HighBuildQueueDepth**: Queue depth > 100 for 5 minutes
- **HighBuildFailureRate**: Failure rate > 20% for 10 minutes
- **LowCacheHitRate**: Hit rate < 50% for 15 minutes
- **HighHTTPErrorRate**: 5xx rate > 5% for 5 minutes
- **HighHTTPLatency**: P95 latency > 5 seconds for 10 minutes
- **LicenseExpiringSoon**: Licenses expiring within 7 days

### SLO Alerts
- **HighErrorBudgetBurn**: Fast burn rate (10x)
- **P95ResponseTimeHigh**: Latency SLO violation
- **SustainedHighErrorRate**: Multi-window error rate detection

Configure notification channels in `alertmanager/alertmanager.yml`.

## Structured Logging

Logs are written in JSON format for easy parsing:

```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "level": "Information",
  "messageTemplate": "Build {BuildId} completed successfully in {Duration}ms",
  "properties": {
    "BuildId": "abc123",
    "Duration": 1234,
    "ServiceName": "Honua.Server",
    "MachineName": "web-01",
    "ThreadId": 42
  }
}
```

Logs are written to:
- Console (JSON format)
- Files in `logs/` directory (rotating daily, 30 day retention)

## Production Deployment

### Environment Variables

```bash
# Application
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Host=postgres;Database=honua;...

# Prometheus
PROMETHEUS_RETENTION_TIME=15d
PROMETHEUS_RETENTION_SIZE=50GB

# Grafana
GF_SECURITY_ADMIN_PASSWORD=<strong-password>
GF_SERVER_ROOT_URL=https://monitoring.example.com
```

### Kubernetes Deployment

Example ServiceMonitor for Prometheus Operator:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: honua-server
  namespace: monitoring
spec:
  selector:
    matchLabels:
      app: honua-server
  endpoints:
  - port: metrics
    path: /metrics
    interval: 15s
```

### Remote Storage

For long-term metrics storage, configure Prometheus remote write:

```yaml
remote_write:
  - url: 'https://prometheus-remote-storage.example.com/api/v1/write'
    basic_auth:
      username: 'honua'
      password: '<password>'
```

## Troubleshooting

### Metrics Not Appearing

1. Check if `/metrics` endpoint is accessible:
   ```bash
   curl http://localhost:5000/metrics
   ```

2. Verify Prometheus is scraping:
   ```bash
   curl http://localhost:9090/api/v1/targets
   ```

3. Check Prometheus logs:
   ```bash
   docker logs honua-prometheus
   ```

### High Memory Usage

If metrics collection causes high memory usage:

1. Reduce metric cardinality (fewer unique label combinations)
2. Increase Prometheus scrape interval
3. Adjust histogram buckets in metric definitions

### Dashboard Not Loading

1. Verify Grafana datasource configuration
2. Check dashboard JSON syntax
3. Import dashboard manually via Grafana UI

## Documentation

### Guides
- **[Cloud Provider Setup Guide](../../docs/observability/cloud-provider-setup.md)** - Configure Azure, AWS, GCP, or self-hosted
- **[SLI/SLO Monitoring Guide](../../docs/observability/sli-slo-monitoring.md)** - Track service reliability and error budgets
- **[Deployment Checklist](../../docs/deployment/observability-deployment-checklist.md)** - Production deployment checklist
- **[Migration Guide](../../docs/observability/migration-guide.md)** - Migrate to new observability features
- **[Distributed Tracing Guide](../../docs/architecture/tracing.md)** - Advanced tracing configuration

### Quick References
- **[QUICKSTART.md](QUICKSTART.md)** - 5-minute setup guide
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - Architecture and file overview

## Contributing

When adding new metrics:

1. Create metric class in `Metrics/` directory
2. Register meter name in `ServiceCollectionExtensions.cs`
3. Update Grafana dashboard JSON
4. Add Prometheus alert rules if needed
5. Add recording rules for SLI metrics if applicable
6. Document usage in this README

## License

Copyright (c) 2024 Honua.io. All rights reserved.
