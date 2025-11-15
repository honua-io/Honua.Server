# Honua.Server Observability Implementation Guide

This guide documents the comprehensive observability implementation across the Honua.Server codebase following OpenTelemetry standards and industry best practices.

## Table of Contents

1. [Overview](#overview)
2. [OpenTelemetry Implementation](#opentelemetry-implementation)
3. [Health Checks](#health-checks)
4. [Structured Logging](#structured-logging)
5. [Configuration](#configuration)
6. [Cloud Provider Integration](#cloud-provider-integration)
7. [Troubleshooting](#troubleshooting)

## Overview

The Honua.Server observability stack provides comprehensive monitoring, tracing, and logging capabilities:

- **Distributed Tracing**: W3C Trace Context-compliant tracing with OpenTelemetry
- **Metrics**: Prometheus-compatible metrics with semantic conventions
- **Structured Logging**: Serilog with OpenTelemetry integration and OTLP export
- **Health Checks**: RFC-compliant health check endpoints with dependency monitoring
- **Correlation IDs**: Automatic request tracking across service boundaries

## OpenTelemetry Implementation

### Architecture

The observability implementation uses OpenTelemetry SDK with:

1. **Resource Attributes**: Semantic conventions for service, host, process, and cloud metadata
2. **Instrumentation**: Auto-instrumentation for ASP.NET Core, HTTP clients, databases, and Redis
3. **Exporters**: OTLP, Prometheus, and Console exporters
4. **Sampling**: Configurable trace sampling for high-traffic environments

### Resource Attributes

All services are automatically configured with semantic convention attributes:

```csharp
{
  "service.name": "Honua.Server.Host",
  "service.version": "1.0.0",
  "service.namespace": "Honua",
  "service.instance.id": "unique-instance-id",
  "deployment.environment": "Production",
  "host.name": "server-hostname",
  "host.id": "server-id",
  "process.pid": 12345,
  "process.runtime.name": ".NET",
  "process.runtime.version": "9.0.0",
  "os.type": "linux",
  "telemetry.sdk.name": "opentelemetry",
  "telemetry.sdk.language": "dotnet"
}
```

#### Cloud-Specific Attributes

When running in cloud environments, additional attributes are automatically detected:

**AWS:**
```csharp
{
  "cloud.provider": "aws",
  "cloud.region": "us-east-1",
  "cloud.platform": "aws_ecs", // or aws_ec2
  "cloud.account.id": "123456789012"
}
```

**Azure:**
```csharp
{
  "cloud.provider": "azure",
  "cloud.region": "eastus",
  "cloud.platform": "azure_app_service", // or azure_container_apps, azure_kubernetes_service
  "service.instance.id": "instance-replica-id"
}
```

**GCP:**
```csharp
{
  "cloud.provider": "gcp",
  "cloud.region": "us-central1",
  "cloud.platform": "gcp_cloud_run", // or gcp_kubernetes_engine
  "cloud.account.id": "project-id"
}
```

**Kubernetes:**
```csharp
{
  "k8s.namespace.name": "production",
  "k8s.pod.name": "honua-server-abc123",
  "k8s.cluster.name": "production-cluster",
  "k8s.deployment.name": "honua-server"
}
```

**Docker:**
```csharp
{
  "container.id": "container-id",
  "container.name": "honua-server",
  "container.image.name": "honua/server:latest"
}
```

### Metrics

#### Built-in Instrumentation

All services include automatic metrics for:

- **ASP.NET Core**: Request duration, active requests, exceptions
- **HTTP Client**: Request duration, failures
- **Runtime**: GC collections, thread pool, exceptions
- **Process**: CPU usage, memory usage, thread count

#### Custom Meters

Application-specific meters are configured for each service:

**Host Service:**
- `Honua.Server.Api` - API request metrics
- `Honua.Server.Database` - Database query metrics
- `Honua.Server.Cache` - Cache hit/miss metrics
- `Honua.Server.Query` - Query execution metrics
- `Honua.Server.VectorTiles` - Tile generation metrics
- `Honua.Server.Security` - Authentication/authorization metrics
- `Honua.Server.Business` - Business logic metrics

**AlertReceiver Service:**
- `Honua.Server.Alerts` - Alert processing metrics
- `Honua.Server.AlertPublishing` - Alert delivery metrics

#### Histogram Buckets

Response time histograms use OpenTelemetry-recommended bucket boundaries (milliseconds):

```
[0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000]
```

### Distributed Tracing

#### Activity Sources

All services define activity sources for distributed tracing:

```csharp
HonuaTelemetry.OgcProtocols  // OGC protocol operations
HonuaTelemetry.OData         // OData queries
HonuaTelemetry.Stac          // STAC operations
HonuaTelemetry.Database      // Database operations
HonuaTelemetry.RasterTiles   // Raster tile generation
HonuaTelemetry.Metadata      // Metadata operations
HonuaTelemetry.Authentication // Authentication operations
```

#### Trace Enrichment

Traces are automatically enriched with:

- HTTP method, route, status code
- User agent and content type
- Correlation IDs and tenant IDs
- Database statements (sanitized)
- Exception details with stack traces

#### Sampling

Configure sampling ratio for high-traffic environments:

```json
{
  "observability": {
    "tracing": {
      "samplingRatio": 0.1  // Sample 10% of traces
    }
  }
}
```

### Structured Logging with OpenTelemetry

Logs can be exported via OTLP for unified observability:

```json
{
  "observability": {
    "logging": {
      "exporter": "otlp",
      "otlpEndpoint": "http://otel-collector:4317",
      "otlpHeaders": "x-api-key=your-api-key"
    }
  }
}
```

Features:
- Automatic correlation with traces (trace ID, span ID)
- Structured log fields become span attributes
- Exception details included
- Scopes preserved for context

## Health Checks

### RFC-Compliant Format

All health check endpoints return responses following the [IETF Health Check Response Format for HTTP APIs](https://datatracker.ietf.org/doc/html/draft-inadarei-api-health-check-06):

```json
{
  "status": "pass",
  "version": "1",
  "releaseId": "1.2.3",
  "serviceId": "honua-server-instance-123",
  "description": "Health status for Honua.Server.Host",
  "checks": {
    "database_connectivity": [
      {
        "componentType": "datastore",
        "status": "pass",
        "time": "2025-01-14T12:00:00Z",
        "observedValue": 15,
        "observedUnit": "ms"
      }
    ],
    "redisStores": [
      {
        "componentType": "datastore",
        "status": "pass",
        "time": "2025-01-14T12:00:00Z"
      }
    ]
  }
}
```

### Health Check Endpoints

#### Host Service

- **`/health`** - Comprehensive health check (all dependencies)
- **`/health/ready`** - Readiness probe (critical dependencies only)
- **`/health/live`** - Liveness probe (always returns 200 if app is running)

#### AlertReceiver Service

- **`/health`** - All health checks
- **`/health/live`** - Liveness probe
- **`/health/ready`** - Readiness probe (database connectivity)

### Dependency Health Checks

**Host Service Checks:**
- `metadata` - Metadata registry health
- `dataSources` - Data source connectivity
- `database_connectivity` - PostgreSQL connectivity
- `schema` - Database schema validation
- `crs_transformation` - CRS transformation library availability
- `redisStores` - Redis connectivity (distributed cache, rate limiting)
- `oidc` - OIDC discovery endpoint availability

**AlertReceiver Checks:**
- `alert-history` - Alert history database connectivity
- `sns` - AWS SNS service registration

### HTTP Status Codes

Per RFC specification:

| Health Status | HTTP Status Code | Description |
|--------------|------------------|-------------|
| `pass` (Healthy) | 200 OK | All checks passed |
| `warn` (Degraded) | 200 OK | Non-critical issues detected |
| `fail` (Unhealthy) | 503 Service Unavailable | Critical failures |

### Cache-Control Headers

Health check responses include cache control to prevent stale data:

```
Cache-Control: no-cache, no-store, must-revalidate
```

## Structured Logging

### Correlation IDs

All requests are automatically assigned correlation IDs via `CorrelationIdMiddleware`:

**Sources (priority order):**
1. `X-Correlation-ID` header (explicit)
2. W3C Trace Context `traceparent` header (trace-id component)
3. Generated UUID (fallback)

**Propagation:**
- Added to all logs via `LogContext`
- Included in response headers
- Propagated to downstream services
- Included in distributed traces

### Log Levels

Follow these guidelines for appropriate log levels:

| Level | Usage | Examples |
|-------|-------|----------|
| **Trace** | Very detailed debugging (dev only) | SQL parameters, request/response bodies |
| **Debug** | Detailed debugging information | Cache hits, algorithm decisions |
| **Information** | General informational messages | Service start, state changes |
| **Warning** | Unexpected but handled situations | Slow queries, retry attempts |
| **Error** | Errors that prevent operation | Failed queries, invalid input |
| **Critical** | Catastrophic failures | Database unavailable, startup failures |

### Sensitive Data Protection

The following data is **NEVER** logged:

- Passwords, password hashes, reset tokens
- API keys, secrets, access tokens
- Full JWT tokens (metadata only)
- Credit card numbers, PII
- Database connection strings with credentials
- Encryption keys, certificates

**Automatic Redaction:**

`RequestResponseLoggingMiddleware` automatically redacts:

- Headers: `Authorization`, `Cookie`, `Set-Cookie`, `X-API-Key`
- JSON fields: `password`, `secret`, `token`, `apiKey`
- Query parameters: `password`, `secret`, `token`

See `docs/Honua.Server.Observability/Logging/LoggingGuidelines.md` for complete guidelines.

## Configuration

### Complete Configuration Example

```json
{
  "observability": {
    "cloudProvider": "aws",

    "logging": {
      "jsonConsole": true,
      "includeScopes": true,
      "exporter": "otlp",
      "otlpEndpoint": "http://otel-collector:4317",
      "otlpHeaders": "x-api-key=your-api-key"
    },

    "metrics": {
      "enabled": true,
      "endpoint": "/metrics",
      "usePrometheus": true
    },

    "tracing": {
      "exporter": "otlp",
      "samplingRatio": 1.0,
      "otlpEndpoint": "http://otel-collector:4317",
      "otlpHeaders": "x-api-key=your-api-key"
    },

    "requestLogging": {
      "enabled": true,
      "logHeaders": false,
      "slowThresholdMs": 5000
    }
  }
}
```

### Environment Variables

Override configuration via environment variables:

```bash
# Cloud provider
export observability__cloudProvider=aws

# Logging
export observability__logging__exporter=otlp
export observability__logging__otlpEndpoint=http://otel-collector:4317

# Metrics
export observability__metrics__enabled=true

# Tracing
export observability__tracing__exporter=otlp
export observability__tracing__samplingRatio=0.1
export observability__tracing__otlpEndpoint=http://otel-collector:4317

# Cloud-specific
export AWS_REGION=us-east-1
export AZURE_REGION=eastus
export GCP_PROJECT=my-project
```

## Cloud Provider Integration

### AWS

**X-Ray Support:**

Set cloud provider to enable AWS X-Ray trace ID propagation:

```json
{
  "observability": {
    "cloudProvider": "aws"
  }
}
```

**Required Environment Variables:**
- `AWS_REGION` - AWS region
- `AWS_ACCOUNT_ID` (optional) - AWS account ID
- `ECS_CONTAINER_METADATA_URI_V4` (auto-detected in ECS)

### Azure

**Application Insights:**

```json
{
  "observability": {
    "cloudProvider": "azure"
  },
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;..."
  }
}
```

**Required Environment Variables:**
- `AZURE_REGION` or `REGION_NAME`
- `WEBSITE_SITE_NAME` (auto-detected in App Service)
- `CONTAINER_APP_NAME` (auto-detected in Container Apps)

### GCP

**Cloud Trace:**

```json
{
  "observability": {
    "cloudProvider": "gcp"
  }
}
```

**Required Environment Variables:**
- `GCP_PROJECT` or `GOOGLE_CLOUD_PROJECT`
- `GCP_REGION` or `GOOGLE_CLOUD_REGION`
- `K_SERVICE` (auto-detected in Cloud Run)

### Self-Hosted

Use OpenTelemetry Collector for self-hosted deployments:

```json
{
  "observability": {
    "cloudProvider": "none",
    "logging": {
      "exporter": "otlp",
      "otlpEndpoint": "http://otel-collector:4317"
    },
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://otel-collector:4317"
    },
    "metrics": {
      "enabled": true,
      "usePrometheus": true
    }
  }
}
```

**OpenTelemetry Collector Configuration:**

```yaml
receivers:
  otlp:
    protocols:
      grpc:
        endpoint: 0.0.0.0:4317
      http:
        endpoint: 0.0.0.0:4318

exporters:
  prometheus:
    endpoint: 0.0.0.0:8889
  jaeger:
    endpoint: jaeger:14250
  loki:
    endpoint: http://loki:3100/loki/api/v1/push

service:
  pipelines:
    traces:
      receivers: [otlp]
      exporters: [jaeger]
    metrics:
      receivers: [otlp]
      exporters: [prometheus]
    logs:
      receivers: [otlp]
      exporters: [loki]
```

## Troubleshooting

### Common Issues

#### Metrics Not Appearing in Prometheus

**Check:**
1. Metrics enabled: `observability:metrics:enabled = true`
2. Endpoint accessible: `curl http://localhost:5000/metrics`
3. Prometheus scrape config targets the correct endpoint

**Debug:**
```bash
# View metrics locally
curl http://localhost:5000/metrics

# Check logs for errors
grep -i "prometheus" logs/honua-*.log
```

#### Traces Not Appearing in Jaeger/Tempo

**Check:**
1. Tracing exporter configured: `observability:tracing:exporter = otlp`
2. OTLP endpoint reachable: `observability:tracing:otlpEndpoint`
3. Sampling ratio: `observability:tracing:samplingRatio = 1.0` (for testing)

**Debug:**
```bash
# Test OTLP endpoint connectivity
curl -v http://otel-collector:4317

# Enable console exporter for debugging
export observability__tracing__exporter=console

# Check for trace activity in logs
grep -i "activity" logs/honua-*.log
```

#### Logs Not Exported via OTLP

**Check:**
1. Log exporter configured: `observability:logging:exporter = otlp`
2. OTLP endpoint reachable: `observability:logging:otlpEndpoint`
3. Serilog configured correctly (should see both Serilog and OpenTelemetry logs)

**Debug:**
```bash
# Test with console exporter first
export observability__logging__exporter=console

# Check log output format
tail -f logs/honua-*.log
```

#### Health Checks Return 503

**Check:**
1. Dependency connectivity (database, Redis, storage)
2. Health check timeout configuration
3. Individual health check status in response body

**Debug:**
```bash
# View detailed health check response
curl -s http://localhost:5000/health | jq .

# Check specific dependency
curl -s http://localhost:5000/health | jq '.checks.database_connectivity'

# Test database connectivity directly
psql -h localhost -U user -d dbname -c "SELECT 1"
```

#### Correlation IDs Not Appearing in Logs

**Check:**
1. `CorrelationIdMiddleware` registered in pipeline
2. Serilog configured with `Enrich.FromLogContext()`
3. Logging scopes enabled: `observability:logging:includeScopes = true`

**Debug:**
```bash
# Check if correlation ID middleware is registered
grep -i "CorrelationIdMiddleware" logs/honua-*.log

# Send request with correlation ID and verify
curl -H "X-Correlation-ID: test-123" http://localhost:5000/health
grep "test-123" logs/honua-*.log
```

### Performance Optimization

#### High-Volume Environments

**Reduce Trace Sampling:**
```json
{
  "observability": {
    "tracing": {
      "samplingRatio": 0.1  // Sample 10% of traces
    }
  }
}
```

**Disable Request Logging:**
```json
{
  "observability": {
    "requestLogging": {
      "enabled": false
    }
  }
}
```

**Use Head-Based Sampling:**

Configure sampler in `appsettings.Production.json` to sample based on trace ID:

```csharp
builder.SetSampler(new TraceIdRatioBasedSampler(0.1));  // 10% sampling
```

## References

- [OpenTelemetry Specification](https://opentelemetry.io/docs/specs/)
- [OpenTelemetry Semantic Conventions](https://opentelemetry.io/docs/specs/semconv/)
- [Health Check Response Format RFC](https://datatracker.ietf.org/doc/html/draft-inadarei-api-health-check-06)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [Logging Guidelines](../Honua.Server.Observability/Logging/LoggingGuidelines.md)
