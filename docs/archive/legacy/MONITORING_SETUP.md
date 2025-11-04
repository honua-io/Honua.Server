# Honua Monitoring Setup Guide

**Last Updated**: 2025-10-17
**Version**: 2.0.0
**Status**: Production Ready

## Table of Contents

1. [Overview](#overview)
2. [Observability Stack Components](#observability-stack-components)
3. [Installation & Configuration](#installation--configuration)
4. [Grafana Dashboard Setup](#grafana-dashboard-setup)
5. [Alert Configuration](#alert-configuration)
6. [Log Aggregation](#log-aggregation)
7. [Tracing Setup](#tracing-setup)
8. [Metric Collection Verification](#metric-collection-verification)
9. [Troubleshooting](#troubleshooting)

---

## Overview

Honua provides comprehensive observability through OpenTelemetry integration, supporting metrics, distributed tracing, and structured logging. This guide covers setting up the complete observability stack for development, staging, and production environments.

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Honua Applications                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  Server.Host │  │   Cli.AI     │  │   Cli        │          │
│  │  (OTLP)      │  │   (OTLP)     │  │              │          │
│  └──────┬───────┘  └──────┬───────┘  └──────────────┘          │
│         │                 │                                      │
│         └─────────┬───────┘                                      │
│                   │ OTLP (gRPC/HTTP)                             │
│                   │ Metrics, Traces, Logs                        │
└───────────────────┼──────────────────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────────────────┐
│           OpenTelemetry Collector (Optional)                     │
│  - Protocol translation (OTLP → Prometheus, Jaeger, Loki)       │
│  - Batching and buffering                                        │
│  - Attribute processing and filtering                            │
│  - Multi-backend export                                          │
└─────────────┬───────────────┬───────────────┬────────────────────┘
              │               │               │
     ┌────────▼─────┐  ┌──────▼──────┐  ┌────▼────────┐
     │  Prometheus  │  │   Jaeger    │  │    Loki     │
     │  (Metrics)   │  │  (Traces)   │  │    (Logs)   │
     └────────┬─────┘  └──────┬──────┘  └────┬────────┘
              │               │               │
              └───────────────┼───────────────┘
                              │
                      ┌───────▼───────┐
                      │    Grafana    │
                      │  (Dashboards) │
                      └───────────────┘
```

---

## Observability Stack Components

### 1. OpenTelemetry Exporter (Built-in)

**Purpose**: Export metrics, traces, and logs from Honua applications

**Protocols**:
- OTLP/gRPC (recommended for production)
- OTLP/HTTP (firewall-friendly)
- Prometheus (pull-based metrics)
- Console (development debugging)

**Configuration** (in `appsettings.json`):
```json
{
  "OpenTelemetry": {
    "ServiceName": "honua-server",
    "Metrics": {
      "Enabled": true,
      "Exporters": ["otlp", "prometheus"]
    },
    "Tracing": {
      "Enabled": true,
      "Exporters": ["otlp"],
      "SamplingRatio": 0.1
    },
    "Logging": {
      "Enabled": true,
      "Exporters": ["otlp"]
    },
    "Otlp": {
      "Endpoint": "http://otel-collector:4317",
      "Protocol": "grpc"
    }
  }
}
```

### 2. OpenTelemetry Collector (Optional but Recommended)

**Purpose**: Centralized telemetry aggregation and routing

**Benefits**:
- Decouples application from backend changes
- Batching and buffering for performance
- Protocol translation
- Sampling and filtering

**When to Use**:
- Production deployments
- Multiple backends (Prometheus, Jaeger, Datadog, New Relic)
- Large-scale deployments (100+ instances)

**When to Skip**:
- Simple development setup
- Direct Prometheus scraping is sufficient
- Single backend

### 3. Prometheus (Metrics)

**Purpose**: Time-series metric storage and querying

**Metrics Collected**:
- Request rates and latencies (P50, P95, P99)
- Database connection pool stats
- Raster tile cache hits/misses
- Process framework step execution
- .NET runtime metrics (GC, threads, memory)

### 4. Grafana (Visualization)

**Purpose**: Dashboard and alerting platform

**Pre-built Dashboards**:
- Honua Server Overview
- Process Framework Execution
- Raster Processing Performance
- Database Health
- Redis Metrics

### 5. Loki (Logs)

**Purpose**: Log aggregation and querying

**Log Sources**:
- Application logs (structured JSON)
- Kubernetes pod logs
- Process framework execution logs
- Security audit logs

### 6. Jaeger (Distributed Tracing)

**Purpose**: Trace request flows across services

**Use Cases**:
- Debug slow requests
- Visualize multi-step workflows
- Identify bottlenecks in Process Framework

---

## Installation & Configuration

### Option 1: Local Development Stack

**Prerequisites**:
- Docker 20.10+
- docker-compose 1.29+
- 2GB+ available RAM

**1. Start Observability Stack**:
```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/start-testing-stack.sh
```

**2. Verify Services**:
```bash
./scripts/verify-health.sh
```

**Expected Output**:
```
✓ Redis is healthy (localhost:6379)
✓ Prometheus is healthy (http://localhost:9090)
✓ Grafana is healthy (http://localhost:3000)
✓ Loki is healthy (http://localhost:3100)
✓ OTLP Collector is healthy (localhost:4317)
```

**3. Access Dashboards**:
- **Grafana**: http://localhost:3000 (admin/admin)
- **Prometheus**: http://localhost:9090
- **OTLP Collector**: http://localhost:55679/debug/tracez

**4. Configure Honua to Use Stack**:
```bash
export ASPNETCORE_ENVIRONMENT=Testing
cd /home/mike/projects/HonuaIO/src/Honua.Server.Host
dotnet run --urls http://localhost:5000
```

The `appsettings.Testing.json` already configures OTLP endpoint as `http://localhost:4317`.

---

### Option 2: Kubernetes Production Stack

**1. Install Prometheus Operator**:
```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install kube-prometheus-stack prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace \
  --set prometheus.prometheusSpec.serviceMonitorSelectorNilUsesHelmValues=false \
  --set grafana.adminPassword=${GRAFANA_PASSWORD}
```

**2. Install Loki**:
```bash
helm repo add grafana https://grafana.github.io/helm-charts

helm install loki grafana/loki-stack \
  --namespace monitoring \
  --set promtail.enabled=true \
  --set loki.persistence.enabled=true \
  --set loki.persistence.size=100Gi
```

**3. Install Jaeger**:
```bash
helm repo add jaegertracing https://jaegertracing.github.io/helm-charts

helm install jaeger jaegertracing/jaeger \
  --namespace monitoring \
  --set collector.service.otlp.grpc.enabled=true \
  --set collector.service.otlp.http.enabled=true
```

**4. Install OpenTelemetry Collector**:
```bash
helm repo add open-telemetry https://open-telemetry.github.io/opentelemetry-helm-charts

helm install otel-collector open-telemetry/opentelemetry-collector \
  --namespace monitoring \
  --values otel-collector-values.yaml
```

**otel-collector-values.yaml**:
```yaml
mode: deployment
image:
  repository: otel/opentelemetry-collector-k8s
config:
  receivers:
    otlp:
      protocols:
        grpc:
          endpoint: 0.0.0.0:4317
        http:
          endpoint: 0.0.0.0:4318
  processors:
    batch:
      timeout: 10s
      send_batch_size: 1024
    memory_limiter:
      check_interval: 5s
      limit_mib: 512
  exporters:
    prometheus:
      endpoint: 0.0.0.0:8889
    jaeger:
      endpoint: jaeger-collector:14250
      tls:
        insecure: true
    loki:
      endpoint: http://loki:3100/loki/api/v1/push
  service:
    pipelines:
      metrics:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [prometheus]
      traces:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [jaeger]
      logs:
        receivers: [otlp]
        processors: [memory_limiter, batch]
        exporters: [loki]
```

**5. Create ServiceMonitor for Honua**:
```yaml
# honua-servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: honua-server
  namespace: honua-production
  labels:
    app: honua-server
spec:
  selector:
    matchLabels:
      app: honua-server
  endpoints:
  - port: metrics
    interval: 30s
    path: /metrics
```

```bash
kubectl apply -f honua-servicemonitor.yaml
```

**6. Verify Prometheus Targets**:
```bash
# Port-forward Prometheus
kubectl port-forward -n monitoring svc/kube-prometheus-stack-prometheus 9090:9090

# Open http://localhost:9090/targets
# Verify honua-server target is UP
```

---

### Option 3: Managed Services (Cloud Providers)

#### AWS (CloudWatch + X-Ray)

**1. Install AWS OTLP Exporter**:
```bash
dotnet add package AWS.Distro.OpenTelemetry --version 1.4.0
```

**2. Configure in `appsettings.Production.json`**:
```json
{
  "OpenTelemetry": {
    "Exporters": ["aws"],
    "AWS": {
      "Region": "us-west-2",
      "Service": "honua-server"
    }
  }
}
```

**3. Grant IAM Permissions**:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "xray:PutTraceSegments",
        "xray:PutTelemetryRecords",
        "cloudwatch:PutMetricData",
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "*"
    }
  ]
}
```

#### Azure (Application Insights)

**1. Configure in `appsettings.Production.json`**:
```json
{
  "OpenTelemetry": {
    "Exporters": ["azureai"],
    "AzureAI": {
      "ConnectionString": "${APPLICATIONINSIGHTS_CONNECTION_STRING}"
    }
  }
}
```

**2. Set Environment Variable**:
```bash
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=xxx;IngestionEndpoint=https://westus2-3.in.applicationinsights.azure.com/"
```

#### GCP (Cloud Monitoring + Cloud Trace)

**1. Install Google Cloud Exporter**:
```bash
dotnet add package Google.Cloud.Diagnostics.AspNetCore --version 5.1.0
```

**2. Configure in `appsettings.Production.json`**:
```json
{
  "OpenTelemetry": {
    "Exporters": ["gcp"],
    "GCP": {
      "ProjectId": "my-gcp-project"
    }
  }
}
```

---

## Grafana Dashboard Setup

### Install Pre-built Dashboards

**1. Access Grafana**:
```bash
# Local
open http://localhost:3000

# Kubernetes
kubectl port-forward -n monitoring svc/kube-prometheus-stack-grafana 3000:80
open http://localhost:3000
```

**2. Import Dashboards**:

**Method A: Automatic Provisioning (Kubernetes)**
```yaml
# grafana-dashboards-configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: grafana-dashboards
  namespace: monitoring
  labels:
    grafana_dashboard: "1"
data:
  honua-server.json: |
    {{ .Files.Get "dashboards/honua-server.json" | nindent 4 }}
  process-framework.json: |
    {{ .Files.Get "dashboards/process-framework.json" | nindent 4 }}
```

**Method B: Manual Import**
1. Open Grafana → Dashboards → Import
2. Upload dashboard JSON files from:
   - `/home/mike/projects/HonuaIO/docker/grafana/dashboards/honua-detailed.json`
   - `/home/mike/projects/HonuaIO/docker/process-testing/grafana/dashboards/process-framework-dashboard.json`

### Dashboard Descriptions

#### 1. Honua Server Overview

**File**: `docker/grafana/dashboards/honua-detailed.json`

**Panels**:
- **Request Rate**: Requests per second by endpoint
- **Response Latency**: P50, P95, P99 latencies
- **Error Rate**: 4xx and 5xx error percentages
- **Database Connections**: Active, idle, total connections
- **Tile Cache**: Hit rate, miss rate, evictions
- **CPU & Memory**: Resource utilization
- **GC Metrics**: Gen0, Gen1, Gen2 collection counts

**Key Queries**:
```promql
# Request rate
rate(http_server_requests_total[5m])

# P95 latency
histogram_quantile(0.95, rate(http_server_request_duration_bucket[5m]))

# Error rate
sum(rate(http_server_requests_total{status=~"5.."}[5m]))
/
sum(rate(http_server_requests_total[5m]))

# Cache hit rate
sum(rate(honua_tile_cache_hits_total[5m]))
/
sum(rate(honua_tile_cache_requests_total[5m]))
```

#### 2. Process Framework Dashboard

**File**: `docker/process-testing/grafana/dashboards/process-framework-dashboard.json`

**Panels**:
- **Active Processes**: Currently running process instances
- **Step Execution Rate**: Steps executed per second
- **Step Duration**: P50, P95, P99 step execution time
- **Process Success Rate**: Successful vs failed processes
- **Redis Metrics**: Memory usage, connected clients, commands/sec
- **Step Breakdown**: Execution time by step name

**Key Queries**:
```promql
# Active process instances
honua_process_active_instances

# Step execution rate
rate(honua_process_steps_total[5m])

# Step P95 duration
histogram_quantile(0.95, rate(honua_process_step_duration_bucket[5m]))

# Success rate
sum(rate(honua_process_steps_total{status="success"}[5m]))
/
sum(rate(honua_process_steps_total[5m]))
```

#### 3. Database Health Dashboard

**Panels**:
- **Connection Pool**: Active, idle, waiting connections
- **Query Performance**: Slow queries, query rate
- **Replication Lag**: (for read replicas)
- **Index Usage**: Index scans vs sequential scans
- **Transaction Rate**: Commits, rollbacks

**Key Queries**:
```promql
# Connection pool stats
honua_postgres_connection_pool_active_connections
honua_postgres_connection_pool_idle_connections

# Query rate
rate(honua_database_queries_total[5m])

# Slow queries (requires pg_stat_statements)
honua_database_slow_queries_total
```

---

## Alert Configuration

### Prometheus Alerting Rules

**1. Create AlertmanagerConfig** (Kubernetes):
```yaml
# alertmanager-config.yaml
apiVersion: v1
kind: Secret
metadata:
  name: alertmanager-kube-prometheus-stack-alertmanager
  namespace: monitoring
type: Opaque
stringData:
  alertmanager.yaml: |
    global:
      resolve_timeout: 5m
    route:
      group_by: ['alertname', 'cluster']
      group_wait: 10s
      group_interval: 10s
      repeat_interval: 12h
      receiver: 'slack-honua'
      routes:
      - match:
          severity: critical
        receiver: 'pagerduty-critical'
    receivers:
    - name: 'slack-honua'
      slack_configs:
      - api_url: '${SLACK_WEBHOOK_URL}'
        channel: '#honua-alerts'
        title: '{{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'
    - name: 'pagerduty-critical'
      pagerduty_configs:
      - service_key: '${PAGERDUTY_SERVICE_KEY}'
```

**2. Create PrometheusRule**:
```yaml
# honua-alerts.yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: honua-alerts
  namespace: honua-production
spec:
  groups:
  - name: honua.server.alerts
    interval: 30s
    rules:
    # High error rate
    - alert: HonuaHighErrorRate
      expr: |
        sum(rate(http_server_requests_total{status=~"5.."}[5m]))
        /
        sum(rate(http_server_requests_total[5m]))
        > 0.05
      for: 5m
      labels:
        severity: critical
      annotations:
        summary: "Honua server has high error rate"
        description: "Error rate is {{ $value | humanizePercentage }} for the last 5 minutes"

    # High latency
    - alert: HonuaHighLatency
      expr: |
        histogram_quantile(0.95,
          rate(http_server_request_duration_bucket[5m])
        ) > 2
      for: 10m
      labels:
        severity: warning
      annotations:
        summary: "Honua server has high latency"
        description: "P95 latency is {{ $value }}s for the last 10 minutes"

    # Database connection pool exhaustion
    - alert: HonuaDatabasePoolExhaustion
      expr: |
        honua_postgres_connection_pool_active_connections
        /
        honua_postgres_connection_pool_max_connections
        > 0.9
      for: 5m
      labels:
        severity: warning
      annotations:
        summary: "Honua database connection pool near exhaustion"
        description: "Connection pool is {{ $value | humanizePercentage }} full"

    # Tile cache low hit rate
    - alert: HonuaTileCacheLowHitRate
      expr: |
        sum(rate(honua_tile_cache_hits_total[5m]))
        /
        sum(rate(honua_tile_cache_requests_total[5m]))
        < 0.7
      for: 15m
      labels:
        severity: warning
      annotations:
        summary: "Honua tile cache has low hit rate"
        description: "Cache hit rate is {{ $value | humanizePercentage }}"

    # Process framework failures
    - alert: HonuaProcessFrameworkFailures
      expr: |
        sum(rate(honua_process_steps_total{status="error"}[5m]))
        /
        sum(rate(honua_process_steps_total[5m]))
        > 0.1
      for: 10m
      labels:
        severity: warning
      annotations:
        summary: "Honua process framework has high failure rate"
        description: "Process failure rate is {{ $value | humanizePercentage }}"

    # Pod not ready
    - alert: HonuaPodNotReady
      expr: |
        kube_pod_status_ready{namespace="honua-production", condition="false"} == 1
      for: 5m
      labels:
        severity: critical
      annotations:
        summary: "Honua pod is not ready"
        description: "Pod {{ $labels.pod }} has been not ready for 5 minutes"
```

```bash
kubectl apply -f honua-alerts.yaml
```

### Grafana Alerting (Alternative)

**1. Create Grafana Alert Rule**:

Navigate to: Grafana → Alerting → Alert Rules → New Alert Rule

**Example: High Error Rate Alert**
```yaml
Name: Honua High Error Rate
Folder: Honua Alerts
Evaluation Group: honua-server
Evaluation Interval: 1m

Query:
  A: sum(rate(http_server_requests_total{status=~"5.."}[5m])) / sum(rate(http_server_requests_total[5m]))

Condition: WHEN avg() OF A IS ABOVE 0.05

For: 5m

Annotations:
  Summary: Honua server has high error rate ({{ $value | humanizePercentage }})
  Description: Error rate has been above 5% for the last 5 minutes
  Runbook: https://docs.honua.io/runbooks/high-error-rate

Contact Point: slack-honua-alerts
```

---

## Log Aggregation

### Loki Configuration

**1. Configure Promtail (Log Shipper)** for Kubernetes:

Promtail is automatically deployed by `loki-stack` Helm chart.

**2. Query Logs in Grafana**:

Navigate to: Grafana → Explore → Select "Loki" datasource

**Example LogQL Queries**:
```logql
# All honua-server logs
{namespace="honua-production", app="honua-server"}

# Error logs only
{namespace="honua-production", app="honua-server"} |= "error" or "ERROR"

# Process framework logs
{namespace="honua-production", app="honua-server"} |= "ProcessStep"

# Slow queries
{namespace="honua-production", app="honua-server"}
| json
| duration > 1000ms

# Failed authentication attempts
{namespace="honua-production", app="honua-server"}
|= "authentication failed"
| json
| __error__=""
```

**3. Create Log-based Alerts**:

```yaml
# loki-alerts.yaml
apiVersion: monitoring.coreos.com/v1
kind: PrometheusRule
metadata:
  name: honua-log-alerts
  namespace: honua-production
spec:
  groups:
  - name: honua.logs.alerts
    rules:
    - alert: HonuaHighErrorLogRate
      expr: |
        sum(rate({namespace="honua-production", app="honua-server"} |= "ERROR" [5m])) > 10
      for: 5m
      labels:
        severity: warning
      annotations:
        summary: "High error log rate in Honua"
        description: "{{ $value }} errors/sec in logs"
```

### Structured Logging Best Practices

**Enable JSON Logging** in `appsettings.Production.json`:
```json
{
  "Logging": {
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "SingleLine": false,
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ"
      }
    }
  }
}
```

**Example Structured Log**:
```csharp
_logger.LogInformation(
    "WMS GetMap request completed in {DurationMs}ms for layer {LayerName}",
    duration.TotalMilliseconds,
    layerName
);
```

**Resulting JSON**:
```json
{
  "timestamp": "2025-10-17T12:00:00.123Z",
  "level": "Information",
  "message": "WMS GetMap request completed in 234ms for layer elevation",
  "DurationMs": 234,
  "LayerName": "elevation",
  "RequestId": "0HMVXXXXXX",
  "UserId": "user@example.com"
}
```

---

## Tracing Setup

### Jaeger Configuration

**1. View Traces**:

```bash
# Port-forward Jaeger UI
kubectl port-forward -n monitoring svc/jaeger-query 16686:16686

# Open http://localhost:16686
```

**2. Search for Traces**:
- Service: `honua-server`
- Operation: `GET /wms`
- Tags: `http.status_code=200`
- Min/Max Duration filtering

**3. Trace Example**: WMS GetMap Request

```
Trace ID: 1a2b3c4d5e6f7g8h
Service: honua-server
Duration: 456ms

├─ HTTP GET /wms (456ms)
│  ├─ MetadataRegistry.GetLayer (12ms)
│  ├─ RasterStorageRouter.GetTile (420ms)
│  │  ├─ S3RasterTileCacheProvider.GetAsync (15ms) [CACHE MISS]
│  │  ├─ S3RasterSourceProvider.ReadAsync (380ms)
│  │  │  ├─ HTTP GET s3://bucket/data.tif (350ms)
│  │  │  └─ LibTiffCogReader.ReadTile (30ms)
│  │  └─ S3RasterTileCacheProvider.PutAsync (25ms)
│  └─ SkiaSharpRasterRenderer.RenderTile (24ms)
```

### Azure AI Foundry Tracing (for AI Consultant)

**1. Configure in `appsettings.Production.json`**:
```json
{
  "OpenTelemetry": {
    "Tracing": {
      "Exporters": ["azureai"]
    },
    "AzureAI": {
      "ConnectionString": "${APPLICATIONINSIGHTS_CONNECTION_STRING}"
    }
  }
}
```

**2. View Agent Traces**:
- Open Azure Portal → Application Insights
- Navigate to "Transaction Search"
- Filter by Operation Name: `AgentCoordinator.ProcessRequest`

**3. Trace Example**: Deployment Process

```
Operation: DeploymentProcess.Execute
Duration: 15.3 minutes

├─ ValidateRequirements (23s)
├─ GenerateInfrastructure (2.1m)
│  └─ LLM Call: gpt-4 (1.8m)
├─ DeployInfrastructure (8.5m)
│  ├─ Terraform Init (45s)
│  ├─ Terraform Plan (1.2m)
│  └─ Terraform Apply (6.3m)
├─ ConfigureServices (1.5m)
└─ ValidateDeployment (3.2m)
```

---

## Metric Collection Verification

### Verify Metrics are Being Collected

**1. Check Prometheus Scrape Targets**:
```bash
curl http://prometheus:9090/api/v1/targets | jq '.data.activeTargets[] | select(.labels.job == "honua-server")'
```

**Expected Output**:
```json
{
  "discoveredLabels": {...},
  "labels": {
    "instance": "10.0.1.5:9090",
    "job": "honua-server"
  },
  "scrapePool": "honua-production/honua-server/0",
  "scrapeUrl": "http://10.0.1.5:9090/metrics",
  "health": "up",
  "lastScrape": "2025-10-17T12:00:00.123Z",
  "lastScrapeDuration": 0.023456
}
```

**2. Query Sample Metrics**:
```bash
# Request rate
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=rate(http_server_requests_total[5m])'

# Database connections
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=honua_postgres_connection_pool_active_connections'

# Process framework metrics
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=honua_process_active_instances'
```

**3. Verify Metrics Endpoint**:
```bash
curl http://honua-server:9090/metrics
```

**Expected Metrics** (sample):
```
# HELP http_server_requests_total Total HTTP requests
# TYPE http_server_requests_total counter
http_server_requests_total{method="GET",endpoint="/wms",status="200"} 12345

# HELP http_server_request_duration_bucket HTTP request duration histogram
# TYPE http_server_request_duration_bucket histogram
http_server_request_duration_bucket{method="GET",endpoint="/wms",le="0.1"} 8234
http_server_request_duration_bucket{method="GET",endpoint="/wms",le="0.5"} 11890
http_server_request_duration_bucket{method="GET",endpoint="/wms",le="1"} 12200

# HELP honua_tile_cache_hits_total Tile cache hits
# TYPE honua_tile_cache_hits_total counter
honua_tile_cache_hits_total{provider="S3"} 45678

# HELP honua_process_active_instances Active process instances
# TYPE honua_process_active_instances gauge
honua_process_active_instances 3
```

---

## Troubleshooting

### Issue: No Metrics in Prometheus

**Diagnosis**:
```bash
# Check Prometheus targets
kubectl port-forward -n monitoring svc/kube-prometheus-stack-prometheus 9090:9090
open http://localhost:9090/targets

# Check if honua-server target is listed and UP
```

**Common Causes**:
1. **ServiceMonitor not created**:
   ```bash
   kubectl get servicemonitor -n honua-production
   ```

2. **Label mismatch**: ServiceMonitor selector doesn't match Service labels
   ```bash
   kubectl get svc honua-server -o yaml | grep -A 5 "labels:"
   kubectl get servicemonitor honua-server -o yaml | grep -A 5 "selector:"
   ```

3. **Metrics endpoint not exposed**:
   ```bash
   kubectl exec -it deploy/honua-server -- curl http://localhost:9090/metrics
   ```

**Resolution**:
```bash
# Recreate ServiceMonitor with correct labels
kubectl apply -f honua-servicemonitor.yaml

# Restart Prometheus
kubectl rollout restart -n monitoring statefulset/prometheus-kube-prometheus-stack-prometheus
```

### Issue: Dashboards Show "No Data"

**Diagnosis**:
```bash
# Check datasource connection in Grafana
curl -u admin:${GRAFANA_PASSWORD} \
  http://grafana:3000/api/datasources

# Test Prometheus query manually
curl -G http://prometheus:9090/api/v1/query \
  --data-urlencode 'query=up{job="honua-server"}'
```

**Resolution**:
1. Verify Prometheus datasource URL in Grafana
2. Check time range in dashboard (adjust to "Last 15 minutes")
3. Verify metrics exist: `curl http://honua-server:9090/metrics | grep honua_`

### Issue: Loki Logs Not Showing

**Diagnosis**:
```bash
# Check Promtail pods
kubectl get pods -n monitoring -l app=promtail

# Check Promtail logs
kubectl logs -n monitoring -l app=promtail --tail=100
```

**Common Causes**:
1. Promtail not running or misconfigured
2. Labels don't match Loki datasource query
3. Log retention expired

**Resolution**:
```bash
# Restart Promtail
kubectl rollout restart -n monitoring daemonset/loki-promtail

# Check Loki ingestion
kubectl logs -n monitoring -l app=loki --tail=100 | grep "ingester"
```

### Issue: Traces Not Appearing in Jaeger

**Diagnosis**:
```bash
# Check OTLP collector logs
kubectl logs -n monitoring -l app=otel-collector --tail=100

# Verify trace export configuration
kubectl get configmap -n monitoring otel-collector -o yaml
```

**Resolution**:
1. Verify `Tracing.Enabled: true` in appsettings.json
2. Check OTLP endpoint is reachable from pods
3. Increase sampling ratio: `SamplingRatio: 1.0` (for debugging)

---

## Performance Impact

### Metrics Collection Overhead

**Expected Overhead**:
- CPU: +2-5%
- Memory: +50-100MB
- Network: +10-20KB/s per instance

**Optimization Tips**:
- Use OTLP batching (default: 10s)
- Reduce Prometheus scrape interval to 60s (from 30s) if not needed
- Lower trace sampling ratio in production: `0.1` (10%)

### Recommended Sampling Rates

**Development**:
```json
{
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 1.0
    }
  }
}
```

**Production (High Traffic)**:
```json
{
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 0.01
    }
  }
}
```

**Production (Low Traffic)**:
```json
{
  "OpenTelemetry": {
    "Tracing": {
      "SamplingRatio": 0.1
    }
  }
}
```

---

## Next Steps

1. **Review Dashboards**: Check all panels have data flowing
2. **Test Alerts**: Trigger test alerts to verify notification channels
3. **Set Up On-Call Rotation**: Configure PagerDuty/Opsgenie schedules
4. **Document Runbooks**: Create runbooks for each alert type
5. **Capacity Planning**: Set up long-term metric retention (6+ months)

---

## Support

For monitoring issues:
- Check [Troubleshooting Guide](rag/05-02-common-issues.md)
- Review [Performance Baselines](observability/performance-baselines.md)
- Contact DevOps team

---

**Last Updated**: 2025-10-17
**Version**: 2.0.0
**Contributors**: Honua Observability Team
