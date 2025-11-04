# Distributed Tracing Deployment Guide

This guide covers deploying and configuring Grafana Tempo for distributed tracing in both testing and production environments for the Honua Process Framework.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Local Development Setup](#local-development-setup)
- [Production Deployment](#production-deployment)
- [Configuration](#configuration)
- [Monitoring and Operations](#monitoring-and-operations)
- [Troubleshooting](#troubleshooting)

## Overview

The Honua Process Framework uses **Grafana Tempo** as the distributed tracing backend. Tempo is designed to be:

- **Cost-effective**: Uses object storage (S3, GCS, Azure Blob) instead of expensive databases
- **Scalable**: Horizontally scalable architecture
- **Simple**: No dependencies on databases or caches
- **Integrated**: Works seamlessly with Grafana, Prometheus, and Loki

### Why Tempo over Jaeger?

- Lower operational overhead (no database required)
- Better integration with existing Grafana stack
- Native support for object storage backends
- Lower costs at scale
- Built-in metrics generation from traces

## Architecture

```
┌──────────────────┐
│  Honua.Cli.AI    │
│  Application     │
└────────┬─────────┘
         │ OTLP (gRPC/HTTP)
         ▼
┌──────────────────┐
│   OpenTelemetry  │
│    Collector     │◄──── 10% Head-based Sampling
└────────┬─────────┘
         │
         ├─────────────────┐
         │                 │
         ▼                 ▼
┌──────────────┐   ┌──────────────┐
│    Tempo     │   │  Prometheus  │
│  (Traces)    │   │  (Metrics)   │
└──────┬───────┘   └──────────────┘
       │
       ▼
┌──────────────┐
│   Grafana    │
│  (Viz/Query) │
└──────────────┘
```

### Data Flow

1. **Application** emits traces via OpenTelemetry SDK
2. **OTel Collector** receives traces, applies 10% probabilistic sampling
3. **Tempo** ingests and stores traces in object storage
4. **Tempo Metrics Generator** creates metrics from traces and sends to Prometheus
5. **Grafana** queries Tempo for traces and correlates with metrics/logs

## Local Development Setup

### Quick Start

```bash
# Navigate to process testing directory
cd /home/mike/projects/HonuaIO/docker/process-testing

# Start the full observability stack
./scripts/start-testing-stack.sh

# Verify all services are healthy
./scripts/verify-health.sh
```

### Access Points

- **Grafana**: http://localhost:3000 (admin/admin)
- **Tempo UI**: http://localhost:3200
- **OTel Collector**: http://localhost:4317 (gRPC), http://localhost:4318 (HTTP)
- **Prometheus**: http://localhost:9090
- **Loki**: http://localhost:3100

### Configuration Files

- **Docker Compose**: `docker-compose.yml`
- **Tempo Config**: `tempo/tempo-config.yaml`
- **OTel Collector**: `otel-collector/otel-collector-config.yml`
- **Grafana Datasources**: `grafana/provisioning/datasources/datasources.yml`
- **Environment**: `.env`

## Production Deployment

### Prerequisites

- Object storage (AWS S3, GCS, or Azure Blob Storage)
- Kubernetes cluster or Docker Swarm (recommended)
- LoadBalancer for ingress
- DNS configured for endpoints

### Production Tempo Configuration

Replace local storage with object storage in `tempo-config.yaml`:

#### AWS S3 Backend

```yaml
storage:
  trace:
    backend: s3
    s3:
      bucket: honua-tempo-traces
      endpoint: s3.us-east-1.amazonaws.com
      region: us-east-1
      access_key: ${AWS_ACCESS_KEY_ID}
      secret_key: ${AWS_SECRET_ACCESS_KEY}
      insecure: false
      # Optional: Use IAM roles instead of access keys
      # iam_role: arn:aws:iam::123456789012:role/tempo-s3-role
    wal:
      path: /var/tempo/wal
      encoding: snappy
    pool:
      max_workers: 100
      queue_depth: 10000

compactor:
  compaction:
    block_retention: 168h  # 7 days (production)
    compacted_block_retention: 24h
    compaction_window: 1h
```

#### Google Cloud Storage Backend

```yaml
storage:
  trace:
    backend: gcs
    gcs:
      bucket_name: honua-tempo-traces
      # Use workload identity or service account
      # chunk_buffer_size: 10485760
      # object_prefix: tempo-traces/
    wal:
      path: /var/tempo/wal
      encoding: snappy
```

#### Azure Blob Storage Backend

```yaml
storage:
  trace:
    backend: azure
    azure:
      container_name: honua-tempo-traces
      storage_account_name: ${AZURE_STORAGE_ACCOUNT}
      storage_account_key: ${AZURE_STORAGE_KEY}
      # Or use managed identity:
      # use_managed_identity: true
    wal:
      path: /var/tempo/wal
      encoding: snappy
```

### Kubernetes Deployment

#### Helm Installation (Recommended)

```bash
# Add Grafana Helm repo
helm repo add grafana https://grafana.github.io/helm-charts
helm repo update

# Create namespace
kubectl create namespace observability

# Create values file
cat > tempo-values.yaml <<EOF
tempo:
  resources:
    requests:
      cpu: 500m
      memory: 1Gi
    limits:
      cpu: 2000m
      memory: 4Gi

  storage:
    trace:
      backend: s3
      s3:
        bucket: honua-tempo-traces
        region: us-east-1
        insecure: false

  retention: 168h  # 7 days

  metrics_generator:
    enabled: true
    remote_write:
      - url: http://prometheus:9090/api/v1/write

ingester:
  max_block_duration: 5m

compactor:
  compaction:
    block_retention: 168h

serviceAccount:
  create: true
  annotations:
    eks.amazonaws.com/role-arn: arn:aws:iam::ACCOUNT:role/tempo-s3-role
EOF

# Install Tempo
helm install tempo grafana/tempo-distributed \
  -n observability \
  -f tempo-values.yaml
```

#### Manual Kubernetes Deployment

See the example manifests in `kubernetes/tempo/` directory.

### Docker Swarm Deployment

```yaml
# docker-compose.production.yml
version: "3.9"

services:
  tempo:
    image: grafana/tempo:latest
    command: ["-config.file=/etc/tempo.yaml"]
    configs:
      - source: tempo_config
        target: /etc/tempo.yaml
    environment:
      - AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
      - AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
    deploy:
      replicas: 3
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '0.5'
          memory: 1G
    volumes:
      - tempo-wal:/var/tempo/wal
    networks:
      - observability

configs:
  tempo_config:
    file: ./tempo/tempo-production.yaml

volumes:
  tempo-wal:
    driver: local

networks:
  observability:
    driver: overlay
```

### Scaling Considerations

#### Horizontal Scaling

Tempo components can be scaled independently:

- **Distributor**: Scales with write throughput (add replicas)
- **Ingester**: Scales with trace ingestion rate (add replicas)
- **Querier**: Scales with read/query load (add replicas)
- **Compactor**: Usually 1 replica is sufficient

#### Recommended Production Setup

| Component   | Replicas | CPU   | Memory | Storage      |
|-------------|----------|-------|--------|--------------|
| Distributor | 3-5      | 500m  | 1Gi    | -            |
| Ingester    | 3-6      | 1000m | 2Gi    | WAL (local)  |
| Querier     | 2-4      | 500m  | 1Gi    | -            |
| Compactor   | 1-2      | 1000m | 2Gi    | -            |

### Sampling Configuration

#### Head-based Sampling (Current: 10%)

Configured in OpenTelemetry Collector:

```yaml
processors:
  probabilistic_sampler:
    sampling_percentage: 10.0  # Adjust based on traffic volume
```

**Recommendations by Traffic:**
- Low traffic (<100 req/s): 50-100%
- Medium traffic (100-1000 req/s): 10-30%
- High traffic (>1000 req/s): 1-10%

#### Tail-based Sampling (Advanced)

For more intelligent sampling, enable tail sampling:

```yaml
processors:
  tail_sampling:
    decision_wait: 10s
    num_traces: 100
    expected_new_traces_per_sec: 10
    policies:
      # Always sample errors
      - name: errors-policy
        type: status_code
        status_code:
          status_codes: [ERROR]

      # Sample slow traces
      - name: slow-traces-policy
        type: latency
        latency:
          threshold_ms: 1000

      # Sample 5% of successful traces
      - name: random-ok-policy
        type: probabilistic
        probabilistic:
          sampling_percentage: 5
```

## Configuration

### OpenTelemetry SDK Configuration

Configure your application to send traces to the OTel Collector:

```csharp
// In Honua.Cli.AI Program.cs or Startup
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("Honua.*")
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault()
                    .AddService("honua-cli-ai")
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["deployment.environment"] = "production",
                        ["service.version"] = Assembly.GetExecutingAssembly()
                            .GetName().Version?.ToString() ?? "unknown"
                    }))
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddHttpClientInstrumentation(options =>
            {
                options.RecordException = true;
            })
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://otel-collector:4317");
                options.Protocol = OtlpExportProtocol.Grpc;
            });
    });
```

### Custom Span Attributes

Add process-specific attributes to traces:

```csharp
using var activity = ActivitySource.StartActivity("ProcessStep");
activity?.SetTag("process.name", processName);
activity?.SetTag("step.name", stepName);
activity?.SetTag("process.instance_id", instanceId);
activity?.SetTag("step.index", stepIndex);

// Execution
await ExecuteStepAsync();

activity?.SetStatus(ActivityStatusCode.Ok);
```

### Retention Policies

Configure trace retention based on your compliance/debugging needs:

- **Testing**: 24 hours (configured)
- **Development**: 7 days
- **Production**: 30-90 days (depending on compliance)
- **Compliance/Audit**: 1 year+

Update in `tempo-config.yaml`:

```yaml
compactor:
  compaction:
    block_retention: 720h  # 30 days
```

### Resource Limits

Configure Tempo resource limits:

```yaml
overrides:
  defaults:
    max_traces_per_user: 100000
    max_bytes_per_trace: 10000000  # 10MB
    ingestion_rate_limit_bytes: 50000000  # 50MB/s
    ingestion_burst_size_bytes: 100000000  # 100MB
```

## Monitoring and Operations

### Health Checks

Tempo exposes health endpoints:

```bash
# Readiness check
curl http://tempo:3200/ready

# Liveness check
curl http://tempo:3200/status

# Metrics
curl http://tempo:3200/metrics
```

### Key Metrics to Monitor

Monitor these Tempo metrics in Prometheus:

```promql
# Ingestion rate
rate(tempo_distributor_spans_received_total[5m])

# Query latency
histogram_quantile(0.95, rate(tempo_query_frontend_result_metrics_latency_bucket[5m]))

# Storage usage
tempo_ingester_blocks_total

# Error rate
rate(tempo_distributor_ingester_append_failures_total[5m])

# Compaction lag
tempo_compactor_blocks_compacted_total
```

### Alerts

Recommended Prometheus alerts:

```yaml
groups:
  - name: tempo
    rules:
      - alert: TempoHighIngestionErrors
        expr: rate(tempo_distributor_ingester_append_failures_total[5m]) > 1
        for: 5m
        annotations:
          summary: "High trace ingestion error rate"

      - alert: TempoHighQueryLatency
        expr: histogram_quantile(0.95, rate(tempo_query_frontend_result_metrics_latency_bucket[5m])) > 5000
        for: 10m
        annotations:
          summary: "High query latency (p95 > 5s)"

      - alert: TempoCompactorDown
        expr: up{job="tempo-compactor"} == 0
        for: 5m
        annotations:
          summary: "Tempo compactor is down"
```

### Dashboard Usage

The included Grafana dashboard (`distributed-tracing-dashboard.json`) provides:

1. **Overview Metrics**
   - Traces per second
   - P95 latency
   - Error rate
   - Active services

2. **Trace Explorer**
   - Recent traces viewer
   - TraceQL query interface

3. **Operation Analysis**
   - Latency percentiles by operation
   - Request rate by operation
   - Success vs error breakdown

4. **Service Dependencies**
   - Service graph visualization
   - Request flow between services

5. **Operations Table**
   - Sortable table with key metrics per operation

### Querying Traces

#### Via Grafana UI

1. Navigate to Explore → Select Tempo datasource
2. Use TraceQL query language:

```traceql
# Find all traces for a specific service
{ service.name="honua-cli-ai" }

# Find slow traces
{ service.name="honua-cli-ai" && duration > 1s }

# Find error traces
{ service.name="honua-cli-ai" && status=error }

# Find traces with specific attributes
{ service.name="honua-cli-ai" && span.process.name="DeploymentProcess" }

# Complex queries
{
  service.name="honua-cli-ai" &&
  (duration > 500ms || status=error) &&
  span.step.name="ValidateDeployment"
}
```

#### Via API

```bash
# Search traces by TraceQL
curl -G http://tempo:3200/api/search \
  --data-urlencode 'q={ service.name="honua-cli-ai" }' \
  --data-urlencode 'start=1704067200' \
  --data-urlencode 'end=1704153600'

# Get specific trace by ID
curl http://tempo:3200/api/traces/<trace-id>
```

### Log Correlation

Traces are automatically correlated with logs via trace IDs. In your application:

```csharp
using var activity = Activity.Current;
logger.LogInformation(
    "Processing step {StepName} for process {ProcessName}. TraceId: {TraceId}",
    stepName,
    processName,
    activity?.TraceId.ToString()
);
```

Loki will extract trace IDs and provide links to traces in Grafana.

## Troubleshooting

### No Traces Appearing

1. **Check OTel Collector logs:**
   ```bash
   docker logs honua-process-otel
   ```

2. **Verify application is sending traces:**
   ```bash
   # Check OTel collector metrics
   curl http://localhost:8888/metrics | grep receiver_accepted_spans
   ```

3. **Check Tempo ingestion:**
   ```bash
   curl http://localhost:3200/metrics | grep tempo_distributor_spans_received
   ```

### High Memory Usage

1. **Reduce ingestion rate:**
   - Increase sampling percentage (reduce from 10%)
   - Add rate limiting in OTel collector

2. **Tune Tempo limits:**
   ```yaml
   ingester:
     max_block_bytes: 5242880  # Reduce to 5MB
     max_block_duration: 3m    # Reduce block duration
   ```

3. **Scale ingesters horizontally**

### Query Performance Issues

1. **Enable caching:**
   ```yaml
   query_frontend:
     search:
       cache:
         enabled: true
   ```

2. **Reduce search time range**
3. **Add more querier replicas**
4. **Optimize TraceQL queries** (use specific service names and time ranges)

### Storage Issues

1. **Monitor object storage costs:**
   - Review compaction settings
   - Adjust retention period
   - Enable compression

2. **WAL disk full:**
   ```bash
   # Increase WAL volume size or
   # Reduce max_block_duration to flush more frequently
   ```

### Connection Issues

1. **OTel Collector can't reach Tempo:**
   ```bash
   # Test connectivity
   docker exec honua-process-otel nc -zv tempo 4317
   ```

2. **Application can't reach OTel Collector:**
   ```bash
   # Verify collector is listening
   netstat -tlnp | grep 4317
   ```

### Debugging Tips

1. **Enable debug logging in Tempo:**
   ```yaml
   server:
     log_level: debug
   ```

2. **Use OTel collector debug exporter:**
   ```yaml
   exporters:
     logging:
       verbosity: detailed
   ```

3. **Check Tempo status page:**
   ```bash
   curl http://localhost:3200/status/services
   ```

## Cost Optimization

### Storage Costs

1. **Optimize retention:**
   - Keep only necessary data (7-30 days typical)
   - Use tiered storage (hot/warm/cold)

2. **Sampling strategy:**
   - Start with 10% sampling
   - Use tail-sampling to keep important traces
   - Reduce to 1-5% for very high traffic

3. **Compression:**
   - Tempo uses Snappy by default
   - Consider block-level compression in object storage

### Compute Costs

1. **Right-size replicas:**
   - Start small and scale based on metrics
   - Use HPA (Horizontal Pod Autoscaling) in Kubernetes

2. **Optimize queries:**
   - Use specific time ranges
   - Filter by service name
   - Avoid wildcard searches

## Security

### Network Security

1. **Enable TLS for OTel Collector:**
   ```yaml
   receivers:
     otlp:
       protocols:
         grpc:
           tls:
             cert_file: /certs/server.crt
             key_file: /certs/server.key
   ```

2. **Use private endpoints for object storage**
3. **Implement network policies in Kubernetes**

### Authentication

1. **Tempo supports basic auth:**
   ```yaml
   server:
     http_listen_address: 0.0.0.0:3200
     http_tls_config:
       cert_file: /certs/server.crt
       key_file: /certs/server.key
   ```

2. **Use OAuth2 proxy for Grafana access**

### Data Privacy

1. **Scrub sensitive data:**
   ```yaml
   # In OTel Collector
   processors:
     attributes:
       actions:
         - key: http.request.header.authorization
           action: delete
   ```

2. **Enable encryption at rest** in object storage

## Migration Guide

### From Jaeger

1. **Update application instrumentation:**
   - Change from Jaeger exporter to OTLP exporter
   - Update endpoint configuration

2. **Migrate historical data** (optional):
   - Export Jaeger traces via Jaeger API
   - Import into Tempo using tempo-cli

3. **Update dashboards:**
   - Recreate Jaeger dashboards for Tempo
   - Update data source references

### From Zipkin

1. **Tempo supports Zipkin protocol:**
   ```yaml
   # No application changes needed
   # Point Zipkin reporter to tempo:9411
   ```

2. **Or migrate to OTLP for better features**

## Additional Resources

- [Grafana Tempo Documentation](https://grafana.com/docs/tempo/latest/)
- [OpenTelemetry Documentation](https://opentelemetry.io/docs/)
- [TraceQL Guide](https://grafana.com/docs/tempo/latest/traceql/)
- [Tempo GitHub Repository](https://github.com/grafana/tempo)

## Support

For issues specific to Honua Process Framework tracing:

1. Check the troubleshooting section above
2. Review OTel Collector and Tempo logs
3. Consult the main Honua documentation
4. Open an issue in the Honua repository

For Tempo-specific issues:
- [Tempo Community Slack](https://slack.grafana.com/)
- [Tempo GitHub Issues](https://github.com/grafana/tempo/issues)
