# Centralized Log Aggregation

This document describes the centralized log aggregation system for the Honua Process Framework using Grafana Loki and Promtail.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Configuration](#configuration)
- [Usage](#usage)
- [Sample Queries](#sample-queries)
- [Log Retention](#log-retention)
- [Troubleshooting](#troubleshooting)

## Overview

The Honua observability stack includes a comprehensive log aggregation system with:

- **Grafana Loki**: High-performance log aggregation system
- **Promtail**: Log collection agent that scrapes Docker container logs
- **30-day retention policy**: Automatic log cleanup after 30 days
- **Structured logging support**: JSON log parsing with label extraction
- **Trace correlation**: Automatic linking between logs, traces, and metrics

## Architecture

```
┌─────────────────┐
│ Docker          │
│ Containers      │──┐
└─────────────────┘  │
                     │
┌─────────────────┐  │
│ Application     │  │
│ Logs (Serilog)  │──┼──> ┌──────────┐    ┌──────────┐    ┌──────────┐
└─────────────────┘  │    │ Promtail │───>│   Loki   │───>│ Grafana  │
                     │    └──────────┘    └──────────┘    └──────────┘
┌─────────────────┐  │         │               │
│ System Logs     │──┘         │               │
└─────────────────┘            │               └──> 30-day retention
                               │
                        Parse & Label
                      (JSON, timestamps,
                       trace IDs, etc.)
```

### Components

1. **Loki** (`localhost:3100`)
   - Log storage and indexing
   - 30-day retention policy
   - Efficient compression and chunking
   - Label-based indexing for fast queries

2. **Promtail** (internal)
   - Scrapes Docker container logs
   - Parses structured JSON logs
   - Extracts labels for efficient filtering
   - Forwards logs to Loki

3. **Grafana** (`localhost:3000`)
   - Log visualization and exploration
   - Pre-built dashboards
   - Trace correlation
   - Real-time log streaming

## Configuration

### Loki Configuration

Located at: `/docker/process-testing/loki/loki-config.yaml`

Key settings:
- **Retention period**: 720 hours (30 days)
- **Chunk size**: 256KB
- **Max log line size**: 256KB
- **Ingestion rate**: 10MB/s per stream
- **Query timeout**: 60 seconds

### Promtail Configuration

Located at: `/docker/process-testing/promtail/promtail-config.yaml`

Features:
- Docker service discovery
- JSON log parsing
- Automatic label extraction
- Timestamp parsing from multiple formats
- Service-specific pipeline stages

### Supported Services

Promtail automatically collects logs from:
- `honua-cli-ai` - Process framework application
- `redis` - State storage
- `prometheus` - Metrics collection
- `grafana` - Visualization
- `otel-collector` - Observability collector
- `tempo` - Distributed tracing
- `loki` - Log aggregation (self-monitoring)

## Usage

### Starting the Stack

```bash
cd docker/process-testing
docker-compose up -d
```

### Accessing Logs

1. **Grafana Dashboard**: http://localhost:3000
   - Navigate to "Honua - Centralized Logs" dashboard
   - Default credentials: `admin` / `admin`

2. **Loki API**: http://localhost:3100
   - Health check: http://localhost:3100/ready
   - Metrics: http://localhost:3100/metrics

### Verifying Log Collection

```bash
# Check Promtail is running
docker logs honua-process-promtail

# Check Loki is receiving logs
curl http://localhost:3100/loki/api/v1/label/service/values

# Test log query
curl -G -s "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service="honua-cli-ai"}' | jq
```

## Sample Queries

### LogQL Basics

LogQL is Loki's query language, similar to PromQL for metrics.

#### Basic Log Stream Selection

```logql
# All logs from a service
{service="honua-cli-ai"}

# Filter by log level
{service="honua-cli-ai", level="ERROR"}

# Multiple services
{service=~"honua-cli-ai|redis"}

# All error and warning logs
{level=~"ERROR|WARN"}
```

#### Text Filtering

```logql
# Contains text (case-sensitive)
{service="honua-cli-ai"} |= "exception"

# Does not contain text
{service="honua-cli-ai"} != "debug"

# Regular expression match
{service="honua-cli-ai"} |~ "error|exception|failed"

# Case-insensitive search
{service="honua-cli-ai"} |~ "(?i)error"
```

#### JSON Parsing

```logql
# Parse JSON and filter
{service="honua-cli-ai"} | json | level="ERROR"

# Extract specific fields
{service="honua-cli-ai"} | json | line_format "{{.timestamp}} {{.message}}"

# Filter by nested JSON field
{service="honua-cli-ai"} | json | Properties.ProcessName="DeploymentProcess"
```

### Process Framework Queries

#### All Process Logs

```logql
{service="honua-cli-ai", process_name=~".+"}
```

#### Specific Process Execution

```logql
{service="honua-cli-ai", process_id="abc123"}
```

#### Process Step Logs

```logql
{service="honua-cli-ai", process_name="DeploymentProcess", step_name="ValidateDeploymentStep"}
```

#### Failed Process Steps

```logql
{service="honua-cli-ai", level="ERROR"}
  | json
  | message =~ "failed|error"
  | line_format "Process: {{.process_name}} Step: {{.step_name}} Error: {{.message}}"
```

#### Process Duration Analysis

```logql
sum by(process_name) (
  count_over_time(
    {service="honua-cli-ai", process_name=~".+"}
    | json
    | message =~ "completed"
    [5m]
  )
)
```

### Trace Correlation Queries

#### Logs by Trace ID

```logql
{trace_id="8e3c9d4f2a1b5e7c"}
```

#### Logs with Trace Context

```logql
{service="honua-cli-ai"}
  | json
  | trace_id != ""
  | line_format "TraceID: {{.trace_id}} | {{.message}}"
```

### Error Analysis Queries

#### Top Error Messages

```logql
topk(10,
  sum by(message) (
    count_over_time({level="ERROR"} | json [24h])
  )
)
```

#### Error Rate by Service

```logql
sum by(service) (
  rate({level="ERROR"} [5m])
)
```

#### Errors with Stack Traces

```logql
{level="ERROR"}
  | json
  | exception != ""
  | line_format "{{.timestamp}} [{{.service}}] {{.message}}\n{{.exception}}"
```

### Performance Queries

#### Slow Operations

```logql
{service="honua-cli-ai"}
  | json
  | duration_ms > 5000
  | line_format "{{.process_name}}.{{.step_name}}: {{.duration_ms}}ms"
```

#### Log Volume by Service

```logql
sum by(service) (count_over_time({service=~".+"} [1h]))
```

#### Logs per Second

```logql
sum(rate({service=~".+"} [1m]))
```

### Aggregation Queries

#### Count Logs in Time Range

```logql
count_over_time({service="honua-cli-ai"} [1h])
```

#### Average Log Rate

```logql
avg_over_time(
  sum(rate({service="honua-cli-ai"} [1m])) [5m]
)
```

#### Error Percentage

```logql
sum(rate({level="ERROR"} [5m]))
/
sum(rate({service=~".+"} [5m]))
* 100
```

## Log Retention

### Retention Policy

- **Duration**: 30 days (720 hours)
- **Enforcement**: Automated by Loki compactor
- **Deletion delay**: 2 hours after retention period
- **Grace period**: Logs up to 7 days old can still be ingested

### Retention Configuration

In `loki-config.yaml`:

```yaml
compactor:
  working_directory: /loki/compactor
  shared_store: filesystem
  compaction_interval: 10m
  retention_enabled: true
  retention_delete_delay: 2h
  retention_delete_worker_count: 150

limits_config:
  retention_period: 720h  # 30 days
```

### Monitoring Retention

Check compactor status:

```bash
# View compactor logs
docker logs honua-process-loki | grep compactor

# Check storage usage
docker exec honua-process-loki du -sh /loki/*
```

### Adjusting Retention

To change retention period, edit `loki-config.yaml`:

```yaml
limits_config:
  retention_period: 1440h  # 60 days
```

Then restart Loki:

```bash
docker-compose restart loki
```

## Structured Logging

### Serilog Configuration

The Honua.Cli.AI application uses Serilog for structured logging. Configuration in `appsettings.Testing.json`:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "System": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId", "WithProcessId"]
  }
}
```

### OpenTelemetry Logging

Logs are also exported via OpenTelemetry to Loki:

```json
{
  "OpenTelemetry": {
    "Logging": {
      "Enabled": true,
      "Exporters": ["otlp", "console"],
      "IncludeFormattedMessage": true,
      "IncludeScopes": true
    }
  }
}
```

### Adding Contextual Information

```csharp
using Serilog;

// Add structured properties
_logger.LogInformation(
    "Process {ProcessName} step {StepName} completed in {DurationMs}ms",
    processName,
    stepName,
    durationMs
);

// Add trace context
using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId))
using (LogContext.PushProperty("SpanId", Activity.Current?.SpanId))
{
    _logger.LogInformation("Operation completed");
}
```

## Grafana Dashboard

### Pre-built Dashboard

The "Honua - Centralized Logs" dashboard includes:

1. **Log Volume by Service** - Bar chart showing log volume
2. **Log Level Distribution** - Pie chart of log levels
3. **Error & Warning Rate** - Time series of error rates
4. **Process Framework Logs** - Filtered log viewer for processes
5. **Service Logs** - General log viewer for all services
6. **Logs by Trace ID** - Trace correlation viewer
7. **Top 10 Error Messages** - Most common errors
8. **Top Error/Warning Sources** - Services with most errors

### Dashboard Variables

- **service**: Filter by service name
- **level**: Filter by log level (INFO, WARN, ERROR, etc.)
- **process**: Filter by process framework process name
- **search**: Free-text search filter
- **trace_id**: Filter by distributed trace ID

### Creating Custom Queries

1. Open Grafana: http://localhost:3000
2. Go to Explore > Loki
3. Enter LogQL query
4. Adjust time range
5. Click "Run query"
6. Save to dashboard if needed

## Troubleshooting

### No Logs Appearing in Loki

1. **Check Promtail is running:**
   ```bash
   docker ps | grep promtail
   docker logs honua-process-promtail
   ```

2. **Verify Promtail can reach Loki:**
   ```bash
   docker exec honua-process-promtail wget -q -O- http://loki:3100/ready
   ```

3. **Check Promtail configuration:**
   ```bash
   docker exec honua-process-promtail cat /etc/promtail/promtail-config.yaml
   ```

4. **Test Loki directly:**
   ```bash
   curl http://localhost:3100/ready
   curl http://localhost:3100/loki/api/v1/label/service/values
   ```

### Logs Not Parsing Correctly

1. **Check Promtail pipeline stages:**
   ```bash
   docker logs honua-process-promtail | grep -i error
   ```

2. **Verify log format:**
   ```bash
   docker logs honua-cli-ai | head -10
   ```

3. **Test JSON parsing:**
   ```bash
   # Query raw logs
   curl -G -s "http://localhost:3100/loki/api/v1/query" \
     --data-urlencode 'query={service="honua-cli-ai"}' \
     --data-urlencode 'limit=10' | jq
   ```

### High Memory Usage

1. **Check Loki memory limits:**
   ```yaml
   processors:
     memory_limiter:
       check_interval: 1s
       limit_mib: 512
   ```

2. **Adjust chunk cache:**
   ```yaml
   chunk_store_config:
     chunk_cache_config:
       memcached:
         batch_size: 128  # Reduce from 256
   ```

3. **Reduce query cache:**
   ```yaml
   query_range:
     results_cache:
       cache:
         embedded_cache:
           max_size_mb: 250  # Reduce from 500
   ```

### Slow Queries

1. **Add more specific labels:**
   ```logql
   # Slow - scans all logs
   {service=~".+"} |= "error"

   # Fast - uses indexed labels
   {service="honua-cli-ai", level="ERROR"}
   ```

2. **Use smaller time ranges:**
   - Limit queries to recent time periods
   - Use `[1h]` instead of `[24h]` when possible

3. **Check query performance:**
   ```bash
   # Enable query logging in Loki
   docker logs honua-process-loki | grep "query_frontend"
   ```

### Disk Space Issues

1. **Check current usage:**
   ```bash
   docker exec honua-process-loki du -sh /loki/*
   ```

2. **Verify compaction is running:**
   ```bash
   docker logs honua-process-loki | grep compactor
   ```

3. **Manual compaction trigger:**
   ```bash
   # Restart Loki to trigger compaction
   docker-compose restart loki
   ```

4. **Reduce retention period:**
   Edit `loki-config.yaml` and reduce `retention_period`

## Integration with Other Services

### Tempo (Traces)

Logs automatically include trace IDs when using OpenTelemetry:

```csharp
using var activity = ActivitySource.StartActivity("OperationName");
_logger.LogInformation("Operation started");
// trace_id automatically added to logs
```

View correlated logs in Grafana by clicking trace IDs in Tempo.

### Prometheus (Metrics)

Query logs to generate metrics:

```logql
# Convert logs to metrics
sum by(service) (count_over_time({level="ERROR"} [5m]))
```

Create recording rules in Prometheus based on log patterns.

### Alerting

Create alerts based on log patterns:

```yaml
# In loki-config.yaml rules
groups:
  - name: error_alerts
    interval: 1m
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate({level="ERROR"} [5m])) > 10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High error rate detected"
```

## Performance Best Practices

1. **Use specific labels**: Always filter by `service`, `level`, or other indexed labels
2. **Limit time ranges**: Query recent logs first, expand if needed
3. **Use JSON parsing efficiently**: Parse JSON once, then filter
4. **Avoid regex when possible**: Use `|=` for substring matching instead of `|~`
5. **Pre-filter before aggregation**: Apply label filters before log line filters
6. **Use dashboard variables**: Enable dynamic filtering without query changes
7. **Set appropriate max lines**: Don't fetch more logs than needed

## Additional Resources

- [Loki Documentation](https://grafana.com/docs/loki/latest/)
- [LogQL Reference](https://grafana.com/docs/loki/latest/logql/)
- [Promtail Configuration](https://grafana.com/docs/loki/latest/clients/promtail/configuration/)
- [Grafana Explore Guide](https://grafana.com/docs/grafana/latest/explore/)

## Support

For issues or questions:
1. Check logs: `docker logs honua-process-loki` and `docker logs honua-process-promtail`
2. Review configuration files in `docker/process-testing/loki/` and `docker/process-testing/promtail/`
3. Test Loki API: http://localhost:3100/ready
4. Verify Grafana datasource: http://localhost:3000/datasources
