# Distributed Tracing Implementation Summary

## Overview

Successfully implemented Grafana Tempo as the distributed tracing backend for the Honua Process Framework testing stack. This provides complete observability with traces, metrics, and logs all correlated through Grafana.

## What Was Implemented

### 1. Tempo Service (Tracing Backend)

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/docker-compose.yml`

Added Grafana Tempo service with:
- OTLP gRPC/HTTP receivers (ports 4317/4318)
- Zipkin compatibility (port 9411)
- Jaeger compatibility (port 14268)
- HTTP API (port 3200)
- Health checks
- Local storage for testing (easily swappable to S3/GCS/Azure for production)

**Configuration File:** `/home/mike/projects/HonuaIO/docker/process-testing/tempo/tempo-config.yaml`

Features:
- Multi-protocol ingestion (OTLP, Zipkin, Jaeger)
- Metrics generation from traces (service graphs, span metrics)
- 24-hour retention for testing
- Automatic compaction
- Remote write to Prometheus for trace-derived metrics

### 2. OpenTelemetry Collector Updates

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/otel-collector/otel-collector-config.yml`

Implemented:
- **10% Head-based Sampling** using probabilistic sampler
- Trace export to Tempo via OTLP
- Retry logic with exponential backoff
- Queuing for reliability
- Commented tail-sampling configuration for future use

**Sampling Strategy:**
- Application sends 100% of traces to collector
- Collector applies 10% probabilistic sampling
- All traces available for metrics generation
- Only 10% stored in Tempo (cost optimization)

### 3. Grafana Integration

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/grafana/provisioning/datasources/datasources.yml`

Added Tempo datasource with:
- TraceQL query support
- Trace-to-logs correlation (links to Loki)
- Trace-to-metrics correlation (links to Prometheus)
- Service map visualization
- Node graph support
- Exemplar support in Prometheus

**Updated Loki datasource:**
- Derived fields to extract trace IDs from logs
- Multiple regex patterns for trace ID matching
- Automatic links to traces

**Updated Prometheus datasource:**
- Exemplar trace ID destinations
- Links from metrics to traces

### 4. Distributed Tracing Dashboard

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/grafana/dashboards/distributed-tracing-dashboard.json`

Comprehensive dashboard with:

**Overview Metrics:**
- Traces per second
- P95 trace latency
- Error rate from traces
- Active services count

**Trace Explorer:**
- Recent traces viewer with TraceQL
- Configurable time ranges
- Service filtering

**Performance Analysis:**
- Span duration percentiles (p99, p95, p50) by operation
- Request rate by operation
- Success vs error rate breakdown

**Service Dependencies:**
- Service graph visualization
- Request flow between services

**Operations Summary Table:**
- All operations with key metrics
- Request rate, P95 latency, error rate
- Sortable and filterable

### 5. Environment Configuration

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/.env`

Added:
```bash
TEMPO_PORT=3200
TEMPO_OTLP_GRPC_PORT=4317
TEMPO_OTLP_HTTP_PORT=4318
TEMPO_ZIPKIN_PORT=9411
TEMPO_JAEGER_THRIFT_PORT=14268
```

### 6. Health Check Updates

**File:** `/home/mike/projects/HonuaIO/docker/process-testing/scripts/verify-health.sh`

Enhanced to include:
- Tempo container health check
- Tempo HTTP API health endpoint
- Tempo OTLP receivers connectivity
- Tempo datasource verification in Grafana
- Updated total check count (13 → 17 checks)

### 7. Documentation

Created comprehensive documentation:

**TRACING_QUICKSTART.md** - Quick reference guide covering:
- Access points and URLs
- Viewing traces (4 different methods)
- TraceQL query examples
- Instrumenting code
- Common queries
- Troubleshooting
- Configuration reference
- Best practices

**DISTRIBUTED_TRACING_DEPLOYMENT.md** - Production deployment guide covering:
- Architecture overview
- Local development setup
- Production deployment (Kubernetes, Docker Swarm)
- Cloud storage backends (AWS S3, GCS, Azure Blob)
- Scaling considerations
- Sampling strategies (head-based and tail-based)
- Configuration options
- Monitoring and operations
- Alerts
- Querying traces
- Security
- Cost optimization
- Migration from Jaeger/Zipkin

**README.md** - Updated main documentation:
- Added Tempo to services table
- Updated architecture diagram
- Added distributed tracing dashboard info
- Added tracing quick start section
- Updated configuration examples
- Updated file structure

## Key Features

### 1. Complete Observability Stack

```
Application (Honua.Cli.AI)
    ↓
OpenTelemetry Collector (10% sampling)
    ↓
    ├── Tempo (traces)
    ├── Prometheus (metrics + trace-derived metrics)
    └── Loki (logs)
    ↓
Grafana (unified visualization)
```

### 2. Correlation Capabilities

- **Logs → Traces:** Click trace ID in logs to view full trace
- **Traces → Logs:** View logs for specific trace/span
- **Metrics → Traces:** Click exemplars to view trace
- **Traces → Metrics:** View metrics for traced operations

### 3. Intelligent Sampling

**Current Implementation:**
- 10% head-based probabilistic sampling
- Consistent sampling (same trace ID = same decision)
- Configurable percentage

**Available for Future:**
- Tail-based sampling (commented in config)
  - Always sample errors
  - Always sample slow requests (>1s)
  - Sample 5% of normal requests
  - More cost-effective for high-traffic scenarios

### 4. Production-Ready

The implementation is designed for easy transition to production:

**Local Development:**
- Uses local storage
- 24-hour retention
- All features enabled

**Production (via config change):**
- Object storage (S3, GCS, Azure Blob)
- Configurable retention (7-90 days typical)
- Horizontal scaling
- HA deployment patterns
- Cloud-native

## Access Points

After starting the stack:

| Service | URL | Purpose |
|---------|-----|---------|
| Grafana Dashboard | http://localhost:3000/d/honua-distributed-tracing | View traces, latency, errors |
| Grafana Explore | http://localhost:3000/explore?datasource=tempo | Query traces with TraceQL |
| Tempo API | http://localhost:3200 | Direct API access |
| Tempo Health | http://localhost:3200/ready | Health check |
| Tempo Metrics | http://localhost:3200/metrics | Tempo internal metrics |

## Configuration Files

All configuration files are located in `/home/mike/projects/HonuaIO/docker/process-testing/`:

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Tempo service definition |
| `tempo/tempo-config.yaml` | Tempo backend configuration |
| `otel-collector/otel-collector-config.yml` | Sampling and export config |
| `grafana/provisioning/datasources/datasources.yml` | Tempo datasource |
| `grafana/dashboards/distributed-tracing-dashboard.json` | Tracing dashboard |
| `.env` | Port configuration |

## Usage Examples

### Query Recent Traces

```traceql
{ service.name="honua-cli-ai" }
```

### Find Slow Traces

```traceql
{ service.name="honua-cli-ai" && duration > 1s }
```

### Find Errors

```traceql
{ service.name="honua-cli-ai" && status=error }
```

### Find Specific Process

```traceql
{
  service.name="honua-cli-ai" &&
  span.process.name="DeploymentProcess"
}
```

## Metrics Generated from Traces

Tempo automatically generates these Prometheus metrics:

```promql
# Request rate
traces_spanmetrics_calls_total

# Latency
traces_spanmetrics_latency_bucket

# Service dependencies
traces_service_graph_request_total
```

These are used in the distributed tracing dashboard.

## Testing the Implementation

### 1. Start the Stack

```bash
cd /home/mike/projects/HonuaIO/docker/process-testing
./scripts/start-testing-stack.sh
```

### 2. Verify Health

```bash
./scripts/verify-health.sh
```

Should show 17/17 checks passing (including Tempo).

### 3. Run Application

```bash
export ASPNETCORE_ENVIRONMENT=Testing
dotnet run --project /home/mike/projects/HonuaIO/src/Honua.Cli.AI
```

### 4. Generate Traces

Execute any process (deployment, upgrade, etc.)

### 5. View in Grafana

1. Open http://localhost:3000
2. Navigate to Dashboards → Distributed Tracing
3. See traces appearing in real-time

## Performance Characteristics

### Storage Requirements

With 10% sampling and typical usage:

- **Trace ingestion:** ~100-1000 traces/hour (10% of actual)
- **Storage per trace:** ~5-50 KB (depends on span count)
- **Daily storage:** ~12-1200 MB
- **24-hour retention:** ~12-1200 MB total

For production with 7-day retention:
- ~84 MB - 8.4 GB storage needed
- Use object storage for cost efficiency

### Resource Usage

Testing environment:
- Tempo: ~100-500 MB RAM
- OTel Collector: ~100-200 MB RAM (with sampling)
- Total overhead: ~200-700 MB RAM

Production environment (recommended):
- Tempo Distributor: 500 MB - 1 GB RAM
- Tempo Ingester: 1-2 GB RAM
- Tempo Querier: 500 MB - 1 GB RAM

## Advantages Over Alternatives

### Why Tempo over Jaeger?

1. **No database required** - Uses object storage
2. **Lower cost at scale** - S3/GCS storage cheaper than databases
3. **Better Grafana integration** - Native support
4. **Simpler operations** - Fewer components to manage
5. **Metrics generation** - Automatic trace-to-metrics

### Why Tempo over Zipkin?

1. **Modern architecture** - Cloud-native design
2. **Better scalability** - Horizontal scaling
3. **TraceQL** - Powerful query language
4. **Metrics integration** - Automatic service graphs
5. **Active development** - Grafana backing

## Future Enhancements

### Immediate (Available Now)

1. **Enable tail-based sampling:**
   - Uncomment tail_sampling processor
   - Configure policies for errors, slow requests
   - More intelligent sampling decisions

2. **Add custom attributes:**
   - Process instance IDs
   - User IDs
   - Deployment environments
   - Custom business logic tags

### Short-term

1. **Service mesh integration:**
   - Istio/Linkerd automatic instrumentation
   - No code changes needed

2. **Advanced dashboards:**
   - SLA tracking
   - Customer journey visualization
   - Cost attribution by trace

### Long-term

1. **Production deployment:**
   - Switch to S3/GCS/Azure backend
   - Multi-region replication
   - HA deployment

2. **Advanced features:**
   - Trace sampling based on user ID
   - Trace replay for debugging
   - Automated anomaly detection

## Troubleshooting

### No Traces Appearing

1. Check OTel Collector logs:
   ```bash
   docker logs honua-process-otel
   ```

2. Verify Tempo ingestion:
   ```bash
   curl http://localhost:3200/metrics | grep tempo_distributor_spans_received
   ```

3. Check sampling rate (increase if too low)

### High Memory Usage

1. Reduce sampling percentage in `otel-collector-config.yml`
2. Reduce retention in `tempo-config.yaml`
3. Reduce max_block_duration in `tempo-config.yaml`

### Query Performance Issues

1. Narrow time range
2. Use specific service names
3. Add filters to TraceQL queries
4. Enable query caching (for production)

## Security Considerations

### Current Implementation (Testing)

- No authentication (local only)
- No TLS (local only)
- No data scrubbing

### Production Recommendations

1. **Enable TLS:**
   - OTLP receiver TLS
   - Tempo API TLS

2. **Authentication:**
   - OAuth2 proxy for Grafana
   - API keys for Tempo

3. **Data Privacy:**
   - Scrub sensitive attributes
   - Encrypt at rest (object storage)
   - Network policies (Kubernetes)

## Cost Optimization

### Testing Environment

- Minimal cost (local storage)
- 24-hour retention
- 10% sampling

### Production Recommendations

1. **Sampling Strategy:**
   - Start at 10%, adjust based on traffic
   - High traffic (>1000 req/s): 1-5%
   - Medium traffic: 10-30%
   - Low traffic: 50-100%

2. **Storage Optimization:**
   - Use object storage lifecycle policies
   - Archive old traces to glacier/coldline
   - Compress blocks

3. **Retention:**
   - 7 days: debugging
   - 30 days: compliance
   - 90+ days: audit requirements

## Support and Documentation

### Quick Reference

- **Quick Start:** [TRACING_QUICKSTART.md](TRACING_QUICKSTART.md)
- **Production Deployment:** [DISTRIBUTED_TRACING_DEPLOYMENT.md](DISTRIBUTED_TRACING_DEPLOYMENT.md)
- **Main README:** [README.md](README.md)

### External Resources

- [Grafana Tempo Docs](https://grafana.com/docs/tempo/latest/)
- [TraceQL Reference](https://grafana.com/docs/tempo/latest/traceql/)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

## Conclusion

The distributed tracing implementation provides:

✅ Complete observability (traces, metrics, logs)
✅ Cost-effective sampling (10% configurable)
✅ Production-ready architecture
✅ Easy correlation across signals
✅ Comprehensive documentation
✅ Health monitoring
✅ Scalability path

The stack is now ready for:
- Local development and testing
- Debugging process framework issues
- Performance optimization
- Production deployment (with config changes)

All deliverables completed as requested.
