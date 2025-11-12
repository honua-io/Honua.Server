# Honua Server Service Level Agreements (SLAs) and Objectives (SLOs)

This document defines the service level commitments and objectives for Honua Server.

## Overview

- **SLA**: Service Level Agreement - Legal commitment to customers
- **SLO**: Service Level Objective - Internal target performance metrics
- **SLI**: Service Level Indicator - Actual measured performance

## Service Level Objectives (SLOs)

### Primary SLOs

#### Availability SLO

**Objective**: 99.9% uptime (monthly)

**SLI Definition**: Successful HTTP responses / Total HTTP requests

**Target**: ≥ 99.9% requests with status < 500

**Exclusions**:
- Planned maintenance windows (announced 48h+ in advance)
- Client errors (4xx status codes)
- Rate-limited requests (429 status)
- Requests from disabled/revoked licenses

**Measurement**:
```promql
# Monthly availability
sum(increase(http_requests_total{status_class!="5xx"}[30d])) /
sum(increase(http_requests_total[30d])) >= 0.999
```

**Error Budget**:
- Monthly: 43.2 seconds (0.0012%)
- Weekly: 10.1 seconds
- Daily: 1.4 seconds
- Hourly: 0.06 seconds

---

#### Latency SLO

**Objective**: p95 < 5 seconds, p99 < 10 seconds

**SLI Definition**: 95th and 99th percentile HTTP response time

**Inclusions**:
- All API endpoints
- Excludes health checks (/health/*, /metrics)
- Excludes long-running exports (>30s expected)

**Measurement**:
```promql
# p95 latency
histogram_quantile(0.95,
  sum(rate(http_request_duration_seconds_bucket[30d])) by (le)
) < 5

# p99 latency
histogram_quantile(0.99,
  sum(rate(http_request_duration_seconds_bucket[30d])) by (le)
) < 10
```

**Tracking**:
- Dashboard: Honua - Response Times & Latency
- Alert: P95ResponseTimeHigh (warning), P99ResponseTimeHigh (critical)
- Review: Weekly

---

#### Error Rate SLO

**Objective**: < 0.1% error rate (99.9% success)

**SLI Definition**: 5xx errors / Total requests

**Inclusions**:
- All 5xx responses
- Timeouts (408, 504)
- Service unavailable (503)

**Exclusions**:
- 4xx client errors
- 429 rate limiting (expected)
- Requests to non-existent endpoints

**Measurement**:
```promql
# Monthly error rate
sum(increase(http_requests_total{status_class="5xx"}[30d])) /
sum(increase(http_requests_total[30d])) < 0.001
```

**Error Budget** (Monthly):
- 0.1% of requests can fail
- = ~2,880 errors/month (assuming 30M requests)
- = ~96 errors/day
- = ~4 errors/hour

**Alerts**:
- HighHTTPErrorRate: > 5% for 5 minutes (warning)
- HighErrorBudgetBurn: > 10% for 5 minutes (critical)

---

### Secondary SLOs

#### Database Performance

**Objective**: p95 query duration < 2 seconds

**SLI Definition**: 95th percentile database query duration

**Measurement**:
```promql
histogram_quantile(0.95,
  sum(rate(db_query_duration_seconds_bucket[30d])) by (le)
) < 2
```

**Actions**:
- Alert when p95 > 2s (DatabaseSlowQueries)
- Investigate root cause
- Optimize or add indexes

#### Cache Performance

**Objective**: Cache hit rate > 50%

**SLI Definition**: Cache hits / Total cache lookups

**Measurement**:
```promql
rate(cache_lookups_total{result="hit"}[30d]) /
rate(cache_lookups_total[30d]) > 0.50
```

**Target Benefits**:
- Reduces database load by 50%
- Improves p95 latency by 30%
- Reduces operational costs

#### Dependency Health

**Objective**: External dependency uptime > 99.5%

**SLI Definition**: Successful requests to external service

**Includes**:
- PostgreSQL database
- Redis cache
- Third-party APIs
- Load balancers

**Measurement**:
```promql
# Database availability
rate(db_connection_pool_available[5m]) > 0

# External API availability
rate(http_requests_total{destination="external"}[5m]) >
(rate(http_requests_total{destination="external", status_class="5xx"}[5m]) * 1.005)
```

---

## Service Level Agreements (SLAs)

### Customer SLA Commitments

#### Availability SLA

| Tier | Target | Monthly Downtime | Rebate |
|------|--------|-----------------|--------|
| Bronze | 95% | 36 hours | 5% |
| Silver | 99% | 7.2 hours | 10% |
| Gold | 99.5% | 3.6 hours | 15% |
| Platinum | 99.9% | 43 minutes | 20% |

**Definitions**:
- Uptime: Service responds to health checks and API calls
- Downtime: Consecutive minutes when > 50% of requests fail
- Excludes: Scheduled maintenance, customer misconfiguration

#### Performance SLA

| Tier | p95 Latency | p99 Latency |
|------|------------|-----------|
| Bronze | 10s | 20s |
| Silver | 5s | 10s |
| Gold | 2s | 5s |
| Platinum | 1s | 3s |

**Measurement**:
- 30-day rolling window
- All API endpoints except long-running operations
- Excludes requests from rate-limited clients

#### Rebate Policy

**Conditions**:
- SLA breach must be ≥ 30 consecutive minutes
- Customer must request rebate within 30 days
- Not combinable with other credits

**Process**:
1. Customer opens support ticket
2. Engineering verifies SLA breach
3. Rebate calculated and applied to next invoice
4. Root cause analysis performed

---

## Monitoring and Enforcement

### SLO Dashboards

**Honua - SLO Status Dashboard** (to be created):

```
┌────────────────────────────┐
│ Monthly Availability       │ 99.89%  [YELLOW]  Target: 99.9%
├────────────────────────────┤
│ P95 Response Time          │ 4.2s    [GREEN]   Target: < 5s
├────────────────────────────┤
│ P99 Response Time          │ 8.7s    [GREEN]   Target: < 10s
├────────────────────────────┤
│ Error Rate (Monthly)       │ 0.08%   [GREEN]   Target: < 0.1%
├────────────────────────────┤
│ Error Budget (Remaining)   │ 0.02%   [YELLOW]  Only 25 errors left
├────────────────────────────┤
│ Database P95 Latency       │ 1.8s    [GREEN]   Target: < 2s
├────────────────────────────┤
│ Cache Hit Rate             │ 62%     [GREEN]   Target: > 50%
└────────────────────────────┘
```

### Alerts and Escalation

**SLO Breach Alerts**:

```yaml
- name: SLOBreachAvailability
  expr: |
    (1 - (sum(increase(http_requests_total{status_class!="5xx"}[30d])) /
           sum(increase(http_requests_total[30d])))) > 0.001
  for: 1m
  labels:
    severity: critical
  annotations:
    summary: "Monthly availability SLO breached (< 99.9%)"
    description: "Current: {{ $value | humanizePercentage }}. Error budget exhausted."

- name: HighErrorBudgetBurn
  expr: |
    (rate(http_requests_total{status_class="5xx"}[5m]) /
     rate(http_requests_total[5m])) > 0.10
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "Error rate too high (SLO at risk)"
    description: "Current: {{ $value | humanizePercentage }}, Target: < 0.1%. At {{ $value | humanizePercentage }}, monthly SLO will be breached in ~{{ printf \"%.0f\" (1.0 / ($value / 0.001) / 1440.0) }} days."
```

---

## Error Budget Analysis

### What is Error Budget?

Error budget is the amount of errors (downtime) you can have while still meeting your SLO.

**Formula**: Error Budget = 100% - SLO Target

**Examples**:

#### 99.9% Availability SLO
```
Error Budget = 100% - 99.9% = 0.1%

Monthly (30 days):
- 0.1% × (30 × 24 × 60 × 60) = 25,920 seconds = 7.2 hours

Weekly:
- 0.1% × (7 × 24 × 60 × 60) = 60,480 seconds = 1 hour 44 minutes

Daily:
- 0.1% × (24 × 60 × 60) = 8,640 seconds = 2.4 hours
```

### Error Budget Tracking

**Monthly Budget Consumption**:

```
November 2024 Error Budget
┌─────────────────────────────────────────┐
│ [████████████░░░░░░░░░░░░░░░░░░░░░░░░] │
│ 67% consumed (16.1 hours of 24 hours)   │
└─────────────────────────────────────────┘

Breakdown by incident:
- Database outage (Nov 5): 2 hours (26%)
- Memory leak incident (Nov 12): 1.5 hours (20%)
- Slow query (Nov 18): 45 minutes (10%)
- Other operational issues: 1.5 hours (20%)
- Normal error rate: 36 minutes (8%)
- Remaining budget: 7.9 hours (33%)
```

### Error Budget Policy

**Spending Error Budget Wisely**:

✓ **DO**:
- Use budget for risky deployments
- Experiment with new features
- Optimize without rushing
- Bulk-fix issues together

✗ **DON'T**:
- Ignore error budget
- Keep deploying when budget low
- Defer critical fixes
- Release untested code

**Low Error Budget Response**:

When error budget < 20% remaining:
1. Pause feature development (if safe)
2. Focus on reliability
3. Increase testing
4. Slow down deployment
5. Code review more carefully
6. Monitor more closely

---

## SLO Review and Adjustment

### Quarterly Review Process

**When**: End of every quarter

**Participants**:
- Engineering lead
- Product manager
- Ops engineer
- Customer success manager

**Agenda**:
1. Review actual vs. target SLOs
2. Identify trends
3. Discuss customer impact
4. Plan improvements

**Actions**:
- Update SLO targets if needed (with notice)
- Plan improvements for next quarter
- Document changes
- Communicate to customers

### SLO Improvement Plan

**Current Targets** (Baseline):
- Availability: 99.9%
- p95 Latency: 5s
- Error Rate: 0.1%

**Q1 2025 Targets** (Stretch):
- Availability: 99.95% (higher tier commitments)
- p95 Latency: 2s (performance optimization)
- Error Rate: 0.05% (better reliability)

**Q2 2025 Targets** (Future):
- Availability: 99.99% (high availability architecture)
- p95 Latency: 1s (advanced caching)
- Error Rate: 0.01% (proactive monitoring)

---

## Dependencies and Their SLOs

### Critical Dependencies

#### PostgreSQL Database

**Our SLO**: Database p95 latency < 2s

**Dependency SLO**: PostgreSQL availability 99.99%

**Failure Mode**:
- If database is down: All APIs fail
- If database is slow: All APIs slow down
- Impact: Critical (P1)

**Mitigation**:
- Read replicas for load distribution
- Connection pooling
- Automated failover
- Regular backups

#### Redis Cache

**Our SLO**: Cache hit rate > 50%

**Dependency SLO**: Redis availability 99.9%

**Failure Mode**:
- If cache is down: Performance degrades by 30%
- If cache is slow: CPU usage increases
- Impact: High (P2)

**Mitigation**:
- Cache-aside pattern (fallback to DB)
- Multiple cache instances
- Regular eviction policy tuning

#### External APIs

**Dependency SLOs**:
- Auth provider: 99.5% availability
- Third-party APIs: 99% availability

**Failure Mode**:
- Auth down: Can't authenticate users
- API down: Degrades service (not full outage)
- Impact: Medium (P2/P3)

**Mitigation**:
- Circuit breakers
- Caching of API responses
- Graceful degradation
- Fallback logic

---

## Incidents and Impact

### Incident Classification

**P1 - Critical (SLO Impact)**
- Service completely unavailable
- OR error rate > 5%
- OR p95 latency > 10s
- Action: Page on-call immediately

**P2 - Major (SLO Risk)**
- Service degraded
- OR error rate 1-5%
- OR p95 latency 5-10s
- Action: Alert team, investigate

**P3 - Minor (No SLO Impact)**
- Small user set affected
- OR error rate < 1%
- OR p95 latency > 10s but <20s
- Action: Log ticket, schedule fix

### Example: How to Calculate SLO Impact

**Scenario**: Database down for 30 minutes on November 15

```
Impact Calculation:
- Duration: 30 minutes
- Requests during period: ~500k (normal load)
- Requests that failed: 500k (100% failure)
- Monthly error rate: 500k / 30M = 0.0167%

SLO Status:
- Target error rate: 0.1%
- Actual error rate: 0.0167% (within budget)
- Impact: Incident consumed 16.7% of monthly error budget

If 2 more similar incidents: SLO would be breached
```

---

## Reporting and Communication

### Internal Reporting

**Weekly Report**:
```
Subject: [SLO] Weekly Status Report - Week 45

Availability: 99.94% (target 99.9%) ✓
P95 Latency: 4.1s (target < 5s) ✓
Error Rate: 0.08% (target < 0.1%) ✓
Cache Hit Rate: 58% (target > 50%) ✓

Error Budget: 67% consumed
Incidents: 1 (database slow query)

Next week: Monitor cache performance
```

**Monthly Report**:
```
Subject: [SLO] November 2024 Report

                                Target    Actual    Status
Availability                     99.9%     99.89%    ✗ MISS
P95 Latency                       5s        4.8s      ✓
P99 Latency                       10s       9.2s      ✓
Error Rate                        0.1%      0.11%     ✗ MISS
Cache Hit Rate                    50%       58%       ✓

Error Budget: 134% (EXCEEDED)
Incidents: 3 (all resolved)
Customer Impact: 2 breached SLAs (rebates issued)

Root Causes:
1. Database upgrade caused 2.5 hour outage
2. Memory leak caused intermittent 5xx errors

Improvements:
- Better database capacity planning
- Memory monitoring alerts improved
```

### Customer Communication

**SLA Breach Notification**:

```
Subject: Service Availability Update - Notification & Credit

Dear Customer,

During November 2024, Honua Server experienced a service availability
below our Service Level Agreement commitment.

INCIDENT DETAILS:
Date:           November 15, 2024
Duration:       90 minutes
Root Cause:     Database connectivity issue
Impact:         0.15% error rate (SLA: < 0.1%)
Your SLA Tier:  Silver (99% commitment)

REMEDY:
Per your service agreement, you qualify for a service credit of 10%
of your monthly fees (approximately $X).

This credit will automatically be applied to your December invoice.

IMPROVEMENTS:
We've taken the following actions to prevent future occurrences:
1. Implemented automatic database failover
2. Added monitoring alerts for connection pool exhaustion
3. Increased test coverage for database failures

Thank you for your patience and continued business.

Best regards,
Honua Support
```

---

## References

- [Monitoring Guide](./README.md)
- [Alert Runbook](./RUNBOOK.md)
- [Google SRE Book - SLOs](https://sre.google/sre-book/service-level-objectives/)
- [Prometheus Docs](https://prometheus.io/docs/)
- [OpenTelemetry Docs](https://opentelemetry.io/docs/)

---

**Version**: 1.0.0
**Last Updated**: November 2024
**Effective Date**: November 15, 2024
**Review Date**: January 15, 2025 (Quarterly)
