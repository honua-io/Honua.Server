# SLI/SLO Implementation Summary

## Files Created

### 1. `/prometheus/recording-rules.yml`
**Production-ready Prometheus recording rules** with comprehensive SLI/SLO coverage:

#### Coverage:
- ✅ **Availability SLI**: 99.9% uptime target across 7 time windows (5m, 30m, 1h, 6h, 1d, 3d, 30d)
- ✅ **Error Budget Tracking**: Remaining budget, burn rates, and percentage calculations
- ✅ **Latency SLI**: P50, P95, P99 across multiple time windows
- ✅ **Error Rate SLI**: Separate tracking for 4xx (client) and 5xx (server) errors
- ✅ **Database SLI**: Query latency, success rate, connection pool health
- ✅ **Cache SLI**: Hit rate, miss rate, operation latency, eviction tracking
- ✅ **Infrastructure SLI**: GC pauses, memory pressure, thread pool health

#### Alert Rules (Google SRE Multi-Window Multi-Burn-Rate):
- 🔴 **Critical: ErrorBudgetBurnRateFast** - 14.4x burn rate, 1h window (budget exhausted in 2 days)
- 🟡 **Warning: ErrorBudgetBurnRateSlow** - 6x burn rate, 6h window (budget exhausted in 5 days)
- 🔴 **Critical: LatencyP95Violation** - P95 > 5s
- 🟡 **Warning: LatencyP95Degradation** - P95 > 4s (approaching limit)
- 🔴 **Critical: DatabaseLatencyP95Violation** - DB P95 > 1s
- 🟡 **Warning: CacheHitRateLow** - Cache hit rate < 80%
- 🔴 **Critical: ErrorBudgetExhausted** - Monthly budget completely consumed

### 2. `/prometheus/SLO_GUIDE.md`
**Comprehensive operations guide** covering:
- SLO strategy and targets
- Error budget mathematics and interpretation
- 50+ Prometheus query examples
- Grafana dashboard panel configurations
- Alert response playbooks
- Troubleshooting guide
- Best practices and quick reference card

### 3. `/prometheus/grafana-slo-dashboard.json`
**Importable Grafana dashboard** with:
- Availability gauge (30d)
- Error budget remaining gauge
- P95 latency gauge
- Cache hit rate gauge
- Availability time series
- Error budget burn rate chart
- Latency percentiles (P50, P95, P99)
- Error rates (4xx vs 5xx)
- Database query latency by type
- Cache performance metrics
- Top 5 slowest endpoints
- Top 5 endpoints by error rate

### 4. `/prometheus/prometheus.yml` (Updated)
Added recording-rules.yml to the `rule_files` configuration.

## Quick Start

### 1. Verify Metrics Are Being Collected

Check that base metrics exist:
```bash
curl http://localhost:9090/api/v1/query?query=honua_http_requests_total | jq
```

### 2. Load Recording Rules

If using Docker:
```bash
docker-compose -f docker-compose.monitoring.yml restart prometheus
```

Or reload Prometheus:
```bash
curl -X POST http://localhost:9090/-/reload
```

### 3. Verify Recording Rules Are Loaded

```bash
curl http://localhost:9090/api/v1/rules | jq '.data.groups[] | select(.name | contains("honua"))'
```

### 4. Test a Recording Rule

```promql
honua:availability:success_rate_5m
```

Should return a value between 0 and 1 (e.g., 0.999 = 99.9% availability).

### 5. Import Grafana Dashboard

1. Open Grafana (http://localhost:3000)
2. Go to **Dashboards > Import**
3. Upload `grafana-slo-dashboard.json`
4. Select Prometheus as the data source
5. Click **Import**

## Key Metrics to Monitor

### Daily Operations

```promql
# Overall health check
honua:availability:success_rate_1d > 0.999  # Should be true
honua:error_budget:remaining_percent_30d > 10  # Should have budget left
honua:latency:p95_5m < 5000  # P95 under 5s
honua:cache:hit_rate_1h > 0.80  # Cache hit rate > 80%
```

### SLO Compliance Check

```bash
# Availability compliance (30 day)
curl -s http://localhost:9090/api/v1/query?query=honua:availability:success_rate_30d | jq -r '.data.result[0].value[1]'

# Error budget remaining (as percentage)
curl -s http://localhost:9090/api/v1/query?query=honua:error_budget:remaining_percent_30d | jq -r '.data.result[0].value[1]'
```

### Alert State

```bash
# Check current alerts
curl http://localhost:9090/api/v1/alerts | jq '.data.alerts[] | select(.state == "firing")'
```

## Metric Name Mapping

Prometheus automatically converts metric names from **dot notation** to **underscore notation**:

| C# Code (emitted) | Prometheus (stored) |
|-------------------|---------------------|
| `honua.http.requests.total` | `honua_http_requests_total` |
| `honua.http.request.duration` | `honua_http_request_duration` |
| `honua.database.query_duration` | `honua_database_query_duration` |
| `honua.cache.hits` | `honua_cache_hits` |

The recording rules use **underscore notation** as that's how they're stored in Prometheus.

## Understanding the Metrics

### Availability

```promql
honua:availability:success_rate_5m
```
- **Returns**: 0 to 1 (0.999 = 99.9%)
- **Formula**: 1 - (5xx_errors / total_requests)
- **Good**: > 0.999 (99.9%)
- **Bad**: < 0.999

### Error Budget Remaining

```promql
honua:error_budget:remaining_percent_30d
```
- **Returns**: -100 to 100
- **Formula**: ((error_budget - consumed) / error_budget) * 100
- **Good**: > 50%
- **Warning**: 10-50%
- **Critical**: < 10%
- **Exhausted**: < 0%

### Burn Rate

```promql
honua:error_budget:burn_rate_1h
```
- **Returns**: 0 to infinity
- **Formula**: (1 - availability) / error_budget
- **Normal**: ~1.0
- **Slow Burn**: > 6.0
- **Fast Burn**: > 14.4

### Latency

```promql
honua:latency:p95_5m
```
- **Returns**: Milliseconds
- **Target**: < 5000ms (5 seconds)
- **Warning**: > 4000ms
- **Critical**: > 5000ms

## Example Queries

### How much error budget do I have left?

```promql
honua:error_budget:remaining_percent_30d
```

### At current rate, when will error budget be exhausted?

```promql
30 / honua:error_budget:burn_rate_1h
```

### What's my current availability?

```promql
honua:availability:success_rate_1d * 100
```

### Which endpoints are slowest?

```promql
topk(5, honua:latency:p95_by_endpoint_5m)
```

### Which endpoints have the most errors?

```promql
topk(5, honua:errors:5xx_rate_by_endpoint_5m)
```

### Is my cache effective?

```promql
honua:cache:hit_rate_1h * 100
```

### How's database performance?

```promql
honua:database:query_duration_p95_5m
```

## Troubleshooting

### Recording Rules Not Showing Data

1. **Check base metrics exist:**
   ```promql
   count(honua_http_requests_total)
   ```

2. **Check recording rule evaluation:**
   ```bash
   curl http://localhost:9090/api/v1/rules | jq '.data.groups[] | select(.name == "honua_sli_availability")'
   ```

3. **Check Prometheus logs:**
   ```bash
   docker logs prometheus 2>&1 | grep -i "error\|warning"
   ```

### Metrics Use Wrong Naming Convention

If metrics are emitted with dots but queries use underscores (or vice versa):

```promql
# Check what naming convention is used
{__name__=~"honua.*"}
```

Prometheus should auto-convert dots to underscores, so:
- C# emits: `honua.http.requests.total`
- Prometheus stores: `honua_http_requests_total`
- Queries use: `honua_http_requests_total`

### Histogram Queries Return No Data

Ensure you're using `_bucket` suffix for histogram quantile queries:

```promql
# Correct
histogram_quantile(0.95, rate(honua_http_request_duration_bucket[5m]))

# Wrong (will not work)
histogram_quantile(0.95, rate(honua_http_request_duration[5m]))
```

### Alert Not Firing

1. **Check alert exists:**
   ```bash
   curl http://localhost:9090/api/v1/rules | jq '.data.groups[].rules[] | select(.type == "alerting")'
   ```

2. **Check alert expression manually:**
   ```promql
   honua:error_budget:burn_rate_1h > 14.4
   ```

3. **Check Alertmanager is configured:**
   ```bash
   curl http://localhost:9090/api/v1/alertmanagers
   ```

## Production Deployment Checklist

- [ ] Recording rules loaded in Prometheus
- [ ] Alert rules configured in Alertmanager
- [ ] Grafana dashboards imported
- [ ] Team trained on SLO interpretation
- [ ] Runbooks created for each alert
- [ ] Error budget policy documented
- [ ] SLO review cadence established (weekly/monthly)
- [ ] Dashboard URLs shared with team
- [ ] Alert channels configured (Slack, PagerDuty, etc.)
- [ ] Test alerts triggered to verify configuration

## Next Steps

1. **Monitor for 1 week** to establish baseline
2. **Adjust thresholds** if needed based on real-world data
3. **Create runbooks** for each alert type
4. **Set up alert routing** to appropriate teams
5. **Schedule SLO reviews** (monthly recommended)
6. **Document postmortems** when SLOs are violated
7. **Iterate on SLOs** based on user feedback and business needs

## Support

- **Documentation**: See `SLO_GUIDE.md` for detailed information
- **Queries**: See `SLO_GUIDE.md` Prometheus Queries section
- **Dashboards**: See `SLO_GUIDE.md` Grafana Dashboard Examples section
- **Alerts**: See `SLO_GUIDE.md` Alert Response Playbook section

## References

- Google SRE Workbook: https://sre.google/workbook/alerting-on-slos/
- Prometheus Recording Rules: https://prometheus.io/docs/prometheus/latest/configuration/recording_rules/
- Metric Names in Honua: See `*Metrics.cs` files in `Honua.Server.Core/Observability/`
