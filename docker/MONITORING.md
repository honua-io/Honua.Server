# Honua Monitoring & Observability

This document provides comprehensive guidance for monitoring and observing the Honua geospatial server.

## Stack Components

### Application
- **Honua Server** (`web`): Main geospatial API server
  - Exposes metrics at `/metrics` endpoint
  - Configured via environment variables and `appsettings.json`

### Observability
- **Prometheus** (`prometheus`): Metrics collection and alerting
  - Scrapes Honua metrics every 10 seconds
  - Evaluates alert rules every 30 seconds
  - UI available at `http://localhost:9090`

- **Grafana** (`grafana`): Metrics visualization and dashboards
  - Pre-configured datasources and dashboards
  - UI available at `http://localhost:3000`
  - Default credentials: `admin` / `admin`

- **Alertmanager** (optional): Alert routing and notification
  - Configurable notification channels (email, Slack, PagerDuty)
  - UI available at `http://localhost:9093`

## Grafana Dashboards

Three pre-configured dashboards are available:

1. **Honua Overview** (`honua-overview.json`)
   - High-level service metrics
   - Request rate, error rate, latency
   - Suitable for NOC/operations display

2. **Honua Detailed** (`honua-detailed.json`)
   - Comprehensive monitoring across all subsystems
   - API performance by protocol
   - Raster cache metrics
   - Error analysis and diagnostics
   - System resource utilization

3. **Honua SLO** (`honua-slo.json`)
   - Service Level Objective tracking
   - 30-day availability and latency SLOs
   - Error budget burn rate
   - Compliance trending

### Accessing Dashboards
1. Navigate to Grafana: `http://localhost:3000`
2. Login with default credentials: `admin` / `admin`
3. Browse to **Dashboards** → **Honua**

## Alert Rules

Alert rules are defined in `/prometheus/alerts/`:
- `honua-alerts.yml`: Core service alerts (40+ alerts)
- `recording-rules.yml`: Pre-aggregated queries for dashboard performance

### Key Alert Groups
1. **Availability**: Service down, high error rate, SLO breaches
2. **Performance**: High latency (warning & critical thresholds)
3. **Resources**: Memory, CPU, thread pool, GC metrics
4. **Raster Cache**: Low hit rate, high render latency, preseed failures
5. **Errors**: Database, storage, security, OOM errors
6. **Business Metrics**: Low traffic, protocol-specific monitoring

## Performance Baselines

See `docs/observability/performance-baselines.md` for detailed SLOs and expected metrics.

### Quick Reference
- **Availability SLO**: 99.9% (43.2 min downtime/month)
- **Latency SLO**: P95 < 2000ms
- **Raster Cache Hit Rate**: > 70%
- **Memory Usage**: 2-4 GB baseline, 8 GB warning, 12 GB critical
- **CPU Usage**: 10-30% baseline, 80% warning, 95% critical

## Alertmanager Configuration

Configure notification channels in `alertmanager/config.yml`:

```yaml
# Example: Slack notifications
receivers:
  - name: 'slack'
    slack_configs:
      - api_url: 'YOUR_WEBHOOK_URL'
        channel: '#honua-alerts'

# Example: PagerDuty integration
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_SERVICE_KEY'
```

## Related Documentation

- [Performance Baselines](../docs/observability/performance-baselines.md)
- [Prometheus Configuration](./prometheus/prometheus.yml)
- [Grafana Dashboards](./grafana/dashboards/)
- [Alert Rules](./prometheus/alerts/)
