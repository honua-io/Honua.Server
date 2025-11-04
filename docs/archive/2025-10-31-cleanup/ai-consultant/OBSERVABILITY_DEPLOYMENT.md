# Observability Deployment Topologies

## Overview

The Honua AI Consultant now supports automatic deployment configuration generation for observability stacks, including Prometheus, Grafana, VictoriaMetrics, and .NET Aspire Dashboard. This allows you to request complete monitoring infrastructure alongside your Honua Server deployment.

## Supported Observability Stacks

### 1. Prometheus + Grafana (Production)

**Best for**: Production environments, comprehensive monitoring, long-term metrics storage

**Components**:
- **Prometheus**: Metrics collection and storage
- **Grafana**: Visualization and dashboarding

**Ports**:
- Prometheus: `9090`
- Grafana: `3000` (default credentials: admin/admin)

**Example Request**:
```
Generate a Docker Compose deployment with PostGIS, Redis, and Prometheus with Grafana for production monitoring
```

**Generated Services**:
```yaml
services:
  honua:
    # ... Honua Server config
    environment:
      - OBSERVABILITY__METRICS__ENABLED=true
      - OBSERVABILITY__METRICS__USEPROMETHEUS=true

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
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
```

**Required Configuration File**: `prometheus.yml`
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'honua-server'
    static_configs:
      - targets: ['honua:8080']
    metrics_path: '/metrics'
```

---

### 2. .NET Aspire Dashboard (Development)

**Best for**: Local development, debugging, quick visualization

**Components**:
- **Aspire Dashboard**: Lightweight OpenTelemetry viewer with metrics, traces, and logs

**Ports**:
- Dashboard UI: `18888`
- OTLP Endpoint: `18889`

**Example Request**:
```
Generate a Docker Compose deployment for development with Aspire Dashboard for metrics
```

**Generated Services**:
```yaml
services:
  honua:
    # ... Honua Server config
    environment:
      - OBSERVABILITY__METRICS__ENABLED=true
      - OBSERVABILITY__METRICS__OPENTELEMETRY__ENDPOINT=http://aspire-dashboard:18889

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.5
    ports:
      - "18888:18888"
      - "18889:18889"
    environment:
      - DOTNET_DASHBOARD_OTLP_ENDPOINT_URL=http://0.0.0.0:18889
      - DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true
```

**Access**: Navigate to `http://localhost:18888` to view metrics, traces, and logs

**Features**:
- ✅ Zero configuration required
- ✅ Real-time metrics visualization
- ✅ Trace and log correlation
- ✅ In-memory only (no persistence)
- ✅ Perfect for development/testing

---

### 3. VictoriaMetrics (Lightweight)

**Best for**: Resource-constrained environments, Prometheus-compatible alternative

**Components**:
- **VictoriaMetrics**: Lightweight time-series database with built-in UI (vmui)

**Ports**:
- VictoriaMetrics: `8428`

**Example Request**:
```
Generate a Docker Compose deployment with VictoriaMetrics for lightweight monitoring
```

**Generated Services**:
```yaml
services:
  honua:
    # ... Honua Server config
    environment:
      - OBSERVABILITY__METRICS__ENABLED=true
      - OBSERVABILITY__METRICS__USEPROMETHEUS=true

  victoriametrics:
    image: victoriametrics/victoria-metrics:latest
    ports:
      - "8428:8428"
    volumes:
      - victoriametrics-data:/victoria-metrics-data
    command:
      - '--storageDataPath=/victoria-metrics-data'
      - '--httpListenAddr=:8428'
```

**Required Configuration File**: `prometheus.yml` (points to VictoriaMetrics)
```yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'honua-server'
    static_configs:
      - targets: ['honua:8080']
    metrics_path: '/metrics'

remote_write:
  - url: http://victoriametrics:8428/api/v1/write
```

**Access**: Navigate to `http://localhost:8428/vmui` for built-in UI

---

## Usage Examples

### Example 1: Full Production Stack

**Request**:
```
Create a Docker Compose deployment for production with PostGIS database, Redis cache, Nginx load balancer, and Prometheus with Grafana for monitoring
```

**Generated Components**:
- Honua Server
- PostGIS database
- Redis cache
- Nginx reverse proxy
- Prometheus (metrics collection)
- Grafana (dashboards)

**CLI Command**:
```bash
honua ai deploy --request "production deployment with PostGIS, Redis, Nginx, and Prometheus with Grafana"
```

---

### Example 2: Development Environment with Aspire

**Request**:
```
Generate a Docker Compose for development with PostGIS and Aspire Dashboard
```

**Generated Components**:
- Honua Server
- PostGIS database
- Aspire Dashboard (metrics/traces/logs)

**CLI Command**:
```bash
honua ai deploy --request "dev environment with PostGIS and Aspire Dashboard for monitoring"
```

---

### Example 3: Minimal Kubernetes Deployment

**Request**:
```
Create Kubernetes manifests for staging with MySQL and VictoriaMetrics
```

**Generated Components**:
- Honua Server deployment
- MySQL StatefulSet
- VictoriaMetrics deployment
- Services and Ingress

**CLI Command**:
```bash
honua ai deploy --request "Kubernetes deployment for staging with MySQL and VictoriaMetrics monitoring"
```

---

## LLM Analysis

When you request a deployment with observability, the AI Consultant's LLM analyzes your request and extracts:

```json
{
  "deploymentType": "DockerCompose",
  "targetEnvironment": "production",
  "requiredServices": ["honua-server", "postgis", "prometheus", "grafana"],
  "infrastructureNeeds": {
    "needsDatabase": true,
    "databaseType": "postgis",
    "needsCache": false,
    "needsLoadBalancer": false,
    "needsObservability": true,
    "observabilityStack": "prometheus-grafana"
  }
}
```

The `observabilityStack` field determines which components are included:
- `"prometheus-grafana"` → Prometheus + Grafana
- `"aspire-dashboard"` → .NET Aspire Dashboard
- `"victoriametrics"` → VictoriaMetrics

---

## Keywords Recognized by AI

The AI Consultant recognizes these keywords for observability:

| Keyword | Triggers |
|---------|----------|
| `prometheus` | Prometheus service |
| `grafana` | Grafana service (usually with Prometheus) |
| `aspire` or `aspire dashboard` | .NET Aspire Dashboard |
| `victoriametrics` or `victoria` | VictoriaMetrics |
| `metrics` | Generic observability (defaults to Prometheus+Grafana) |
| `monitoring` | Generic observability (defaults to Prometheus+Grafana) |
| `observability` | Generic observability (defaults to Prometheus+Grafana) |

---

## Configuration Details

### Honua Server Observability Settings

All generated deployments automatically configure Honua Server with observability enabled:

```yaml
environment:
  # Enable metrics collection
  - OBSERVABILITY__METRICS__ENABLED=true

  # For Prometheus/VictoriaMetrics
  - OBSERVABILITY__METRICS__USEPROMETHEUS=true
  - OBSERVABILITY__METRICS__ENDPOINT=/metrics

  # For Aspire Dashboard (OTLP)
  - OBSERVABILITY__METRICS__OPENTELEMETRY__ENDPOINT=http://aspire-dashboard:18889
```

### Metrics Exposed

Honua Server exposes these custom metrics (see `docs/observability/METRICS_VIEWER_OPTIONS.md`):

- `honua.api.requests` - Request count by protocol/service/layer
- `honua.api.request_duration` - Request latency
- `honua.api.errors` - Error count
- `honua.api.features_returned` - Features served
- `honua.raster.cache.hits` / `honua.raster.cache.misses` - Cache performance

---

## Post-Deployment Steps

### Prometheus + Grafana

1. **Create `prometheus.yml`** (generated in workspace):
   ```yaml
   global:
     scrape_interval: 15s

   scrape_configs:
     - job_name: 'honua-server'
       static_configs:
         - targets: ['honua:8080']
       metrics_path: '/metrics'
   ```

2. **Start services**:
   ```bash
   docker-compose up -d
   ```

3. **Access Grafana**:
   - URL: `http://localhost:3000`
   - Login: `admin` / `admin`

4. **Add Prometheus data source**:
   - Configuration → Data Sources → Add Prometheus
   - URL: `http://prometheus:9090`

5. **Create dashboards**:
   - Use example queries from `docs/observability/METRICS_VIEWER_OPTIONS.md`

### Aspire Dashboard

1. **Start services**:
   ```bash
   docker-compose up -d
   ```

2. **Access dashboard**:
   - URL: `http://localhost:18888`
   - No authentication required (development only!)

3. **View metrics**:
   - Navigate to "Metrics" tab
   - Filter by `honua.api.*` or `honua.raster.*`

### VictoriaMetrics

1. **Create `prometheus.yml`** with remote_write config

2. **Start services**:
   ```bash
   docker-compose up -d
   ```

3. **Access vmui**:
   - URL: `http://localhost:8428/vmui`
   - No authentication required

4. **Query metrics**: Use PromQL in the query box

---

## Troubleshooting

### Prometheus can't scrape Honua metrics

**Issue**: Prometheus shows target as "down"

**Solution**: Ensure Honua's `/metrics` endpoint is accessible:
```bash
curl http://localhost:5000/metrics
```

Check Docker network connectivity:
```bash
docker-compose exec prometheus wget -O- http://honua:8080/metrics
```

### Grafana can't connect to Prometheus

**Issue**: Data source test fails

**Solution**: Use service name `prometheus` not `localhost`:
```
URL: http://prometheus:9090
```

### Aspire Dashboard shows no metrics

**Issue**: Metrics tab is empty

**Solution**: Verify OTLP endpoint configuration:
```yaml
# In honua service
environment:
  - OBSERVABILITY__METRICS__OPENTELEMETRY__ENDPOINT=http://aspire-dashboard:18889
```

---

## Deployment Type Support

| Deployment Type | Observability Support |
|----------------|----------------------|
| **Docker Compose** | ✅ Full support (Prometheus, Grafana, Aspire, VictoriaMetrics) |
| **Kubernetes** | 🚧 Planned (will generate Prometheus Operator, Grafana deployment) |
| **Terraform AWS** | 🚧 Planned (will use CloudWatch + Managed Grafana) |
| **Terraform Azure** | 🚧 Planned (will use Azure Monitor + Managed Grafana) |
| **Terraform GCP** | 🚧 Planned (will use Cloud Monitoring) |

Currently, observability stack generation is **fully supported for Docker Compose** deployments.

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     Docker Compose Network                   │
│                                                               │
│  ┌──────────────┐      ┌──────────────┐                     │
│  │   Honua      │─────▶│  Prometheus  │                     │
│  │   Server     │      │              │                     │
│  │   :8080      │      │    :9090     │                     │
│  └──────────────┘      └──────┬───────┘                     │
│         │                      │                              │
│         │                      ▼                              │
│         │              ┌──────────────┐                      │
│         │              │   Grafana    │                      │
│         │              │              │                      │
│         │              │    :3000     │                      │
│         │              └──────────────┘                      │
│         │                                                     │
│         │              OR                                     │
│         │                                                     │
│         │              ┌──────────────┐                      │
│         └─────────────▶│   Aspire     │                      │
│                        │  Dashboard   │                      │
│                        │  :18888      │                      │
│                        └──────────────┘                      │
│                                                               │
│         ┌──────────────┐      ┌──────────────┐             │
│         │   PostGIS    │      │    Redis     │             │
│         │   :5432      │      │    :6379     │             │
│         └──────────────┘      └──────────────┘             │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

---

## Related Documentation

- [Metrics Viewer Options](../observability/METRICS_VIEWER_OPTIONS.md) - Detailed comparison of viewers
- [Runtime Configuration API](../RUNTIME_CONFIGURATION_API.md) - Service control endpoints
- [AI Consultant Guide](./AI_CONSULTANT_GUIDE.md) - Full AI deployment capabilities

---

## Example Generated `docker-compose.yml`

```yaml
version: '3.8'

services:
  honua:
    image: honuaio/honua-server:latest
    container_name: honua-server
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=production
      - HONUA__DATABASE__PROVIDER=postgis
      - HONUA__DATABASE__HOST=postgis
      - HONUA__DATABASE__DATABASE=honua
      - HONUA__DATABASE__USERNAME=honua
      - HONUA__DATABASE__PASSWORD=honua_password
      - OBSERVABILITY__METRICS__ENABLED=true
      - OBSERVABILITY__METRICS__USEPROMETHEUS=true
    depends_on:
      - postgis
    networks:
      - honua-network

  postgis:
    image: postgis/postgis:16-3.4
    container_name: honua-postgis
    environment:
      - POSTGRES_DB=honua
      - POSTGRES_USER=honua
      - POSTGRES_PASSWORD=honua_password
    ports:
      - "5432:5432"
    volumes:
      - postgis-data:/var/lib/postgresql/data
    networks:
      - honua-network

  prometheus:
    image: prom/prometheus:latest
    container_name: honua-prometheus
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
      - prometheus-data:/prometheus
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
      - '--storage.tsdb.path=/prometheus'
    depends_on:
      - honua
    networks:
      - honua-network

  grafana:
    image: grafana/grafana:latest
    container_name: honua-grafana
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
      - GF_SERVER_ROOT_URL=http://localhost:3000
    volumes:
      - grafana-data:/var/lib/grafana
    depends_on:
      - prometheus
    networks:
      - honua-network

networks:
  honua-network:
    driver: bridge

volumes:
  postgis-data:
  prometheus-data:
  grafana-data:
```

---

## Future Enhancements

- **Kubernetes Observability**: Prometheus Operator, Grafana deployment
- **Jaeger Integration**: Distributed tracing support
- **Loki Integration**: Log aggregation with Grafana
- **Alert Manager**: Automated alerting configuration
- **Custom Dashboards**: Pre-built Grafana dashboards for Honua metrics
- **Health Check Integration**: Integrate with Honua's `/healthz` endpoints
