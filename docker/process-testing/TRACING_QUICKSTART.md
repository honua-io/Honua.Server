# Distributed Tracing Quick Start Guide

A quick reference for using distributed tracing with Grafana Tempo in the Honua Process Framework.

## Start the Stack

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/start-testing-stack.sh
```

## Access Points

| Service | URL | Purpose |
|---------|-----|---------|
| Grafana | http://localhost:3000 | Visualization & dashboards |
| Tempo | http://localhost:3200 | Trace queries & API |
| OTel Collector | http://localhost:4317 (gRPC)<br>http://localhost:4318 (HTTP) | Trace ingestion |
| Prometheus | http://localhost:9090 | Metrics & trace-derived metrics |

**Grafana Credentials**: `admin` / `admin` (change after first login)

## Viewing Traces

### Method 1: Tracing Dashboard

1. Open Grafana: http://localhost:3000
2. Navigate to **Dashboards** → **Honua Distributed Tracing - Process Framework**
3. View:
   - Recent traces
   - Latency percentiles
   - Error rates
   - Service dependencies

### Method 2: Explore Interface

1. In Grafana, click **Explore** (compass icon)
2. Select **Tempo** datasource
3. Use **TraceQL** queries:

```traceql
# All traces for honua-cli-ai
{ service.name="honua-cli-ai" }

# Slow traces (>1 second)
{ service.name="honua-cli-ai" && duration > 1s }

# Error traces
{ service.name="honua-cli-ai" && status=error }

# Specific process
{ service.name="honua-cli-ai" && span.process.name="DeploymentProcess" }

# Complex query
{
  service.name="honua-cli-ai" &&
  (duration > 500ms || status=error) &&
  span.step.name="ValidateDeployment"
}
```

### Method 3: From Logs (Correlation)

1. In Grafana Explore, select **Loki** datasource
2. Query logs:
   ```logql
   {container_name="honua-cli-ai"} |= "trace_id"
   ```
3. Click on **Tempo** button next to log entries to view the associated trace

### Method 4: From Metrics (Exemplars)

1. In Grafana, view any Process Framework metric
2. Look for small dots on the graph (exemplars)
3. Click a dot to view the trace for that specific request

## Instrumenting Your Code

### Basic Span

```csharp
using var activity = ActivitySource.StartActivity("MyOperation");

try
{
    // Your code here
    activity?.SetTag("custom.attribute", "value");
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

### Process Framework Integration

```csharp
// In your process step
using var activity = ActivitySource.StartActivity($"ProcessStep:{stepName}");
activity?.SetTag("process.name", processName);
activity?.SetTag("step.name", stepName);
activity?.SetTag("process.instance_id", instanceId);

var result = await ExecuteStepAsync(context, cancellationToken);

if (result.IsSuccess)
{
    activity?.SetStatus(ActivityStatusCode.Ok);
}
else
{
    activity?.SetStatus(ActivityStatusCode.Error, result.ErrorMessage);
}
```

### Adding Events

```csharp
activity?.AddEvent(new ActivityEvent(
    "ValidationStarted",
    DateTimeOffset.UtcNow,
    new ActivityTagsCollection
    {
        { "validator.type", "ConfigValidator" },
        { "config.count", configCount }
    }
));
```

## Common Queries

### Find Recent Errors

```traceql
{ service.name="honua-cli-ai" && status=error }
```

### Find Slow Process Executions

```traceql
{
  service.name="honua-cli-ai" &&
  span.name =~ "Process.*" &&
  duration > 2s
}
```

### Find Specific Process Instance

```traceql
{
  service.name="honua-cli-ai" &&
  span.process.instance_id="<instance-id>"
}
```

### Find All Deployment Operations

```traceql
{
  service.name="honua-cli-ai" &&
  span.process.name="DeploymentProcess"
}
```

## Monitoring Tracing Health

### Check Ingestion Rate

In Prometheus: http://localhost:9090

```promql
# Traces received per second
rate(tempo_distributor_spans_received_total[5m])

# Trace ingestion errors
rate(tempo_distributor_ingester_append_failures_total[5m])
```

### Check Trace Coverage

```promql
# What percentage of requests are traced?
sum(rate(traces_spanmetrics_calls_total[5m])) / sum(rate(http_requests_total[5m]))
```

Note: With 10% sampling, this should be approximately 0.10 (10%)

## Troubleshooting

### No Traces Appearing

1. **Check if application is sending traces:**
   ```bash
   docker logs honua-process-otel | grep "Span"
   ```

2. **Check OTel Collector health:**
   ```bash
   curl http://localhost:13133
   curl http://localhost:8888/metrics | grep receiver_accepted_spans
   ```

3. **Check Tempo health:**
   ```bash
   curl http://localhost:3200/ready
   curl http://localhost:3200/metrics | grep tempo_distributor_spans_received
   ```

### Traces Not Appearing in Grafana

1. **Verify Tempo datasource:**
   - Go to Configuration → Data Sources → Tempo
   - Click "Save & Test"
   - Should show "Data source is working"

2. **Check time range:**
   - Traces may be outside your selected time range
   - Try "Last 1 hour" or wider range

3. **Verify sampling:**
   - With 10% sampling, not all requests will have traces
   - Increase sampling in `otel-collector-config.yml` if needed

### High Memory Usage

1. **Reduce sampling percentage:**
   - Edit `otel-collector/otel-collector-config.yml`
   - Change `sampling_percentage: 10.0` to `5.0` or `1.0`
   - Restart: `docker compose restart otel-collector`

2. **Reduce retention:**
   - Edit `tempo/tempo-config.yaml`
   - Change `block_retention: 24h` to `12h` or `6h`
   - Restart: `docker compose restart tempo`

## Configuration Files Reference

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Service definitions |
| `tempo/tempo-config.yaml` | Tempo backend configuration |
| `otel-collector/otel-collector-config.yml` | Trace collection & sampling |
| `grafana/provisioning/datasources/datasources.yml` | Grafana datasource config |
| `grafana/dashboards/distributed-tracing-dashboard.json` | Tracing dashboard |
| `.env` | Port configuration |

## Adjusting Sampling Rate

Edit `/home/mike/projects/HonuaIO/docker/process-testing/otel-collector/otel-collector-config.yml`:

```yaml
processors:
  probabilistic_sampler:
    sampling_percentage: 10.0  # Change this value (0-100)
```

Then restart the OTel Collector:

```bash
docker compose restart otel-collector
```

**Recommendations:**
- **Low traffic** (<100 req/s): 50-100%
- **Medium traffic** (100-1000 req/s): 10-30%
- **High traffic** (>1000 req/s): 1-10%

## Advanced: Tail-Based Sampling

For more intelligent sampling (sample all errors, slow requests, and a percentage of normal requests):

1. Edit `otel-collector/otel-collector-config.yml`
2. Uncomment the `tail_sampling` section
3. Update the traces pipeline to use `tail_sampling` instead of `probabilistic_sampler`
4. Restart OTel Collector

## Metrics Generated from Traces

Tempo automatically generates these metrics:

```promql
# Request rate
traces_spanmetrics_calls_total

# Latency histogram
traces_spanmetrics_latency_bucket

# Service graph (dependencies)
traces_service_graph_request_total
```

These metrics are available in Prometheus and used in the tracing dashboard.

## Best Practices

1. **Use meaningful span names:**
   - Good: `ProcessStep:ValidateDeployment`
   - Bad: `Step1`

2. **Add relevant attributes:**
   ```csharp
   activity?.SetTag("process.name", processName);
   activity?.SetTag("step.index", stepIndex);
   activity?.SetTag("resource.id", resourceId);
   ```

3. **Record exceptions:**
   ```csharp
   catch (Exception ex)
   {
       activity?.RecordException(ex);
       activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
   }
   ```

4. **Use events for important milestones:**
   ```csharp
   activity?.AddEvent(new ActivityEvent("ValidationCompleted"));
   ```

5. **Don't over-instrument:**
   - Focus on business logic and external calls
   - Avoid tracing every internal method

## Further Reading

- [Full Deployment Guide](DISTRIBUTED_TRACING_DEPLOYMENT.md)
- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
- [TraceQL Reference](https://grafana.com/docs/tempo/latest/traceql/)
- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/instrumentation/net/)

## Support

For issues or questions:
1. Check the [troubleshooting section](#troubleshooting) above
2. Review logs: `docker compose logs tempo otel-collector`
3. See full deployment guide: `DISTRIBUTED_TRACING_DEPLOYMENT.md`
