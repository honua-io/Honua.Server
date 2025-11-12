# Observability Configuration Migration Guide

## Overview

As of the latest version, observability (metrics, tracing, and request logging) is **enabled by default in production** configurations to ensure proper monitoring and troubleshooting capabilities.

## What Changed

### Production Configuration (appsettings.Production.json)

| Setting | Previous Default | New Default | Reason |
|---------|-----------------|-------------|---------|
| `observability.metrics.enabled` | `false` | `true` | Enable Prometheus metrics for monitoring |
| `observability.requestLogging.enabled` | Not configured | `true` | Enable audit trails and performance monitoring |
| `observability.tracing.exporter` | Not configured | `none` | Tracing ready but disabled by default (configure to enable) |

### Development Configuration (appsettings.Development.json)

| Setting | Previous Default | New Default | Reason |
|---------|-----------------|-------------|---------|
| `observability.logging.jsonConsole` | `true` | `false` | Plain text for better dev readability |
| `observability.metrics.enabled` | `false` | `true` | Enable local metrics testing |
| `observability.requestLogging.enabled` | Not configured | `true` | Enable request debugging |

## Breaking Changes

**None.** All changes are backward compatible:

1. **Environment Variables:** You can override any setting via environment variables
2. **Existing Configs:** If you have custom `appsettings.Production.json`, your settings take precedence
3. **Metrics Endpoint:** Already requires authentication (Viewer role) except in QuickStart mode

## Migration Paths

### Path 1: Keep Metrics Disabled (Not Recommended)

If you want to keep metrics disabled in production:

**Option A: Environment Variable**
```bash
export observability__metrics__enabled=false
```

**Option B: Custom Production Config**
```json
{
  "observability": {
    "metrics": {
      "enabled": false
    }
  }
}
```

### Path 2: Enable Distributed Tracing (Recommended)

To enable distributed tracing with Jaeger or Tempo:

**Via Environment Variables:**
```bash
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

**Via appsettings.Production.json:**
```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://tempo:4317"
    }
  }
}
```

### Path 3: Disable Request Logging (Not Recommended)

If you want to disable request logging:

```bash
export observability__requestLogging__enabled=false
```

## Verification Steps

### 1. Verify Metrics Endpoint

After deployment, verify the metrics endpoint is accessible:

```bash
# With authentication
curl -u username:password http://your-server/metrics

# Expected output: Prometheus text format metrics
# HELP http_requests_total Total HTTP requests
# TYPE http_requests_total counter
# http_requests_total{method="GET",path="/api/layers"} 123
```

### 2. Configure Prometheus Scraping

Add your Honua Server instance to Prometheus configuration:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'honua-server'
    scrape_interval: 15s
    static_configs:
      - targets: ['honua-server:5000']
    metrics_path: '/metrics'
    basic_auth:
      username: 'metrics-user'
      password: 'metrics-password'
```

### 3. Set Up Grafana Dashboards

Import the pre-built Grafana dashboards from `src/Honua.Server.Observability/grafana/dashboards/`.

### 4. Verify Request Logging

Check your logs for HTTP request entries:

```json
{
  "timestamp": "2025-11-10T10:30:45.123Z",
  "level": "Information",
  "message": "HTTP GET /api/layers completed with 200 in 45ms",
  "properties": {
    "RequestPath": "/api/layers",
    "RequestMethod": "GET",
    "StatusCode": 200,
    "ElapsedMs": 45
  }
}
```

## Performance Impact

### Metrics Collection

- **CPU Overhead:** ~1-2% (Prometheus exporter)
- **Memory Overhead:** ~10-50 MB (depending on metric cardinality)
- **Network:** Negligible (Prometheus pulls metrics)

### Request Logging

- **CPU Overhead:** ~0.5-1%
- **Disk I/O:** Depends on request volume and log destination
- **Recommendation:** Use structured logging with log aggregation (ELK, Loki, Splunk)

### Distributed Tracing

- **CPU Overhead:** ~2-5% (with OTLP exporter)
- **Network:** ~1-5 KB per trace span
- **Recommendation:** Use sampling (0.01-0.1) for high-volume production systems

## Troubleshooting

### Metrics Endpoint Returns 401 Unauthorized

The metrics endpoint requires authentication except in QuickStart mode.

**Solution:** Create a service account with Viewer role:

```bash
# Using Honua CLI
honua auth create-user --username metrics-scraper --role Viewer

# Or via API
curl -X POST http://localhost:5000/api/auth/users \
  -H "Content-Type: application/json" \
  -d '{"username":"metrics-scraper","password":"secure-password","role":"Viewer"}'
```

### High Memory Usage After Enabling Metrics

Reduce metric cardinality by limiting label combinations.

**Solution:** Review and optimize metric labels in custom code.

### Request Logging Too Verbose

Reduce logging by increasing the slow threshold or disabling in non-production environments.

**Solution:**
```bash
export observability__requestLogging__slowThresholdMs=10000  # Only log >10s requests
```

## Rollback Instructions

If you need to rollback to the previous behavior:

### Quick Rollback (Environment Variables)

```bash
export observability__metrics__enabled=false
export observability__requestLogging__enabled=false
```

### Permanent Rollback (Configuration File)

Create or update `appsettings.Production.json`:

```json
{
  "observability": {
    "metrics": {
      "enabled": false
    },
    "requestLogging": {
      "enabled": false
    }
  }
}
```

## Security Considerations

### Metrics Endpoint

- **Default:** Requires authentication (Viewer role or higher)
- **QuickStart Mode:** No authentication (development only)
- **Recommendation:** Use a dedicated service account with minimal permissions

### Request Logging

- **Headers:** NOT logged by default (may contain sensitive data)
- **To Enable:** Set `observability.requestLogging.logHeaders` to `true`
- **Warning:** May expose authentication tokens, cookies, and API keys

### Distributed Tracing

- **PII:** Avoid adding personally identifiable information to trace tags
- **Sensitive Data:** Do not include passwords, tokens, or API keys in trace attributes
- **Recommendation:** Use trace sampling and redact sensitive fields

## Monitoring Best Practices

### 1. Set Up Alerting

Configure Prometheus alerts for critical metrics:

```yaml
# alerts.yml
groups:
  - name: honua-server
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status_code=~"5.."}[5m]) > 0.05
        annotations:
          summary: "High error rate detected"

      - alert: SlowRequests
        expr: histogram_quantile(0.95, http_request_duration_seconds_bucket) > 5
        annotations:
          summary: "95th percentile latency > 5s"
```

### 2. Use Grafana Dashboards

Import dashboards from `src/Honua.Server.Observability/grafana/dashboards/`:

- `honua-overview.json` - High-level system metrics
- Custom dashboards for specific features (STAC, OGC, WFS, etc.)

### 3. Enable Distributed Tracing

For production systems with complex request flows:

```bash
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

### 4. Integrate with Log Aggregation

Configure Serilog to send logs to your aggregation system:

- **ELK Stack:** Use Serilog.Sinks.Elasticsearch
- **Loki:** Use Serilog.Sinks.Grafana.Loki
- **Splunk:** Use Serilog.Sinks.Splunk

## Support

If you encounter issues with the new observability defaults:

1. **Documentation:** See [Configuration Guide](README.md) and [Tracing Guide](../architecture/tracing.md)
2. **GitHub Issues:** Report bugs at https://github.com/honua-io/Honua.Server/issues
3. **Environment Variables:** Use environment variables to override defaults without modifying configs

## Summary

The new observability defaults provide:

- ✅ **Better Monitoring:** Metrics enabled by default in production
- ✅ **Audit Trails:** Request logging for troubleshooting
- ✅ **Backward Compatible:** Override via environment variables or configs
- ✅ **Secure:** Metrics endpoint requires authentication
- ✅ **Flexible:** Disable if needed, enable tracing when ready

**Recommendation:** Keep the new defaults and configure distributed tracing for comprehensive observability.
