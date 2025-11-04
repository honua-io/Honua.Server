# Honua Observability

Comprehensive monitoring, metrics, alerting, distributed tracing, and performance tracking for the Honua geospatial server.

## Overview

Honua uses OpenTelemetry for metrics collection and distributed tracing, with Prometheus for storage and Grafana for visualization. This observability stack provides:

- **Real-time monitoring** of API performance, resource usage, and errors
- **Distributed tracing** for request flow analysis across services
- **Proactive alerting** for SLO breaches, resource exhaustion, and system failures
- **Performance baselines** for capacity planning and optimization
- **Pre-configured dashboards** for operations, development, and business metrics

## Components

### Metrics Collection
- **OpenTelemetry SDK**: Native .NET instrumentation
- **Custom Meters** (8 total, 74+ metrics):
  - `Honua.Server.Api` - API performance metrics
  - `Honua.Server.Database` - Database operation metrics
  - `Honua.Server.Cache` - Cache performance metrics
  - `Honua.Server.VectorTiles` - Vector tile generation metrics
  - `Honua.Server.RasterCache` - Raster tile cache metrics
  - `Honua.Server.Security` - Authentication and authorization metrics
  - `Honua.Server.Business` - Business-level KPIs
  - `Honua.Server.Infrastructure` - Infrastructure health metrics
- **Prometheus Export**: `/metrics` endpoint in Prometheus exposition format
- **Comprehensive Coverage**: Application, database, cache, tiles, security, business, and infrastructure metrics
- **Detailed Documentation**: See [METRICS.md](./METRICS.md) for complete reference

### Distributed Tracing
- **OpenTelemetry Tracing**: W3C trace context propagation
- **Activity Sources**: 9 dedicated sources for different subsystems
  - `Honua.Server.OgcProtocols` - WMS, WFS, WMTS, WCS, CSW operations
  - `Honua.Server.OData` - OData query operations
  - `Honua.Server.Stac` - STAC catalog operations
  - `Honua.Server.Database` - Database queries
  - `Honua.Server.RasterTiles` - Raster tile rendering and caching
  - `Honua.Server.Metadata` - Metadata operations
  - `Honua.Server.Authentication` - Auth operations
  - `Honua.Server.Export` - Data export
  - `Honua.Server.Import` - Data import
- **Trace Exporters**: Console, OTLP (Jaeger, Tempo, Azure, AWS, GCP)
- **Runtime Configuration**: Admin API for dynamic tracing setup

### Storage & Querying
- **Prometheus**: Time-series database with 30-day retention
- **Recording Rules**: Pre-aggregated queries for dashboard performance
- **Alert Rules**: 40+ alerts across availability, performance, and resources

### Visualization
- **Grafana**: Four pre-configured dashboards
  - Honua Overview (operations view)
  - Honua Detailed (comprehensive monitoring)
  - Honua SLO (service level tracking)
  - Honua Metrics (comprehensive metrics dashboard - 30 panels)

### Alerting
- **Alertmanager**: Alert routing and deduplication
- **Notification Channels**: Slack, PagerDuty, email, webhooks
- **Severity Levels**: Critical, Warning, Info

## Quick Start

### Deploy Monitoring Stack
```bash
# Start Honua with Prometheus and Grafana
cd docker
docker-compose -f docker-compose.yml -f docker-compose.prometheus.yml up -d

# Verify metrics endpoint
curl http://localhost:8080/metrics

# Access Grafana
open http://localhost:3000  # Login: admin / admin
```

### View Dashboards
1. Navigate to Grafana: http://localhost:3000
2. Browse to **Dashboards** → **Honua**
3. Select dashboard:
   - **Overview** for high-level metrics
   - **Detailed** for deep-dive analysis
   - **SLO** for service level tracking

### Check Alerts
```bash
# View active alerts in Prometheus
open http://localhost:9090/alerts

# View Alertmanager
open http://localhost:9093
```

## Key Metrics

**Total Metrics**: 74+ metrics across 8 categories

For complete metric reference, see [METRICS.md](./METRICS.md)

### API Performance (4 metrics)
- `honua_api_requests` - Request count by protocol, service, layer
- `honua_api_request_duration` - Request latency histogram
- `honua_api_errors` - Error count by type and category
- `honua_api_features_returned` - Features served

### Database Operations (9 metrics)
- `honua_database_queries` - Query count by type and table
- `honua_database_query_duration` - Query execution time
- `honua_database_slow_queries` - Slow query tracking (>1s)
- `honua_database_connection_wait_time` - Connection pool wait time
- `honua_database_connection_errors` - Connection errors
- `honua_database_transaction_commits` - Committed transactions
- `honua_database_transaction_rollbacks` - Rolled back transactions

### Cache Performance (7 metrics)
- `honua_cache_hits` - Cache hits by cache name
- `honua_cache_misses` - Cache misses by cache name
- `honua_cache_operation_duration` - Cache operation latency
- `honua_cache_evictions` - Evictions by reason
- `honua_cache_writes` - Cache write operations
- `honua_cache_errors` - Cache operation errors
- `honua_cache_write_size` - Size of cached data

### Raster Tiles (8 metrics)
- `honua_raster_cache_hits` - Cache hit count by dataset
- `honua_raster_cache_misses` - Cache miss count by dataset
- `honua_raster_render_latency_ms` - Tile render time histogram
- `honua_raster_preseed_jobs_completed` - Completed preseed jobs
- `honua_raster_preseed_jobs_failed` - Failed preseed jobs
- `honua_raster_preseed_jobs_cancelled` - Cancelled preseed jobs
- `honua_raster_cache_purges_succeeded` - Successful cache purges
- `honua_raster_cache_purges_failed` - Failed cache purges

### Vector Tiles (10 metrics)
- `honua_vectortile_tiles_generated` - Tiles generated by zoom
- `honua_vectortile_tiles_served` - Tiles served to clients
- `honua_vectortile_generation_duration` - Generation time
- `honua_vectortile_features_per_tile` - Feature count per tile
- `honua_vectortile_simplifications` - Geometry simplification operations
- `honua_vectortile_simplification_ratio` - Simplification efficiency
- `honua_vectortile_preseed_jobs_started` - Preseed jobs initiated
- `honua_vectortile_preseed_jobs_completed` - Preseed jobs finished
- `honua_vectortile_preseed_jobs_failed` - Preseed job failures
- `honua_vectortile_errors` - Vector tile errors

### Security & Authentication (10 metrics)
- `honua_security_login_attempts` - Login attempts by method
- `honua_security_login_failures` - Failed logins by reason
- `honua_security_token_validations` - Token validation operations
- `honua_security_token_refreshes` - Token refresh operations
- `honua_security_authorization_checks` - Authorization checks
- `honua_security_authorization_denials` - Denied requests
- `honua_security_sessions_created` - New sessions
- `honua_security_sessions_terminated` - Ended sessions
- `honua_security_events` - Security events by severity
- `honua_security_api_key_usage` - API key usage tracking

### Business Metrics (13 metrics)
- `honua_business_features_served` - Features delivered to clients
- `honua_business_raster_tiles_served` - Raster tiles served
- `honua_business_vector_tiles_served` - Vector tiles served
- `honua_business_data_ingestions` - Data ingestion operations
- `honua_business_stac_searches` - STAC catalog searches
- `honua_business_stac_catalog_access` - STAC item access
- `honua_business_exports` - Data export operations
- `honua_business_active_sessions` - Currently active sessions (gauge)
- `honua_business_dataset_accesses` - Dataset access operations
- `honua_business_ingestion_duration` - Ingestion time
- `honua_business_export_duration` - Export time
- `honua_business_export_size` - Export data size
- `honua_business_session_duration` - Session duration

### Infrastructure Health (13 metrics)
- `honua_infrastructure_memory_working_set` - Working set memory
- `honua_infrastructure_memory_gc_heap` - GC heap size
- `honua_infrastructure_memory_private_bytes` - Private memory
- `honua_infrastructure_gc_collections` - GC count by generation
- `honua_infrastructure_gc_duration` - GC pause time
- `honua_infrastructure_gc_freed_bytes` - Freed memory
- `honua_infrastructure_threadpool_worker_threads` - Available workers
- `honua_infrastructure_threadpool_io_threads` - Available I/O threads
- `honua_infrastructure_threadpool_queue_length` - Queued items
- `honua_infrastructure_thread_count` - Total threads
- `honua_infrastructure_cpu_usage_percent` - CPU utilization
- `honua_infrastructure_threadpool_max_threads` - Max thread pool size
- `honua_infrastructure_threadpool_min_threads` - Min thread pool size

## Service Level Objectives (SLOs)

### Availability SLO
- **Target**: 99.9% (43.2 minutes downtime per month)
- **Measurement**: Success rate over 30-day rolling window
- **Alert**: Fires when availability < 99.9% for 5 minutes

### Latency SLO
- **Target**: P95 < 2000ms
- **Measurement**: 95th percentile latency over 30-day window
- **Warning**: P95 > 2000ms for 10 minutes
- **Critical**: P95 > 5000ms for 5 minutes

### Cache Performance
- **Target**: 70% hit rate for raster tiles
- **Measurement**: Ratio of hits to total operations
- **Alert**: Hit rate < 70% for 15 minutes

## Alert Groups

### P0 - Critical (Page Immediately)
- Service down
- Availability SLO breach
- Critical latency (P95 > 5000ms)
- Critical memory usage (> 12 GB)
- Out of memory errors
- Database connection failures
- Storage failures

### P1 - High (Alert During Business Hours)
- High error rate (> 5%)
- High latency (P95 > 2000ms)
- High memory usage (> 8 GB)
- High CPU usage (> 80%)
- Low cache hit rate (< 70%)
- Database query slowness

### P2 - Warning (Review Daily)
- Elevated error rate (> 1%)
- Elevated latency (P95 > 1000ms)
- Thread pool growth
- GC pressure
- Preseed job failures
- Low traffic alerts

## Recording Rules

Pre-aggregated metrics for efficient dashboard queries:

### Rate Metrics (5-minute windows)
- `honua:api_requests:rate5m` - Request rate by protocol
- `honua:api_errors:rate5m` - Error rate by type
- `honua:api_errors:ratio5m` - Error percentage

### Latency Percentiles
- `honua:api_latency:p50` - Median latency
- `honua:api_latency:p95` - 95th percentile
- `honua:api_latency:p99` - 99th percentile

### Cache Metrics
- `honua:raster_cache:hit_rate5m` - Cache hit rate
- `honua:raster_render:p95` - Render latency P95

### SLO Calculations
- `honua:slo:availability:30d` - 30-day availability
- `honua:slo:latency_p95:30d` - 30-day P95 latency
- `honua:slo:error_budget_consumed:30d` - Error budget burn rate

## Performance Baselines

See [performance-baselines.md](./performance-baselines.md) for detailed expectations.

### Quick Reference
| Metric | Baseline | Warning | Critical |
|--------|----------|---------|----------|
| **Availability** | 99.9% | < 99.5% | < 99% |
| **P95 Latency** | < 1000ms | > 2000ms | > 5000ms |
| **Error Rate** | < 0.5% | > 1% | > 5% |
| **Memory Usage** | 2-4 GB | > 8 GB | > 12 GB |
| **CPU Usage** | 10-30% | > 80% | > 95% |
| **Cache Hit Rate** | > 85% | < 70% | < 50% |

### Protocol-Specific Baselines
| Protocol | P50 | P95 | P99 |
|----------|-----|-----|-----|
| **WFS** | 100-300ms | 500-1000ms | 1000-2000ms |
| **WMS** | 200-500ms | 1000-2000ms | 2000-4000ms |
| **WMTS** | 10-50ms* | 100ms* | 200ms* |
| **OGC API Features** | 150-400ms | 600-1200ms | 1200-2500ms |
| **STAC** | 50-200ms | 300-600ms | 600-1000ms |

*WMTS cached performance. Uncached adds 200-500ms.

## Configuration

### Enable Metrics
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

### Enable Distributed Tracing
```json
{
  "observability": {
    "tracing": {
      "exporter": "otlp",
      "otlpEndpoint": "http://jaeger:4317",
      "samplingRatio": 0.1
    }
  }
}
```

**Exporter Options:**
- `none` - Tracing disabled (default)
- `console` - Log traces to stdout (development)
- `otlp` - Export to OTLP collector (Jaeger, Tempo, etc.)

**Runtime Configuration:**
Tracing can be configured at runtime via the Admin API (requires restart for exporter changes):
```bash
# Get current tracing configuration
GET /admin/observability/tracing

# Update exporter type
PATCH /admin/observability/tracing/exporter
Content-Type: application/json
{ "exporter": "otlp" }

# Update OTLP endpoint
PATCH /admin/observability/tracing/endpoint
Content-Type: application/json
{ "endpoint": "http://jaeger:4317" }

# Update sampling ratio (takes effect immediately, no restart)
PATCH /admin/observability/tracing/sampling
Content-Type: application/json
{ "ratio": 0.1 }

# Test tracing with a sample trace
POST /admin/observability/tracing/test

# Get platform-specific setup guidance
GET /admin/observability/tracing/platforms
```

### Prometheus Scrape Configuration
```yaml
scrape_configs:
  - job_name: honua
    scrape_interval: 10s
    scrape_timeout: 5s
    static_configs:
      - targets:
          - honua-server:8080
```

### Grafana Datasource
```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    url: http://prometheus:9090
    isDefault: true
```

### Jaeger (Distributed Tracing)
```bash
# Deploy Jaeger all-in-one with OTLP support
docker run -d \
  --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest

# Configure Honua to use Jaeger
# Set in appsettings.json or use Admin API
observability__tracing__exporter=otlp
observability__tracing__otlpEndpoint=http://jaeger:4317

# View traces at http://localhost:16686
```

## Troubleshooting

### No Metrics in Prometheus

**Symptom**: Prometheus shows no data for Honua

**Check**:
1. Metrics endpoint accessible: `curl http://localhost:8080/metrics`
2. Prometheus scrape status: http://localhost:9090/targets
3. Metrics enabled in configuration

**Solution**:
```bash
# Verify metrics configuration
docker exec honua-web cat /app/appsettings.json | grep -A 5 observability

# Check logs for errors
docker logs honua-web | grep -i metric
```

### High Memory Usage Alert

**Symptom**: Alert fires for high memory usage

**Investigation**:
```promql
# Current memory usage
process_resident_memory_bytes{job="honua"} / (1024*1024*1024)

# Memory growth rate
rate(process_resident_memory_bytes{job="honua"}[1h])

# GC heap size
dotnet_gc_heap_size_bytes{job="honua"}
```

**Common Causes**:
- Large raster datasets in cache
- Memory leak in query processing
- Insufficient GC pressure
- Too many concurrent requests

**Solutions**:
- Reduce cache size: `HONUA_RASTER_CACHE_SIZE_MB=512`
- Enable aggressive GC: `COMPlus_gcServer=0`
- Increase rate limiting: `HONUA_RATELIMITING_DEFAULT_LIMIT=50`

### No Traces in Jaeger/Tempo

**Symptom**: Trace backend shows no traces from Honua

**Check**:
1. Exporter is set correctly: `GET /admin/observability/tracing`
2. OTLP endpoint is reachable: `curl http://jaeger:4317`
3. Sampling ratio > 0
4. Trace backend is running and accepting traces

**Solution**:
```bash
# Verify tracing configuration
curl http://localhost:8080/admin/observability/tracing

# Test with a sample trace
curl -X POST http://localhost:8080/admin/observability/tracing/test

# Check Honua logs for tracing errors
docker logs honua-web | grep -i trace

# Verify Jaeger is receiving traces
curl http://localhost:16686/api/services

# Set exporter via API
curl -X PATCH http://localhost:8080/admin/observability/tracing/exporter \
  -H "Content-Type: application/json" \
  -d '{"exporter": "otlp"}'

# Set endpoint via API
curl -X PATCH http://localhost:8080/admin/observability/tracing/endpoint \
  -H "Content-Type: application/json" \
  -d '{"endpoint": "http://jaeger:4317"}'

# Restart Honua for exporter changes to take effect
docker restart honua-web
```

**Common Causes**:
- Exporter set to "none" (tracing disabled)
- OTLP endpoint unreachable or incorrect
- Sampling ratio set to 0
- Trace backend not accepting OTLP gRPC connections
- Firewall blocking port 4317

### Grafana Dashboard Shows "No Data"

**Symptom**: Dashboard panels empty or showing "No data"

**Check**:
1. Prometheus datasource connected
2. Time range includes data
3. Metrics exist in Prometheus

**Solution**:
```bash
# Test Prometheus query
curl 'http://localhost:9090/api/v1/query?query=honua_api_requests_total'

# Check time range
# In Grafana: Use "Last 1 hour" instead of "Last 24 hours"

# Verify datasource
# Grafana → Configuration → Data Sources → Test
```

## Best Practices

### Dashboard Organization
- **Overview Dashboard**: For NOCs, status displays, and quick health checks
- **Detailed Dashboard**: For on-call engineers investigating issues
- **SLO Dashboard**: For stakeholders and monthly reviews

### Alert Design
- **Use recording rules** to reduce query load
- **Set appropriate for durations** to avoid flapping
- **Include runbook links** in alert annotations
- **Test alerts regularly** with chaos engineering

### Performance Tuning
- **Monitor recording rule evaluation time** (`prometheus_rule_evaluation_duration_seconds`)
- **Use metric relabeling** to drop high-cardinality labels
- **Aggregate before alerting** (use `sum()`, `avg()` instead of per-instance alerts)
- **Set appropriate scrape intervals** (10s for application, 30s for infrastructure)

### Capacity Planning
- **Review SLO trends monthly** to identify degradation
- **Monitor error budget burn rate** for early warning
- **Track resource growth** (memory, CPU, disk) over time
- **Load test before scaling** to validate baselines

## Metrics Verification

Use the metrics verification script to test the observability setup:

```bash
# Test local instance
./scripts/verify-metrics.sh

# Test remote instance
./scripts/verify-metrics.sh http://honua-server:5000
```

The script will:
- Verify metrics endpoint accessibility
- Check all 74+ metrics are exposed
- Validate Prometheus format
- Check for high cardinality issues
- Display sample metric values

## Related Documentation

- **[Comprehensive Metrics Reference](./METRICS.md)** - Complete catalog of all 74+ metrics
- [Performance Baselines](./performance-baselines.md) - Detailed SLOs and expectations
- [Managed Services Guide](./managed-services-guide.md) - Cloud platform integration
- [Prometheus Configuration](../../docker/prometheus/prometheus.yml)
- [Alert Rules](../../docker/prometheus/alerts/honua-alerts.yml)
- [Recording Rules](../../docker/prometheus/alerts/recording-rules.yml)
- [Grafana Dashboards](../../docker/grafana/dashboards/)
- [Monitoring Guide](../../docker/MONITORING.md)

## Support

For questions or issues with observability:

1. Check [Troubleshooting](#troubleshooting) section
2. Review [Prometheus documentation](https://prometheus.io/docs/)
3. Review [Grafana documentation](https://grafana.com/docs/)
4. Open an issue on GitHub with:
   - Dashboard/alert screenshots
   - Relevant Prometheus queries
   - Logs from Honua and Prometheus
