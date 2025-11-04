# Process Framework Observability Architecture

## Complete Stack with Distributed Tracing

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                             │
│                         Honua.Cli.AI Application                            │
│                      (Process Framework + Semantic Kernel)                  │
│                                                                             │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                     │
│  │ Deployment   │  │  Upgrade     │  │   GitOps     │  ... more processes │
│  │  Process     │  │  Process     │  │   Process    │                     │
│  └──────────────┘  └──────────────┘  └──────────────┘                     │
│         │                  │                  │                             │
│         └──────────────────┴──────────────────┘                             │
│                            │                                                │
│                            │ OpenTelemetry SDK                              │
│                            │ - Activity API                                 │
│                            │ - Instrumentation                              │
│                            │                                                │
└────────────────────────────┼────────────────────────────────────────────────┘
                             │
                             │ OTLP Protocol
                             │ (100% of traces, metrics, logs)
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                                                                             │
│                        OpenTelemetry Collector                              │
│                            (Port 4317/4318)                                 │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                          Receivers                                   │  │
│  │  - OTLP gRPC (4317)                                                  │  │
│  │  - OTLP HTTP (4318)                                                  │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                │                                            │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                          Processors                                  │  │
│  │  - Memory Limiter (512 MB)                                           │  │
│  │  - Resource Attributes (service.name, environment)                   │  │
│  │  - Probabilistic Sampler (10% for traces) ◄─────┐                   │  │
│  │  - Batch Processor (10s timeout)                │                   │  │
│  └──────────────────────────────────────────────────┼───────────────────┘  │
│                                │                    │                       │
│                                │              Sampling reduces              │
│                                │              trace volume by 90%           │
│                                │                    │                       │
│  ┌────────────────────────────┴────────────────────┴───────────────────┐  │
│  │                          Exporters                                   │  │
│  │  - Tempo (traces - 10% sampled)                                      │  │
│  │  - Prometheus (metrics - 100%)                                       │  │
│  │  - Loki (logs - 100%)                                                │  │
│  │  - Console (debugging)                                               │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└────────────┬────────────────────────┬────────────────────┬─────────────────┘
             │                        │                    │
             │ OTLP                   │ Prometheus         │ HTTP
             │ (10% traces)           │ Remote Write       │ (Logs)
             │                        │ (Metrics)          │
             ▼                        ▼                    ▼
┌───────────────────┐    ┌───────────────────┐    ┌──────────────────┐
│                   │    │                   │    │                  │
│   Grafana Tempo   │    │    Prometheus     │    │      Loki        │
│  (Port 3200)      │    │   (Port 9090)     │    │   (Port 3100)    │
│                   │    │                   │    │                  │
│ ┌───────────────┐ │    │ ┌───────────────┐ │    │ ┌──────────────┐ │
│ │  Distributor  │ │    │ │ Time Series   │ │    │ │ Log Streams  │ │
│ │  (Ingestion)  │ │    │ │   Database    │ │    │ │   Storage    │ │
│ └───────┬───────┘ │    │ └───────────────┘ │    │ └──────────────┘ │
│         │         │    │                   │    │                  │
│ ┌───────▼───────┐ │    │ ┌───────────────┐ │    │ ┌──────────────┐ │
│ │   Ingester    │ │    │ │  Scrape       │ │    │ │ Index        │ │
│ │  (WAL/Blocks) │ │    │ │  Targets      │ │    │ │ (Labels)     │ │
│ └───────┬───────┘ │    │ └───────────────┘ │    │ └──────────────┘ │
│         │         │    │                   │    │                  │
│ ┌───────▼───────┐ │    │ Stores:           │    │ Stores:          │
│ │   Querier     │ │    │ • Process metrics │    │ • App logs       │
│ │  (Search API) │ │    │ • Redis metrics   │    │ • Process logs   │
│ └───────┬───────┘ │    │ • Trace metrics   │    │ • System logs    │
│         │         │    │ • Runtime metrics │    │ (with trace IDs) │
│ ┌───────▼───────┐ │    │                   │    │                  │
│ │   Compactor   │ │    │ Features:         │    │ Features:        │
│ │  (Retention)  │ │    │ • Alerting        │    │ • LogQL queries  │
│ └───────────────┘ │    │ • Recording rules │    │ • Label filters  │
│                   │    │ • Service graph   │    │ • Trace linking  │
│ ┌───────────────┐ │    │   from traces     │    │                  │
│ │Metrics        │ │    │                   │    │                  │
│ │Generator      │─┼────▶ (receives trace-  │    │                  │
│ │(Trace→Metric) │ │    │  derived metrics) │    │                  │
│ └───────────────┘ │    │                   │    │                  │
│                   │    │                   │    │                  │
│ Storage:          │    │ Retention: 7 days │    │ Retention:       │
│ • Local (test)    │    │                   │    │  30 days         │
│ • S3 (prod)       │    │                   │    │                  │
│ • GCS (prod)      │    │                   │    │                  │
│ • Azure (prod)    │    │                   │    │                  │
│                   │    │                   │    │                  │
│ Retention:        │    │                   │    │                  │
│  24h (test)       │    │                   │    │                  │
│  7-30d (prod)     │    │                   │    │                  │
└────────┬──────────┘    └─────────┬─────────┘    └────────┬─────────┘
         │                         │                       │
         │                         │                       │
         │                         │                       │
┌────────┴────────────────────────┴───────────────────────┴──────────┐
│                                                                     │
│                             Redis                                   │
│                          (Port 6379)                                │
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │               Process State Storage                          │  │
│  │                                                              │  │
│  │  Keys (with TTL):                                           │  │
│  │  • HonuaProcessFramework:process:{id}                       │  │
│  │  • HonuaProcessFramework:step:{process-id}:{step-id}        │  │
│  │  • HonuaProcessFramework:lock:{process-id}                  │  │
│  │                                                              │  │
│  │  Features:                                                  │  │
│  │  • Append-only file (AOF)                                   │  │
│  │  • Persistence to disk                                      │  │
│  │  • Pub/Sub for events                                       │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────┬───────────────────────────────────────────┘
                          │
                          │
                          │ All backends feed into:
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                                                                     │
│                          Grafana                                    │
│                        (Port 3000)                                  │
│                    admin / admin (default)                          │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                    Data Sources                             │  │
│  │                                                             │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐     │  │
│  │  │  Prometheus  │  │     Loki     │  │    Tempo     │     │  │
│  │  │   (default)  │  │              │  │              │     │  │
│  │  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘     │  │
│  │         │                 │                 │             │  │
│  │         │ Exemplars       │ Trace IDs       │             │  │
│  │         └────────┬────────┴────────┬────────┘             │  │
│  │                  │                 │                      │  │
│  │            Correlation Enabled     │                      │  │
│  │                  │                 │                      │  │
│  └──────────────────┼─────────────────┼──────────────────────┘  │
│                     │                 │                         │
│  ┌──────────────────┼─────────────────┼──────────────────────┐  │
│  │                  │    Dashboards   │                      │  │
│  │                  │                 │                      │  │
│  │  ┌───────────────▼────────────┐  ┌▼──────────────────────┐ │
│  │  │ Process Framework Dashboard│  │ Distributed Tracing   │ │
│  │  │                            │  │ Dashboard             │ │
│  │  │ • Step execution rates     │  │ • Traces/second       │ │
│  │  │ • Active instances         │  │ • P95 latency         │ │
│  │  │ • Duration percentiles     │  │ • Error rate          │ │
│  │  │ • Success/error rates      │  │ • Recent traces       │ │
│  │  │ • Redis metrics            │  │ • Span durations      │ │
│  │  │ • CPU/Memory usage         │  │ • Request rates       │ │
│  │  │                            │  │ • Service graph       │ │
│  │  │ Links to: Traces, Logs     │  │ • Operations table    │ │
│  │  └────────────────────────────┘  └───────────────────────┘ │
│  │                                                             │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │                      Explore Views                          │  │
│  │                                                             │  │
│  │  • Prometheus: Query metrics, click exemplars → traces     │  │
│  │  • Loki: Query logs, click trace IDs → traces              │  │
│  │  • Tempo: TraceQL queries, view full traces                │  │
│  │                                                             │  │
│  │  Navigation Flow:                                           │  │
│  │  Metrics → Exemplar → Trace → Logs → Related Metrics       │  │
│  │                                                             │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘


                        ┌─────────────────────────┐
                        │   User/Developer        │
                        │   (Browser)             │
                        └─────────────────────────┘
                                    │
                                    │ HTTP/WebSocket
                                    │
                                    ▼
                         Access all services via:
                         • http://localhost:3000 (Grafana)
                         • http://localhost:9090 (Prometheus)
                         • http://localhost:3200 (Tempo)
                         • http://localhost:3100 (Loki)
```

## Data Flow Details

### 1. Trace Flow (with Sampling)

```
Application
  ↓ (100% of traces)
OpenTelemetry SDK
  ↓ (OTLP)
OTel Collector
  ↓
  ├─ Probabilistic Sampler (keeps 10%)
  │
  ├─ (100%) → Metrics Generator → Prometheus
  │            (traces_spanmetrics_*, traces_service_graph_*)
  │
  └─ (10%) → Tempo
              ↓
         Storage (S3/GCS/Azure or Local)
```

**Why this works:**
- All traces generate metrics (100% coverage)
- Only 10% stored for detailed inspection
- Reduces storage costs by 90%
- All errors/slow traces still generate metrics

### 2. Metrics Flow

```
Application
  ↓ (Process metrics)
OpenTelemetry SDK
  ↓ (OTLP)
OTel Collector
  ↓ (Prometheus exporter)
Prometheus
  ↓
  ├─ Time series storage
  ├─ Recording rules
  └─ Alerting rules
```

### 3. Logs Flow

```
Application
  ↓ (Structured logs with trace IDs)
OpenTelemetry SDK
  ↓ (OTLP)
OTel Collector
  ↓ (Loki exporter)
Loki
  ↓
  ├─ Stream-based storage
  ├─ Label indexing
  └─ Trace ID extraction
```

### 4. Correlation Flow

```
User clicks metric exemplar in Grafana
  ↓
Grafana queries Tempo for trace ID
  ↓
Tempo returns full trace
  ↓
User clicks "Logs for this trace"
  ↓
Grafana queries Loki with trace ID filter
  ↓
Shows all logs for that specific request
  ↓
User clicks related metrics
  ↓
Shows metric panels for that service/operation
```

## Network Topology

```
┌────────────────────────────────────────────────────────────┐
│  Docker Network: honua-process-testing (bridge)            │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │    Redis     │  │ OTel-Collector│  │  Prometheus  │    │
│  │  :6379       │  │ :4317/:4318   │  │   :9090      │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │     Loki     │  │    Tempo     │  │   Grafana    │    │
│  │   :3100      │  │   :3200      │  │    :3000     │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│                                                            │
└────────────────────────────────────────────────────────────┘
                           │
                           │ Port forwarding to host
                           ▼
                    ┌──────────────┐
                    │  Host System │
                    │  (localhost) │
                    └──────────────┘
```

## Storage Architecture

### Development/Testing

```
Docker Volumes:
├─ redis-data/          (Process state)
├─ prometheus-data/     (Metrics time series)
├─ loki-data/          (Log streams)
├─ tempo-data/         (Traces - local blocks)
└─ grafana-data/       (Dashboards, users, settings)
```

### Production

```
Cloud Storage:
├─ Redis: AWS ElastiCache / Azure Cache / GCP Memorystore
├─ Prometheus: Remote storage (Thanos, Cortex, Mimir)
├─ Loki: S3/GCS/Azure Blob
├─ Tempo: S3/GCS/Azure Blob
└─ Grafana: Database backend (PostgreSQL)
```

## Health Check Flow

```bash
./scripts/verify-health.sh
  │
  ├─ Check Docker containers (6)
  │  ├─ Redis: healthy
  │  ├─ OTel Collector: healthy
  │  ├─ Prometheus: healthy
  │  ├─ Grafana: healthy
  │  ├─ Loki: healthy
  │  └─ Tempo: healthy
  │
  ├─ Check TCP endpoints (7)
  │  ├─ Redis :6379
  │  ├─ OTLP gRPC :4317
  │  ├─ OTLP HTTP :4318
  │  ├─ Tempo gRPC :4317
  │  └─ Tempo HTTP :4318
  │
  ├─ Check HTTP endpoints (6)
  │  ├─ OTel health :13133
  │  ├─ Prometheus :9090/-/healthy
  │  ├─ Prometheus targets :9090/api/v1/targets
  │  ├─ Loki :3100/ready
  │  ├─ Tempo :3200/ready
  │  └─ Grafana :3000/api/health
  │
  └─ Check Grafana datasources
     ├─ Prometheus: configured
     ├─ Loki: configured
     └─ Tempo: configured
```

Result: **17/17 checks passed**

## Component Responsibilities

| Component | Responsibility | Scaling Strategy |
|-----------|---------------|------------------|
| **Application** | Business logic, instrumentation | Horizontal (multiple instances) |
| **Redis** | State storage, pub/sub | Sentinel/Cluster for HA |
| **OTel Collector** | Telemetry pipeline, sampling | Horizontal (stateless) |
| **Prometheus** | Metrics storage, querying | Federation/Remote storage |
| **Loki** | Log aggregation, indexing | Horizontal (microservices mode) |
| **Tempo** | Trace storage, querying | Horizontal (microservices mode) |
| **Grafana** | Visualization, correlation | Multiple instances behind LB |

## Summary

This architecture provides:

✅ **Complete Observability**: Metrics, Logs, Traces
✅ **Intelligent Sampling**: 10% trace storage, 100% metrics coverage
✅ **Full Correlation**: Click from any signal to any other
✅ **Cost Efficient**: Object storage, sampling, retention policies
✅ **Scalable**: Horizontal scaling for all components
✅ **Production Ready**: Cloud-native backends supported
✅ **Developer Friendly**: Single command to start, rich dashboards
