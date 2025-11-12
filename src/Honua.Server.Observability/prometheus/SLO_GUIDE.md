# Honua Server SLI/SLO Guide

## Overview

This guide explains how to use the Service Level Indicators (SLIs) and Service Level Objectives (SLOs) for monitoring Honua Server's reliability and performance.

## Table of Contents

1. [SLO Strategy](#slo-strategy)
2. [Understanding Error Budgets](#understanding-error-budgets)
3. [Prometheus Queries](#prometheus-queries)
4. [Grafana Dashboard Examples](#grafana-dashboard-examples)
5. [Alert Response Playbook](#alert-response-playbook)
6. [Troubleshooting](#troubleshooting)

---

## SLO Strategy

### Target SLOs

| SLO | Target | Error Budget | Downtime/Month |
|-----|--------|--------------|----------------|
| **Availability** | 99.9% (3 nines) | 0.1% | ~43 minutes |
| **Latency (P95)** | < 5 seconds | - | - |
| **Latency (P99)** | < 10 seconds | - | - |
| **Database (P95)** | < 1 second | - | - |
| **Cache Hit Rate** | > 80% | - | - |

### Multi-Window Multi-Burn-Rate Alerting

We use the **Google SRE Workbook** strategy for error budget burn rate alerting:

#### Fast Burn Alert (Critical)
- **Burn Rate**: 14.4x
- **Detection Window**: 1 hour + 5 minutes
- **Consumption**: 2% of monthly budget per hour
- **Budget Exhaustion**: ~2 days at this rate
- **Action**: Immediate response required

#### Slow Burn Alert (Warning)
- **Burn Rate**: 6x
- **Detection Window**: 6 hours + 1 hour
- **Consumption**: ~3.6% of monthly budget per 6 hours
- **Budget Exhaustion**: ~5 days at this rate
- **Action**: Investigate and plan mitigation

### Why Multi-Window Multi-Burn-Rate?

1. **Reduces False Positives**: Requires multiple windows to agree before alerting
2. **Actionable Alerts**: Different burn rates indicate different severities
3. **Time to React**: Fast burns need immediate action, slow burns allow planning
4. **Budget-Aware**: Alerts based on actual error budget consumption, not arbitrary thresholds

---

## Understanding Error Budgets

### What is an Error Budget?

An error budget is the allowed amount of unreliability within your SLO. For a 99.9% SLO:
- Error budget = 100% - 99.9% = **0.1%** (or 0.001)
- This equals approximately **43 minutes** of downtime per month
- Or ~**4.3 hours** per year

### Error Budget Math

```
Error Budget Remaining = Error Budget - (1 - Current Availability)

Example:
- Target: 99.9% (0.999)
- Current: 99.95% (0.9995)
- Error Budget: 0.1% (0.001)
- Consumed: 1 - 0.9995 = 0.0005 (0.05%)
- Remaining: 0.001 - 0.0005 = 0.0005 (0.05%)
- Remaining %: (0.0005 / 0.001) * 100 = 50%
```

### Burn Rate

Burn rate tells you how fast you're consuming your error budget:

```
Burn Rate = (1 - Availability) / Error Budget

Example:
- Current Availability (1h): 99.5% (0.995)
- Error Rate: 1 - 0.995 = 0.005 (0.5%)
- Error Budget: 0.001 (0.1%)
- Burn Rate: 0.005 / 0.001 = 5x

At this rate, you'll exhaust your monthly budget in:
30 days / 5 = 6 days
```

### Error Budget Policy

| Budget Remaining | Action |
|------------------|--------|
| > 50% | Normal operations, new features OK |
| 25-50% | Monitor closely, prioritize reliability |
| 10-25% | Feature freeze, focus on stability |
| < 10% | Emergency: halt features, fix issues |
| Exhausted | Postmortem required, incident review |

---

## Prometheus Queries

### Availability Queries

#### Current Availability (Last 5 minutes)
```promql
honua:availability:success_rate_5m
```

#### Current Availability (Last 24 hours)
```promql
honua:availability:success_rate_1d
```

#### Availability by Endpoint (Last 5 minutes)
```promql
honua:availability:success_rate_by_endpoint_5m
```

### Error Budget Queries

#### Error Budget Remaining (30 days)
```promql
honua:error_budget:remaining_30d
```

#### Error Budget Remaining as Percentage
```promql
honua:error_budget:remaining_percent_30d
```

#### Error Budget Burn Rate (Last hour)
```promql
honua:error_budget:burn_rate_1h
```

#### Days Until Budget Exhausted (at current burn rate)
```promql
30 / honua:error_budget:burn_rate_1h
```

### Latency Queries

#### P95 Latency (Last 5 minutes)
```promql
honua:latency:p95_5m
```

#### P99 Latency (Last 5 minutes)
```promql
honua:latency:p99_5m
```

#### P95 Latency by Endpoint
```promql
honua:latency:p95_by_endpoint_5m
```

#### Percentage of Requests Under 5s
```promql
honua:latency:success_rate_5s_5m * 100
```

### Error Rate Queries

#### 5xx Error Rate (Last 5 minutes)
```promql
honua:errors:5xx_rate_5m * 100
```

#### 4xx Error Rate (Last 5 minutes)
```promql
honua:errors:4xx_rate_5m * 100
```

#### Error Rate by Status Code
```promql
honua:errors:rate_by_status_5m
```

#### Top 5 Endpoints by 5xx Errors
```promql
topk(5, honua:errors:5xx_rate_by_endpoint_5m)
```

### Database Queries

#### Database Query Success Rate
```promql
honua:database:success_rate_5m * 100
```

#### Database P95 Query Latency
```promql
honua:database:query_duration_p95_5m
```

#### Database P95 by Query Type
```promql
honua:database:query_duration_p95_by_type_5m
```

#### Database Connection Pool Utilization
```promql
honua:database:connection_pool_utilization * 100
```

### Cache Queries

#### Cache Hit Rate
```promql
honua:cache:hit_rate_5m * 100
```

#### Cache Hit Rate by Cache Name
```promql
honua:cache:hit_rate_by_cache_5m * 100
```

#### Cache Eviction Rate
```promql
honua:cache:eviction_rate_5m
```

### Infrastructure Queries

#### GC Pause P95
```promql
honua:infrastructure:gc_pause_p95_5m
```

#### Thread Pool Availability
```promql
honua:infrastructure:threadpool_worker_availability * 100
```

#### Memory Pressure
```promql
honua:infrastructure:memory_pressure_percent
```

#### CPU Usage
```promql
honua:infrastructure:cpu_usage_avg_5m
```

---

## Grafana Dashboard Examples

### Dashboard 1: SLO Overview

#### Row 1: Availability SLO

**Panel 1: Availability Gauge (30d)**
```promql
honua:availability:success_rate_30d * 100
```
- Visualization: Gauge
- Thresholds: Red < 99.9%, Yellow < 99.95%, Green >= 99.95%

**Panel 2: Error Budget Remaining**
```promql
honua:error_budget:remaining_percent_30d
```
- Visualization: Gauge
- Thresholds: Red < 10%, Yellow < 50%, Green >= 50%

**Panel 3: Availability Time Series**
```promql
honua:availability:success_rate_5m * 100
honua:availability:success_rate_1h * 100
honua:availability:success_rate_1d * 100
```
- Visualization: Time series
- Legend: 5m, 1h, 1d

**Panel 4: Error Budget Burn Rate**
```promql
honua:error_budget:burn_rate_5m
honua:error_budget:burn_rate_1h
honua:error_budget:burn_rate_6h
```
- Visualization: Time series
- Thresholds: Line at 1.0 (normal), 6.0 (slow burn), 14.4 (fast burn)

#### Row 2: Latency SLO

**Panel 5: P95 Latency Gauge**
```promql
honua:latency:p95_5m
```
- Visualization: Gauge
- Unit: ms
- Thresholds: Red > 5000ms, Yellow > 4000ms, Green < 4000ms

**Panel 6: Latency Percentiles**
```promql
honua:latency:p50_5m
honua:latency:p95_5m
honua:latency:p99_5m
```
- Visualization: Time series
- Legend: P50, P95, P99
- Threshold line at 5000ms

**Panel 7: Requests Meeting SLO**
```promql
honua:latency:success_rate_5s_5m * 100
```
- Visualization: Stat
- Unit: percent
- Thresholds: Red < 95%, Yellow < 98%, Green >= 98%

**Panel 8: Top 5 Slowest Endpoints**
```promql
topk(5, honua:latency:p95_by_endpoint_5m)
```
- Visualization: Bar chart
- Unit: ms

#### Row 3: Error Rates

**Panel 9: 5xx Error Rate**
```promql
honua:errors:5xx_rate_5m * 100
```
- Visualization: Time series
- Unit: percent
- Thresholds: Red > 0.1%, Yellow > 0.05%

**Panel 10: Error Rate by Status Code**
```promql
sum by (http_status_code) (rate(honua_http_requests_total{http_status_code=~"4..|5.."}[5m]))
```
- Visualization: Pie chart

**Panel 11: Top Endpoints by Errors**
```promql
topk(5, honua:errors:5xx_rate_by_endpoint_5m)
```
- Visualization: Table

### Dashboard 2: Database Performance

**Panel 1: Database P95 Latency**
```promql
honua:database:query_duration_p95_5m
```
- Visualization: Gauge
- Thresholds: Red > 1000ms, Yellow > 500ms

**Panel 2: Database Latency by Query Type**
```promql
honua:database:query_duration_p95_by_type_5m
```
- Visualization: Time series

**Panel 3: Database Success Rate**
```promql
honua:database:success_rate_5m * 100
```
- Visualization: Stat

**Panel 4: Connection Pool Utilization**
```promql
honua:database:connection_pool_utilization * 100
```
- Visualization: Gauge
- Thresholds: Red > 80%, Yellow > 60%

**Panel 5: Slow Query Rate**
```promql
honua:database:slow_query_rate_5m
```
- Visualization: Time series

### Dashboard 3: Cache Performance

**Panel 1: Cache Hit Rate**
```promql
honua:cache:hit_rate_5m * 100
```
- Visualization: Gauge
- Thresholds: Red < 80%, Yellow < 90%, Green >= 90%

**Panel 2: Hit Rate by Cache**
```promql
honua:cache:hit_rate_by_cache_5m * 100
```
- Visualization: Time series

**Panel 3: Cache Eviction Rate**
```promql
honua:cache:eviction_rate_5m
```
- Visualization: Time series

**Panel 4: Cache Operation Latency**
```promql
honua:cache:operation_duration_p95_5m
honua:cache:operation_duration_p99_5m
```
- Visualization: Time series

---

## Alert Response Playbook

### ErrorBudgetBurnRateFast (Critical)

**Symptoms:**
- Error budget burning at 14.4x or higher
- Monthly budget will be exhausted in ~2 days

**Immediate Actions:**
1. **Check error logs:**
   ```bash
   kubectl logs -l app=honua-server --tail=100 | grep "ERROR\|CRITICAL"
   ```

2. **Review recent deployments:**
   ```bash
   kubectl rollout history deployment/honua-server
   ```

3. **Check infrastructure health:**
   - CPU/Memory usage
   - Database connection pool
   - Network connectivity

4. **Identify error patterns:**
   ```promql
   topk(10, sum by (http_endpoint, http_status_code) (rate(honua_http_requests_total{http_status_class="5xx"}[5m])))
   ```

5. **Consider rollback:**
   ```bash
   kubectl rollout undo deployment/honua-server
   ```

**Investigation Questions:**
- Was there a recent deployment?
- Are errors isolated to specific endpoints?
- Is there a database/cache issue?
- Are there infrastructure problems?

### ErrorBudgetBurnRateSlow (Warning)

**Symptoms:**
- Error budget burning at 6x
- Monthly budget will be exhausted in ~5 days

**Actions:**
1. **Investigate error patterns** (similar to fast burn)
2. **Review application metrics** for degradation
3. **Check for slow burns over time** (not acute incidents)
4. **Plan mitigation** if burn continues
5. **Schedule postmortem** if pattern continues

### LatencyP95Violation (Critical)

**Symptoms:**
- P95 latency > 5 seconds

**Actions:**
1. **Check slow request logs:**
   ```promql
   topk(10, honua:latency:p95_by_endpoint_5m)
   ```

2. **Review database query performance:**
   ```promql
   honua:database:query_duration_p95_5m
   ```

3. **Check cache hit rates:**
   ```promql
   honua:cache:hit_rate_5m
   ```

4. **Investigate infrastructure:**
   - CPU throttling
   - Memory pressure
   - GC pauses

5. **Review slow query logs** in database

### CacheHitRateLow (Warning)

**Symptoms:**
- Cache hit rate < 80%

**Actions:**
1. **Check cache eviction rate:**
   ```promql
   honua:cache:eviction_rate_5m
   ```

2. **Review cache TTL configuration**

3. **Monitor database load:**
   ```promql
   rate(honua_database_queries[5m])
   ```

4. **Consider cache size increase** or cache warming

---

## Troubleshooting

### No Data for Recording Rules

**Check:**
1. Recording rules loaded:
   ```bash
   curl http://localhost:9090/api/v1/rules | jq '.data.groups[] | select(.name | contains("honua"))'
   ```

2. Base metrics available:
   ```promql
   honua_http_requests_total
   ```

3. Prometheus logs:
   ```bash
   docker logs prometheus
   ```

### Recording Rules Not Evaluating

**Check:**
1. Rule evaluation interval in prometheus.yml
2. Metric name typos (underscore vs dot notation)
3. Label mismatches

### Alerts Not Firing

**Check:**
1. Alertmanager connected:
   ```bash
   curl http://localhost:9090/api/v1/alertmanagers
   ```

2. Alert rule syntax:
   ```bash
   promtool check rules recording-rules.yml
   ```

3. Alert state:
   ```bash
   curl http://localhost:9090/api/v1/alerts | jq '.data.alerts'
   ```

### Metric Name Issues

The recording rules use **underscore notation** for metric names, but the actual metrics may use **dot notation**:

- Recording rule: `honua_http_requests_total`
- Actual metric: `honua.http.requests.total`

**Fix:** Prometheus automatically converts dots to underscores when scraping metrics, so `honua.http.requests.total` becomes `honua_http_requests_total` in Prometheus.

**Verify:**
```promql
# Check if metric exists with underscores
count(honua_http_requests_total)

# Check if metric exists with dots
count({__name__=~"honua\\.http.*"})
```

---

## Best Practices

### 1. Error Budget Review Cadence

- **Daily**: Check error budget remaining
- **Weekly**: Review burn rate trends
- **Monthly**: Postmortem if budget exhausted

### 2. SLO Adjustment

SLOs should be:
- **Achievable**: Based on historical performance
- **Meaningful**: Aligned with user expectations
- **Reviewed**: Quarterly or after major changes

### 3. Alert Fatigue

Avoid:
- Too many alerts (causes alert fatigue)
- Unactionable alerts (no clear response)
- Duplicate alerts (same issue, multiple alerts)

Prefer:
- Multi-window multi-burn-rate (reduces noise)
- Clear severity levels (critical vs warning)
- Actionable descriptions (what to do)

### 4. Dashboard Design

- **SLO Overview**: High-level health at a glance
- **Component Dashboards**: Deep dives into specific areas
- **Correlation**: Show related metrics together
- **Annotations**: Mark deployments, incidents

---

## References

- [Google SRE Workbook - Alerting on SLOs](https://sre.google/workbook/alerting-on-slos/)
- [Google SRE Book - Service Level Objectives](https://sre.google/sre-book/service-level-objectives/)
- [Prometheus Recording Rules](https://prometheus.io/docs/prometheus/latest/configuration/recording_rules/)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/best-practices/dashboards/)

---

## Quick Reference Card

### Key Metrics

| Metric | Query | Good | Warning | Critical |
|--------|-------|------|---------|----------|
| Availability | `honua:availability:success_rate_30d` | > 99.95% | < 99.95% | < 99.9% |
| Error Budget | `honua:error_budget:remaining_percent_30d` | > 50% | < 50% | < 10% |
| Burn Rate | `honua:error_budget:burn_rate_1h` | < 1x | 6x | > 14.4x |
| P95 Latency | `honua:latency:p95_5m` | < 3s | < 5s | > 5s |
| P99 Latency | `honua:latency:p99_5m` | < 5s | < 10s | > 10s |
| Cache Hit | `honua:cache:hit_rate_1h` | > 90% | < 90% | < 80% |
| DB P95 | `honua:database:query_duration_p95_5m` | < 500ms | < 1s | > 1s |

### Common PromQL Patterns

**Calculate availability over custom window:**
```promql
1 - (
  sum(rate(honua_http_requests_total{http_status_class="5xx"}[2h]))
  /
  sum(rate(honua_http_requests_total[2h]))
)
```

**Calculate burn rate over custom window:**
```promql
(1 - <availability_query>) / 0.001
```

**Days until budget exhausted:**
```promql
30 / honua:error_budget:burn_rate_1h
```

**Requests per second:**
```promql
sum(rate(honua_http_requests_total[5m]))
```

**Error rate:**
```promql
sum(rate(honua_http_requests_total{http_status_class="5xx"}[5m]))
/
sum(rate(honua_http_requests_total[5m]))
```
