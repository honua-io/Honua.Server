# GitOps Monitoring Dashboard

**Version:** 1.0
**Last Updated:** 2025-10-23

Configuration for monitoring and alerting on GitOps operations.

---

## Key Metrics

### OpenTelemetry Metrics Exposed

The Honua GitOps system exposes the following metrics via OpenTelemetry:

| Metric Name | Type | Labels | Description |
|-------------|------|--------|-------------|
| `honua.gitops.reconciliations.total` | Counter | `environment` | Total reconciliation attempts |
| `honua.gitops.reconciliations.success` | Counter | `environment` | Successful reconciliations |
| `honua.gitops.reconciliations.failure` | Counter | `environment` | Failed reconciliations |
| `honua.gitops.reconciliation.duration` | Histogram | `environment`, `status` | Reconciliation duration in seconds |

---

## Prometheus Configuration

### Scrape Configuration

```yaml
# prometheus.yml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'honua-gitops'
    static_configs:
      - targets: ['honua-server:9090']
    metric_relabel_configs:
      # Keep only GitOps metrics
      - source_labels: [__name__]
        regex: 'honua\.gitops\..*'
        action: keep
```

### Alert Rules

```yaml
# /etc/prometheus/rules/gitops-alerts.yml
groups:
  - name: gitops_critical
    interval: 30s
    rules:
      - alert: GitOpsReconciliationFailing
        expr: |
          increase(honua_gitops_reconciliations_failure[5m]) > 2
        for: 5m
        labels:
          severity: critical
          component: gitops
        annotations:
          summary: "GitOps reconciliation failing repeatedly"
          description: "Environment {{ $labels.environment }} has {{ $value }} reconciliation failures in the last 5 minutes"
          runbook_url: "https://docs.example.com/runbooks/gitops-reconciliation-failing"

      - alert: GitOpsReconciliationStalled
        expr: |
          rate(honua_gitops_reconciliations_total[10m]) == 0
        for: 10m
        labels:
          severity: critical
          component: gitops
        annotations:
          summary: "GitOps reconciliation has stalled"
          description: "No reconciliation attempts in environment {{ $labels.environment }} for 10 minutes"
          runbook_url: "https://docs.example.com/runbooks/gitops-stalled"

      - alert: GitOpsReconciliationSlow
        expr: |
          histogram_quantile(0.95,
            rate(honua_gitops_reconciliation_duration_bucket[5m])
          ) > 60
        for: 5m
        labels:
          severity: warning
          component: gitops
        annotations:
          summary: "GitOps reconciliation is slow"
          description: "95th percentile reconciliation duration for {{ $labels.environment }} is {{ $value }}s"
          runbook_url: "https://docs.example.com/runbooks/gitops-slow"

  - name: gitops_warnings
    interval: 60s
    rules:
      - alert: GitOpsLowSuccessRate
        expr: |
          (
            rate(honua_gitops_reconciliations_success[1h]) /
            rate(honua_gitops_reconciliations_total[1h])
          ) < 0.9
        for: 15m
        labels:
          severity: warning
          component: gitops
        annotations:
          summary: "GitOps success rate below 90%"
          description: "Success rate for {{ $labels.environment }} is {{ $value | humanizePercentage }}"

      - alert: GitOpsHighReconciliationFrequency
        expr: |
          rate(honua_gitops_reconciliations_total[5m]) > 2
        for: 15m
        labels:
          severity: info
          component: gitops
        annotations:
          summary: "Unusually high reconciliation frequency"
          description: "Environment {{ $labels.environment }} is reconciling {{ $value | humanize }} times per second"
```

---

## Grafana Dashboard

### Dashboard JSON

```json
{
  "dashboard": {
    "title": "Honua GitOps Operations",
    "tags": ["gitops", "honua"],
    "timezone": "utc",
    "panels": [
      {
        "title": "Reconciliation Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "rate(honua_gitops_reconciliations_total[5m])",
            "legendFormat": "{{environment}} - Total"
          },
          {
            "expr": "rate(honua_gitops_reconciliations_success[5m])",
            "legendFormat": "{{environment}} - Success"
          },
          {
            "expr": "rate(honua_gitops_reconciliations_failure[5m])",
            "legendFormat": "{{environment}} - Failure"
          }
        ],
        "gridPos": {
          "h": 8,
          "w": 12,
          "x": 0,
          "y": 0
        }
      },
      {
        "title": "Reconciliation Duration (p95)",
        "type": "graph",
        "targets": [
          {
            "expr": "histogram_quantile(0.95, rate(honua_gitops_reconciliation_duration_bucket[5m]))",
            "legendFormat": "{{environment}} - p95"
          },
          {
            "expr": "histogram_quantile(0.99, rate(honua_gitops_reconciliation_duration_bucket[5m]))",
            "legendFormat": "{{environment}} - p99"
          }
        ],
        "gridPos": {
          "h": 8,
          "w": 12,
          "x": 12,
          "y": 0
        },
        "yaxes": [
          {
            "format": "s",
            "label": "Duration"
          }
        ]
      },
      {
        "title": "Success Rate",
        "type": "stat",
        "targets": [
          {
            "expr": "(rate(honua_gitops_reconciliations_success[1h]) / rate(honua_gitops_reconciliations_total[1h])) * 100",
            "legendFormat": "{{environment}}"
          }
        ],
        "gridPos": {
          "h": 4,
          "w": 6,
          "x": 0,
          "y": 8
        },
        "options": {
          "unit": "percent"
        }
      },
      {
        "title": "Total Reconciliations (24h)",
        "type": "stat",
        "targets": [
          {
            "expr": "increase(honua_gitops_reconciliations_total[24h])",
            "legendFormat": "{{environment}}"
          }
        ],
        "gridPos": {
          "h": 4,
          "w": 6,
          "x": 6,
          "y": 8
        }
      }
    ]
  }
}
```

### Dashboard Provisioning

```yaml
# /etc/grafana/provisioning/dashboards/gitops.yaml
apiVersion: 1

providers:
  - name: 'GitOps'
    orgId: 1
    folder: 'Honua'
    type: file
    disableDeletion: false
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /etc/grafana/provisioning/dashboards/gitops
```

---

## Log Aggregation Queries

### ELK Stack (Elasticsearch)

```json
{
  "query": {
    "bool": {
      "must": [
        {
          "match": {
            "source": "GitWatcher"
          }
        },
        {
          "match": {
            "event": "reconciliation_completed"
          }
        }
      ],
      "filter": {
        "range": {
          "timestamp": {
            "gte": "now-1h"
          }
        }
      }
    }
  },
  "aggs": {
    "avg_duration": {
      "avg": {
        "field": "duration_ms"
      }
    },
    "by_environment": {
      "terms": {
        "field": "environment"
      }
    }
  }
}
```

### Splunk Query

```spl
index=honua source="GitWatcher"
| where event="reconciliation_completed" OR event="reconciliation_failed"
| stats count by environment, event
| eval success_rate=round(success/(success+failure)*100, 2)
```

---

## Health Check Endpoints

### Kubernetes Liveness Probe

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 5000
  initialDelaySeconds: 30
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
```

### Readiness Probe

```yaml
readinessProbe:
  httpGet:
    path: /health/ready
    port: 5000
  initialDelaySeconds: 10
  periodSeconds: 5
  timeoutSeconds: 3
  failureThreshold: 3
```

---

## Alerting Channels

### Slack Integration (AlertManager)

```yaml
# alertmanager.yml
route:
  group_by: ['alertname', 'environment']
  group_wait: 10s
  group_interval: 10s
  repeat_interval: 12h
  receiver: 'slack-notifications'

  routes:
    - match:
        severity: critical
      receiver: 'slack-critical'
      continue: true

    - match:
        severity: warning
      receiver: 'slack-warnings'

receivers:
  - name: 'slack-critical'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
        channel: '#alerts-critical'
        title: 'GitOps Critical Alert'
        text: '{{ .CommonAnnotations.description }}'

  - name: 'slack-warnings'
    slack_configs:
      - api_url: 'https://hooks.slack.com/services/YOUR/WEBHOOK/URL'
        channel: '#alerts-warnings'
        title: 'GitOps Warning'
        text: '{{ .CommonAnnotations.description }}'
```

### PagerDuty Integration

```yaml
receivers:
  - name: 'pagerduty'
    pagerduty_configs:
      - service_key: 'YOUR_PAGERDUTY_SERVICE_KEY'
        description: '{{ .CommonAnnotations.summary }}'
        severity: '{{ .CommonLabels.severity }}'
```

---

## Sample Queries

### Average Reconciliation Duration

```promql
# Average duration over last hour
avg(rate(honua_gitops_reconciliation_duration_sum[1h])) by (environment)
```

### Reconciliation Success Rate

```promql
# Success rate percentage
(
  sum(rate(honua_gitops_reconciliations_success[5m])) by (environment) /
  sum(rate(honua_gitops_reconciliations_total[5m])) by (environment)
) * 100
```

### Failed Reconciliations

```promql
# Count of failures in last hour
increase(honua_gitops_reconciliations_failure[1h])
```

### Slow Reconciliations

```promql
# Reconciliations taking > 30 seconds
count_over_time(
  (honua_gitops_reconciliation_duration > 30)[1h:]
)
```

---

## Operational Dashboards

### Daily Operations Dashboard

**Widgets:**

1. **24-Hour Overview**
   - Total reconciliations
   - Success rate
   - Average duration
   - Failure count

2. **Current Status**
   - Last reconciliation time
   - Current deployment state
   - Active alerts

3. **Trends**
   - Reconciliation frequency graph (7 days)
   - Duration trend (7 days)
   - Success rate trend (7 days)

4. **Recent Errors**
   - Last 10 failed reconciliations
   - Error messages
   - Affected environments

### Executive Summary Dashboard

**Metrics:**

- **Deployment Frequency:** Reconciliations per day
- **Lead Time:** Time from commit to deployment
- **Change Failure Rate:** % of deployments that fail
- **MTTR:** Mean time to recovery from failures

---

## Performance Monitoring

### Resource Usage Metrics

```promql
# CPU usage
rate(process_cpu_seconds_total{job="honua-gitops"}[5m]) * 100

# Memory usage
process_resident_memory_bytes{job="honua-gitops"}

# Git operation duration
honua_gitops_git_pull_duration_seconds
```

### Capacity Planning

**Monitor:**

1. **State File Size Growth**
   ```bash
   du -sh /var/honua/deployments/*.json | awk '{print $1}'
   ```

2. **Repository Size**
   ```bash
   du -sh /var/honua/gitops-repo/.git
   ```

3. **Disk I/O**
   ```promql
   rate(node_disk_io_time_seconds_total[5m])
   ```

---

## Incident Response Metrics

### SLA Tracking

| Metric | Target | Alert Threshold |
|--------|--------|-----------------|
| Availability | 99.9% | < 99.5% |
| Reconciliation Success Rate | 95% | < 90% |
| Average Reconciliation Duration | < 10s | > 30s |
| Time to Detection | < 5 min | > 10 min |
| Time to Resolution | < 30 min | > 1 hour |

### Incident Timeline

```promql
# Time between failure and recovery
(
  time() -
  max(honua_gitops_reconciliation_timestamp{status="failed"})
) by (environment)
```

---

## Summary

**Essential Monitoring:**

1. **Always monitor:**
   - Reconciliation success rate
   - Reconciliation duration (p95, p99)
   - Failure count
   - State file integrity

2. **Alert on:**
   - Consecutive failures (> 2)
   - Stalled reconciliation (> 10 min)
   - Slow reconciliation (> 60s)
   - Success rate below 90%

3. **Review weekly:**
   - Deployment frequency trends
   - Performance degradation
   - Alert noise
   - Capacity metrics

4. **Optimize:**
   - Poll intervals based on actual reconciliation duration
   - Resource allocations based on usage
   - Alert thresholds based on historical data

---

**Last Updated:** 2025-10-23
**Version:** 1.0
