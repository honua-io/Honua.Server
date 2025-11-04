# Process Framework Testing Stack

Local testing infrastructure for the Honua Process Framework, including Redis for state storage, Prometheus for metrics, Grafana for visualization, OpenTelemetry Collector for observability aggregation, and Loki for log aggregation.

## Overview

This testing stack provides a complete observability and state management infrastructure for developing and testing the Honua Process Framework integration with Semantic Kernel.

### Services Included

| Service | Purpose | Port | Dashboard/UI |
|---------|---------|------|--------------|
| **Redis** | Process state storage | 6379 | N/A |
| **OpenTelemetry Collector** | Centralized telemetry collection | 4317 (gRPC), 4318 (HTTP) | http://localhost:55679/debug/tracez |
| **Prometheus** | Metrics storage and querying | 9090 | http://localhost:9090 |
| **Loki** | Log aggregation (30-day retention) | 3100 | N/A (query via Grafana) |
| **Promtail** | Log collection agent | N/A | N/A |
| **Tempo** | Distributed tracing backend | 3200 | http://localhost:3200 |
| **Grafana** | Metrics, logs, and traces visualization | 3000 | http://localhost:3000 |

## Quick Start

### Prerequisites

- Docker 20.10+ or Docker Desktop
- docker-compose 1.29+ or Docker Compose V2
- 2GB+ available RAM
- 5GB+ available disk space

### Start the Stack

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/start-testing-stack.sh
```

The script will:
1. Start all services in the background
2. Wait for health checks to pass
3. Display service URLs and credentials

### Verify Health

Check that all services are running correctly:

```bash
./scripts/verify-health.sh
```

### Stop the Stack

```bash
# Stop services but keep data
./scripts/stop-testing-stack.sh

# Stop services and remove all data
./scripts/stop-testing-stack.sh --clean
```

## Configuration

### Environment Variables

Configure the stack by editing `/home/mike/projects/HonuaIO/docker/process-testing/.env`:

```env
REDIS_PORT=6379
PROMETHEUS_PORT=9090
GRAFANA_PORT=3000
GRAFANA_USER=admin
GRAFANA_PASSWORD=admin
LOKI_PORT=3100
TEMPO_PORT=3200
```

### Grafana Configuration

**Default Credentials:**
- Username: `admin`
- Password: `admin`

**Pre-configured Datasources:**
- Prometheus (default datasource)
- Loki (for log queries)
- Tempo (for distributed tracing)

**Pre-configured Dashboards:**
- **Process Framework Dashboard**: http://localhost:3000/d/honua-process-framework
  - Process step execution rates
  - Active process instances
  - Step duration metrics (p50, p95)
  - Success/error rates
  - Redis metrics (memory, clients, commands)
  - CPU and memory usage
- **Centralized Logs Dashboard**: http://localhost:3000/d/honua-logs
  - Log volume by service
  - Log level distribution
  - Error and warning rates
  - Process framework logs with filtering
  - Trace-correlated logs
  - Top error messages and sources
- **Distributed Tracing Dashboard**: http://localhost:3000/d/honua-distributed-tracing
  - Traces per second
  - P95 trace latency
  - Error rate from traces
  - Recent traces viewer
  - Span duration by operation
  - Request rate by operation
  - Service dependencies graph
  - Operations summary table

## Configuring Honua.Cli.AI

### Using the Testing Environment

Set the environment variable to use the Testing configuration:

```bash
export ASPNETCORE_ENVIRONMENT=Testing
dotnet run --project /home/mike/projects/HonuaIO/src/Honua.Cli.AI
```

### Visual Studio / Rider Launch Settings

Add to `launchSettings.json`:

```json
{
  "profiles": {
    "ProcessFramework-Testing": {
      "commandName": "Project",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Testing"
      }
    }
  }
}
```

### appsettings.Testing.json Configuration

The testing configuration (`/home/mike/projects/HonuaIO/src/Honua.Cli.AI/appsettings.Testing.json`) includes:

**Redis Configuration:**
```json
{
  "ProcessFramework": {
    "Redis": {
      "ConnectionString": "localhost:6379",
      "InstanceName": "HonuaProcessFramework:",
      "AbortOnConnectFail": false
    }
  }
}
```

**OpenTelemetry Configuration:**
```json
{
  "OpenTelemetry": {
    "ServiceName": "honua-cli-ai",
    "Metrics": {
      "Enabled": true,
      "Exporters": ["otlp", "console"]
    },
    "Tracing": {
      "Enabled": true,
      "Exporters": ["otlp", "console"],
      "SamplingRate": 1.0
    },
    "Otlp": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "grpc"
    }
  }
}
```

**Note:** Application sends 100% of traces to OTel Collector. The collector applies 10% head-based sampling before forwarding to Tempo. This ensures all traces are available for metrics generation while controlling storage costs.

## Using the Stack

### 1. Start the Testing Stack

```bash
./scripts/start-testing-stack.sh
```

### 2. Run Honua.Cli.AI with Testing Environment

```bash
cd /home/mike/projects/HonuaIO
export ASPNETCORE_ENVIRONMENT=Testing
dotnet run --project src/Honua.Cli.AI
```

### 3. View Metrics in Grafana

1. Open http://localhost:3000
2. Login with `admin` / `admin`
3. Navigate to Dashboards → Honua → Process Framework
4. Watch real-time metrics as your processes execute

### 4. View Distributed Traces

1. Open http://localhost:3000
2. Navigate to Dashboards → Honua → Distributed Tracing
3. View traces, latency, and service dependencies
4. Or use Explore → Select "Tempo" datasource
5. Query traces with TraceQL:
   ```traceql
   { service.name="honua-cli-ai" }
   { service.name="honua-cli-ai" && duration > 1s }
   { service.name="honua-cli-ai" && status=error }
   ```

**Quick Start:** See [TRACING_QUICKSTART.md](TRACING_QUICKSTART.md) for a comprehensive tracing guide.

**Production Deployment:** See [DISTRIBUTED_TRACING_DEPLOYMENT.md](DISTRIBUTED_TRACING_DEPLOYMENT.md) for production setup.

### 5. Query Logs in Loki

1. In Grafana, go to Explore
2. Select "Loki" as the data source
3. Use LogQL queries:
   ```logql
   {service_name="honua-cli-ai"}
   {service_name="honua-cli-ai"} |= "error"
   {service_name="honua-cli-ai"} |= "ProcessStep"
   ```

### 6. Check Prometheus Metrics

Direct Prometheus UI: http://localhost:9090

Example queries:
```promql
# Process step execution rate
rate(honua_process_steps_total[5m])

# Active process instances
honua_process_active_instances

# Step duration p95
histogram_quantile(0.95, rate(honua_process_step_duration_bucket[5m]))

# Redis memory usage
honua_redis_memory_used_bytes
```

### 7. Verify Redis State

Connect to Redis CLI:
```bash
docker exec -it honua-process-redis redis-cli

# List all process keys
KEYS HonuaProcessFramework:*

# Get specific process state
GET HonuaProcessFramework:process:{process-id}

# Monitor commands
MONITOR
```

## Troubleshooting

### Services Not Starting

**Check Docker resources:**
```bash
docker info
docker stats
```

**View service logs:**
```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
docker-compose logs -f

# Or specific service
docker-compose logs -f redis
docker-compose logs -f otel-collector
docker-compose logs -f prometheus
docker-compose logs -f grafana
```

### Connection Refused Errors

**Verify ports are not in use:**
```bash
lsof -i :6379  # Redis
lsof -i :9090  # Prometheus
lsof -i :3000  # Grafana
lsof -i :4317  # OTLP gRPC
```

**Check service health:**
```bash
./scripts/verify-health.sh
```

### No Metrics in Grafana

**1. Verify Prometheus is scraping targets:**
- Open http://localhost:9090/targets
- Check that `honua-process-framework` target is UP
- Verify `otel-collector:8889` is reachable

**2. Check OpenTelemetry Collector:**
```bash
docker-compose logs otel-collector
```

**3. Verify Honua.Cli.AI is exporting metrics:**
- Check console output for OTLP export messages
- Ensure `ASPNETCORE_ENVIRONMENT=Testing` is set
- Verify appsettings.Testing.json is being loaded

### Grafana Dashboard Not Loading

**Verify dashboard is provisioned:**
```bash
docker exec honua-process-grafana ls -la /var/lib/grafana/dashboards/
```

**Check Grafana logs:**
```bash
docker-compose logs grafana | grep -i dashboard
```

**Manually import dashboard:**
1. Open http://localhost:3000
2. Go to Dashboards → Import
3. Upload `/home/mike/projects/HonuaIO/docker/process-testing/grafana/dashboards/process-framework-dashboard.json`

### Redis Connection Issues

**Test Redis connection:**
```bash
docker exec honua-process-redis redis-cli ping
```

**Check Redis logs:**
```bash
docker-compose logs redis
```

**Verify Redis is accepting connections:**
```bash
telnet localhost 6379
```

## Advanced Configuration

### Custom Prometheus Scrape Configs

Edit `/home/mike/projects/HonuaIO/docker/process-testing/prometheus/prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'honua-cli-ai-direct'
    metrics_path: /metrics
    static_configs:
      - targets: ['host.docker.internal:5000']
```

Reload Prometheus:
```bash
docker exec honua-process-prometheus kill -HUP 1
```

### Custom OpenTelemetry Collector Config

Edit `/home/mike/projects/HonuaIO/docker/process-testing/otel-collector/otel-collector-config.yml`

Restart collector:
```bash
docker-compose restart otel-collector
```

### Adding Custom Grafana Dashboards

1. Create JSON dashboard file
2. Place in `/home/mike/projects/HonuaIO/docker/process-testing/grafana/dashboards/`
3. Restart Grafana:
   ```bash
   docker-compose restart grafana
   ```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Honua.Cli.AI                              │
│                    (Process Framework)                           │
└──────────┬────────────────────────┬─────────────────────────────┘
           │                        │
           │ Redis                  │ OTLP (gRPC/HTTP)
           │ (State)                │ (Metrics, Traces, Logs)
           │                        │
           ▼                        ▼
    ┌─────────────┐        ┌──────────────────┐
    │    Redis    │        │ OTLP Collector   │◄── 10% Sampling
    │             │        │                  │
    │  Port 6379  │        │ gRPC: 4317       │
    └─────────────┘        │ HTTP: 4318       │
                           └────────┬──────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    │               │               │
                    ▼               ▼               ▼
            ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
            │ Prometheus  │ │    Loki     │ │    Tempo    │
            │  (Metrics)  │ │   (Logs)    │ │  (Traces)   │
            │  Port 9090  │ │  Port 3100  │ │  Port 3200  │
            └──────┬──────┘ └──────┬──────┘ └──────┬──────┘
                   │               │  ▲            │
                   │               │  │            │
                   │               │  │ Push logs  │
                   │               │  │            │
                   │     ┌─────────────┴──────┐    │
                   │     │    Promtail        │    │
                   │     │  (Log Collector)   │    │
                   │     └─────────┬──────────┘    │
                   │               │                │
                   │               │ Scrape Docker  │
                   │               │ container logs │
                   │               │                │
                   └───────────────┼────────────────┘
                                   │
                                   ▼
                            ┌─────────────┐
                            │   Grafana   │
                            │             │
                            │  Port 3000  │
                            └─────────────┘

Log Flow:
  Docker Containers → Promtail → Loki → Grafana
  Application → OTLP Collector → Loki → Grafana

Features:
  - 30-day log retention
  - JSON log parsing
  - Trace correlation
  - Label-based indexing
```

## Metrics Reference

### Process Framework Metrics

- `honua_process_steps_total{step_name, status}` - Counter of executed process steps
- `honua_process_step_duration_bucket{step_name}` - Histogram of step execution time
- `honua_process_active_instances` - Gauge of currently running process instances
- `honua_process_errors_total{error_type}` - Counter of process errors

### Redis Metrics

- `honua_redis_memory_used_bytes` - Redis memory usage
- `honua_redis_connected_clients` - Number of connected clients
- `honua_redis_commands_processed_total` - Total Redis commands processed

### Runtime Metrics

- `process_cpu_seconds_total` - CPU usage
- `process_working_set_bytes` - Working set memory
- `process_private_memory_bytes` - Private memory

## Files and Directories

```
/home/mike/projects/HonuaIO/docker/process-testing/
├── docker-compose.yml                          # Main compose file
├── .env                                        # Environment variables
├── README.md                                   # This file
├── TRACING_QUICKSTART.md                      # Quick start guide for tracing
├── DISTRIBUTED_TRACING_DEPLOYMENT.md          # Production tracing deployment guide
├── prometheus/
│   └── prometheus.yml                          # Prometheus config
├── tempo/
│   └── tempo-config.yaml                       # Tempo tracing backend config
├── otel-collector/
│   └── otel-collector-config.yml              # OTLP Collector config (with sampling)
├── grafana/
│   ├── provisioning/
│   │   ├── datasources/
│   │   │   └── datasources.yml                # Datasource config (Prom, Loki, Tempo)
│   │   └── dashboards/
│   │       └── dashboards.yml                 # Dashboard provisioning
│   └── dashboards/
│       ├── process-framework-dashboard.json    # Process Framework dashboard
│       └── distributed-tracing-dashboard.json  # Distributed Tracing dashboard
└── scripts/
    ├── start-testing-stack.sh                 # Start script
    ├── stop-testing-stack.sh                  # Stop script
    └── verify-health.sh                       # Health check script (includes Tempo)
```

## Additional Configuration

```
/home/mike/projects/HonuaIO/src/Honua.Cli.AI/
└── appsettings.Testing.json                   # Testing environment config
```

## Documentation

### Detailed Guides

- **[Log Aggregation Guide](LOG_AGGREGATION.md)** - Comprehensive documentation for Loki/Promtail
  - Architecture overview
  - Configuration details
  - Sample LogQL queries
  - Retention policies
  - Troubleshooting
  - Integration with traces and metrics

- **[Query Examples](loki/QUERY_EXAMPLES.md)** - Quick reference for common LogQL queries
  - Basic log filtering
  - Process framework queries
  - Error analysis
  - Performance monitoring
  - Trace correlation
  - Aggregations and metrics

### Quick References

- **Grafana Dashboards**: http://localhost:3000
  - Process Framework Dashboard
  - Centralized Logs Dashboard
  - Distributed Tracing Dashboard

- **Direct Service Access**:
  - Prometheus: http://localhost:9090
  - Loki: http://localhost:3100
  - Tempo: http://localhost:3200

## Support

For issues or questions:
1. Check this README's troubleshooting section
2. Run `./scripts/verify-health.sh` for diagnostics
3. Check service logs: `docker-compose logs -f [service]`
4. Review the OpenTelemetry Collector logs for telemetry pipeline issues
5. Consult the [Log Aggregation Guide](LOG_AGGREGATION.md) for log-related issues

## License

Part of the Honua project. See main project LICENSE for details.
