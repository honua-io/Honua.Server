# Observability Deployment Checklist

Use this checklist to ensure proper observability configuration when deploying Honua Server to production.

## Pre-Deployment Checklist

### 1. Metrics Configuration

- [ ] Verify `observability.metrics.enabled` is `true` in production config
- [ ] Confirm metrics endpoint is set (default: `/metrics`)
- [ ] Test metrics endpoint accessibility with authentication
- [ ] Create service account with Viewer role for Prometheus scraping

**Command to test:**
```bash
curl -u metrics-user:password https://your-server/metrics
```

**Expected:** Prometheus text format output

### 2. Prometheus Setup

- [ ] Add Honua Server to Prometheus scrape configuration
- [ ] Configure authentication credentials in Prometheus
- [ ] Set appropriate scrape interval (15-30 seconds recommended)
- [ ] Verify Prometheus can reach the metrics endpoint
- [ ] Check Prometheus targets page shows Honua Server as "UP"

**Prometheus config example:**
```yaml
scrape_configs:
  - job_name: 'honua-server-prod'
    scrape_interval: 15s
    static_configs:
      - targets: ['honua-server:5000']
    metrics_path: '/metrics'
    basic_auth:
      username: 'metrics-scraper'
      password: '${METRICS_PASSWORD}'
```

### 3. Grafana Dashboards

- [ ] Import Grafana dashboards from `src/Honua.Server.Observability/grafana/dashboards/`
- [ ] Configure Prometheus data source in Grafana
- [ ] Verify dashboards display data correctly
- [ ] Set up dashboard permissions for operations team
- [ ] Configure dashboard refresh intervals

**Dashboards to import:**
- `honua-overview.json` - High-level system metrics
- Additional feature-specific dashboards as needed

### 4. Distributed Tracing (Optional but Recommended)

- [ ] Deploy tracing backend (Jaeger, Tempo, Grafana Cloud, etc.)
- [ ] Set `observability.tracing.exporter` to `otlp`
- [ ] Configure `observability.tracing.otlpEndpoint`
- [ ] Test trace collection with sample requests
- [ ] Verify traces appear in tracing UI (Jaeger, Grafana)

**Environment variables:**
```bash
export observability__tracing__exporter=otlp
export observability__tracing__otlpEndpoint=http://tempo:4317
```

### 5. Request Logging

- [ ] Verify `observability.requestLogging.enabled` is `true`
- [ ] Confirm `logHeaders` is `false` (security best practice)
- [ ] Set appropriate `slowThresholdMs` (default: 5000ms)
- [ ] Configure log aggregation (ELK, Loki, Splunk, etc.)
- [ ] Test log collection and searching

**Log aggregation setup:**
- Ensure logs are in JSON format (`observability.logging.jsonConsole: true`)
- Configure log shipping (Filebeat, Fluentd, Promtail, etc.)
- Set up log retention policies

### 6. Alerting

- [ ] Import Prometheus alert rules from `src/Honua.Server.Observability/prometheus/alerts.yml`
- [ ] Configure Alertmanager notification channels (email, Slack, PagerDuty)
- [ ] Test critical alerts (high error rate, service down, etc.)
- [ ] Document alert response procedures
- [ ] Set up on-call rotation for critical alerts

**Key alerts to configure:**
- High error rate (5xx responses)
- High latency (P95 > threshold)
- Service availability
- Database connection issues
- Memory/CPU usage

### 7. Security

- [ ] Metrics endpoint requires authentication (except QuickStart mode)
- [ ] Request logging does NOT log headers by default
- [ ] Tracing does not include PII or sensitive data
- [ ] Review logs for any accidental sensitive data exposure
- [ ] Configure network policies to restrict metrics endpoint access

**Security verification:**
```bash
# Should return 401 without authentication
curl https://your-server/metrics

# Should return metrics with authentication
curl -u viewer:password https://your-server/metrics
```

### 8. Performance Validation

- [ ] Measure CPU overhead (expect ~1-3% increase)
- [ ] Monitor memory usage (expect ~10-50 MB increase)
- [ ] Verify no significant latency impact on requests
- [ ] Check disk I/O for log volume
- [ ] Validate network traffic for metrics scraping

**Performance benchmarks:**
- Metrics collection: ~1-2% CPU overhead
- Request logging: ~0.5-1% CPU overhead
- Distributed tracing: ~2-5% CPU overhead (if enabled)

## Post-Deployment Verification

### 1. Metrics Validation (15 minutes after deployment)

- [ ] Check Prometheus targets page - Honua Server should be "UP"
- [ ] Verify metrics are being scraped (check last scrape time)
- [ ] Open Grafana dashboards and verify data is flowing
- [ ] Check for any missing or zero-valued metrics

**Prometheus queries to verify:**
```promql
# Should show non-zero values
up{job="honua-server-prod"}
http_requests_total
http_request_duration_seconds_count
```

### 2. Logging Validation

- [ ] Verify logs are appearing in log aggregation system
- [ ] Test log search functionality
- [ ] Confirm structured logging format is preserved
- [ ] Check log volume and retention settings
- [ ] Validate log timestamps and metadata

**Sample log search queries:**
```
# Search for errors
level:error AND service:honua-server

# Search for slow requests
properties.ElapsedMs:>5000

# Search for specific endpoint
properties.RequestPath:"/api/layers"
```

### 3. Tracing Validation (if enabled)

- [ ] Generate sample requests through the system
- [ ] Verify traces appear in tracing UI
- [ ] Check trace completeness (all spans present)
- [ ] Validate span timing and relationships
- [ ] Test trace search and filtering

**Test trace generation:**
```bash
# Make sample requests to generate traces
curl -u user:pass https://your-server/api/layers
curl -u user:pass https://your-server/stac/collections
curl -u user:pass https://your-server/ogc/tiles
```

### 4. Alerting Validation

- [ ] Trigger test alert (if safe to do so)
- [ ] Verify alert notification delivery
- [ ] Check alert routing to correct channels
- [ ] Test alert acknowledgment workflow
- [ ] Validate alert escalation policies

### 5. Dashboard Review

- [ ] Review all Grafana dashboards for completeness
- [ ] Verify all panels show data (no empty panels)
- [ ] Check dashboard variables and filters work correctly
- [ ] Validate time range selectors
- [ ] Confirm dashboard auto-refresh is working

## Rollback Plan

If observability is causing issues:

### Quick Disable (Environment Variables)

```bash
# Disable metrics
export observability__metrics__enabled=false

# Disable request logging
export observability__requestLogging__enabled=false

# Disable tracing
export observability__tracing__exporter=none

# Restart service
systemctl restart honua-server
```

### Configuration File Rollback

Create or update `appsettings.Production.json`:

```json
{
  "observability": {
    "metrics": {
      "enabled": false
    },
    "requestLogging": {
      "enabled": false
    },
    "tracing": {
      "exporter": "none"
    }
  }
}
```

## Troubleshooting

### Issue: Metrics endpoint returns 401 Unauthorized

**Cause:** Authentication required for metrics endpoint

**Solution:**
1. Create service account with Viewer role
2. Configure Prometheus with basic auth credentials
3. Or disable auth in QuickStart mode (dev only)

### Issue: High memory usage after enabling metrics

**Cause:** High metric cardinality (too many unique label combinations)

**Solution:**
1. Review custom metrics and reduce label cardinality
2. Increase Prometheus scrape interval
3. Adjust metric retention settings

### Issue: Traces not appearing in Jaeger/Tempo

**Cause:** OTLP endpoint configuration issue

**Solution:**
1. Verify `observability.tracing.exporter` is set to `otlp`
2. Check `observability.tracing.otlpEndpoint` is correct
3. Verify network connectivity to tracing backend
4. Check tracing backend logs for errors

### Issue: Logs too verbose in production

**Cause:** Default log levels may be too detailed

**Solution:**
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning",
      "Override": {
        "Honua": "Information"
      }
    }
  }
}
```

## Monitoring Schedule

### Daily
- [ ] Check Grafana dashboards for anomalies
- [ ] Review critical alerts
- [ ] Verify metrics scraping is working

### Weekly
- [ ] Review slow request logs
- [ ] Check error rate trends
- [ ] Validate log aggregation and retention

### Monthly
- [ ] Review alert rules and thresholds
- [ ] Update Grafana dashboards as needed
- [ ] Audit metrics cardinality and optimize
- [ ] Review tracing sampling rate

## Documentation Links

- [Observability Migration Guide](observability-migration-guide.md)
- [Configuration Guide](README.md)
- [Distributed Tracing Guide](../architecture/tracing.md)
- [Observability README](../../src/Honua.Server.Observability/README.md)

## Support

If you need help with observability configuration:

1. Review the documentation links above
2. Check the [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
3. Contact support: support@honua.io

---

**Remember:** Observability is critical for production systems. Don't disable it unless absolutely necessary, and always have monitoring in place before deployment.
