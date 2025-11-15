# SRE and SLO Tracking Infrastructure

## Overview

The Honua Server includes comprehensive Site Reliability Engineering (SRE) features for Tier 3 enterprise deployments with Service Level Agreement (SLA) commitments. This infrastructure provides:

- **Service Level Indicators (SLIs)**: Quantitative measurements of service behavior
- **Service Level Objectives (SLOs)**: Reliability targets for your service
- **Error Budget Tracking**: Automated budget calculation and monitoring
- **Deployment Policy Recommendations**: Data-driven deployment decisions

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                     HTTP Requests                           │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│         SliIntegrationMiddleware                            │
│  (Captures latency, status codes, endpoints)                │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│              ISliMetrics                                     │
│  • RecordLatency()                                          │
│  • RecordAvailability()                                     │
│  • RecordError()                                            │
│  • RecordHealthCheck()                                      │
│  • GetStatistics()                                          │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│           OpenTelemetry Metrics                             │
│  • honua.sli.compliance                                     │
│  • honua.sli.events.total                                   │
│  • honua.sli.good_events.total                              │
│  • honua.sli.bad_events.total                               │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│          SloEvaluator (Background Service)                  │
│  • Runs every N minutes (configurable)                      │
│  • Calculates SLO compliance                                │
│  • Emits compliance metrics                                 │
│  • Logs warnings for at-risk SLOs                           │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│         IErrorBudgetTracker                                 │
│  • GetErrorBudget()                                         │
│  • GetDeploymentPolicy()                                    │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────────┐
│            Admin API Endpoints                              │
│  • GET /admin/sre/slos                                      │
│  • GET /admin/sre/error-budgets                             │
│  • GET /admin/sre/deployment-policy                         │
└─────────────────────────────────────────────────────────────┘
```

## Configuration

### Basic Configuration

SRE features are **disabled by default**. Enable them for Tier 3 deployments:

```bash
# Enable SRE features
SRE__ENABLED=true

# Rolling window for SLO calculations (default: 28 days)
SRE__ROLLINGWINDOWDAYS=28

# Evaluation interval (default: 5 minutes)
SRE__EVALUATIONINTERVALMINUTES=5
```

### Defining SLOs

#### Latency SLO

Track the percentage of requests completing within a latency threshold:

```bash
# Enable latency SLO
SRE__SLOS__LATENCY_SLO__ENABLED=true
SRE__SLOS__LATENCY_SLO__TYPE=Latency
SRE__SLOS__LATENCY_SLO__TARGET=0.99        # 99% of requests
SRE__SLOS__LATENCY_SLO__THRESHOLDMS=500   # Under 500ms
SRE__SLOS__LATENCY_SLO__DESCRIPTION="99% of requests complete in <500ms"
```

**What it means:**
- Target: 99% of requests must complete in under 500ms
- Error budget: You can exceed 500ms on 1% of requests (100 per 10,000 requests)

#### Availability SLO

Track the percentage of successful requests (non-5xx responses):

```bash
# Enable availability SLO
SRE__SLOS__AVAILABILITY_SLO__ENABLED=true
SRE__SLOS__AVAILABILITY_SLO__TYPE=Availability
SRE__SLOS__AVAILABILITY_SLO__TARGET=0.999   # 99.9% availability
SRE__SLOS__AVAILABILITY_SLO__DESCRIPTION="99.9% of requests succeed"
```

**What it means:**
- Target: 99.9% of requests must return non-5xx status codes
- Allowed downtime: ~43 minutes per month
- 4xx errors (client errors) don't count against availability

#### Error Rate SLO

Track the percentage of requests without server errors:

```bash
# Enable error rate SLO
SRE__SLOS__ERROR_RATE_SLO__ENABLED=true
SRE__SLOS__ERROR_RATE_SLO__TYPE=ErrorRate
SRE__SLOS__ERROR_RATE_SLO__TARGET=0.995     # 99.5% error-free
SRE__SLOS__ERROR_RATE_SLO__DESCRIPTION="99.5% of requests succeed without server errors"
```

**What it means:**
- Target: 99.5% of requests must not result in 5xx errors
- 4xx errors are excluded (they're client-side issues)

#### Health Check SLO

Track health check success rate:

```bash
# Enable health check SLO
SRE__SLOS__HEALTH_CHECK_SLO__ENABLED=true
SRE__SLOS__HEALTH_CHECK_SLO__TYPE=HealthCheckSuccess
SRE__SLOS__HEALTH_CHECK_SLO__TARGET=0.9999  # 99.99% health check success
SRE__SLOS__HEALTH_CHECK_SLO__DESCRIPTION="99.99% of health checks pass"
```

### Endpoint Filtering

Include or exclude specific endpoints from SLO tracking:

```bash
# Only track API endpoints
SRE__SLOS__API_LATENCY__INCLUDEENDPOINTS__0=/api/features
SRE__SLOS__API_LATENCY__INCLUDEENDPOINTS__1=/api/tiles
SRE__SLOS__API_LATENCY__INCLUDEENDPOINTS__2=/api/services

# Exclude non-business endpoints
SRE__SLOS__API_LATENCY__EXCLUDEENDPOINTS__0=/health
SRE__SLOS__API_LATENCY__EXCLUDEENDPOINTS__1=/metrics
SRE__SLOS__API_LATENCY__EXCLUDEENDPOINTS__2=/admin
```

### Error Budget Thresholds

Configure when to trigger warnings:

```bash
# Warning when budget drops below 25%
SRE__ERRORBUDGETTHRESHOLDS__WARNINGTHRESHOLD=0.25

# Critical when budget drops below 10%
SRE__ERRORBUDGETTHRESHOLDS__CRITICALTHRESHOLD=0.10
```

## Complete Configuration Example

```bash
# Enable SRE features
SRE__ENABLED=true
SRE__ROLLINGWINDOWDAYS=28
SRE__EVALUATIONINTERVALMINUTES=5

# Latency SLO: 99% of API requests under 500ms
SRE__SLOS__API_LATENCY__ENABLED=true
SRE__SLOS__API_LATENCY__TYPE=Latency
SRE__SLOS__API_LATENCY__TARGET=0.99
SRE__SLOS__API_LATENCY__THRESHOLDMS=500
SRE__SLOS__API_LATENCY__DESCRIPTION="99% of API requests complete in <500ms"
SRE__SLOS__API_LATENCY__INCLUDEENDPOINTS__0=/api
SRE__SLOS__API_LATENCY__EXCLUDEENDPOINTS__0=/health
SRE__SLOS__API_LATENCY__EXCLUDEENDPOINTS__1=/metrics

# Availability SLO: 99.9% availability
SRE__SLOS__AVAILABILITY__ENABLED=true
SRE__SLOS__AVAILABILITY__TYPE=Availability
SRE__SLOS__AVAILABILITY__TARGET=0.999
SRE__SLOS__AVAILABILITY__DESCRIPTION="99.9% availability (non-5xx responses)"

# Error Rate SLO: 99.95% error-free
SRE__SLOS__ERROR_RATE__ENABLED=true
SRE__SLOS__ERROR_RATE__TYPE=ErrorRate
SRE__SLOS__ERROR_RATE__TARGET=0.9995
SRE__SLOS__ERROR_RATE__DESCRIPTION="99.95% of requests without server errors"

# Error budget thresholds
SRE__ERRORBUDGETTHRESHOLDS__WARNINGTHRESHOLD=0.25
SRE__ERRORBUDGETTHRESHOLDS__CRITICALTHRESHOLD=0.10
```

## Admin API Endpoints

All endpoints require `ServerAdministrator` authorization.

### List All SLOs

```http
GET /admin/sre/slos
```

**Response:**
```json
{
  "enabled": true,
  "rollingWindowDays": 28,
  "evaluationIntervalMinutes": 5,
  "slos": [
    {
      "name": "api_latency",
      "type": "Latency",
      "target": 0.99,
      "description": "99% of API requests complete in <500ms",
      "thresholdMs": 500,
      "compliance": {
        "actual": 0.995,
        "isMet": true,
        "margin": 0.005,
        "totalEvents": 1000000,
        "goodEvents": 995000,
        "badEvents": 5000
      },
      "errorBudget": {
        "status": "Healthy",
        "remaining": 0.5,
        "remainingErrors": 5000,
        "allowedErrors": 10000
      },
      "windowDays": 28
    }
  ]
}
```

### Get SLO Details

```http
GET /admin/sre/slos/{sloName}
```

**Response:**
```json
{
  "name": "api_latency",
  "type": "Latency",
  "target": 0.99,
  "description": "99% of API requests complete in <500ms",
  "thresholdMs": 500,
  "includeEndpoints": ["/api"],
  "excludeEndpoints": ["/health", "/metrics"],
  "compliance": {
    "actual": 0.995,
    "isMet": true,
    "margin": 0.005,
    "totalEvents": 1000000,
    "goodEvents": 995000,
    "badEvents": 5000,
    "windowStart": "2025-10-17T00:00:00Z",
    "windowEnd": "2025-11-14T00:00:00Z"
  },
  "errorBudget": {
    "status": "Healthy",
    "remaining": 0.5,
    "remainingErrors": 5000,
    "allowedErrors": 10000,
    "failedRequests": 5000,
    "totalRequests": 1000000
  },
  "windowDays": 28
}
```

### Get Error Budgets

```http
GET /admin/sre/error-budgets
```

**Response:**
```json
{
  "enabled": true,
  "thresholds": {
    "warning": 0.25,
    "critical": 0.10
  },
  "errorBudgets": [
    {
      "sloName": "api_latency",
      "target": 0.99,
      "status": "Healthy",
      "budgetRemaining": 0.5,
      "remainingErrors": 5000,
      "allowedErrors": 10000,
      "failedRequests": 5000,
      "totalRequests": 1000000,
      "actualSli": 0.995,
      "sloMet": true,
      "windowDays": 28
    }
  ]
}
```

### Get Deployment Policy

```http
GET /admin/sre/deployment-policy
```

**Response (Healthy):**
```json
{
  "enabled": true,
  "canDeploy": true,
  "recommendation": "Normal",
  "reason": "All error budgets are healthy. Normal deployment velocity approved.",
  "details": "Error budgets are in good shape. Continue normal deployment practices.",
  "affectedSlos": []
}
```

**Response (Warning):**
```json
{
  "enabled": true,
  "canDeploy": true,
  "recommendation": "Cautious",
  "reason": "Error budget warning for 1 SLO(s). Reduce deployment velocity.",
  "details": "Error budgets are running low. Consider slowing down feature deployments and increasing testing rigor.",
  "affectedSlos": ["api_latency"]
}
```

**Response (Critical):**
```json
{
  "enabled": true,
  "canDeploy": true,
  "recommendation": "Restricted",
  "reason": "Error budget critical for 1 SLO(s). Deploy only critical fixes.",
  "details": "Error budgets are critically low. Only deploy urgent fixes and carefully monitor impact.",
  "affectedSlos": ["api_latency"]
}
```

**Response (Exhausted):**
```json
{
  "enabled": true,
  "canDeploy": false,
  "recommendation": "Halt",
  "reason": "Error budget exhausted for 1 SLO(s). Focus on reliability and incident response.",
  "details": "One or more SLOs have violated their targets. Halt all non-critical deployments until error budgets recover.",
  "affectedSlos": ["api_latency"]
}
```

## Understanding Error Budgets

### What is an Error Budget?

An error budget is the inverse of your SLO target. It represents how much failure you can tolerate:

- **99% SLO** = 1% error budget (10 errors per 1,000 requests)
- **99.9% SLO** = 0.1% error budget (1 error per 1,000 requests)
- **99.99% SLO** = 0.01% error budget (1 error per 10,000 requests)

### Error Budget Status

| Status | Threshold | Meaning | Action |
|--------|-----------|---------|--------|
| **Healthy** | > 25% remaining | Plenty of budget | Deploy normally, take calculated risks |
| **Warning** | 10-25% remaining | Budget running low | Reduce deployment velocity, increase testing |
| **Critical** | 0-10% remaining | Very little budget | Only critical fixes, careful monitoring |
| **Exhausted** | ≤ 0% remaining | SLO violated | Halt non-essential deployments, fix issues |

### Example Calculation

Given:
- SLO: 99.9% availability
- Rolling window: 28 days
- Total requests: 1,000,000

Calculation:
- **Allowed errors**: (1 - 0.999) × 1,000,000 = 1,000 errors
- **Actual failures**: 500 (5xx errors)
- **Remaining errors**: 1,000 - 500 = 500
- **Budget remaining**: 500 / 1,000 = 50%
- **Status**: Healthy

### Using Error Budgets for Deployment Decisions

```
Error Budget      Deployment Policy
─────────────────────────────────────────────────
> 50%         →   Normal: Deploy freely
25-50%        →   Normal: Continue as usual
10-25%        →   Cautious: Reduce velocity
5-10%         →   Restricted: Critical fixes only
0-5%          →   Critical: Emergency fixes only
< 0%          →   Halt: Stop all deployments
```

## OpenTelemetry Metrics

The SRE infrastructure emits the following metrics:

### SLI Compliance Metrics

```
# Histogram of SLI compliance (1.0 = good, 0.0 = bad)
honua.sli.compliance{slo.name="api_latency", sli.type="latency", threshold.ms="500"}

# Counter of total SLI events
honua.sli.events.total{slo.name="api_latency", sli.type="latency"}

# Counter of good SLI events
honua.sli.good_events.total{slo.name="api_latency", sli.type="latency"}

# Counter of bad SLI events
honua.sli.bad_events.total{slo.name="api_latency", sli.type="latency"}
```

### SLO Compliance Metrics

```
# Gauge of current SLO compliance (0.0-1.0)
honua.slo.compliance{slo.name="api_latency", sli.type="latency", is_met="true"}

# Gauge of SLO target
honua.slo.target{slo.name="api_latency", sli.type="latency"}

# Gauge of total events in window
honua.slo.total_events{slo.name="api_latency", sli.type="latency"}

# Gauge of good events in window
honua.slo.good_events{slo.name="api_latency", sli.type="latency"}

# Gauge of bad events in window
honua.slo.bad_events{slo.name="api_latency", sli.type="latency"}
```

### Error Budget Metrics

```
# Gauge of error budget remaining (0.0-1.0)
honua.slo.error_budget.remaining{slo.name="api_latency", status="healthy"}

# Gauge of total allowed errors
honua.slo.error_budget.allowed_errors{slo.name="api_latency"}

# Gauge of remaining errors
honua.slo.error_budget.remaining_errors{slo.name="api_latency"}
```

## Prometheus Queries

### SLO Compliance Rate (4 weeks)

```promql
# Calculate SLO compliance over 4 weeks
sum(increase(honua.sli.good_events.total{slo.name="api_latency"}[28d]))
/
sum(increase(honua.sli.events.total{slo.name="api_latency"}[28d]))
```

### Error Budget Burn Rate

```promql
# How fast are you consuming your error budget?
rate(honua.sli.bad_events.total{slo.name="api_latency"}[1h])
/
(honua.slo.error_budget.allowed_errors{slo.name="api_latency"} / (28 * 24))
```

### Time to SLO Violation

```promql
# Estimated time until SLO violation at current error rate
honua.slo.error_budget.remaining_errors{slo.name="api_latency"}
/
rate(honua.sli.bad_events.total{slo.name="api_latency"}[1h]) * 3600
```

## Alerting Rules

### Prometheus Alert Examples

```yaml
groups:
  - name: slo_alerts
    interval: 1m
    rules:
      # Alert when error budget is in warning state
      - alert: ErrorBudgetWarning
        expr: honua.slo.error_budget.remaining < 0.25
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Error budget warning for {{ $labels.slo_name }}"
          description: "Error budget is at {{ $value | humanizePercentage }} remaining"

      # Alert when error budget is critical
      - alert: ErrorBudgetCritical
        expr: honua.slo.error_budget.remaining < 0.10
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Error budget critical for {{ $labels.slo_name }}"
          description: "Error budget is at {{ $value | humanizePercentage }} remaining. Reduce deployment velocity."

      # Alert when error budget is exhausted
      - alert: ErrorBudgetExhausted
        expr: honua.slo.error_budget.remaining <= 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Error budget exhausted for {{ $labels.slo_name }}"
          description: "SLO is violated. Halt non-essential deployments immediately."

      # Alert on fast error budget burn (burns 10% in 1 hour)
      - alert: FastErrorBudgetBurn
        expr: |
          (
            rate(honua.sli.bad_events.total[1h])
            /
            (honua.slo.error_budget.allowed_errors / (28 * 24))
          ) > 0.10
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Fast error budget burn for {{ $labels.slo_name }}"
          description: "Burning through error budget at {{ $value | humanizePercentage }}/hour"
```

## Integration with CI/CD

### Pre-Deployment Check

```bash
#!/bin/bash
# Check deployment policy before deploying

POLICY=$(curl -s -H "Authorization: Bearer $TOKEN" \
  https://api.example.com/admin/sre/deployment-policy)

RECOMMENDATION=$(echo $POLICY | jq -r '.recommendation')

if [ "$RECOMMENDATION" == "Halt" ]; then
  echo "❌ Deployment blocked: Error budget exhausted"
  exit 1
elif [ "$RECOMMENDATION" == "Restricted" ]; then
  echo "⚠️  Warning: Error budget critical. Deploy only if critical fix."
  read -p "Is this a critical fix? (yes/no) " answer
  if [ "$answer" != "yes" ]; then
    exit 1
  fi
elif [ "$RECOMMENDATION" == "Cautious" ]; then
  echo "⚠️  Warning: Error budget low. Proceed with caution."
fi

echo "✅ Deployment approved"
```

### GitHub Actions Example

```yaml
name: Check SLO Before Deploy

on:
  push:
    branches: [main]

jobs:
  check-slo:
    runs-on: ubuntu-latest
    steps:
      - name: Check deployment policy
        run: |
          POLICY=$(curl -s -H "Authorization: Bearer ${{ secrets.API_TOKEN }}" \
            https://api.example.com/admin/sre/deployment-policy)

          CAN_DEPLOY=$(echo $POLICY | jq -r '.canDeploy')
          RECOMMENDATION=$(echo $POLICY | jq -r '.recommendation')

          if [ "$CAN_DEPLOY" == "false" ]; then
            echo "::error::Deployment blocked due to exhausted error budget"
            exit 1
          fi

          if [ "$RECOMMENDATION" != "Normal" ]; then
            echo "::warning::Deployment recommendation: $RECOMMENDATION"
          fi

  deploy:
    needs: check-slo
    runs-on: ubuntu-latest
    steps:
      - name: Deploy application
        run: |
          # Your deployment steps here
```

## Best Practices

### 1. Start Conservative

Begin with achievable targets and tighten over time:

```bash
# Phase 1: Establish baseline
SRE__SLOS__API_LATENCY__TARGET=0.95  # 95%

# Phase 2: After 1 month of data
SRE__SLOS__API_LATENCY__TARGET=0.99  # 99%

# Phase 3: After 3 months of stability
SRE__SLOS__API_LATENCY__TARGET=0.999 # 99.9%
```

### 2. Different SLOs for Different Criticality

```bash
# Critical API: Very strict
SRE__SLOS__CRITICAL_API__TARGET=0.999

# Standard API: Moderate
SRE__SLOS__STANDARD_API__TARGET=0.99

# Internal API: Relaxed
SRE__SLOS__INTERNAL_API__TARGET=0.95
```

### 3. Use Error Budgets Proactively

- **Budget healthy (>50%)**: Take risks, experiment, deploy new features
- **Budget medium (25-50%)**: Normal operations
- **Budget low (<25%)**: Focus on reliability, reduce changes
- **Budget critical (<10%)**: Only critical fixes
- **Budget exhausted (<0%)**: All hands on deck for reliability

### 4. Review SLOs Regularly

- Weekly: Review current compliance and trends
- Monthly: Adjust thresholds based on learnings
- Quarterly: Major SLO revisions

### 5. Align SLOs with User Experience

Your SLOs should reflect what users actually care about:

- **User-facing APIs**: Strict latency SLOs (99th percentile < 500ms)
- **Background jobs**: Relaxed latency SLOs
- **Critical services**: High availability SLOs (99.9%)
- **Non-critical services**: Moderate availability SLOs (99%)

## Troubleshooting

### SLO Shows No Data

**Problem**: `GET /admin/sre/slos/{sloName}` returns "No data available yet"

**Solutions**:
1. Verify SRE is enabled: `SRE__ENABLED=true`
2. Verify SLO is enabled: `SRE__SLOS__{NAME}__ENABLED=true`
3. Check middleware is registered (see integration section)
4. Verify traffic is flowing through the system
5. Check logs for errors in SliMetrics

### Error Budget Not Updating

**Problem**: Error budget remains at 0 despite traffic

**Solutions**:
1. Verify SloEvaluator background service is running
2. Check evaluation interval: `SRE__EVALUATIONINTERVALMINUTES=5`
3. Review application logs for SloEvaluator errors
4. Ensure SLI measurements are being recorded

### Metrics Not Appearing in Prometheus

**Problem**: SLO metrics not visible in Prometheus

**Solutions**:
1. Verify OpenTelemetry exporter is configured
2. Check metrics endpoint: `GET /metrics`
3. Ensure `honua.slo.*` and `honua.sli.*` metrics are present
4. Verify Prometheus is scraping the correct endpoint

## Migration Guide

### Existing Deployments

To enable SRE features on an existing deployment:

1. **Week 1**: Enable in observation-only mode
   ```bash
   SRE__ENABLED=true
   # Define SLOs but don't use for deployment decisions yet
   ```

2. **Week 2-4**: Collect baseline data
   - Review `/admin/sre/slos` daily
   - Adjust targets based on actual performance
   - Ensure targets are achievable

3. **Week 5**: Integrate with monitoring
   - Set up Prometheus alerts
   - Create dashboards
   - Train team on SLO concepts

4. **Week 6+**: Enforce deployment policies
   - Add pre-deployment checks to CI/CD
   - Use deployment policy recommendations
   - Establish incident response procedures

## Support

For questions or issues:
- Documentation: This file
- API Reference: OpenAPI docs at `/swagger`
- Metrics: Prometheus at `/metrics`
- Support: Contact your Honua administrator
