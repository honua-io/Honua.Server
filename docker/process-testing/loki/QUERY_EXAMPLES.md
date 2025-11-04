# Loki Query Examples - Quick Reference

This document provides ready-to-use LogQL queries for common log analysis tasks.

## Table of Contents

- [Basic Queries](#basic-queries)
- [Process Framework](#process-framework)
- [Error Analysis](#error-analysis)
- [Performance Monitoring](#performance-monitoring)
- [Trace Correlation](#trace-correlation)
- [Security & Audit](#security--audit)
- [Aggregations & Metrics](#aggregations--metrics)

## Basic Queries

### View All Logs from a Service

```logql
{service="honua-cli-ai"}
```

### Filter by Log Level

```logql
{service="honua-cli-ai", level="ERROR"}
```

### Search for Specific Text

```logql
{service="honua-cli-ai"} |= "deployment"
```

### Case-Insensitive Search

```logql
{service="honua-cli-ai"} |~ "(?i)error"
```

### Exclude Logs Containing Text

```logql
{service="honua-cli-ai"} != "health check"
```

### Multiple Services

```logql
{service=~"honua-cli-ai|redis|prometheus"}
```

### Time Range Filter

```logql
{service="honua-cli-ai"} [5m]
```

## Process Framework

### All Process Executions

```logql
{service="honua-cli-ai", process_name=~".+"}
```

### Specific Process Type

```logql
{service="honua-cli-ai", process_name="DeploymentProcess"}
```

### Single Process Execution by ID

```logql
{service="honua-cli-ai", process_id="550e8400-e29b-41d4-a716-446655440000"}
```

### Process Step Logs

```logql
{service="honua-cli-ai", process_name="DeploymentProcess", step_name="ValidateDeploymentStep"}
```

### Failed Process Steps

```logql
{service="honua-cli-ai", level="ERROR"}
  | json
  | process_name != ""
  | line_format "Process: {{.process_name}} | Step: {{.step_name}} | Error: {{.message}}"
```

### Process Completion Events

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "Process .* completed"
  | line_format "{{.timestamp}} - {{.process_name}} completed in {{.duration_ms}}ms"
```

### Process Rollbacks

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "rollback|rolling back"
  | line_format "{{.timestamp}} - {{.process_name}} rolled back: {{.message}}"
```

### Long-Running Processes

```logql
{service="honua-cli-ai"}
  | json
  | duration_ms > 30000
  | line_format "Slow process: {{.process_name}} ({{.duration_ms}}ms)"
```

### Process State Changes

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "State changed|state transition"
  | line_format "{{.process_name}}: {{.old_state}} → {{.new_state}}"
```

## Error Analysis

### All Errors

```logql
{level="ERROR"}
```

### Errors with Stack Traces

```logql
{level="ERROR"}
  | json
  | exception != ""
  | line_format "{{.message}}\n\nStack Trace:\n{{.exception}}"
```

### Top 10 Error Messages

```logql
topk(10,
  sum by(message) (
    count_over_time({level="ERROR"} | json [24h])
  )
)
```

### Error Rate per Service

```logql
sum by(service) (rate({level="ERROR"} [5m]))
```

### Errors by Source Context

```logql
{level="ERROR"}
  | json
  | source_context != ""
  | line_format "[{{.source_context}}] {{.message}}"
```

### Recent Unique Errors

```logql
sum by(message) (
  count_over_time({level="ERROR"} | json [1h])
)
```

### Errors During Specific Time

```logql
{level="ERROR"}
  | json
  | __timestamp__ >= 1609459200  # Unix timestamp
  | __timestamp__ <= 1609545600
```

### Database Errors

```logql
{service="honua-cli-ai", level="ERROR"}
  | json
  | message =~ "(?i)database|sql|postgres|connection"
```

### Authentication Errors

```logql
{service="honua-cli-ai", level="ERROR"}
  | json
  | message =~ "(?i)auth|unauthorized|forbidden|token"
```

## Performance Monitoring

### Slow Operations (>5 seconds)

```logql
{service="honua-cli-ai"}
  | json
  | duration_ms > 5000
  | line_format "{{.operation}}: {{.duration_ms}}ms"
```

### HTTP Request Duration

```logql
{service="honua-cli-ai"}
  | json
  | http_method != ""
  | line_format "{{.http_method}} {{.http_path}}: {{.duration_ms}}ms ({{.http_status_code}})"
```

### Average Operation Duration

```logql
avg_over_time(
  {service="honua-cli-ai"}
    | json
    | unwrap duration_ms [5m]
)
```

### 95th Percentile Latency

```logql
quantile_over_time(0.95,
  {service="honua-cli-ai"}
    | json
    | unwrap duration_ms [5m]
)
```

### Memory Usage Logs

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)memory|heap|gc"
```

### High CPU Usage

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)cpu|processor"
  | cpu_usage > 80
```

## Trace Correlation

### Logs by Trace ID

```logql
{trace_id="8e3c9d4f2a1b5e7c"}
```

### All Logs with Traces

```logql
{service="honua-cli-ai"}
  | json
  | trace_id != ""
```

### Trace and Span Context

```logql
{service="honua-cli-ai"}
  | json
  | trace_id != ""
  | line_format "Trace: {{.trace_id}} | Span: {{.span_id}} | {{.message}}"
```

### Find Traces with Errors

```logql
{level="ERROR"}
  | json
  | trace_id != ""
  | line_format "Error in Trace {{.trace_id}}: {{.message}}"
```

### Distributed Trace Timeline

```logql
{trace_id="8e3c9d4f2a1b5e7c"}
  | json
  | line_format "{{.timestamp}} | {{.service}} | {{.span_name}} | {{.message}}"
```

## Security & Audit

### Authentication Events

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)login|logout|authenticate"
  | line_format "{{.timestamp}} - User: {{.user}} - {{.message}}"
```

### Authorization Failures

```logql
{service="honua-cli-ai", level=~"ERROR|WARN"}
  | json
  | message =~ "(?i)unauthorized|forbidden|permission denied"
```

### API Key Usage

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)api.key"
  | line_format "{{.timestamp}} - API Key: {{.api_key_id}} - {{.endpoint}}"
```

### Configuration Changes

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)configuration.*changed|config.*updated"
  | line_format "{{.timestamp}} - {{.user}} changed {{.config_key}}: {{.old_value}} → {{.new_value}}"
```

### Sensitive Data Access

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?i)secret|credential|password|token"
  | level =~ "WARN|ERROR"
```

### Failed Login Attempts

```logql
{service="honua-cli-ai", level="WARN"}
  | json
  | message =~ "(?i)failed login|authentication failed"
  | line_format "{{.timestamp}} - Failed login from {{.ip_address}} for user {{.username}}"
```

## Aggregations & Metrics

### Log Volume per Service

```logql
sum by(service) (count_over_time({service=~".+"} [1h]))
```

### Logs per Second

```logql
sum(rate({service=~".+"} [1m]))
```

### Error Percentage

```logql
(
  sum(rate({level="ERROR"} [5m]))
  /
  sum(rate({service=~".+"} [5m]))
) * 100
```

### Most Active Services

```logql
topk(5,
  sum by(service) (rate({service=~".+"} [5m]))
)
```

### Log Level Distribution

```logql
sum by(level) (count_over_time({level=~".+"} [1h]))
```

### Unique Users Active

```logql
count(
  count by(user) (
    {service="honua-cli-ai"} | json | user != "" [1h]
  )
)
```

### Process Completion Rate

```logql
sum(rate({service="honua-cli-ai"} | json | message =~ "completed successfully" [5m]))
/
sum(rate({service="honua-cli-ai"} | json | message =~ "started" [5m]))
```

### Average Process Duration

```logql
avg(
  avg_over_time(
    {service="honua-cli-ai"}
      | json
      | process_name != ""
      | unwrap duration_ms [5m]
  )
)
```

## Advanced Patterns

### Log Pattern Detection

```logql
{service="honua-cli-ai"}
  | pattern `<_> [<level>] <_> <message>`
  | line_format "{{.level}}: {{.message}}"
```

### Multi-line Log Aggregation

```logql
{service="honua-cli-ai"}
  | json
  | message =~ "(?s)Exception.*\n.*"  # Match multi-line exceptions
```

### Cardinality Check

```logql
# Count unique values for a label
count(count by(process_name) ({service="honua-cli-ai"}))
```

### Rate of Change Detection

```logql
# Detect sudden spikes in error rate
(
  rate({level="ERROR"} [5m])
  -
  rate({level="ERROR"} [5m] offset 1h)
) > 5
```

### Logs with Specific JSON Structure

```logql
{service="honua-cli-ai"}
  | json
  | Properties_ProcessId != ""
  | Properties_StepName != ""
  | line_format "Process {{.Properties_ProcessId}} Step {{.Properties_StepName}}: {{.message}}"
```

### Compare Time Periods

```logql
# Current vs 1 hour ago
sum(rate({service="honua-cli-ai"} [5m]))
/
sum(rate({service="honua-cli-ai"} [5m] offset 1h))
```

## Query Optimization Tips

1. **Always start with label filters**: `{service="x"}` is much faster than `{} |= "x"`

2. **Use specific time ranges**: Shorter ranges = faster queries

3. **Parse JSON once**:
   ```logql
   # Good
   {service="x"} | json | field1 != "" | field2 != ""

   # Bad
   {service="x"} | json | field1 != "" | json | field2 != ""
   ```

4. **Avoid unnecessary regex**:
   ```logql
   # Fast
   {service="x"} |= "error"

   # Slower
   {service="x"} |~ "error"
   ```

5. **Use unwrap for numeric operations**:
   ```logql
   avg_over_time(
     {service="x"} | json | unwrap duration_ms [5m]
   )
   ```

6. **Pre-aggregate when possible**: Use metric queries instead of processing all logs

## Common Variables for Dashboards

Use these in Grafana dashboard variables:

```logql
# Services
label_values(service)

# Log levels
label_values(level)

# Process names
label_values(process_name)

# Source contexts
label_values(source_context)

# Containers
label_values(container)
```

## Alerting Query Examples

### High Error Rate Alert

```logql
sum(rate({level="ERROR"} [5m])) > 10
```

### Process Failure Alert

```logql
sum(rate({service="honua-cli-ai"} | json | message =~ "process.*failed" [5m])) > 0
```

### No Logs Received Alert

```logql
absent_over_time({service="honua-cli-ai"} [5m])
```

### Deployment Failure Alert

```logql
sum(rate(
  {service="honua-cli-ai", process_name="DeploymentProcess", level="ERROR"}
  [5m]
)) > 0
```

## Testing Queries

### Test in CLI

```bash
# Basic query
curl -G -s "http://localhost:3100/loki/api/v1/query" \
  --data-urlencode 'query={service="honua-cli-ai"}' \
  --data-urlencode 'limit=10' | jq

# Query range
curl -G -s "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query=rate({service="honua-cli-ai"}[5m])' \
  --data-urlencode 'start=2024-01-01T00:00:00Z' \
  --data-urlencode 'end=2024-01-01T01:00:00Z' \
  --data-urlencode 'step=60s' | jq

# Get labels
curl -s "http://localhost:3100/loki/api/v1/label/service/values" | jq
```

## Further Reading

- [LogQL Documentation](https://grafana.com/docs/loki/latest/logql/)
- [Query Best Practices](https://grafana.com/docs/loki/latest/logql/query_examples/)
- [Label Best Practices](https://grafana.com/docs/loki/latest/best-practices/)
