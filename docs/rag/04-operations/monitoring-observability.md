# Honua Monitoring and Observability Guide

**Keywords**: monitoring, observability, prometheus, grafana, alerting, metrics, logging, tracing, health-checks, opentelemetry, loki, promtail, slo, sli, sla, red-method, use-method, dashboards, performance-monitoring, distributed-tracing, log-aggregation, incident-response, service-level-objectives

**Related Topics**: [Performance Tuning](performance-tuning.md), [Troubleshooting](troubleshooting.md), [Kubernetes Deployment](../02-deployment/kubernetes-deployment.md), [Environment Variables](../01-configuration/environment-variables.md)

---

## Overview

This comprehensive guide provides production-ready monitoring and observability for Honua deployments. Built on OpenTelemetry, Prometheus, and Grafana, it enables full-stack visibility into your geospatial services, from infrastructure metrics to application performance and user experience.

**What You'll Learn**:
- Configure Prometheus metrics collection for Honua, PostgreSQL, and infrastructure
- Build Grafana dashboards for service health, database performance, and API monitoring
- Set up intelligent alerting with alert routing and fatigue prevention
- Implement centralized logging with Loki/Promtail or ELK stack
- Enable distributed tracing with OpenTelemetry
- Apply monitoring best practices (RED/USE methods, SLI/SLO/SLA)

**Key Features**:
- Native OpenTelemetry integration
- PostgreSQL/PostGIS-specific metrics
- Raster tile cache monitoring
- Spatial query performance tracking
- OGC API and OData metrics
- Production-tested configurations

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Prometheus Metrics](#prometheus-metrics)
3. [Grafana Dashboards](#grafana-dashboards)
4. [Alerting Configuration](#alerting-configuration)
5. [Log Aggregation](#log-aggregation)
6. [Distributed Tracing](#distributed-tracing)
7. [Health Checks](#health-checks)
8. [Monitoring Patterns](#monitoring-patterns)
9. [Production Deployment](#production-deployment)
10. [Troubleshooting Observability](#troubleshooting-observability)

---

## Architecture Overview

### Observability Stack

```
┌─────────────────────────────────────────────────────────┐
│                    Visualization Layer                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │   Grafana    │  │  AlertManager│  │   Jaeger     │  │
│  │  Dashboards  │  │   Routing    │  │   Tracing    │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │
└─────────┼──────────────────┼──────────────────┼──────────┘
          │                  │                  │
┌─────────┼──────────────────┼──────────────────┼──────────┐
│         │    Collection & Storage Layer       │          │
│  ┌──────▼───────┐  ┌──────▼───────┐  ┌──────▼───────┐  │
│  │  Prometheus  │  │  Loki/ELK    │  │ OpenTelemetry│  │
│  │   TSDB       │  │  Log Store   │  │   Collector  │  │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘  │
└─────────┼──────────────────┼──────────────────┼──────────┘
          │                  │                  │
┌─────────┼──────────────────┼──────────────────┼──────────┐
│         │      Instrumentation Layer          │          │
│  ┌──────▼───────────────────▼──────────────────▼──────┐  │
│  │              Honua Server (ASP.NET Core)           │  │
│  │  - OpenTelemetry Metrics & Traces                  │  │
│  │  - Structured JSON Logging                         │  │
│  │  - Health Check Endpoints                          │  │
│  │  - Custom Raster Cache Metrics                     │  │
│  └────────────────────────────────────────────────────┘  │
│                                                           │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐         │
│  │ PostgreSQL │  │   Node     │  │   Nginx    │         │
│  │  Exporter  │  │  Exporter  │  │  Exporter  │         │
│  └────────────┘  └────────────┘  └────────────┘         │
└───────────────────────────────────────────────────────────┘
```

### Components

**Metrics Collection**:
- **Honua Server**: OpenTelemetry metrics exposed at `/metrics` endpoint
- **PostgreSQL Exporter**: Database and PostGIS-specific metrics
- **Node Exporter**: System-level metrics (CPU, memory, disk, network)
- **Nginx Exporter**: Reverse proxy and load balancer metrics

**Storage & Queries**:
- **Prometheus**: Time-series database for metrics with PromQL query language
- **Loki**: Log aggregation optimized for Kubernetes
- **ELK Stack**: Alternative log aggregation (Elasticsearch, Logstash, Kibana)

**Visualization & Alerting**:
- **Grafana**: Unified dashboards for metrics, logs, and traces
- **AlertManager**: Alert deduplication, grouping, and routing
- **Jaeger**: Distributed tracing visualization

---

## Prometheus Metrics

### Honua Metrics Configuration

Honua uses OpenTelemetry for metrics instrumentation with Prometheus export.

#### Enable Metrics

**Environment Variables**:
```bash
# Enable metrics collection
observability__metrics__enabled=true

# Use Prometheus format
observability__metrics__usePrometheus=true

# Metrics endpoint (default: /metrics)
observability__metrics__endpoint=/metrics
```

**appsettings.json**:
```json
{
  "observability": {
    "metrics": {
      "enabled": true,
      "usePrometheus": true,
      "endpoint": "/metrics"
    }
  }
}
```

**Docker Compose** (`docker-compose.prometheus.yml`):
```yaml
version: "3.9"

services:
  web:
    environment:
      observability__metrics__enabled: "true"
      observability__metrics__usePrometheus: "true"
      observability__metrics__endpoint: "/metrics"

  prometheus:
    image: prom/prometheus:v2.53.0
    container_name: honua-prometheus
    restart: unless-stopped
    depends_on:
      - web
    ports:
      - "${PROMETHEUS_PORT:-9090}:9090"
    volumes:
      - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
      - '--web.console.libraries=/usr/share/prometheus/console_libraries'
      - '--web.console.templates=/usr/share/prometheus/consoles'

volumes:
  prometheus-data:
```

#### Available Honua Metrics

**HTTP Request Metrics** (ASP.NET Core):
```
# Request duration histogram
http_server_request_duration_seconds{method="GET",route="/collections/{collectionId}/items"}

# Active requests gauge
http_server_active_requests{method="GET"}

# Request count
http_server_requests_total{method="GET",status="200"}
```

**Runtime Metrics** (.NET):
```
# Process CPU usage
dotnet_process_cpu_count
process_cpu_seconds_total

# Memory metrics
dotnet_gc_heap_size_bytes{generation="gen0|gen1|gen2|loh"}
dotnet_gc_collections_total{generation="gen0|gen1|gen2"}
process_working_set_bytes
process_private_memory_bytes

# Thread pool
dotnet_threadpool_num_threads
dotnet_threadpool_queue_length
```

**Raster Cache Metrics** (Honua-specific):
```
# Cache performance
honua_raster_cache_hits{dataset="elevation"}
honua_raster_cache_misses{dataset="elevation"}

# Render latency
honua_raster_render_latency_ms{dataset="elevation",source="request|preseed"}

# Preseed jobs
honua_raster_preseed_jobs_completed{jobId="...",datasets="..."}
honua_raster_preseed_jobs_failed{jobId="...",error="..."}
honua_raster_preseed_jobs_cancelled{jobId="..."}

# Cache operations
honua_raster_cache_purges_succeeded{dataset="elevation"}
honua_raster_cache_purges_failed{dataset="elevation"}
```

**Custom Metric Cardinality Management**:

To prevent metric explosion with high-cardinality labels:

```csharp
// In RasterTileCacheMetrics.cs - dataset normalization
private static string Normalize(string value)
    => string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;

// Limit unique dataset IDs to configured collections
// Avoid user-provided strings as labels
```

### PostgreSQL Metrics

Deploy `postgres_exporter` to monitor database health and PostGIS performance.

**Docker Compose**:
```yaml
services:
  postgres-exporter:
    image: quay.io/prometheuscommunity/postgres-exporter:v0.15.0
    container_name: honua-postgres-exporter
    restart: unless-stopped
    depends_on:
      - db
    ports:
      - "9187:9187"
    environment:
      DATA_SOURCE_NAME: "postgresql://honua:honua_password@db:5432/honua?sslmode=disable"
      PG_EXPORTER_EXTEND_QUERY_PATH: "/etc/postgres_exporter/queries.yaml"
    volumes:
      - ./prometheus/postgres-queries.yaml:/etc/postgres_exporter/queries.yaml:ro
```

**Custom PostGIS Queries** (`postgres-queries.yaml`):
```yaml
# Spatial index usage
pg_postgis_spatial_index_usage:
  query: |
    SELECT
      schemaname,
      tablename,
      indexname,
      idx_scan as scans,
      idx_tup_read as tuples_read,
      idx_tup_fetch as tuples_fetched
    FROM pg_stat_user_indexes
    WHERE indexname LIKE '%_geom_idx'
    ORDER BY idx_scan DESC;
  metrics:
    - schemaname:
        usage: "LABEL"
        description: "Schema name"
    - tablename:
        usage: "LABEL"
        description: "Table name"
    - indexname:
        usage: "LABEL"
        description: "Index name"
    - scans:
        usage: "COUNTER"
        description: "Number of index scans"
    - tuples_read:
        usage: "COUNTER"
        description: "Tuples read from index"
    - tuples_fetched:
        usage: "COUNTER"
        description: "Tuples fetched using index"

# PostGIS geometry statistics
pg_postgis_geometry_stats:
  query: |
    SELECT
      schemaname,
      tablename,
      attname as column_name,
      n_distinct,
      most_common_vals::text as mcv
    FROM pg_stats
    WHERE attname IN (
      SELECT f_geometry_column
      FROM geometry_columns
    )
    AND schemaname NOT IN ('pg_catalog', 'information_schema');
  metrics:
    - schemaname:
        usage: "LABEL"
    - tablename:
        usage: "LABEL"
    - column_name:
        usage: "LABEL"
    - n_distinct:
        usage: "GAUGE"
        description: "Estimated distinct geometry values"

# Long-running spatial queries
pg_postgis_long_queries:
  query: |
    SELECT
      datname,
      usename,
      application_name,
      state,
      EXTRACT(EPOCH FROM (now() - query_start)) as duration_seconds,
      LEFT(query, 100) as query_snippet
    FROM pg_stat_activity
    WHERE query LIKE '%ST_%'
      AND state != 'idle'
      AND query_start < now() - interval '30 seconds'
    ORDER BY duration_seconds DESC
    LIMIT 10;
  metrics:
    - datname:
        usage: "LABEL"
    - usename:
        usage: "LABEL"
    - application_name:
        usage: "LABEL"
    - state:
        usage: "LABEL"
    - duration_seconds:
        usage: "GAUGE"

# Table sizes including geometry columns
pg_postgis_table_sizes:
  query: |
    SELECT
      schemaname,
      tablename,
      pg_total_relation_size(schemaname||'.'||tablename) as total_bytes,
      pg_relation_size(schemaname||'.'||tablename) as table_bytes,
      pg_indexes_size(schemaname||'.'||tablename) as index_bytes
    FROM pg_tables
    WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
    AND tablename IN (
      SELECT f_table_name FROM geometry_columns
    );
  metrics:
    - schemaname:
        usage: "LABEL"
    - tablename:
        usage: "LABEL"
    - total_bytes:
        usage: "GAUGE"
        description: "Total table size including indexes"
    - table_bytes:
        usage: "GAUGE"
        description: "Table data size"
    - index_bytes:
        usage: "GAUGE"
        description: "Index size"

# Connection pool metrics
pg_connection_pool:
  query: |
    SELECT
      state,
      COUNT(*) as connections
    FROM pg_stat_activity
    WHERE datname = current_database()
    GROUP BY state;
  metrics:
    - state:
        usage: "LABEL"
        description: "Connection state"
    - connections:
        usage: "GAUGE"
        description: "Number of connections"
```

**Standard PostgreSQL Metrics**:
```
# Database size
pg_database_size_bytes{datname="honua"}

# Connections
pg_stat_database_numbackends{datname="honua"}

# Transaction rate
rate(pg_stat_database_xact_commit{datname="honua"}[5m])
rate(pg_stat_database_xact_rollback{datname="honua"}[5m])

# Cache hit ratio
pg_stat_database_blks_hit / (pg_stat_database_blks_hit + pg_stat_database_blks_read)

# Lock statistics
pg_locks_count{mode="ExclusiveLock"}

# Replication lag (if using replicas)
pg_replication_lag_seconds
```

### System Metrics (Node Exporter)

**Docker Compose**:
```yaml
services:
  node-exporter:
    image: prom/node-exporter:v1.8.1
    container_name: honua-node-exporter
    restart: unless-stopped
    ports:
      - "9100:9100"
    command:
      - '--path.procfs=/host/proc'
      - '--path.sysfs=/host/sys'
      - '--path.rootfs=/rootfs'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro
```

**Key System Metrics**:
```
# CPU usage
rate(node_cpu_seconds_total{mode="idle"}[5m])
rate(node_cpu_seconds_total{mode="system"}[5m])

# Memory
node_memory_MemAvailable_bytes
node_memory_MemTotal_bytes
(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes

# Disk I/O
rate(node_disk_reads_completed_total[5m])
rate(node_disk_writes_completed_total[5m])
rate(node_disk_read_bytes_total[5m])
rate(node_disk_written_bytes_total[5m])

# Disk space
node_filesystem_avail_bytes{mountpoint="/"}
node_filesystem_size_bytes{mountpoint="/"}

# Network
rate(node_network_receive_bytes_total[5m])
rate(node_network_transmit_bytes_total[5m])
```

### Prometheus Configuration

**Complete prometheus.yml**:
```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s
  external_labels:
    cluster: 'honua-production'
    environment: 'prod'

# Alertmanager configuration
alerting:
  alertmanagers:
    - static_configs:
        - targets:
            - alertmanager:9093

# Load alert rules
rule_files:
  - /etc/prometheus/alerts/*.yml

scrape_configs:
  # Honua application metrics
  - job_name: 'honua'
    metrics_path: '/metrics'
    static_configs:
      - targets:
          - web:8080
    metric_relabel_configs:
      # Drop high-cardinality metrics if needed
      - source_labels: [__name__]
        regex: 'http_server_request_duration_seconds_bucket'
        action: drop

  # PostgreSQL database metrics
  - job_name: 'postgres'
    static_configs:
      - targets:
          - postgres-exporter:9187

  # System metrics
  - job_name: 'node'
    static_configs:
      - targets:
          - node-exporter:9100

  # Prometheus self-monitoring
  - job_name: 'prometheus'
    static_configs:
      - targets:
          - localhost:9090

  # Kubernetes pod discovery (if deploying to K8s)
  - job_name: 'kubernetes-pods'
    kubernetes_sd_configs:
      - role: pod
        namespaces:
          names:
            - honua
    relabel_configs:
      # Only scrape pods with prometheus.io/scrape annotation
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_path]
        action: replace
        target_label: __metrics_path__
        regex: (.+)
      - source_labels: [__address__, __meta_kubernetes_pod_annotation_prometheus_io_port]
        action: replace
        regex: ([^:]+)(?::\d+)?;(\d+)
        replacement: $1:$2
        target_label: __address__
      - source_labels: [__meta_kubernetes_namespace]
        action: replace
        target_label: kubernetes_namespace
      - source_labels: [__meta_kubernetes_pod_name]
        action: replace
        target_label: kubernetes_pod_name
```

**Kubernetes Pod Annotations** (for auto-discovery):
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    metadata:
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
```

---

## Grafana Dashboards

### Installation

**Docker Compose**:
```yaml
services:
  grafana:
    image: grafana/grafana:11.1.0
    container_name: honua-grafana
    restart: unless-stopped
    depends_on:
      - prometheus
    ports:
      - "${GRAFANA_PORT:-3000}:3000"
    environment:
      GF_SECURITY_ADMIN_USER: ${GRAFANA_ADMIN_USER:-admin}
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD:-admin}
      GF_INSTALL_PLUGINS: grafana-piechart-panel,grafana-worldmap-panel
      GF_AUTH_ANONYMOUS_ENABLED: "false"
      GF_USERS_ALLOW_SIGN_UP: "false"
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ./grafana/dashboards:/var/lib/grafana/dashboards:ro

volumes:
  grafana-data:
```

### Data Source Provisioning

**grafana/provisioning/datasources/datasources.yaml**:
```yaml
apiVersion: 1

datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: false
    jsonData:
      timeInterval: '15s'
      queryTimeout: '60s'
      httpMethod: 'POST'

  - name: Loki
    type: loki
    access: proxy
    url: http://loki:3100
    editable: false
    jsonData:
      maxLines: 1000

  - name: Jaeger
    type: jaeger
    access: proxy
    url: http://jaeger:16686
    editable: false
```

### Dashboard 1: Service Health Overview

**grafana/dashboards/service-health.json** (key panels):

```json
{
  "dashboard": {
    "title": "Honua Service Health",
    "tags": ["honua", "overview", "health"],
    "timezone": "browser",
    "panels": [
      {
        "title": "Service Status",
        "type": "stat",
        "targets": [
          {
            "expr": "up{job=\"honua\"}",
            "legendFormat": "Honua Server"
          }
        ],
        "options": {
          "colorMode": "background",
          "graphMode": "none",
          "textMode": "value_and_name"
        },
        "fieldConfig": {
          "defaults": {
            "thresholds": {
              "mode": "absolute",
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 1, "color": "green" }
              ]
            },
            "mappings": [
              { "value": 1, "text": "UP" },
              { "value": 0, "text": "DOWN" }
            ]
          }
        }
      },
      {
        "title": "Request Rate (req/s)",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(http_server_requests_total{job=\"honua\"}[5m]))",
            "legendFormat": "Total Requests"
          },
          {
            "expr": "sum(rate(http_server_requests_total{job=\"honua\",status=~\"2..\"}[5m]))",
            "legendFormat": "2xx Success"
          },
          {
            "expr": "sum(rate(http_server_requests_total{job=\"honua\",status=~\"4..\"}[5m]))",
            "legendFormat": "4xx Client Error"
          },
          {
            "expr": "sum(rate(http_server_requests_total{job=\"honua\",status=~\"5..\"}[5m]))",
            "legendFormat": "5xx Server Error"
          }
        ]
      },
      {
        "title": "Request Latency (p95)",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{job=\"honua\"}[5m])) by (le, route))",
            "legendFormat": "{{route}}"
          }
        ],
        "yaxes": [
          {
            "format": "s",
            "label": "Latency"
          }
        ]
      },
      {
        "title": "Error Rate (%)",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(http_server_requests_total{job=\"honua\",status=~\"5..\"}[5m])) / sum(rate(http_server_requests_total{job=\"honua\"}[5m])) * 100",
            "legendFormat": "Error Rate"
          }
        ],
        "alert": {
          "conditions": [
            {
              "evaluator": {
                "params": [5],
                "type": "gt"
              },
              "operator": {
                "type": "and"
              },
              "query": {
                "params": ["A", "5m", "now"]
              },
              "reducer": {
                "params": [],
                "type": "avg"
              },
              "type": "query"
            }
          ],
          "executionErrorState": "alerting",
          "frequency": "1m",
          "handler": 1,
          "name": "High Error Rate",
          "noDataState": "no_data",
          "notifications": []
        }
      },
      {
        "title": "Active Connections",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(http_server_active_requests{job=\"honua\"})",
            "legendFormat": "Active"
          }
        ]
      },
      {
        "title": "Health Check Status",
        "type": "table",
        "targets": [
          {
            "expr": "up{job=~\"honua|postgres|node\"}",
            "format": "table",
            "instant": true
          }
        ],
        "transformations": [
          {
            "id": "organize",
            "options": {
              "excludeByName": {
                "Time": true,
                "__name__": true
              },
              "indexByName": {
                "job": 0,
                "instance": 1,
                "Value": 2
              },
              "renameByName": {
                "job": "Service",
                "instance": "Instance",
                "Value": "Status"
              }
            }
          }
        ]
      }
    ]
  }
}
```

### Dashboard 2: Database Performance

**Key Panels**:

```json
{
  "panels": [
    {
      "title": "Database Connections",
      "targets": [
        {
          "expr": "pg_stat_database_numbackends{datname=\"honua\"}"
        }
      ]
    },
    {
      "title": "Cache Hit Ratio",
      "targets": [
        {
          "expr": "rate(pg_stat_database_blks_hit{datname=\"honua\"}[5m]) / (rate(pg_stat_database_blks_hit{datname=\"honua\"}[5m]) + rate(pg_stat_database_blks_read{datname=\"honua\"}[5m])) * 100"
        }
      ],
      "thresholds": [
        { "value": 0, "color": "red" },
        { "value": 90, "color": "yellow" },
        { "value": 95, "color": "green" }
      ]
    },
    {
      "title": "Transaction Rate",
      "targets": [
        {
          "expr": "rate(pg_stat_database_xact_commit{datname=\"honua\"}[5m])",
          "legendFormat": "Commits"
        },
        {
          "expr": "rate(pg_stat_database_xact_rollback{datname=\"honua\"}[5m])",
          "legendFormat": "Rollbacks"
        }
      ]
    },
    {
      "title": "Spatial Index Scans",
      "targets": [
        {
          "expr": "sum(pg_postgis_spatial_index_usage_scans) by (tablename, indexname)"
        }
      ]
    },
    {
      "title": "Long-Running Spatial Queries",
      "type": "table",
      "targets": [
        {
          "expr": "pg_postgis_long_queries_duration_seconds > 30"
        }
      ]
    },
    {
      "title": "Table Sizes (GB)",
      "targets": [
        {
          "expr": "pg_postgis_table_sizes_total_bytes / 1024 / 1024 / 1024"
        }
      ]
    },
    {
      "title": "Deadlocks",
      "targets": [
        {
          "expr": "rate(pg_stat_database_deadlocks{datname=\"honua\"}[5m])"
        }
      ]
    }
  ]
}
```

### Dashboard 3: API Metrics

**Panels for OGC API and OData**:

```json
{
  "panels": [
    {
      "title": "Requests by Endpoint",
      "targets": [
        {
          "expr": "sum(rate(http_server_requests_total{job=\"honua\"}[5m])) by (route)"
        }
      ]
    },
    {
      "title": "Collection Query Performance",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{route=~\"/collections/.*/items\"}[5m])) by (le))"
        }
      ]
    },
    {
      "title": "OData Query Latency",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket{route=~\"/odata/.*\"}[5m])) by (le, route))"
        }
      ]
    },
    {
      "title": "Top Slowest Endpoints (p99)",
      "type": "table",
      "targets": [
        {
          "expr": "topk(10, histogram_quantile(0.99, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le, route)))"
        }
      ]
    }
  ]
}
```

### Dashboard 4: Raster Tile Cache

**Honua-specific raster monitoring**:

```json
{
  "panels": [
    {
      "title": "Cache Hit Rate (%)",
      "targets": [
        {
          "expr": "sum(rate(honua_raster_cache_hits[5m])) / (sum(rate(honua_raster_cache_hits[5m])) + sum(rate(honua_raster_cache_misses[5m]))) * 100"
        }
      ]
    },
    {
      "title": "Cache Hits/Misses by Dataset",
      "targets": [
        {
          "expr": "sum(rate(honua_raster_cache_hits[5m])) by (dataset)",
          "legendFormat": "{{dataset}} hits"
        },
        {
          "expr": "sum(rate(honua_raster_cache_misses[5m])) by (dataset)",
          "legendFormat": "{{dataset}} misses"
        }
      ]
    },
    {
      "title": "Render Latency (p95)",
      "targets": [
        {
          "expr": "histogram_quantile(0.95, honua_raster_render_latency_ms) by (dataset, source)"
        }
      ]
    },
    {
      "title": "Preseed Job Status",
      "type": "stat",
      "targets": [
        {
          "expr": "sum(honua_raster_preseed_jobs_completed)",
          "legendFormat": "Completed"
        },
        {
          "expr": "sum(honua_raster_preseed_jobs_failed)",
          "legendFormat": "Failed"
        }
      ]
    },
    {
      "title": "Cache Purge Operations",
      "targets": [
        {
          "expr": "sum(rate(honua_raster_cache_purges_succeeded[5m]))",
          "legendFormat": "Successful"
        },
        {
          "expr": "sum(rate(honua_raster_cache_purges_failed[5m]))",
          "legendFormat": "Failed"
        }
      ]
    }
  ]
}
```

### Dashboard 5: Infrastructure Monitoring

**System resources**:

```json
{
  "panels": [
    {
      "title": "CPU Usage (%)",
      "targets": [
        {
          "expr": "100 - (avg(rate(node_cpu_seconds_total{mode=\"idle\"}[5m])) * 100)"
        }
      ]
    },
    {
      "title": "Memory Usage (%)",
      "targets": [
        {
          "expr": "(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes * 100"
        }
      ]
    },
    {
      "title": "Disk Usage (%)",
      "targets": [
        {
          "expr": "100 - ((node_filesystem_avail_bytes{mountpoint=\"/\"} * 100) / node_filesystem_size_bytes{mountpoint=\"/\"})"
        }
      ]
    },
    {
      "title": "Network I/O (MB/s)",
      "targets": [
        {
          "expr": "rate(node_network_receive_bytes_total[5m]) / 1024 / 1024",
          "legendFormat": "Inbound"
        },
        {
          "expr": "rate(node_network_transmit_bytes_total[5m]) / 1024 / 1024",
          "legendFormat": "Outbound"
        }
      ]
    },
    {
      "title": "Disk I/O (ops/s)",
      "targets": [
        {
          "expr": "rate(node_disk_reads_completed_total[5m])",
          "legendFormat": "Reads"
        },
        {
          "expr": "rate(node_disk_writes_completed_total[5m])",
          "legendFormat": "Writes"
        }
      ]
    }
  ]
}
```

---

## Alerting Configuration

### AlertManager Setup

**Docker Compose**:
```yaml
services:
  alertmanager:
    image: prom/alertmanager:v0.27.0
    container_name: honua-alertmanager
    restart: unless-stopped
    ports:
      - "9093:9093"
    volumes:
      - ./alertmanager/alertmanager.yml:/etc/alertmanager/alertmanager.yml:ro
      - alertmanager-data:/alertmanager
    command:
      - '--config.file=/etc/alertmanager/alertmanager.yml'
      - '--storage.path=/alertmanager'

volumes:
  alertmanager-data:
```

### Alert Routing Configuration

**alertmanager/alertmanager.yml**:
```yaml
global:
  # Slack webhook for general notifications
  slack_api_url: 'https://hooks.slack.com/services/YOUR/SLACK/WEBHOOK'

  # PagerDuty integration key for critical alerts
  pagerduty_url: 'https://events.pagerduty.com/v2/enqueue'

# Alert routing tree
route:
  receiver: 'default'
  group_by: ['alertname', 'cluster', 'service']
  group_wait: 10s
  group_interval: 5m
  repeat_interval: 4h

  routes:
    # Critical alerts -> PagerDuty + Slack
    - match:
        severity: critical
      receiver: 'pagerduty-critical'
      group_wait: 0s
      repeat_interval: 5m
      continue: true

    - match:
        severity: critical
      receiver: 'slack-critical'

    # Warning alerts -> Slack only
    - match:
        severity: warning
      receiver: 'slack-warnings'
      group_wait: 30s
      repeat_interval: 12h

    # Database alerts -> DBA team
    - match:
        component: database
      receiver: 'slack-database'

    # Infrastructure alerts -> ops team
    - match:
        component: infrastructure
      receiver: 'slack-infrastructure'

# Alert inhibition rules (prevent alert fatigue)
inhibit_rules:
  # If service is down, suppress all other alerts for that service
  - source_match:
      severity: 'critical'
      alertname: 'ServiceDown'
    target_match_re:
      severity: 'warning|info'
    equal: ['service']

  # If disk is full, suppress high disk usage warnings
  - source_match:
      alertname: 'DiskFull'
    target_match:
      alertname: 'DiskSpaceWarning'
    equal: ['instance']

receivers:
  - name: 'default'
    slack_configs:
      - channel: '#honua-monitoring'
        title: 'Alert: {{ .GroupLabels.alertname }}'
        text: '{{ range .Alerts }}{{ .Annotations.description }}{{ end }}'

  - name: 'pagerduty-critical'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: '{{ .GroupLabels.alertname }}: {{ .CommonAnnotations.summary }}'
        severity: '{{ .CommonLabels.severity }}'

  - name: 'slack-critical'
    slack_configs:
      - channel: '#honua-critical'
        color: 'danger'
        title: ':fire: CRITICAL: {{ .GroupLabels.alertname }}'
        text: |-
          *Summary:* {{ .CommonAnnotations.summary }}
          *Description:* {{ .CommonAnnotations.description }}
          *Severity:* {{ .CommonLabels.severity }}
          {{ range .Alerts }}
          *Instance:* {{ .Labels.instance }}
          {{ end }}
        actions:
          - type: button
            text: 'View in Grafana'
            url: '{{ .ExternalURL }}'
          - type: button
            text: 'Silence'
            url: '{{ .ExternalURL }}/#/silences/new'

  - name: 'slack-warnings'
    slack_configs:
      - channel: '#honua-monitoring'
        color: 'warning'
        title: ':warning: Warning: {{ .GroupLabels.alertname }}'

  - name: 'slack-database'
    slack_configs:
      - channel: '#honua-database'
        username: 'Honua DB Monitor'

  - name: 'slack-infrastructure'
    slack_configs:
      - channel: '#honua-infrastructure'
        username: 'Honua Infra Monitor'
```

### Alert Rules

**prometheus/alerts/critical.yml**:
```yaml
groups:
  - name: critical_alerts
    interval: 30s
    rules:
      # Service down
      - alert: HonuaServiceDown
        expr: up{job="honua"} == 0
        for: 1m
        labels:
          severity: critical
          component: application
        annotations:
          summary: "Honua service is down"
          description: "Honua server {{ $labels.instance }} has been down for more than 1 minute."
          runbook_url: "https://docs.honua.io/runbooks/service-down"

      # Database down
      - alert: PostgresDatabaseDown
        expr: up{job="postgres"} == 0
        for: 1m
        labels:
          severity: critical
          component: database
        annotations:
          summary: "PostgreSQL database is down"
          description: "PostgreSQL instance {{ $labels.instance }} is unreachable."
          runbook_url: "https://docs.honua.io/runbooks/database-down"

      # High error rate
      - alert: HighErrorRate
        expr: |
          (
            sum(rate(http_server_requests_total{job="honua",status=~"5.."}[5m]))
            /
            sum(rate(http_server_requests_total{job="honua"}[5m]))
          ) * 100 > 5
        for: 5m
        labels:
          severity: critical
          component: application
        annotations:
          summary: "High 5xx error rate"
          description: "Error rate is {{ $value | humanize }}% (threshold: 5%)"
          runbook_url: "https://docs.honua.io/runbooks/high-error-rate"

      # Out of memory
      - alert: OutOfMemory
        expr: |
          (
            node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes
          ) * 100 < 10
        for: 5m
        labels:
          severity: critical
          component: infrastructure
        annotations:
          summary: "Server running out of memory"
          description: "Available memory is {{ $value | humanize }}% (threshold: 10%)"

      # Disk almost full
      - alert: DiskAlmostFull
        expr: |
          (
            node_filesystem_avail_bytes{mountpoint="/"}
            /
            node_filesystem_size_bytes{mountpoint="/"}
          ) * 100 < 10
        for: 5m
        labels:
          severity: critical
          component: infrastructure
        annotations:
          summary: "Disk space critically low"
          description: "Disk {{ $labels.device }} on {{ $labels.instance }} has only {{ $value | humanize }}% available"

      # Database connections exhausted
      - alert: DatabaseConnectionsExhausted
        expr: pg_stat_database_numbackends{datname="honua"} > 90
        for: 5m
        labels:
          severity: critical
          component: database
        annotations:
          summary: "Database connection pool near exhaustion"
          description: "{{ $value }} connections active (threshold: 90)"

      # Replication lag (for HA setups)
      - alert: ReplicationLagHigh
        expr: pg_replication_lag_seconds > 60
        for: 5m
        labels:
          severity: critical
          component: database
        annotations:
          summary: "PostgreSQL replication lag high"
          description: "Replication lag is {{ $value }} seconds (threshold: 60s)"
```

**prometheus/alerts/warnings.yml**:
```yaml
groups:
  - name: warning_alerts
    interval: 1m
    rules:
      # Elevated latency
      - alert: HighLatency
        expr: |
          histogram_quantile(0.95,
            sum(rate(http_server_request_duration_seconds_bucket{job="honua"}[5m])) by (le, route)
          ) > 2
        for: 10m
        labels:
          severity: warning
          component: application
        annotations:
          summary: "High API latency detected"
          description: "p95 latency for {{ $labels.route }} is {{ $value | humanize }}s (threshold: 2s)"

      # Slow database queries
      - alert: SlowDatabaseQueries
        expr: pg_postgis_long_queries_duration_seconds > 60
        for: 5m
        labels:
          severity: warning
          component: database
        annotations:
          summary: "Slow database queries detected"
          description: "Query running for {{ $value }} seconds: {{ $labels.query_snippet }}"

      # Low cache hit ratio
      - alert: LowCacheHitRatio
        expr: |
          (
            rate(pg_stat_database_blks_hit{datname="honua"}[5m])
            /
            (rate(pg_stat_database_blks_hit{datname="honua"}[5m]) + rate(pg_stat_database_blks_read{datname="honua"}[5m]))
          ) * 100 < 90
        for: 10m
        labels:
          severity: warning
          component: database
        annotations:
          summary: "Database cache hit ratio low"
          description: "Cache hit ratio is {{ $value | humanize }}% (threshold: 90%)"

      # Disk space warning
      - alert: DiskSpaceWarning
        expr: |
          (
            node_filesystem_avail_bytes{mountpoint="/"}
            /
            node_filesystem_size_bytes{mountpoint="/"}
          ) * 100 < 20
        for: 10m
        labels:
          severity: warning
          component: infrastructure
        annotations:
          summary: "Disk space running low"
          description: "Disk {{ $labels.device }} has {{ $value | humanize }}% available (threshold: 20%)"

      # High CPU usage
      - alert: HighCPUUsage
        expr: 100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100) > 80
        for: 10m
        labels:
          severity: warning
          component: infrastructure
        annotations:
          summary: "High CPU usage"
          description: "CPU usage is {{ $value | humanize }}% (threshold: 80%)"

      # Raster cache miss rate high
      - alert: RasterCacheMissRateHigh
        expr: |
          (
            sum(rate(honua_raster_cache_misses[5m]))
            /
            (sum(rate(honua_raster_cache_hits[5m])) + sum(rate(honua_raster_cache_misses[5m])))
          ) * 100 > 50
        for: 15m
        labels:
          severity: warning
          component: application
        annotations:
          summary: "High raster cache miss rate"
          description: "Cache miss rate is {{ $value | humanize }}% (threshold: 50%)"

      # Preseed job failures
      - alert: PreseedJobFailures
        expr: rate(honua_raster_preseed_jobs_failed[5m]) > 0
        for: 5m
        labels:
          severity: warning
          component: application
        annotations:
          summary: "Raster preseed jobs failing"
          description: "{{ $value }} preseed jobs failed in the last 5 minutes"
```

### Alert Fatigue Prevention

**Best Practices**:

1. **Use Appropriate For Durations**:
```yaml
# Critical alerts: Short duration for fast response
- alert: ServiceDown
  for: 1m

# Warning alerts: Longer duration to avoid noise
- alert: HighCPU
  for: 10m
```

2. **Implement Alert Inhibition**:
```yaml
# Suppress downstream alerts when root cause is identified
inhibit_rules:
  - source_match:
      alertname: 'DatabaseDown'
    target_match:
      component: 'database'
    equal: ['instance']
```

3. **Group Related Alerts**:
```yaml
route:
  group_by: ['alertname', 'cluster']
  group_wait: 10s      # Wait before sending first notification
  group_interval: 5m   # Time between grouped notifications
  repeat_interval: 4h  # How often to repeat notifications
```

4. **Use Severity Levels Consistently**:
- **critical**: Immediate action required, service impaired
- **warning**: Attention needed, may lead to critical
- **info**: Informational, no action needed

5. **Create Runbooks**:
```yaml
annotations:
  runbook_url: "https://docs.honua.io/runbooks/{{ $labels.alertname }}"
```

---

## Log Aggregation

### Option 1: Loki + Promtail (Recommended for Kubernetes)

**Architecture**:
```
Application Logs → Promtail → Loki → Grafana
```

**Docker Compose**:
```yaml
services:
  loki:
    image: grafana/loki:3.0.0
    container_name: honua-loki
    restart: unless-stopped
    ports:
      - "3100:3100"
    volumes:
      - ./loki/loki-config.yaml:/etc/loki/local-config.yaml:ro
      - loki-data:/loki
    command: -config.file=/etc/loki/local-config.yaml

  promtail:
    image: grafana/promtail:3.0.0
    container_name: honua-promtail
    restart: unless-stopped
    depends_on:
      - loki
    volumes:
      - ./promtail/promtail-config.yaml:/etc/promtail/config.yml:ro
      - /var/log:/var/log:ro
      - /var/lib/docker/containers:/var/lib/docker/containers:ro
    command: -config.file=/etc/promtail/config.yml

volumes:
  loki-data:
```

**loki/loki-config.yaml**:
```yaml
auth_enabled: false

server:
  http_listen_port: 3100
  grpc_listen_port: 9096

common:
  path_prefix: /loki
  storage:
    filesystem:
      chunks_directory: /loki/chunks
      rules_directory: /loki/rules
  replication_factor: 1
  ring:
    instance_addr: 127.0.0.1
    kvstore:
      store: inmemory

schema_config:
  configs:
    - from: 2024-01-01
      store: tsdb
      object_store: filesystem
      schema: v13
      index:
        prefix: index_
        period: 24h

limits_config:
  retention_period: 30d
  max_query_series: 100000
  max_query_parallelism: 32

chunk_store_config:
  max_look_back_period: 0s

table_manager:
  retention_deletes_enabled: true
  retention_period: 30d

compactor:
  working_directory: /loki/compactor
  compaction_interval: 10m
```

**promtail/promtail-config.yaml**:
```yaml
server:
  http_listen_port: 9080
  grpc_listen_port: 0

positions:
  filename: /tmp/positions.yaml

clients:
  - url: http://loki:3100/loki/api/v1/push

scrape_configs:
  # Docker container logs
  - job_name: docker
    docker_sd_configs:
      - host: unix:///var/run/docker.sock
        refresh_interval: 5s
    relabel_configs:
      - source_labels: ['__meta_docker_container_name']
        regex: '/(.*)'
        target_label: 'container'
      - source_labels: ['__meta_docker_container_log_stream']
        target_label: 'stream'
      - source_labels: ['__meta_docker_container_label_com_docker_compose_service']
        target_label: 'service'
    pipeline_stages:
      # Parse JSON logs from Honua (structured logging)
      - json:
          expressions:
            timestamp: Timestamp
            level: Level
            message: Message
            exception: Exception
            span_id: SpanId
            trace_id: TraceId
      - timestamp:
          source: timestamp
          format: RFC3339
      - labels:
          level:
          span_id:
          trace_id:
      - output:
          source: message

  # System logs
  - job_name: system
    static_configs:
      - targets:
          - localhost
        labels:
          job: varlogs
          __path__: /var/log/*log
```

**Kubernetes Deployment** (DaemonSet for Promtail):
```yaml
apiVersion: apps/v1
kind: DaemonSet
metadata:
  name: promtail
  namespace: honua
spec:
  selector:
    matchLabels:
      app: promtail
  template:
    metadata:
      labels:
        app: promtail
    spec:
      serviceAccountName: promtail
      containers:
      - name: promtail
        image: grafana/promtail:3.0.0
        args:
          - -config.file=/etc/promtail/config.yml
        volumeMounts:
        - name: config
          mountPath: /etc/promtail
        - name: varlog
          mountPath: /var/log
          readOnly: true
        - name: varlibdockercontainers
          mountPath: /var/lib/docker/containers
          readOnly: true
      volumes:
      - name: config
        configMap:
          name: promtail
      - name: varlog
        hostPath:
          path: /var/log
      - name: varlibdockercontainers
        hostPath:
          path: /var/lib/docker/containers
---
apiVersion: v1
kind: ServiceAccount
metadata:
  name: promtail
  namespace: honua
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: promtail
rules:
  - apiGroups: [""]
    resources:
      - nodes
      - nodes/proxy
      - services
      - endpoints
      - pods
    verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: promtail
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: promtail
subjects:
  - kind: ServiceAccount
    name: promtail
    namespace: honua
```

### Option 2: ELK Stack (Elasticsearch, Logstash, Kibana)

**Docker Compose**:
```yaml
services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.14.0
    container_name: honua-elasticsearch
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
    ports:
      - "9200:9200"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data

  logstash:
    image: docker.elastic.co/logstash/logstash:8.14.0
    container_name: honua-logstash
    depends_on:
      - elasticsearch
    volumes:
      - ./logstash/pipeline:/usr/share/logstash/pipeline:ro
      - ./logstash/config/logstash.yml:/usr/share/logstash/config/logstash.yml:ro
    ports:
      - "5000:5000/tcp"
      - "5000:5000/udp"
      - "9600:9600"

  kibana:
    image: docker.elastic.co/kibana/kibana:8.14.0
    container_name: honua-kibana
    depends_on:
      - elasticsearch
    ports:
      - "5601:5601"
    environment:
      ELASTICSEARCH_URL: http://elasticsearch:9200
      ELASTICSEARCH_HOSTS: '["http://elasticsearch:9200"]'

volumes:
  elasticsearch-data:
```

**logstash/pipeline/honua.conf**:
```ruby
input {
  tcp {
    port => 5000
    codec => json
  }
}

filter {
  # Parse Honua JSON logs
  if [Properties][SourceContext] =~ /Honua/ {
    mutate {
      add_field => {
        "application" => "honua"
      }
    }
  }

  # Extract spatial query info
  if [Message] =~ /ST_/ {
    grok {
      match => {
        "Message" => "ST_(?<spatial_function>\w+)"
      }
    }
    mutate {
      add_tag => ["spatial_query"]
    }
  }

  # Parse log level
  mutate {
    rename => {
      "Level" => "log_level"
      "Message" => "message"
      "Timestamp" => "timestamp"
    }
  }

  # Add geolocation (if available in logs)
  if [client_ip] {
    geoip {
      source => "client_ip"
      target => "geoip"
    }
  }
}

output {
  elasticsearch {
    hosts => ["elasticsearch:9200"]
    index => "honua-logs-%{+YYYY.MM.dd}"
  }

  # Debug output (disable in production)
  # stdout { codec => rubydebug }
}
```

### Honua Structured Logging Configuration

**Enable JSON logging**:
```bash
# Environment variable
observability__logging__jsonConsole=true
observability__logging__includeScopes=true
```

**appsettings.json**:
```json
{
  "observability": {
    "logging": {
      "jsonConsole": true,
      "includeScopes": true
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "Honua": "Debug"
    }
  }
}
```

### Log Query Patterns

**Grafana Explore (Loki)**:

```logql
# All errors
{service="honua"} |= "error" | json | level="Error"

# Slow spatial queries
{service="honua"} |~ "ST_.*" | json | duration > 1000

# Failed authentication attempts
{service="honua"} | json | message=~".*authentication failed.*"

# Trace-specific logs
{service="honua"} | json | trace_id="abc123"

# Count errors per minute
sum(count_over_time({service="honua"} | json | level="Error" [1m]))
```

**Kibana (Elasticsearch)**:

```
# Discover queries
application:honua AND log_level:Error
message:"ST_*" AND response_time:>1000
spatial_query:* AND log_level:Warning

# Aggregations
# Top spatial functions
terms aggregation on "spatial_function.keyword"

# Error rate over time
date histogram on "timestamp" + filter "log_level:Error"
```

---

## Distributed Tracing

### OpenTelemetry Integration

Honua uses OpenTelemetry for distributed tracing (future enhancement).

**Planned Configuration**:

```csharp
// In HonuaHostConfigurationExtensions.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracingBuilder =>
    {
        tracingBuilder
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                {
                    // Don't trace health checks
                    return !httpContext.Request.Path.StartsWithSegments("/healthz");
                };
            })
            .AddHttpClientInstrumentation()
            .AddNpgsql() // PostgreSQL tracing
            .AddSource("Honua.*");

        // Export to Jaeger
        if (tracingOptions.Exporter == "jaeger")
        {
            tracingBuilder.AddJaegerExporter(jaegerOptions =>
            {
                jaegerOptions.AgentHost = tracingOptions.JaegerHost;
                jaegerOptions.AgentPort = tracingOptions.JaegerPort;
            });
        }

        // Export to OTLP (OpenTelemetry Protocol)
        if (tracingOptions.Exporter == "otlp")
        {
            tracingBuilder.AddOtlpExporter(otlpOptions =>
            {
                otlpOptions.Endpoint = new Uri(tracingOptions.OtlpEndpoint);
            });
        }

        // Sampling configuration
        tracingBuilder.SetSampler(new TraceIdRatioBasedSampler(tracingOptions.SamplingRate));
    });
```

**Configuration**:
```json
{
  "observability": {
    "tracing": {
      "enabled": true,
      "exporter": "jaeger",
      "jaegerHost": "localhost",
      "jaegerPort": 6831,
      "samplingRate": 0.1
    }
  }
}
```

### Jaeger Deployment

**Docker Compose**:
```yaml
services:
  jaeger:
    image: jaegertracing/all-in-one:1.57
    container_name: honua-jaeger
    restart: unless-stopped
    ports:
      - "5775:5775/udp"   # Zipkin compact
      - "6831:6831/udp"   # Jaeger compact
      - "6832:6832/udp"   # Jaeger binary
      - "5778:5778"       # Serve configs
      - "16686:16686"     # UI
      - "14268:14268"     # Direct spans
      - "14250:14250"     # gRPC
      - "9411:9411"       # Zipkin compatible
    environment:
      COLLECTOR_OTLP_ENABLED: true
      SPAN_STORAGE_TYPE: badger
      BADGER_EPHEMERAL: "false"
      BADGER_DIRECTORY_VALUE: /badger/data
      BADGER_DIRECTORY_KEY: /badger/key
    volumes:
      - jaeger-data:/badger

volumes:
  jaeger-data:
```

### Trace Sampling Strategies

**Head-based Sampling** (configured at instrumentation):
```csharp
// Sample 10% of all traces
.SetSampler(new TraceIdRatioBasedSampler(0.1))

// Always sample errors
.SetSampler(new ParentBasedSampler(
    new AlwaysOnSampler()
))
```

**Tail-based Sampling** (configured in collector):
```yaml
# otel-collector-config.yaml
processors:
  tail_sampling:
    policies:
      - name: errors-policy
        type: status_code
        status_code:
          status_codes: [ERROR]
      - name: slow-requests
        type: latency
        latency:
          threshold_ms: 2000
      - name: probabilistic-policy
        type: probabilistic
        probabilistic:
          sampling_percentage: 10
```

### Performance Analysis with Traces

**Use Cases**:
1. **End-to-end request flow**: See how a single OGC API request flows through authentication → query building → PostGIS execution → serialization
2. **Database query performance**: Identify slow spatial queries
3. **External service calls**: Monitor calls to OAuth providers, S3, etc.
4. **Error correlation**: Link errors across multiple services

**Grafana Trace Visualization**:
- Link traces to logs via `trace_id`
- Correlate metrics spikes with trace data
- Create service dependency graphs

---

## Health Checks

Honua implements Kubernetes-style health checks with startup, liveness, and readiness probes.

### Health Check Endpoints

**Implementation** (from `HonuaHostConfigurationExtensions.cs`):
```csharp
builder.Services.AddHealthChecks()
    .AddCheck<MetadataHealthCheck>("metadata",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "startup", "ready" })
    .AddCheck<DataSourceHealthCheck>("dataSources",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" })
    .AddCheck("self",
        () => HealthCheckResult.Healthy("Application is running."),
        tags: new[] { "live" });

app.MapHealthChecks("/healthz/startup", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("startup"),
    ResponseWriter = HealthResponseWriter.WriteResponse
});

app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteResponse
});

app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteResponse
});
```

### Health Check Types

**1. Startup Probe** (`/healthz/startup`):
- Checks if application has fully initialized
- Includes metadata loading validation
- Used by Kubernetes to delay traffic until ready

**2. Liveness Probe** (`/healthz/live`):
- Minimal check: Is the process alive?
- Fast execution (< 100ms)
- Kubernetes restarts pod if this fails

**3. Readiness Probe** (`/healthz/ready`):
- Comprehensive checks: Can the service handle traffic?
- Validates database connectivity
- Checks all data sources
- Kubernetes removes from load balancer if this fails

### Response Format

```json
{
  "status": "Healthy",
  "duration": "00:00:00.0234567",
  "entries": {
    "metadata": {
      "status": "Healthy",
      "duration": "00:00:00.0123456",
      "description": "Metadata loaded successfully"
    },
    "dataSources": {
      "status": "Healthy",
      "duration": "00:00:00.0345678",
      "description": "All data sources are reachable.",
      "parcels": {
        "status": "Healthy",
        "provider": "postgres"
      },
      "elevation": {
        "status": "Healthy",
        "provider": "postgres"
      }
    },
    "self": {
      "status": "Healthy",
      "duration": "00:00:00.0000012",
      "description": "Application is running."
    }
  }
}
```

### Kubernetes Configuration

**Deployment with Health Checks**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-server
spec:
  template:
    spec:
      containers:
      - name: honua
        image: honua:latest
        ports:
        - containerPort: 8080

        # Startup probe - wait for app initialization
        startupProbe:
          httpGet:
            path: /healthz/startup
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 30  # Allow 150s for startup (30 * 5s)

        # Liveness probe - restart if unhealthy
        livenessProbe:
          httpGet:
            path: /healthz/live
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 3

        # Readiness probe - remove from load balancer if not ready
        readinessProbe:
          httpGet:
            path: /healthz/ready
            port: 8080
          initialDelaySeconds: 0
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
          successThreshold: 1
```

### Custom Health Checks

**Add Custom Check**:
```csharp
public class S3StorageHealthCheck : IHealthCheck
{
    private readonly IS3Client _s3Client;

    public S3StorageHealthCheck(IS3Client s3Client)
    {
        _s3Client = s3Client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canAccess = await _s3Client.CanAccessBucketAsync(cancellationToken);
            return canAccess
                ? HealthCheckResult.Healthy("S3 bucket accessible")
                : HealthCheckResult.Degraded("S3 bucket not accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("S3 health check failed", ex);
        }
    }
}

// Register
builder.Services.AddHealthChecks()
    .AddCheck<S3StorageHealthCheck>("s3storage", tags: new[] { "ready" });
```

### Health Check Monitoring

**Prometheus Endpoint**:
```yaml
# Monitor health check status
up{job="honua"} == 1

# Alert on health check failures
- alert: HealthCheckFailing
  expr: probe_success{job="honua-health"} == 0
  for: 2m
```

**Blackbox Exporter** (external monitoring):
```yaml
services:
  blackbox-exporter:
    image: prom/blackbox-exporter:v0.25.0
    volumes:
      - ./blackbox/blackbox.yml:/etc/blackbox_exporter/config.yml:ro
    ports:
      - "9115:9115"

# blackbox/blackbox.yml
modules:
  http_2xx:
    prober: http
    timeout: 5s
    http:
      valid_http_versions: ["HTTP/1.1", "HTTP/2.0"]
      valid_status_codes: [200]
      method: GET

# prometheus.yml
scrape_configs:
  - job_name: 'honua-health'
    metrics_path: /probe
    params:
      module: [http_2xx]
    static_configs:
      - targets:
          - http://web:8080/healthz/ready
    relabel_configs:
      - source_labels: [__address__]
        target_label: __param_target
      - source_labels: [__param_target]
        target_label: instance
      - target_label: __address__
        replacement: blackbox-exporter:9115
```

---

## Monitoring Patterns

### RED Method (Requests, Errors, Duration)

For **service-level monitoring** (APIs, endpoints):

**1. Rate**: Request throughput
```promql
# Total request rate
sum(rate(http_server_requests_total{job="honua"}[5m]))

# Rate by endpoint
sum(rate(http_server_requests_total{job="honua"}[5m])) by (route)
```

**2. Errors**: Error rate
```promql
# Error percentage
sum(rate(http_server_requests_total{job="honua",status=~"5.."}[5m]))
/
sum(rate(http_server_requests_total{job="honua"}[5m]))
* 100
```

**3. Duration**: Response time
```promql
# p50, p95, p99 latency
histogram_quantile(0.50, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))
histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))
histogram_quantile(0.99, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))
```

**Grafana Panel**:
```json
{
  "title": "RED Method - Service Health",
  "panels": [
    {
      "title": "Request Rate",
      "targets": [{"expr": "sum(rate(http_server_requests_total{job=\"honua\"}[5m]))"}]
    },
    {
      "title": "Error Rate (%)",
      "targets": [{"expr": "sum(rate(http_server_requests_total{job=\"honua\",status=~\"5..\"}[5m])) / sum(rate(http_server_requests_total{job=\"honua\"}[5m])) * 100"}]
    },
    {
      "title": "Latency (p95)",
      "targets": [{"expr": "histogram_quantile(0.95, sum(rate(http_server_request_duration_seconds_bucket[5m])) by (le))"}]
    }
  ]
}
```

### USE Method (Utilization, Saturation, Errors)

For **resource monitoring** (CPU, memory, disk, network):

**1. Utilization**: % of resource in use
```promql
# CPU utilization
100 - (avg(rate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory utilization
(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / node_memory_MemTotal_bytes * 100

# Disk utilization
100 - ((node_filesystem_avail_bytes{mountpoint="/"} * 100) / node_filesystem_size_bytes{mountpoint="/"})
```

**2. Saturation**: Resource queue length
```promql
# Thread pool queue length
dotnet_threadpool_queue_length

# Disk I/O wait
rate(node_disk_io_time_seconds_total[5m])

# Network bandwidth saturation
rate(node_network_transmit_bytes_total[5m]) / node_network_speed_bytes
```

**3. Errors**: Resource errors
```promql
# Disk errors
rate(node_disk_read_errors_total[5m])

# Network errors
rate(node_network_transmit_errs_total[5m])

# Out of memory events
node_vmstat_oom_kill
```

### SLI/SLO/SLA Configuration

**Service Level Indicators (SLIs)**:
```promql
# Availability SLI
sum(rate(http_server_requests_total{job="honua",status!~"5.."}[30d]))
/
sum(rate(http_server_requests_total{job="honua"}[30d]))
* 100

# Latency SLI (% of requests < 1s)
sum(rate(http_server_request_duration_seconds_bucket{le="1.0"}[30d]))
/
sum(rate(http_server_request_duration_seconds_count[30d]))
* 100
```

**Service Level Objectives (SLOs)**:
```yaml
# Example SLO definitions
slos:
  - name: api_availability
    target: 99.9  # 99.9% uptime
    window: 30d

  - name: api_latency_p95
    target: 95    # 95% of requests < 1s
    threshold: 1.0s
    window: 30d

  - name: error_budget
    target: 0.1   # 0.1% error rate
    window: 30d
```

**Error Budget Tracking**:
```promql
# Error budget consumption
(
  1 - (
    sum(rate(http_server_requests_total{job="honua",status!~"5.."}[30d]))
    /
    sum(rate(http_server_requests_total{job="honua"}[30d]))
  )
) / 0.001  # 99.9% SLO = 0.1% error budget
```

**SLO Dashboard Panel**:
```json
{
  "title": "SLO Compliance",
  "panels": [
    {
      "title": "Availability (30d)",
      "type": "gauge",
      "targets": [{
        "expr": "sum(rate(http_server_requests_total{job=\"honua\",status!~\"5..\"}[30d])) / sum(rate(http_server_requests_total{job=\"honua\"}[30d])) * 100"
      }],
      "fieldConfig": {
        "defaults": {
          "min": 99,
          "max": 100,
          "thresholds": {
            "steps": [
              {"value": 99, "color": "red"},
              {"value": 99.5, "color": "yellow"},
              {"value": 99.9, "color": "green"}
            ]
          }
        }
      }
    },
    {
      "title": "Error Budget Remaining",
      "targets": [{
        "expr": "1 - ((1 - (sum(rate(http_server_requests_total{job=\"honua\",status!~\"5..\"}[30d])) / sum(rate(http_server_requests_total{job=\"honua\"}[30d])))) / 0.001)"
      }]
    }
  ]
}
```

---

## Production Deployment

### Complete Docker Compose Stack

**docker-compose.monitoring.yml**:
```yaml
version: "3.9"

services:
  # Application (from base docker-compose.yml)
  web:
    environment:
      observability__metrics__enabled: "true"
      observability__metrics__usePrometheus: "true"
      observability__logging__jsonConsole: "true"

  # Metrics collection
  prometheus:
    image: prom/prometheus:v2.53.0
    container_name: honua-prometheus
    restart: unless-stopped
    depends_on:
      - web
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus/prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - ./prometheus/alerts:/etc/prometheus/alerts:ro
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
      - '--storage.tsdb.retention.time=30d'
      - '--web.enable-lifecycle'

  postgres-exporter:
    image: quay.io/prometheuscommunity/postgres-exporter:v0.15.0
    container_name: honua-postgres-exporter
    restart: unless-stopped
    depends_on:
      - db
    ports:
      - "9187:9187"
    environment:
      DATA_SOURCE_NAME: "postgresql://honua:honua_password@db:5432/honua?sslmode=disable"
      PG_EXPORTER_EXTEND_QUERY_PATH: "/etc/postgres_exporter/queries.yaml"
    volumes:
      - ./prometheus/postgres-queries.yaml:/etc/postgres_exporter/queries.yaml:ro

  node-exporter:
    image: prom/node-exporter:v1.8.1
    container_name: honua-node-exporter
    restart: unless-stopped
    ports:
      - "9100:9100"
    command:
      - '--path.procfs=/host/proc'
      - '--path.sysfs=/host/sys'
      - '--path.rootfs=/rootfs'
      - '--collector.filesystem.mount-points-exclude=^/(sys|proc|dev|host|etc)($$|/)'
    volumes:
      - /proc:/host/proc:ro
      - /sys:/host/sys:ro
      - /:/rootfs:ro

  # Alerting
  alertmanager:
    image: prom/alertmanager:v0.27.0
    container_name: honua-alertmanager
    restart: unless-stopped
    ports:
      - "9093:9093"
    volumes:
      - ./alertmanager/alertmanager.yml:/etc/alertmanager/alertmanager.yml:ro
      - alertmanager-data:/alertmanager
    command:
      - '--config.file=/etc/alertmanager/alertmanager.yml'
      - '--storage.path=/alertmanager'

  # Visualization
  grafana:
    image: grafana/grafana:11.1.0
    container_name: honua-grafana
    restart: unless-stopped
    depends_on:
      - prometheus
      - loki
    ports:
      - "3000:3000"
    environment:
      GF_SECURITY_ADMIN_USER: ${GRAFANA_ADMIN_USER:-admin}
      GF_SECURITY_ADMIN_PASSWORD: ${GRAFANA_ADMIN_PASSWORD:-admin}
      GF_INSTALL_PLUGINS: grafana-piechart-panel,grafana-worldmap-panel
      GF_AUTH_ANONYMOUS_ENABLED: "false"
      GF_USERS_ALLOW_SIGN_UP: "false"
    volumes:
      - grafana-data:/var/lib/grafana
      - ./grafana/provisioning:/etc/grafana/provisioning:ro
      - ./grafana/dashboards:/var/lib/grafana/dashboards:ro

  # Log aggregation
  loki:
    image: grafana/loki:3.0.0
    container_name: honua-loki
    restart: unless-stopped
    ports:
      - "3100:3100"
    volumes:
      - ./loki/loki-config.yaml:/etc/loki/local-config.yaml:ro
      - loki-data:/loki
    command: -config.file=/etc/loki/local-config.yaml

  promtail:
    image: grafana/promtail:3.0.0
    container_name: honua-promtail
    restart: unless-stopped
    depends_on:
      - loki
    volumes:
      - ./promtail/promtail-config.yaml:/etc/promtail/config.yml:ro
      - /var/log:/var/log:ro
      - /var/lib/docker/containers:/var/lib/docker/containers:ro
    command: -config.file=/etc/promtail/config.yml

  # Distributed tracing (optional)
  jaeger:
    image: jaegertracing/all-in-one:1.57
    container_name: honua-jaeger
    restart: unless-stopped
    ports:
      - "6831:6831/udp"
      - "16686:16686"
    environment:
      COLLECTOR_OTLP_ENABLED: true
      SPAN_STORAGE_TYPE: badger
      BADGER_EPHEMERAL: "false"
      BADGER_DIRECTORY_VALUE: /badger/data
      BADGER_DIRECTORY_KEY: /badger/key
    volumes:
      - jaeger-data:/badger

volumes:
  prometheus-data:
  alertmanager-data:
  grafana-data:
  loki-data:
  jaeger-data:
```

**Deploy**:
```bash
# Start full monitoring stack
docker-compose -f docker-compose.yml -f docker-compose.monitoring.yml up -d

# Access UIs
# Grafana: http://localhost:3000
# Prometheus: http://localhost:9090
# AlertManager: http://localhost:9093
# Jaeger: http://localhost:16686
```

### Kubernetes Deployment

**Complete monitoring namespace**:
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: honua-monitoring
---
# Prometheus deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: prometheus
  namespace: honua-monitoring
spec:
  replicas: 1
  selector:
    matchLabels:
      app: prometheus
  template:
    metadata:
      labels:
        app: prometheus
    spec:
      serviceAccountName: prometheus
      containers:
      - name: prometheus
        image: prom/prometheus:v2.53.0
        args:
          - '--config.file=/etc/prometheus/prometheus.yml'
          - '--storage.tsdb.path=/prometheus'
          - '--storage.tsdb.retention.time=30d'
        ports:
        - containerPort: 9090
        volumeMounts:
        - name: config
          mountPath: /etc/prometheus
        - name: storage
          mountPath: /prometheus
      volumes:
      - name: config
        configMap:
          name: prometheus-config
      - name: storage
        persistentVolumeClaim:
          claimName: prometheus-pvc
---
apiVersion: v1
kind: Service
metadata:
  name: prometheus
  namespace: honua-monitoring
spec:
  type: ClusterIP
  ports:
  - port: 9090
  selector:
    app: prometheus
---
# Grafana deployment (similar structure)
# AlertManager deployment
# Loki deployment
# ... (abbreviated for space)
```

**Apply with Helm** (recommended):
```bash
# Add Prometheus community Helm repo
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

# Install kube-prometheus-stack (includes Prometheus, Grafana, AlertManager)
helm install honua-monitoring prometheus-community/kube-prometheus-stack \
  --namespace honua-monitoring \
  --create-namespace \
  --values monitoring-values.yaml

# monitoring-values.yaml
prometheus:
  prometheusSpec:
    retention: 30d
    storageSpec:
      volumeClaimTemplate:
        spec:
          accessModes: ["ReadWriteOnce"]
          resources:
            requests:
              storage: 50Gi

grafana:
  adminPassword: "changeme"
  persistence:
    enabled: true
    size: 10Gi

  dashboardProviders:
    dashboardproviders.yaml:
      apiVersion: 1
      providers:
      - name: 'honua'
        folder: 'Honua'
        type: file
        options:
          path: /var/lib/grafana/dashboards/honua

alertmanager:
  config:
    global:
      slack_api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK'
    route:
      receiver: 'default'
      routes:
      - match:
          severity: critical
        receiver: 'pagerduty'

# Add Honua-specific ServiceMonitor
additionalServiceMonitors:
  - name: honua-app
    selector:
      matchLabels:
        app: honua-server
    namespaceSelector:
      matchNames:
        - honua
    endpoints:
      - port: http
        path: /metrics
```

---

## Troubleshooting Observability

### Metrics Not Appearing

**Check metrics endpoint**:
```bash
# Verify Honua exposes metrics
curl http://localhost:8080/metrics

# Should return Prometheus-formatted metrics
# HELP http_server_request_duration_seconds ...
```

**Verify Prometheus scraping**:
```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Check specific metric
curl 'http://localhost:9090/api/v1/query?query=up{job="honua"}'
```

**Common Issues**:
1. **Metrics disabled**: Check `observability__metrics__enabled=true`
2. **Wrong endpoint**: Verify `observability__metrics__endpoint` matches Prometheus config
3. **Authentication required**: Metrics endpoint may require auth in production
4. **Network isolation**: Ensure Prometheus can reach Honua on network

### High Cardinality Metrics

**Symptom**: Prometheus using excessive memory/disk

**Solution**: Drop high-cardinality labels
```yaml
# prometheus.yml
metric_relabel_configs:
  # Drop per-user labels
  - source_labels: [user_id]
    action: labeldrop

  # Drop histogram buckets if not needed
  - source_labels: [__name__]
    regex: '.*_bucket'
    action: drop
```

### Logs Not Showing in Grafana

**Check Loki connectivity**:
```bash
# Verify Loki is running
curl http://localhost:3100/ready

# Query logs directly
curl -G -s "http://localhost:3100/loki/api/v1/query_range" \
  --data-urlencode 'query={service="honua"}' \
  --data-urlencode 'limit=10'
```

**Check Promtail**:
```bash
# View Promtail logs
docker logs honua-promtail

# Check Promtail targets
curl http://localhost:9080/targets
```

**Common Issues**:
1. **Promtail can't read logs**: Check volume mounts and permissions
2. **JSON parsing failed**: Verify Honua is using JSON logging
3. **Label mismatch**: Ensure service labels match between Promtail and Grafana queries

### Alerts Not Firing

**Check alert rules**:
```bash
# View active alerts in Prometheus
curl http://localhost:9090/api/v1/alerts

# Check alert rule evaluation
curl http://localhost:9090/api/v1/rules
```

**Test alert expression**:
```bash
# Run query manually
curl -G 'http://localhost:9090/api/v1/query' \
  --data-urlencode 'query=up{job="honua"} == 0'
```

**Check AlertManager**:
```bash
# View AlertManager status
curl http://localhost:9093/api/v2/status

# Check silences
curl http://localhost:9093/api/v2/silences
```

### Performance Impact

**Monitor observability overhead**:
```promql
# Prometheus resource usage
process_resident_memory_bytes{job="prometheus"}
rate(process_cpu_seconds_total{job="prometheus"}[5m])

# Metrics ingestion rate
rate(prometheus_tsdb_head_samples_appended_total[5m])
```

**Optimize**:
- Increase scrape intervals for less critical targets
- Reduce metric retention period
- Use recording rules for expensive queries
- Enable metric relabeling to drop unnecessary labels

---

## Summary

This guide provides production-ready monitoring for Honua:

**Metrics**: OpenTelemetry + Prometheus for comprehensive instrumentation
**Dashboards**: Pre-built Grafana dashboards for service health, database, API, and infrastructure
**Alerting**: Intelligent alert routing with fatigue prevention
**Logging**: Centralized logs with Loki/Promtail or ELK stack
**Tracing**: Distributed tracing with OpenTelemetry and Jaeger
**Health Checks**: Kubernetes-style probes for reliability
**Best Practices**: RED/USE methods, SLI/SLO/SLA tracking

**Quick Start**:
```bash
# 1. Enable metrics in Honua
export observability__metrics__enabled=true

# 2. Deploy monitoring stack
docker-compose -f docker-compose.yml -f docker-compose.monitoring.yml up -d

# 3. Access Grafana
open http://localhost:3000

# 4. Import dashboards from grafana/dashboards/

# 5. Configure alerts in prometheus/alerts/

# Done! Full observability stack running.
```

For troubleshooting, see [Troubleshooting Guide](troubleshooting.md).
For optimization, see [Performance Tuning](performance-tuning.md).
