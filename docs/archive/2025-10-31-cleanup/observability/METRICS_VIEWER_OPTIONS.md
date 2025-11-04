# Metrics Viewer Options for Honua Server

## Overview

Honua Server exposes Prometheus-formatted metrics via the `/metrics` endpoint (when observability is enabled). This document outlines options for viewing and visualizing these metrics, ranging from lightweight development tools to full-featured production dashboards.

## Built-in Metrics Available

Honua Server exposes the following custom metrics:

### API Metrics (Meter: `Honua.Server.Api`)

- **`honua.api.requests`** - Total number of API requests by protocol, service, and layer
  - Tags: `api.protocol`, `service.id`, `layer.id`
  - Protocols: `wfs`, `wms`, `wmts`, `csw`, `wcs`, `ogc-api-features`, `ogc-api-tiles`, `stac`, `esri-rest`, `odata`, `carto`

- **`honua.api.request_duration`** - Request duration in milliseconds
  - Tags: `api.protocol`, `service.id`, `layer.id`, `http.status_code`

- **`honua.api.errors`** - Total number of API errors
  - Tags: `api.protocol`, `service.id`, `layer.id`, `error.type`

- **`honua.api.features_returned`** - Total number of features returned in responses
  - Tags: `api.protocol`, `service.id`, `layer.id`

### Raster Tile Cache Metrics (Meter: `Honua.Server.RasterCache`)

- **`honua.raster.cache.hits`** - Cache hit count
- **`honua.raster.cache.misses`** - Cache miss count
- **`honua.raster.cache.evictions`** - Cache eviction count
- **`honua.raster.cache.size_bytes`** - Current cache size in bytes

### ASP.NET Core Built-in Metrics

- HTTP request metrics (request count, duration, active requests)
- .NET runtime metrics (GC, thread pool, exception counts)

## Viewer Options

### 1. .NET Aspire Dashboard (Recommended for Development)

**Best for**: Local development, debugging, and quick visualization without infrastructure setup

The .NET Aspire Dashboard is a lightweight, standalone OpenTelemetry viewer built specifically for .NET applications. It can be used independently without the full Aspire orchestration stack.

#### Features
- ✅ Zero setup - runs as a single Docker container
- ✅ Native OpenTelemetry support
- ✅ Visualizes metrics, traces, and structured logs
- ✅ In-memory storage (no persistence required)
- ✅ Modern UI with filtering and search
- ✅ Completely free and open-source

#### How to Run

```bash
docker run --rm -it \
  -p 18888:18888 \
  -p 4317:18889 \
  --name aspire-dashboard \
  mcr.microsoft.com/dotnet/aspire-dashboard:9.5
```

Then configure Honua to send metrics to the Aspire dashboard by updating `appsettings.json`:

```json
{
  "observability": {
    "metrics": {
      "enabled": true,
      "usePrometheus": false,
      "openTelemetry": {
        "endpoint": "http://localhost:4317"
      }
    }
  }
}
```

Access the dashboard at: `http://localhost:18888`

#### Limitations
- In-memory only (data lost on restart)
- Not suitable for production monitoring
- Limited historical data retention

---

### 2. Prometheus Built-in Expression Browser

**Best for**: Quick ad-hoc queries, debugging individual metrics, zero dependencies

Prometheus includes a simple built-in web UI for querying and graphing metrics directly.

#### Features
- ✅ No additional installation required
- ✅ Accessible at `/graph` endpoint on Prometheus server
- ✅ Supports PromQL queries
- ✅ Simple time-series graphing
- ❌ Limited visualization options
- ❌ No dashboarding capabilities

#### How to Use

1. Run Prometheus pointing to Honua's `/metrics` endpoint
2. Navigate to `http://localhost:9090/graph`
3. Enter PromQL queries like:
   - `honua_api_requests{api_protocol="wfs"}`
   - `rate(honua_api_request_duration_sum[5m])`
   - `sum by (service_id) (honua_api_features_returned)`

#### Example Prometheus Configuration

```yaml
# prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'honua-server'
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

---

### 3. Grafana (Recommended for Production)

**Best for**: Production monitoring, advanced dashboards, alerting, multi-user teams

Grafana is the industry-standard open-source visualization platform for Prometheus metrics.

#### Features
- ✅ Rich visualization library (graphs, tables, heatmaps, geomaps)
- ✅ Advanced dashboarding with templates and variables
- ✅ Alerting and notification system
- ✅ Multi-user support with RBAC
- ✅ Data persistence and historical analysis
- ✅ Plugin ecosystem
- ❌ Requires separate infrastructure (Grafana + Prometheus)

#### Quick Start with Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
      - prometheus-data:/prometheus

  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
    depends_on:
      - prometheus

volumes:
  prometheus-data:
  grafana-data:
```

#### Setting Up Honua Dashboards

1. Access Grafana at `http://localhost:3000` (admin/admin)
2. Add Prometheus data source: Configuration → Data Sources → Add Prometheus
   - URL: `http://prometheus:9090`
3. Import or create dashboards with Honua metrics
4. Use variables for dynamic filtering by service, protocol, layer

#### Example Dashboard Panels

**API Requests by Protocol (Pie Chart)**
```promql
sum by (api_protocol) (honua_api_requests)
```

**Request Rate by Service (Time Series)**
```promql
sum by (service_id) (rate(honua_api_requests[5m]))
```

**Average Request Duration (Gauge)**
```promql
rate(honua_api_request_duration_sum[5m]) / rate(honua_api_request_duration_count[5m])
```

**Features Served (Counter)**
```promql
sum(honua_api_features_returned)
```

**Cache Hit Ratio (Gauge)**
```promql
honua_raster_cache_hits / (honua_raster_cache_hits + honua_raster_cache_misses)
```

---

### 4. VictoriaMetrics + vmui (Lightweight Alternative)

**Best for**: Resource-constrained environments, long-term storage, Prometheus-compatible alternative

VictoriaMetrics is a Prometheus-compatible time-series database with lower resource requirements and includes a built-in UI (vmui).

#### Features
- ✅ Prometheus-compatible (drop-in replacement)
- ✅ Lower memory and CPU usage than Prometheus
- ✅ Built-in UI (vmui) for basic visualization
- ✅ Better compression and long-term storage
- ✅ PromQL support
- ❌ UI less feature-rich than Grafana

#### Quick Start

```bash
docker run -d \
  -p 8428:8428 \
  -v victoria-metrics-data:/victoria-metrics-data \
  --name victoria-metrics \
  victoriametrics/victoria-metrics
```

Update `prometheus.yml` to use VictoriaMetrics as remote write:

```yaml
remote_write:
  - url: http://localhost:8428/api/v1/write
```

Access vmui at: `http://localhost:8428/vmui`

---

## Recommendation Matrix

| Scenario | Recommended Solution | Why |
|----------|---------------------|-----|
| **Local Development** | .NET Aspire Dashboard | Zero-config, .NET-native, all telemetry in one place |
| **Quick Metric Check** | Prometheus Expression Browser | No additional software, PromQL console |
| **Production Monitoring** | Grafana + Prometheus | Industry standard, full feature set, alerting |
| **Resource-Constrained** | VictoriaMetrics + vmui | Lower overhead, Prometheus-compatible |
| **Embedded in App** | Custom Blazor/Razor dashboard | Full control, no external dependencies (requires development) |

---

## Enabling Metrics in Honua

Metrics are controlled via `appsettings.json`:

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

- `enabled`: Master switch for metrics collection
- `usePrometheus`: Expose Prometheus scraping endpoint
- `endpoint`: Path for Prometheus scraper (default: `/metrics`)

The `/metrics` endpoint requires authentication unless QuickStart mode is active:
- Requires `RequireViewer` policy (administrator, datapublisher, or viewer role)

---

## Custom Dashboard Development (Future Option)

If you decide to ship a built-in metrics viewer, consider:

### Option A: Embedded Blazor Dashboard
- Create a Razor Pages or Blazor Server dashboard
- Query metrics directly from `IApiMetrics` service
- No external dependencies
- Control over UI/UX
- Deployment complexity (part of main app)

### Option B: Separate Metrics Service
- Standalone ASP.NET Core app
- Reads from Prometheus or directly from Honua's OpenTelemetry
- Can be deployed independently
- Easier to scale and update separately

### Option C: Bundle Aspire Dashboard
- Package the Aspire Dashboard container with Honua
- Use Docker Compose or Kubernetes sidecar pattern
- Minimal development effort
- Leverages Microsoft's maintained UI

---

## Security Considerations

When exposing metrics:

1. **Authentication**: Metrics endpoint should require authentication in production
2. **Sensitive Data**: Ensure metrics don't leak sensitive layer names or service IDs
3. **Network Isolation**: Run Prometheus/Grafana in the same network as Honua
4. **Rate Limiting**: Metrics scraping can be resource-intensive
5. **Access Control**: Restrict viewer access to appropriate roles

Current Honua configuration:
- `/metrics` endpoint requires `RequireViewer` authorization policy (except in QuickStart mode)
- No sensitive data exposed in metric labels
- Service and layer IDs are considered non-sensitive operational metadata

---

## Example Queries for Monitoring

### Most Active Services
```promql
topk(10, sum by (service_id) (honua_api_requests))
```

### Slowest API Protocols
```promql
sort_desc(
  rate(honua_api_request_duration_sum[5m]) / rate(honua_api_request_duration_count[5m])
)
```

### Error Rate by Protocol
```promql
sum by (api_protocol) (rate(honua_api_errors[5m]))
```

### Cache Efficiency
```promql
(
  honua_raster_cache_hits /
  (honua_raster_cache_hits + honua_raster_cache_misses)
) * 100
```

### Data Served (Features per Minute)
```promql
sum(rate(honua_api_features_returned[1m]))
```

---

## Next Steps

1. **Choose your viewer**: Start with Aspire Dashboard for development, Grafana for production
2. **Enable metrics**: Set `observability.metrics.enabled: true` in `appsettings.json`
3. **Configure scraping**: Point Prometheus at Honua's `/metrics` endpoint
4. **Build dashboards**: Create visualizations for your key metrics
5. **Set alerts**: Define thresholds for error rates, latency, cache misses
6. **Monitor**: Regularly review dashboards and adjust as needed

---

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)
- [.NET Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/overview)
- [VictoriaMetrics Documentation](https://docs.victoriametrics.com/)
