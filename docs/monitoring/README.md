# Honua Server Monitoring Guide

Complete guide to setting up, configuring, and maintaining production monitoring for Honua Server using Prometheus, Grafana, and distributed tracing.

## Table of Contents

- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Setup Instructions](#setup-instructions)
- [Dashboard Descriptions](#dashboard-descriptions)
- [Alert Rules](#alert-rules)
- [Distributed Tracing](#distributed-tracing)
- [SLAs and SLOs](#slas-and-slos)
- [Troubleshooting](#troubleshooting)
- [Best Practices](#best-practices)

## Quick Start

### One-Command Setup (Local Development)

```bash
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
```

This starts:
- **Prometheus** (metrics storage): http://localhost:9090
- **Grafana** (visualization): http://localhost:3000 (admin/admin)
- **Alertmanager** (alert routing): http://localhost:9093
- **PostgreSQL Exporter** (optional): http://localhost:9187
- **Node Exporter** (optional): http://localhost:9100

### With Distributed Tracing

```bash
# Start both monitoring and tracing stacks
cd src/Honua.Server.Observability
docker-compose -f docker-compose.monitoring.yml up -d
docker-compose -f docker-compose.jaeger.yml up -d

# Access Jaeger UI at http://localhost:16686
```

### Configure Honua Server

```csharp
// Program.cs
using Honua.Server.Observability;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Add observability services
builder.Services.AddHonuaObservability(
    serviceName: "Honua.Server",
    serviceVersion: "1.0.0",
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")
);

// Enable metrics endpoint
app.UsePrometheusMetrics();

// Start the app
app.Run();
```

### Verify Setup

```bash
# Check metrics endpoint
curl http://localhost:5000/metrics

# Check Prometheus is scraping
curl http://localhost:9090/api/v1/targets

# Verify Grafana datasource
curl http://localhost:3000/api/datasources
```

## Architecture Overview

### Components

```
┌─────────────────┐
│  Honua Server   │ → Exports metrics to /metrics endpoint
└────────┬────────┘
         │ (Prometheus scrape every 15s)
         ↓
┌─────────────────┐
│  Prometheus     │ → Scrapes metrics, evaluates alerts
└────────┬────────┘
         │
         ├─→ ┌──────────────┐
         │   │   Grafana    │ → Visualizes metrics (dashboards)
         │   └──────────────┘
         │
         └─→ ┌──────────────────┐
             │  Alertmanager    │ → Routes alerts to channels
             └──────────────────┘
                    │
                    ├─→ Slack
                    ├─→ PagerDuty
                    └─→ Email
```

### Metrics Flow

```
Application Instrumentation
    ↓
OpenTelemetry Meters (C# System.Diagnostics.Metrics)
    ↓
Prometheus Exporter (/metrics endpoint)
    ↓
Prometheus Scraper (15s interval)
    ↓
Time Series Database (TSDB)
    ↓
Prometheus Query Engine → Grafana Dashboards
    ↓
Alert Rules Engine → Alertmanager → Notifications
```

## Setup Instructions

### 1. Production Prometheus Configuration

Edit `prometheus/prometheus.yml` for production:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'honua-production'
    environment: 'production'

# Enable authentication for metrics endpoint
scrape_configs:
  - job_name: 'honua-server'
    metrics_path: '/metrics'
    basic_auth:
      username: 'prometheus'
      password: 'secure-password'
    static_configs:
      - targets: ['honua-server:5000']
        labels:
          service: 'honua-server'
          tier: 'backend'
    relabel_configs:
      # Drop high-cardinality metrics to prevent explosion
      - source_labels: [__name__]
        regex: '(process_runtime_.*)'
        action: drop

# Enable long-term storage
remote_write:
  - url: 'https://prometheus-remote-storage.example.com/api/v1/write'
    basic_auth:
      username: 'prometheus'
      password: 'password'
    queue_config:
      capacity: 10000
      max_shards: 5
```

### 2. Application Configuration

Set environment variables for production:

```bash
# Metrics
export observability__metrics__enabled=true

# Distributed Tracing (OTLP)
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317

# Logging
export Serilog__MinimumLevel=Information
export Serilog__WriteTo__0__Name=File
```

### 3. Kubernetes Deployment

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
    scrapeTimeout: 10s
    scheme: https
    tlsConfig:
      insecureSkipVerify: true
    basicAuth:
      username:
        name: prometheus-auth
        key: username
      password:
        name: prometheus-auth
        key: password
```

### 4. Grafana Setup

#### Import Dashboards

1. Open Grafana: http://localhost:3000
2. Log in (admin/admin)
3. Go to Dashboards → Import
4. Paste dashboard JSON or upload from `/grafana/dashboards/`
5. Select Prometheus datasource
6. Click Import

Available dashboards:
- `honua-overview.json` - System overview
- `honua-database.json` - Database metrics
- `honua-cache.json` - Cache performance
- `honua-errors.json` - Error rates and health
- `honua-latency.json` - Response times

#### Create Alert Notification Channels

1. Go to Alerting → Contact Points
2. Add channels:
   - **Slack**: Webhook URL from Slack workspace
   - **PagerDuty**: Integration key
   - **Email**: SMTP configuration
   - **Webhook**: Custom endpoint

### 5. Alert Configuration

Edit `alertmanager/alertmanager.yml`:

```yaml
global:
  resolve_timeout: 5m

route:
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'slack-warnings'
  routes:
    - match:
        severity: critical
      receiver: 'pagerduty'
      continue: true

    - match:
        severity: warning
      receiver: 'slack-warnings'
      continue: false

receivers:
  - name: 'slack-warnings'
    slack_configs:
      - channel: '#honua-alerts'
        title: 'Alert: {{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'
        send_resolved: true
        api_url: 'YOUR_SLACK_WEBHOOK_URL'

  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'

inhibit_rules:
  - source_match:
      severity: 'critical'
    target_match:
      severity: 'warning'
    equal: ['alertname', 'cluster', 'service']
```

## Dashboard Descriptions

### Honua - Overview Dashboard

**Purpose**: System-wide health and performance

**Panels**:
- Build Queue Depth: Number of builds waiting to run
- Cache Hit Rate: Percentage of cache lookups that hit
- HTTP Error Rate: Percentage of 5xx responses
- Active Conversations: Current AI conversations

**Key Metrics**:
- `builds_in_queue` - Current queue depth
- `cache_lookups_total` - Cache lookup rate
- `http_requests_total` - Request throughput

### Honua - Database Metrics Dashboard

**Purpose**: Database performance and health

**Panels**:
- Query Rate: Queries per second
- Query Duration (p50, p95, p99): Latency percentiles
- Connection Pool: Available, in-use, and max connections
- Error Rate: Database error rate

**Key Metrics**:
- `db_queries_total` - Query count
- `db_query_duration_seconds_bucket` - Query latency histogram
- `db_connection_pool_available` - Available connections
- `db_errors_total` - Error count

**Thresholds**:
- p95 Query Duration: Alert if > 2 seconds
- Connection Pool: Alert if < 5 available
- Error Rate: Alert if > 5%

### Honua - Cache Performance Dashboard

**Purpose**: Cache effectiveness and efficiency

**Panels**:
- Cache Hit Rate: Gauge showing hit percentage
- Lookups (Hits vs Misses): Time series comparison
- Eviction Rate: Cache evictions per second
- Cache Entries: Number of cached items
- Time Saved: Cumulative time saved by cache hits

**Key Metrics**:
- `cache_lookups_total` - Lookup count by result
- `cache_evictions_total` - Eviction count
- `cache_entries_total` - Current entries
- `cache_savings_seconds_total` - Time saved

**Target**: Cache hit rate > 50%

### Honua - Error Rates & Health Dashboard

**Purpose**: Error tracking and SLO monitoring

**Panels**:
- 5xx Error Rate: Gauge and time series
- 4xx Error Rate: Client error tracking
- HTTP Error Rate Over Time: Historical trend
- Component-Specific Error Rates: By component
- Top Error Endpoints: Endpoints with most errors

**Key Metrics**:
- `http_requests_total` - Request count by status
- `db_errors_total` - Database errors
- `conversation_errors_total` - AI conversation errors

**SLO Target**: < 0.1% error rate (99.9% success)

### Honua - Response Times & Latency Dashboard

**Purpose**: Performance and user experience

**Panels**:
- p50/p95/p99/Max Response Times: Individual gauges
- Response Time Distribution: Time series with percentiles
- p95 by Endpoint: Latency per API endpoint

**Key Metrics**:
- `http_request_duration_seconds_bucket` - Latency histogram

**SLO Targets**:
- p50 < 500ms (median response)
- p95 < 5s (95th percentile)
- p99 < 10s (99th percentile)

## Alert Rules

### System Alerts

#### HighMemoryUsage (Warning)
- Condition: Process memory > 4GB
- Duration: 10 minutes
- Action: Check for memory leaks, consider scaling

#### CriticalMemoryUsage (Critical)
- Condition: Process memory > 6GB
- Duration: 5 minutes
- Action: Immediate intervention, may cause OOM

### Database Alerts

#### DatabaseConnectionPoolExhausted (Critical)
- Condition: Available connections < 5
- Duration: 5 minutes
- Action: Increase pool size or identify slow queries

#### DatabaseSlowQueries (Warning)
- Condition: p95 query duration > 2 seconds
- Duration: 10 minutes
- Action: Review slow query log, optimize indexes

#### HighDatabaseErrorRate (Warning)
- Condition: Error rate > 5%
- Duration: 5 minutes
- Action: Check logs, verify database connectivity

### HTTP/Performance Alerts

#### P95ResponseTimeHigh (Warning)
- Condition: p95 response time > 5s
- Duration: 10 minutes
- Action: Profile application, check resources

#### P99ResponseTimeHigh (Critical)
- Condition: p99 response time > 10s
- Duration: 5 minutes
- Action: Immediate investigation required

#### HighHTTPErrorRate (Warning)
- Condition: 5xx error rate > 5%
- Duration: 5 minutes
- Action: Check logs, identify root cause

### Availability Alerts

#### ServiceDown (Critical)
- Condition: Service unreachable
- Duration: 2 minutes
- Action: Page on-call, start incident response

#### HighErrorBudgetBurn (Critical)
- Condition: 5xx error rate > 10%
- Duration: 5 minutes
- Action: SLO at risk, escalate immediately

### Business Alerts

#### HighBuildQueueDepth (Warning)
- Condition: Queue depth > 100
- Duration: 5 minutes
- Action: Scale build workers

#### LowCacheHitRate (Warning)
- Condition: Hit rate < 50%
- Duration: 15 minutes
- Action: Review cache configuration, increase size

## Distributed Tracing

### Quick Start with Jaeger

```bash
# Start Jaeger (all-in-one)
docker-compose -f docker-compose.jaeger.yml up -d

# Configure Honua for OTLP
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://localhost:4317

# Access UI at http://localhost:16686
```

### Tracing Configuration

#### Enable in Development

```json
{
  "observability": {
    "tracing": {
      "exporter": "console"
    }
  }
}
```

#### Enable in Production

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

### Activity Sources (Tracing Scopes)

| Source | Scope |
|--------|-------|
| `Honua.Server.Database` | Database operations |
| `Honua.Server.Authentication` | Auth/authz checks |
| `Honua.Server.Export` | Data export operations |
| `Honua.Server.Import` | Data import operations |

### Trace Analysis Examples

#### Finding Slow Requests

1. Open Jaeger UI
2. Select service "Honua.Server"
3. Click "Find Traces"
4. In "Look Back", select "1h"
5. Click "Find Traces"
6. Sort by "Duration" (descending)
7. Click on slowest trace
8. Examine span timeline to find bottleneck

#### Identifying Error Chains

1. Use "Tags" filter: `error=true`
2. Click on errored trace
3. Find span with status "ERROR"
4. Check "Exception" field for stack trace
5. Identify root cause in parent spans

#### Tracking Request Flow

1. Copy trace ID from logs
2. Go to Jaeger UI
3. Paste trace ID in search
4. View complete request flow:
   - HTTP ingress
   - Authentication
   - Database queries
   - Response serialization
   - HTTP egress

### Span Tags Best Practices

When instrumenting code:

```csharp
using var activity = HonuaTelemetry.Database.StartActivity("QueryUsers");
activity?.SetTag("db.system", "postgresql");
activity?.SetTag("db.statement", "SELECT * FROM users");
activity?.SetTag("db.rows", resultCount);
activity?.SetTag("span.kind", "client");

try
{
    var result = await ExecuteQueryAsync();
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.RecordException(ex);
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    throw;
}
```

## SLAs and SLOs

### Service Level Objectives (SLOs)

SLOs define the acceptable performance and reliability targets.

#### Availability SLO

**Target**: 99.9% uptime (9 hours downtime/month)

- Measured: Successful HTTP responses / Total HTTP requests
- Excludes: Planned maintenance, user errors (4xx)
- Tracked: Per-dashboard metric "HighErrorBudgetBurn"

#### Latency SLO

**Target**: p95 < 5 seconds

- Measured: 95th percentile HTTP response time
- Includes: All endpoints except health checks
- Tracked: Dashboard "Honua - Response Times & Latency"

#### Error Budget

Monthly error budget: 1 - 0.999 = 0.001 = 0.1%

If you exceed 0.1% errors in a month:
1. Alert fires: "HighErrorBudgetBurn"
2. Incident response initiated
3. Focus shifts to stability over features
4. Postmortem scheduled after resolution

#### Database SLO

**Target**: p95 query duration < 2 seconds

- Measured: 95th percentile query duration
- Excludes: Backup, maintenance operations
- Tracked: Dashboard "Honua - Database Metrics"

### Example SLO Calculation

If your service processes 1M requests/month:

- Error budget = 1,000 errors (0.1%)
- Per day = ~33 errors/day
- Per hour = ~1.4 errors/hour
- Per minute = ~0.023 errors/minute

If error rate hits 5% for 5 minutes:
- You've used 31% of monthly budget immediately
- Alert fires
- Investigation + remediation required

### Monitoring SLOs

```promql
# Monthly error rate
(1 - (sum(increase(http_requests_total{status_class!="5xx"}[30d])) /
       sum(increase(http_requests_total[30d])))) * 100

# Error budget remaining (%)
100 - ((sum(increase(http_requests_total{status_class="5xx"}[30d])) /
        (sum(increase(http_requests_total[30d])) * 0.001)) * 100)

# P95 latency
histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[30d])) by (le))
```

## Troubleshooting

### Metrics Not Appearing

**Problem**: Dashboard shows no data or metrics are missing.

**Investigation**:

1. Verify `/metrics` endpoint is accessible:
   ```bash
   curl http://localhost:5000/metrics | head -20
   ```

2. Check Prometheus is scraping:
   ```bash
   curl http://localhost:9090/api/v1/targets
   # Look for status: "up" for honua-server job
   ```

3. Verify metrics endpoint is enabled:
   ```csharp
   // In Program.cs
   app.UsePrometheusMetrics(); // Must be called
   ```

4. Check Prometheus logs:
   ```bash
   docker logs honua-prometheus | tail -50
   ```

**Common Causes**:
- `UsePrometheusMetrics()` not called
- Wrong port in prometheus.yml (default: 5000)
- Firewall blocking 5000 port
- Authentication required but not configured

### High Memory Usage

**Problem**: Prometheus or Grafana consuming excessive memory.

**Investigation**:

1. Check metric cardinality:
   ```bash
   # High cardinality metrics can use lots of memory
   curl http://localhost:9090/api/v1/label/__name__/values | wc -l
   ```

2. Identify high-cardinality metrics:
   ```bash
   # In Prometheus UI, query:
   # count by (__name__) (topk(50, count by(__name__, job) (rate(foo[5m]))))
   ```

**Solutions**:
- Add relabel_config to drop unwanted labels:
  ```yaml
  relabel_configs:
    - source_labels: [__name__]
      regex: 'high_cardinality_metric'
      action: drop
  ```
- Reduce Prometheus retention time (default: 15d)
- Increase scrape interval (default: 15s)
- Use remote storage for long-term retention

### Alerts Not Firing

**Problem**: Alert rules are defined but not triggering.

**Investigation**:

1. Check alert rule syntax in Prometheus:
   ```bash
   curl http://localhost:9090/api/v1/rules
   # Look for your alert in response
   ```

2. Test alert rule manually:
   ```bash
   # In Prometheus UI, query the alert expression
   # e.g., for HighMemoryUsage alert:
   # process_resident_memory_bytes / 1024 / 1024 / 1024 > 4
   ```

3. Check Alertmanager configuration:
   ```bash
   curl http://localhost:9093/api/v1/alerts
   # Should show active alerts
   ```

4. Verify notification channels:
   ```bash
   # In Alertmanager UI, check:
   # - Status → Alerts (should show active alerts)
   # - Config (check receivers are configured)
   ```

**Common Causes**:
- Alert expression doesn't match any metrics
- "for" duration not met (e.g., alert requires 10m duration)
- Alertmanager receiver not configured
- Webhook URL invalid or unreachable

### Dashboard Not Loading

**Problem**: Grafana dashboard shows errors or no data.

**Investigation**:

1. Check datasource connectivity:
   - Grafana → Configuration → Data Sources
   - Click Prometheus datasource
   - Scroll down, click "Test"

2. Verify dashboard JSON:
   - Check browser console (F12) for errors
   - Verify metric names exist in Prometheus

3. Check query syntax:
   - Click panel edit icon
   - Verify PromQL query syntax
   - Run query in Prometheus UI

**Solutions**:
- Re-import dashboard JSON
- Create new dashboard from scratch
- Check Prometheus UI to verify metrics exist

### High Cardinality Issues

**Problem**: "Metric cardinality too high" error or OOM.

**Investigation**:

1. Identify problematic metrics:
   ```bash
   # List metrics with their cardinality
   curl 'http://localhost:9090/api/v1/label/__name__/values' | \
   xargs -I {} curl 'http://localhost:9090/api/v1/query' \
   -d query='count({__name__="{}"})' | jq '.data.result[].value'
   ```

2. Find high-cardinality labels:
   ```bash
   # Look for metrics with many label combinations
   # e.g., request_path label with 1000+ unique values
   ```

**Solutions**:
- Drop metrics with unnecessary labels:
  ```yaml
  relabel_configs:
    - source_labels: [__name__, path]
      regex: 'http_request_duration_seconds;.*/api/internal/.*'
      action: drop
  ```
- Use metric_relabel_configs to drop label values
- Configure histogram buckets carefully (each bucket = new series)

### Tracing Not Working

**Problem**: No traces appearing in Jaeger/Tempo.

**Investigation**:

1. Verify Jaeger is running:
   ```bash
   curl http://localhost:16686 # Should respond
   ```

2. Verify OTLP endpoint is reachable:
   ```bash
   curl http://localhost:4317
   # Should return connection reset (GRPC endpoint, not HTTP)
   ```

3. Check Honua configuration:
   ```bash
   export observability__tracing__exporter=otlp
   export observability__tracing__otlpEndpoint=http://localhost:4317
   ```

4. Verify service name in traces:
   - Go to Jaeger UI
   - Service dropdown should show "Honua.Server"
   - If not, check ServiceName parameter in code

**Common Causes**:
- `exporter: "none"` (tracing disabled)
- OTLP endpoint unreachable
- Jaeger not running
- Service name not configured
- Network/Docker DNS issues

## Best Practices

### Metrics Instrumentation

1. **Choose the Right Metric Type**:
   - Counter: Always increasing (requests, errors, bytes)
   - Gauge: Up and down (queue depth, connections)
   - Histogram: Distribution of values (latencies)
   - Summary: Similar to histogram but with quantiles

2. **Keep Label Cardinality Low**:
   ```csharp
   // Good: Fixed label values
   _meter.CreateCounter<long>("http_requests_total")
       .Add(1, new("method", "GET"), new("status", "200"));

   // Bad: User ID as label (unbounded cardinality)
   .Add(1, new("user_id", userId));
   ```

3. **Use Appropriate Histogram Buckets**:
   ```csharp
   // For latency (seconds)
   var histogram = _meter.CreateHistogram<double>(
       "request_duration_seconds",
       unit: "s"
   );
   // Default buckets work for most cases
   ```

### Alerting Best Practices

1. **Avoid Alert Fatigue**:
   - Set reasonable thresholds (not too sensitive)
   - Use "for" duration to reduce noise
   - Group related alerts

2. **Write Clear Descriptions**:
   ```yaml
   annotations:
     summary: "High memory usage detected"
     description: |
       Memory usage is {{ $value }}GB (threshold: 4GB).
       Possible causes:
       - Memory leak in {{ $labels.component }}
       - Insufficient GC tuning
       - Spike in legitimate traffic
   ```

3. **Page On-Call for Critical Only**:
   ```yaml
   routes:
     - match:
         severity: critical
       receiver: 'pagerduty'  # Pages on-call
     - match:
         severity: warning
       receiver: 'slack'      # Just notification
   ```

### Dashboard Best Practices

1. **Organize by User Journey**:
   - Overview dashboard first
   - Component-specific dashboards
   - Drill-down patterns

2. **Use Consistent Color Schemes**:
   - Green: Healthy/Good
   - Yellow: Warning/Attention
   - Red: Critical/Error

3. **Include Context**:
   - Add annotation when alerts fire
   - Link to runbooks
   - Show SLO status

### On-Call Runbook Practices

1. **Make it Actionable**:
   - List specific diagnostic commands
   - Include metric queries
   - Provide escalation path

2. **Keep it Updated**:
   - Review after each incident
   - Update based on lessons learned
   - Version control runbooks

3. **Include Examples**:
   ```bash
   # Example: Check high error rate
   curl 'http://prometheus:9090/api/v1/query' \
     -d 'query=rate(http_requests_total{status_class="5xx"}[5m])'
   ```

## See Also

- [Tracing Documentation](../architecture/tracing.md)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)

## Support

For issues or questions:
1. Check [Troubleshooting](#troubleshooting) section
2. Review [Honua Server Observability README](../../src/Honua.Server.Observability/README.md)
3. Open an issue on GitHub

---

**Last Updated**: November 2024
**Version**: 1.0.0
