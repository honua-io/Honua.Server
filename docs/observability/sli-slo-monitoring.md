# SLI/SLO Monitoring Guide

## Overview

Service Level Indicators (SLIs) and Service Level Objectives (SLOs) are fundamental to site reliability engineering (SRE). This guide explains how Honua Server implements SLI/SLO monitoring using Prometheus recording rules and provides guidance on tracking error budgets.

### What Are SLIs and SLOs?

- **SLI (Service Level Indicator)**: A quantitative measure of service level
  - Example: "99.9% of HTTP requests return successfully"
  - Example: "95th percentile latency is under 5 seconds"

- **SLO (Service Level Objective)**: A target value or range for an SLI
  - Example: "We target 99.9% availability"
  - Example: "We target P95 latency < 5 seconds"

- **Error Budget**: The allowed amount of unreliability before breaking your SLO
  - For 99.9% availability SLO, error budget is 0.1%
  - For 30 days: ~43 minutes of downtime allowed

### Why SLI/SLO Monitoring Matters

1. **Objective Reliability Targets**: Move from "the site is slow" to "P95 latency exceeds 5s"
2. **Prioritization**: Balance feature work vs. reliability improvements
3. **Risk Management**: Know when you're at risk of breaking your SLO
4. **Customer Communication**: Transparent about service reliability

---

## Honua's SLO Targets

### Default SLOs

| Service | SLI | Target SLO | Error Budget | Alert Threshold |
|---------|-----|------------|--------------|-----------------|
| **HTTP API** | Availability | 99.9% | 0.1% | 10x burn rate |
| **HTTP API** | Latency (P95) | < 5 seconds | 5% slow requests | P95 > 5s for 10m |
| **Database** | Query Success Rate | 99.95% | 0.05% | 5x burn rate |
| **Build Queue** | Success Rate | 99% | 1% | 20% failure rate |
| **Cache** | Hit Rate | > 70% | N/A | < 50% for 15m |

### Time Windows

SLIs are calculated over multiple time windows:
- **5 minutes**: Fast feedback for incidents
- **1 hour**: Short-term trending
- **24 hours**: Daily performance
- **30 days**: SLO compliance period

---

## Implementation

### Recording Rules

Honua Server uses Prometheus recording rules to pre-compute SLI metrics. These rules run every 30-60 seconds and create efficient time-series data.

**Location**: `/home/user/Honua.Server/src/Honua.Server.Observability/prometheus/recording-rules.yml`

### HTTP Availability SLI

```yaml
# 5-minute availability (1 = 100%, 0 = 0%)
- record: honua:availability:ratio_5m
  expr: |
    1 - (
      rate(honua.http.requests.total{http.status_class="5xx"}[5m]) /
      (rate(honua.http.requests.total[5m]) > 0)
    )
```

**What it measures**: Percentage of HTTP requests that don't return 5xx errors

**Query in Grafana**:
```promql
honua:availability:ratio_5m * 100
```

### Error Budget

```yaml
# Error budget remaining (positive = good, negative = over budget)
- record: honua:error_budget:remaining_5m
  expr: |
    0.001 - (1 - honua:availability:ratio_5m)
```

**What it measures**: How much error budget is left (for 99.9% SLO)
- **Positive**: You're within budget
- **Negative**: You've exceeded your error budget

**Query in Grafana**:
```promql
honua:error_budget:remaining_5m * 100
```

### Error Budget Burn Rate

```yaml
# Error budget burn rate (> 1.0 = burning faster than allowed)
- record: honua:error_budget:burn_rate_5m
  expr: |
    (1 - honua:availability:ratio_5m) / 0.001
```

**What it measures**: How fast you're consuming error budget
- **< 1.0**: Burning slower than allowed (good!)
- **1.0**: Burning at exactly the allowed rate
- **> 1.0**: Burning faster than allowed (warning!)
- **> 10.0**: Burning 10x faster (critical!)

---

## Monitoring Error Budgets

### Grafana Dashboards

#### 1. Availability Overview Panel

```json
{
  "targets": [
    {
      "expr": "honua:availability:ratio_5m * 100",
      "legendFormat": "Current Availability"
    },
    {
      "expr": "99.9",
      "legendFormat": "SLO Target (99.9%)"
    }
  ],
  "title": "HTTP Availability (5m)",
  "type": "graph"
}
```

#### 2. Error Budget Panel

```json
{
  "targets": [
    {
      "expr": "honua:error_budget:remaining_5m * 100",
      "legendFormat": "Error Budget Remaining (%)"
    }
  ],
  "title": "Error Budget (5m)",
  "type": "graph",
  "thresholds": [
    {
      "value": 0,
      "colorMode": "critical"
    }
  ]
}
```

#### 3. Burn Rate Panel

```json
{
  "targets": [
    {
      "expr": "honua:error_budget:burn_rate_5m",
      "legendFormat": "Burn Rate (5m)"
    },
    {
      "expr": "1.0",
      "legendFormat": "Allowed Rate"
    },
    {
      "expr": "10.0",
      "legendFormat": "Critical Threshold"
    }
  ],
  "title": "Error Budget Burn Rate",
  "type": "graph"
}
```

### Complete SLO Dashboard

Create a comprehensive SLO dashboard with these panels:

1. **Availability Gauge** - Current availability vs. target
2. **Error Budget Remaining** - Time series of error budget
3. **Burn Rate** - Current burn rate
4. **Request Rate** - Total request volume
5. **Error Rate** - 5xx error rate over time
6. **Latency Percentiles** - P50, P95, P99 latency
7. **Multi-Window Availability** - 5m, 1h, 24h, 30d

---

## Alert Strategy

### Multi-Window Multi-Burn-Rate Alerting

Honua uses Google's recommended multi-window multi-burn-rate alerting strategy to reduce false positives while maintaining fast detection.

#### Critical Alert (Fast Burn)

**Scenario**: You're burning error budget 10x faster than allowed

```yaml
- alert: HighErrorBudgetBurn
  expr: |
    (
      rate(honua.http.requests.total{http.status_class=~"5xx"}[5m]) /
      rate(honua.http.requests.total[5m])
    ) > 0.10
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "High error budget burn rate"
    description: "Error rate is {{ $value | humanizePercentage }} (threshold: 10%). SLO at risk."
```

**When it fires**: 10% error rate for 5 minutes (10x normal)
**Action**: Page on-call engineer immediately

#### Warning Alert (Slow Burn)

**Scenario**: You're burning error budget 2x faster than allowed

```yaml
- alert: ModerateErrorBudgetBurn
  expr: |
    (
      rate(honua.http.requests.total{http.status_class=~"5xx"}[1h]) /
      rate(honua.http.requests.total[1h])
    ) > 0.02
  for: 1h
  labels:
    severity: warning
  annotations:
    summary: "Moderate error budget burn"
    description: "Error rate is {{ $value | humanizePercentage }} over 1 hour (threshold: 2%)."
```

**When it fires**: 2% error rate for 1 hour (2x normal)
**Action**: Create ticket, investigate during business hours

### Multi-Window Validation

To reduce false positives, combine short and long windows:

```yaml
- alert: SustainedHighErrorRate
  expr: |
    (
      rate(honua.http.requests.total{http.status_class=~"5xx"}[5m]) > 0.10
      AND
      rate(honua.http.requests.total{http.status_class=~"5xx"}[1h]) > 0.02
    )
  for: 2m
  labels:
    severity: critical
  annotations:
    summary: "Sustained high error rate detected"
```

**What this prevents**: Alerting on short-lived spikes that don't impact the overall SLO

---

## Interpreting SLO Metrics

### Example Scenarios

#### Scenario 1: Healthy System

```promql
honua:availability:ratio_5m = 0.9998        # 99.98% availability
honua:error_budget:remaining_5m = 0.0008    # 0.08% error budget remaining (positive = good)
honua:error_budget:burn_rate_5m = 0.2       # Burning at 20% of allowed rate
```

**Interpretation**: System is healthy, plenty of error budget remaining

#### Scenario 2: Minor Incident

```promql
honua:availability:ratio_5m = 0.995         # 99.5% availability
honua:error_budget:remaining_5m = -0.004    # -0.4% (negative = over budget)
honua:error_budget:burn_rate_5m = 5.0       # Burning at 5x allowed rate
```

**Interpretation**: Recent incident consumed error budget, but not critical yet

#### Scenario 3: Critical Incident

```promql
honua:availability:ratio_5m = 0.90          # 90% availability
honua:error_budget:remaining_5m = -0.099    # -9.9% (way over budget)
honua:error_budget:burn_rate_5m = 100.0     # Burning at 100x allowed rate
```

**Interpretation**: Major outage, immediate action required

---

## Latency SLI

### P95 Latency Recording Rule

```yaml
# 95th percentile latency over 5 minutes
- record: honua:latency:p95_5m
  expr: |
    histogram_quantile(0.95,
      sum(rate(honua.http.request.duration[5m])) by (le, http.endpoint)
    )
```

### Latency SLO Compliance

```yaml
# Percentage of time P95 latency meets SLO (< 5000ms)
- record: honua:latency:slo_compliance_5m
  expr: |
    (honua:latency:p95_5m < 5000)
```

**Query Result**:
- `1` = Latency meets SLO
- `0` = Latency exceeds SLO

### Latency Alert

```yaml
- alert: HighHTTPLatency
  expr: honua:latency:p95_5m > 5000
  for: 10m
  labels:
    severity: warning
  annotations:
    summary: "High HTTP request latency"
    description: "P95 latency is {{ $value }}ms (threshold: 5000ms)."
```

---

## Database SLI

### Query Success Rate

```yaml
- record: honua:database:success_rate_5m
  expr: |
    rate(honua.database.queries{success="True"}[5m]) /
    (rate(honua.database.queries[5m]) > 0)
```

**SLO Target**: 99.95% success rate

### Database Latency

```yaml
- record: honua:database:query_duration:p95_5m
  expr: |
    histogram_quantile(0.95,
      sum(rate(honua.database.query_duration[5m])) by (le, query.type)
    )
```

**SLO Target**: P95 query duration < 2 seconds

---

## Build Queue SLI

### Build Success Rate

```yaml
- record: honua:builds:success_rate_5m
  expr: |
    rate(build_success_total[5m]) /
    ((rate(build_success_total[5m]) + rate(build_failure_total[5m])) > 0)
```

**SLO Target**: 99% build success rate

### Build Duration

```yaml
- record: honua:builds:duration:p95_5m
  expr: |
    histogram_quantile(0.95,
      sum(rate(build_duration_seconds[5m])) by (le, tier)
    )
```

**SLO Target**: P95 build duration < 300 seconds (5 minutes)

---

## Service Health Score

### Composite Health Score

Honua computes an overall service health score combining multiple SLIs:

```yaml
- record: honua:service:health_score_5m
  expr: |
    (
      honua:availability:ratio_5m * 0.4 +
      ((honua:latency:p95_5m < 5000) or 0) * 0.3 +
      honua:database:success_rate_5m * 0.2 +
      (honua:infrastructure:threadpool_availability > 0.5) * 0.1
    )
```

**Weights**:
- 40% - HTTP availability
- 30% - Latency compliance
- 20% - Database health
- 10% - Infrastructure health

**Score Range**:
- **0.9 - 1.0**: Excellent health
- **0.8 - 0.9**: Good health
- **0.7 - 0.8**: Degraded health
- **< 0.7**: Poor health (investigate immediately)

---

## Adjusting SLO Targets

### When to Adjust SLOs

- **Initial deployment**: Start with conservative targets (99% availability)
- **After baseline**: Adjust based on actual performance (upgrade to 99.9%)
- **Business requirements**: Match SLOs to customer expectations
- **Cost considerations**: Higher SLOs require more infrastructure investment

### How to Adjust SLO Targets

1. **Update recording rules** in `/prometheus/recording-rules.yml`:

```yaml
# Change from 99.9% to 99.95%
- record: honua:error_budget:remaining_5m
  expr: |
    0.0005 - (1 - honua:availability:ratio_5m)  # Changed from 0.001 to 0.0005
```

2. **Update alert thresholds** in `/prometheus/alerts.yml`:

```yaml
# Adjust burn rate threshold
- alert: HighErrorBudgetBurn
  expr: |
    (1 - honua:availability:ratio_5m) / 0.0005 > 10  # Changed denominator
```

3. **Update dashboards** in Grafana:

```json
{
  "expr": "99.95",  // Changed from 99.9
  "legendFormat": "SLO Target (99.95%)"
}
```

4. **Reload Prometheus**:

```bash
curl -X POST http://localhost:9090/-/reload
```

### SLO Targets by Tier

| Tier | Availability | Latency P95 | Cost | Downtime/Month |
|------|--------------|-------------|------|----------------|
| **Bronze** | 99% | < 10s | $ | 7.3 hours |
| **Silver** | 99.9% | < 5s | $$ | 43 minutes |
| **Gold** | 99.95% | < 2s | $$$ | 22 minutes |
| **Platinum** | 99.99% | < 1s | $$$$ | 4.3 minutes |

---

## Grafana Dashboard Setup

### Complete SLO Dashboard JSON

Create a file `grafana-slo-dashboard.json`:

```json
{
  "dashboard": {
    "title": "Honua SLO Dashboard",
    "panels": [
      {
        "title": "Availability (30 days)",
        "targets": [
          {
            "expr": "honua:availability:ratio_30d * 100",
            "legendFormat": "Actual"
          },
          {
            "expr": "99.9",
            "legendFormat": "Target"
          }
        ],
        "type": "stat",
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 99.8, "color": "yellow" },
                { "value": 99.9, "color": "green" }
              ]
            }
          }
        }
      },
      {
        "title": "Error Budget Remaining",
        "targets": [
          {
            "expr": "honua:error_budget:remaining_5m * 100",
            "legendFormat": "5 minutes"
          },
          {
            "expr": "honua:error_budget:remaining_1h * 100",
            "legendFormat": "1 hour"
          },
          {
            "expr": "honua:error_budget:remaining_24h * 100",
            "legendFormat": "24 hours"
          },
          {
            "expr": "honua:error_budget:remaining_30d * 100",
            "legendFormat": "30 days"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Burn Rate (5m)",
        "targets": [
          {
            "expr": "honua:error_budget:burn_rate_5m",
            "legendFormat": "Current"
          },
          {
            "expr": "1",
            "legendFormat": "Allowed"
          },
          {
            "expr": "10",
            "legendFormat": "Critical"
          }
        ],
        "type": "graph"
      }
    ]
  }
}
```

### Import Dashboard

```bash
# Via Grafana UI
1. Navigate to Dashboards → Import
2. Upload grafana-slo-dashboard.json
3. Select Prometheus datasource

# Via Grafana API
curl -X POST http://admin:admin@localhost:3000/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @grafana-slo-dashboard.json
```

---

## Best Practices

### 1. Start Simple

Begin with 2-3 key SLIs:
- HTTP availability
- Latency (P95)
- Database success rate

### 2. Use Multiple Time Windows

Track SLIs over:
- **Short window (5m)**: Fast incident detection
- **Medium window (1h)**: Trending
- **Long window (30d)**: SLO compliance

### 3. Alert on Burn Rate, Not Absolute Thresholds

Instead of alerting on "availability < 99.9%", alert on:
- "Burning error budget 10x faster than allowed"

### 4. Review SLOs Quarterly

- Check if targets are realistic
- Adjust based on business needs
- Update based on customer feedback

### 5. Document SLO Review Process

Example process:
1. **Weekly**: Review current availability and error budget
2. **Monthly**: Analyze trends and incidents
3. **Quarterly**: Adjust SLO targets if needed
4. **Yearly**: Major review and documentation update

---

## Troubleshooting

### Recording Rules Not Evaluating

**Check 1: Verify rules are loaded**
```bash
# Prometheus UI → Status → Rules
# Look for "honua_http_sli" group
```

**Check 2: Check Prometheus logs**
```bash
docker logs prometheus | grep -i "rule"
```

**Check 3: Validate YAML syntax**
```bash
promtool check rules /etc/prometheus/recording-rules.yml
```

### Metrics Show "No Data"

**Possible causes**:
1. No HTTP traffic (metrics require requests to calculate)
2. Prometheus scrape interval too long
3. Time range too short in Grafana

**Solution**:
```bash
# Generate test traffic
for i in {1..100}; do
  curl http://localhost:5000/api/layers
done

# Query raw metrics
curl http://localhost:9090/api/v1/query?query=honua:availability:ratio_5m
```

### SLO Alerts Not Firing

**Check alert rule syntax**:
```bash
promtool check rules /etc/prometheus/alerts.yml
```

**Check alert state in Prometheus UI**:
```
Prometheus → Alerts → Filter by "honua"
```

---

## References

### Books
- [Site Reliability Engineering (Google)](https://sre.google/books/)
- [Implementing Service Level Objectives](https://www.oreilly.com/library/view/implementing-service-level/9781492076803/)

### Articles
- [Google SRE: SLI, SLO, SLA](https://sre.google/sre-book/service-level-objectives/)
- [Multi-Window Multi-Burn-Rate Alerts](https://sre.google/workbook/alerting-on-slos/)
- [Error Budget Policy](https://sre.google/workbook/error-budget-policy/)

### Related Guides
- [Cloud Provider Setup](cloud-provider-setup.md)
- [Deployment Checklist](../deployment/observability-deployment-checklist.md)
- [Observability README](../../src/Honua.Server.Observability/README.md)

---

**Next Steps:**
1. Review default SLO targets and adjust for your environment
2. Import the SLO dashboard into Grafana
3. Set up error budget alerts
4. Document your SLO review process
5. Train team on interpreting SLO metrics
