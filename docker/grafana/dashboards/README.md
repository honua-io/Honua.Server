# Honua Grafana Dashboards

Comprehensive observability dashboards for the Honua geospatial platform.

## Dashboard Overview

### 1. Platform Overview (`honua-platform-overview`)
**File**: `platform-overview.json`
**Purpose**: Unified view of all platform metrics
**Key Metrics**:
- System health and service availability
- API request rates and latency
- Active processes and circuit breaker status
- Cache hit rates and Redis health
- Resource utilization (CPU, memory, network)

**Use Cases**:
- Operations team daily monitoring
- Incident response starting point
- Executive dashboards and reporting
- Quick health checks

**Links to**: All specialized dashboards

---

### 2. Process Framework (`honua-process-framework`)
**File**: `process-framework.json`
**Purpose**: Monitor AI-powered process workflows
**Key Metrics**:
- Active process count
- Process start/completion/failure rates
- Success rates by workflow type
- Process execution duration (p50/p95/p99)
- Step execution metrics
- Error analysis by workflow and reason

**Alerts**:
- `ProcessFrameworkHighFailureRate`: >20% failure rate
- `ProcessExecutionSlow`: p95 >60s
- `TooManyActiveProcesses`: >50 active
- `ProcessStepFailuresHigh`: Step failures >1/sec

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name
- `$workflow`: Filter by workflow type

---

### 3. Circuit Breaker & Resilience (`honua-circuit-breaker`)
**File**: `circuit-breaker.json`
**Purpose**: Monitor resilience policies for external services (S3, Azure Blob, HTTP)
**Key Metrics**:
- Circuit breaker state changes (open/closed/half-open)
- Failure rates by service and exception type
- Break duration percentiles
- Service availability tracking

**Alerts**:
- `CircuitBreakerOpen`: Circuit opened (immediate)
- `HighCircuitBreakerFailureRate`: >10 failures/sec
- `CircuitBreakerFlapping`: >5 state changes in 10min

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name
- `$service`: Filter by service (S3, Azure, etc.)

**Metrics Source**: `ExternalServiceResiliencePolicies.cs`

---

### 4. API Performance & Metrics (`honua-api-metrics`)
**File**: `api-metrics.json`
**Purpose**: Comprehensive API performance monitoring
**Key Metrics**:
- Request rate (requests/sec)
- Error rate (%) by protocol and type
- Latency percentiles (p50/p95/p99) by protocol
- HTTP status code distribution
- Features returned by protocol

**Alerts**:
- `HighAPIErrorRate`: >5% error rate
- `HighAPILatency`: p95 >2s
- `APILatencyCritical`: p99 >5s

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name
- `$protocol`: Filter by API protocol (WFS, WMS, OGC API Features, STAC, etc.)

**Metrics Source**: `ApiMetrics.cs`

---

### 5. Redis Health & Performance (`honua-redis`)
**File**: `redis-performance.json`
**Purpose**: Monitor Redis cache and connection pool
**Key Metrics**:
- Connected clients
- Operations per second
- Memory usage and eviction rate
- Cache hit/miss rates
- Network I/O
- Connection pool wait times

**Alerts**:
- `RedisDown`: Instance not responding
- `RedisHighMemoryUsage`: >90% memory
- `RedisHighEvictionRate`: >100 evictions/sec
- `RedisConnectionsSaturated`: >1000 clients

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name

---

### 6. Distributed Cache Metrics (`honua-cache-metrics`)
**File**: `cache-metrics.json`
**Purpose**: Monitor raster and vector tile caching
**Key Metrics**:
- Cache hit rates by dataset
- Render latency percentiles
- Preseed job status (completed/failed/cancelled)
- Cache purge operations
- Hit/miss rates over time

**Alerts**:
- `LowCacheHitRate`: <70% hit rate
- `HighRenderLatency`: p95 >500ms
- `PreseedJobFailures`: >0.1 failures/sec

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name
- `$dataset`: Filter by dataset ID

**Metrics Source**: `RasterTileCacheMetrics.cs`

---

### 7. LLM Provider Metrics (`honua-llm-providers`)
**File**: `llm-providers.json`
**Purpose**: Monitor AI/LLM provider performance and costs
**Key Metrics**:
- Request rates by provider (OpenAI, Azure, Anthropic, etc.)
- Rate limit hits and retry attempts
- Response time percentiles
- Token usage (input/output)
- Error rates by provider and type
- Retry success rates

**Alerts**:
- `LLMRateLimitHit`: >10 hits in 5min
- `LLMHighErrorRate`: >10% error rate
- `LLMSlowResponses`: p95 >10s

**Variables**:
- `$datasource`: Prometheus datasource
- `$job`: Filter by job name
- `$provider`: Filter by LLM provider

**Note**: Metrics are emitted from `LlmRateLimitHandler` and provider implementations

---

## Metrics Coverage

### Process Framework Metrics
**Meter**: `Honua.ProcessFramework` (v1.0.0)

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `process.started` | Counter | Processes started | workflow.type, process.id |
| `process.completed` | Counter | Processes completed | workflow.type, process.id |
| `process.failed` | Counter | Processes failed | workflow.type, process.id, error.reason, error.type |
| `process.execution.duration` | Histogram | Process duration (ms) | workflow.type, process.id |
| `process.active.count` | UpDownCounter | Active processes | workflow.type, process.id |
| `process.step.executed` | Counter | Steps executed | workflow.type, process.id, step.name |
| `process.step.failed` | Counter | Steps failed | workflow.type, process.id, step.name, error.reason |
| `process.step.duration` | Histogram | Step duration (ms) | workflow.type, process.id, step.name |
| `process.step.active.count` | UpDownCounter | Active steps | workflow.type, process.id, step.name |
| `process.workflow.success_rate` | ObservableGauge | Success rate by workflow | workflow.type |
| `process.workflow.total_executions` | ObservableGauge | Total executions | workflow.type |

### Circuit Breaker Metrics
**Meter**: `Honua.Server.Core.Resilience` (v1.0.0)

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `honua.circuit_breaker.state_changes` | Counter | State changes | service, state |
| `honua.circuit_breaker.failures` | Counter | Failures handled | service, exception_type |
| `honua.circuit_breaker.break_duration_seconds` | Histogram | Break duration | service |

### API Metrics
**Meter**: `Honua.Server.Api` (v1.0.0)

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `honua.api.requests` | Counter | API requests | api.protocol, service.id, layer.id |
| `honua.api.request_duration` | Histogram | Request duration (ms) | api.protocol, service.id, layer.id, http.status_code |
| `honua.api.request_latency` | Histogram | Latency for percentiles | api.protocol, http.status_category, success |
| `honua.api.errors` | Counter | API errors | api.protocol, service.id, layer.id, error.type, error.category |
| `honua.api.error_rate` | Histogram | Error rate tracking | api.protocol, http.status_category |
| `honua.api.features_returned` | Counter | Features returned | api.protocol, service.id, layer.id |

### Raster Cache Metrics
**Meter**: `Honua.Server.RasterCache`

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `honua.raster.cache_hits` | Counter | Cache hits | dataset |
| `honua.raster.cache_misses` | Counter | Cache misses | dataset |
| `honua.raster.render_latency_ms` | Histogram | Render latency | dataset, source |
| `honua.raster.preseed_jobs_completed` | Counter | Preseed jobs completed | jobId, datasets |
| `honua.raster.preseed_jobs_failed` | Counter | Preseed jobs failed | jobId, error |
| `honua.raster.preseed_jobs_cancelled` | Counter | Preseed jobs cancelled | jobId |
| `honua.raster.cache_purges_succeeded` | Counter | Successful purges | dataset |
| `honua.raster.cache_purges_failed` | Counter | Failed purges | dataset |

### Postgres Connection Pool Metrics
**Meter**: `Honua.Server.Core.Data.Postgres` (v1.0.0)

| Metric Name | Type | Description | Labels |
|------------|------|-------------|--------|
| `postgres.pool.connections.active` | ObservableGauge | Active connections | - |
| `postgres.pool.connections.idle` | ObservableGauge | Idle connections | - |
| `postgres.pool.connection.failures` | Counter | Connection failures | error.type, connection.masked |
| `postgres.pool.wait.duration` | Histogram | Pool wait time (ms) | connection.masked |

## Alert Rules

### Configuration
Alert rules are defined in: `/home/mike/projects/HonuaIO/docker/prometheus/alerts/honua-platform-alerts.yml`

### Alert Groups
1. **honua_api_alerts**: API performance and errors
2. **honua_process_framework_alerts**: Process workflow health
3. **honua_circuit_breaker_alerts**: Resilience policy monitoring
4. **honua_cache_alerts**: Cache performance
5. **honua_redis_alerts**: Redis health
6. **honua_llm_alerts**: LLM provider issues
7. **honua_system_alerts**: System resources and health

### Severity Levels
- **Critical**: Immediate action required (paging)
  - ServiceDown
  - CircuitBreakerOpen
  - HighAPIErrorRate
  - APILatencyCritical
  - RedisDown
  - RedisHighMemoryUsage
  - ProcessFrameworkHighFailureRate
  - LLMHighErrorRate
  - HighDiskUsage

- **Warning**: Investigate soon (ticket/email)
  - HighAPILatency
  - ProcessExecutionSlow
  - HighCircuitBreakerFailureRate
  - LowCacheHitRate
  - RedisHighEvictionRate
  - LLMRateLimitHit

## Dashboard Variables

All dashboards support the following standard variables:

| Variable | Type | Purpose |
|----------|------|---------|
| `$datasource` | Datasource | Select Prometheus instance |
| `$job` | Query | Filter by job/service name |

Additional dashboard-specific variables:
- **Process Framework**: `$workflow` (workflow type)
- **Circuit Breaker**: `$service` (external service)
- **API Metrics**: `$protocol` (API standard)
- **Cache Metrics**: `$dataset` (dataset ID)
- **LLM Providers**: `$provider` (LLM provider)

## Thresholds and SLOs

### API Performance
- **Latency SLO**: p95 < 500ms, p99 < 2s
- **Error Rate SLO**: < 1%
- **Availability SLO**: 99.9%

### Process Framework
- **Success Rate SLO**: > 95%
- **Execution Time**: p95 < 30s
- **Concurrent Processes**: < 20 normal, < 50 max

### Cache Performance
- **Hit Rate SLO**: > 90%
- **Render Latency**: p95 < 100ms, p99 < 500ms

### Circuit Breaker
- **Acceptable Failures**: < 5/min per service
- **Break Duration**: 30s standard

### Redis
- **Memory Usage**: < 80% normal, < 90% critical
- **Hit Rate**: > 95%
- **Eviction Rate**: < 10/sec

## Integration

### Prometheus Configuration
Add to `prometheus.yml`:
```yaml
rule_files:
  - /etc/prometheus/alerts/honua-alerts.yml
  - /etc/prometheus/alerts/honua-platform-alerts.yml
  - /etc/prometheus/alerts/recording-rules.yml
```

### Grafana Provisioning
Dashboards are auto-provisioned from:
```
/etc/grafana/provisioning/dashboards/honua/
```

### Alerting Channels
Configure in Prometheus Alertmanager:
- **Critical**: PagerDuty, Slack #incidents
- **Warning**: Email, Slack #alerts
- **Info**: Slack #monitoring

## Best Practices

1. **Time Ranges**: Use 1h for troubleshooting, 24h for trends
2. **Refresh Rates**: 10s for active incidents, 30s for normal ops
3. **Drill Down**: Start with platform overview, drill into specific dashboards
4. **Alert Fatigue**: Adjust thresholds based on baseline performance
5. **Cost Optimization**: Monitor LLM token usage to control costs

## Maintenance

### Dashboard Updates
1. Edit JSON files in `/home/mike/projects/HonuaIO/docker/grafana/dashboards/`
2. Reload Grafana or wait for provisioning cycle (1min)
3. Test changes in development environment first

### Adding New Metrics
1. Add metric to appropriate `*Metrics.cs` class
2. Update dashboard JSON with new panel
3. Add alert rule if critical
4. Document in this README

### Version Control
- All dashboards are JSON files in git
- Use meaningful commit messages
- Tag releases: `monitoring-v1.0.0`

## Troubleshooting

### Dashboard Not Loading
- Check Grafana logs: `docker logs grafana`
- Verify datasource connection
- Validate JSON syntax

### Metrics Missing
- Check Prometheus targets: `http://prometheus:9090/targets`
- Verify metric names in queries
- Check application logs for metrics export

### Alerts Not Firing
- Verify alert rule syntax in Prometheus UI
- Check Alertmanager configuration
- Review alert evaluation intervals

## References

- [Grafana Documentation](https://grafana.com/docs/)
- [Prometheus Query Language](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [OpenTelemetry Metrics](https://opentelemetry.io/docs/specs/otel/metrics/)
- [Honua Observability Guide](/home/mike/projects/HonuaIO/docs/observability/README.md)
