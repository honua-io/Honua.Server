# Application Insights Alert Thresholds Documentation

**Last Updated**: 2025-10-18
**Version**: 1.0.0
**Status**: Production Ready

## Table of Contents

1. [Overview](#overview)
2. [Alert Severity Levels](#alert-severity-levels)
3. [Availability Alerts](#availability-alerts)
4. [Performance Alerts](#performance-alerts)
5. [Error Rate Alerts](#error-rate-alerts)
6. [Resource Monitoring Alerts](#resource-monitoring-alerts)
7. [AI/LLM Monitoring Alerts](#aillm-monitoring-alerts)
8. [Database Monitoring Alerts](#database-monitoring-alerts)
9. [Smart Detection Rules](#smart-detection-rules)
10. [Notification Configuration](#notification-configuration)
11. [Alert Response Procedures](#alert-response-procedures)
12. [Threshold Tuning Guidelines](#threshold-tuning-guidelines)

---

## Overview

This document describes the comprehensive alerting strategy for the Honua AI Deployment Consultant Azure infrastructure. The alert rules are designed to provide early warning of issues while minimizing false positives.

### Alert Coverage

- **22 Total Alert Rules** across 7 categories
- **2 Action Groups** (Critical and Warning levels)
- **3 Smart Detection Rules** for anomaly detection
- **Multi-channel Notifications** (Email, SMS, Webhook, Azure App)

### Design Principles

1. **Layered Alerting**: Multiple severity levels (Critical → Error → Warning)
2. **Early Warning**: Catch issues before they impact users
3. **Actionable Alerts**: Each alert has clear remediation steps
4. **Reduced Noise**: Appropriate thresholds to avoid alert fatigue
5. **Context-Aware**: Different thresholds for dev/staging/prod

---

## Alert Severity Levels

| Severity | Level | Response Time | Notification Channels | Examples |
|----------|-------|---------------|----------------------|----------|
| **0** | Critical | Immediate (24/7) | Email, SMS, Azure App, Webhook | Service down, Database unavailable |
| **1** | Error | Within 1 hour | Email, Azure App, Webhook | High error rate, Rate limit errors |
| **2** | Warning | Within 4 hours | Email, Webhook | High latency, Resource usage |
| **3** | Informational | Review during business hours | Email | Trends, capacity planning |

---

## Availability Alerts

### 1. Low Availability Alert

**Alert Name**: `honua-low-availability-{environment}`

**Threshold**: Availability < 99%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Critical (0)

**Description**: Triggers when the overall application availability falls below the 99% SLA target.

**Metric**: `availabilityResults/availabilityPercentage`

**What It Measures**:
- Success rate of availability tests
- Endpoint health checks
- Overall service uptime

**Remediation Steps**:
1. Check Azure Service Health dashboard
2. Review Application Insights Live Metrics
3. Check for deployment issues in the last 30 minutes
4. Verify Function App is running: `az functionapp list --query "[?state=='Running']"`
5. Check for resource constraints (CPU, memory)
6. Review recent exceptions in Application Insights

**False Positive Scenarios**:
- During planned maintenance (suppress alerts)
- Transient Azure platform issues (<5 minutes)
- Health check misconfiguration

**SLA Impact**: Direct impact on 99% uptime SLA

---

### 2. Service Down Alert

**Alert Name**: `honua-service-down-{environment}`

**Threshold**: Request count < 1

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Critical (0)

**Description**: Triggers when no requests are being processed, indicating complete service outage.

**Metric**: `requests/count`

**What It Measures**:
- Total incoming HTTP requests
- Complete service availability

**Remediation Steps**:
1. **Immediate**: Check if Function App is running
   ```bash
   az functionapp show --name func-honua-{suffix} --resource-group rg-honua-{env}-{location}
   ```
2. Check Azure Status: https://status.azure.com
3. Review Function App logs:
   ```bash
   az functionapp log tail --name func-honua-{suffix} --resource-group rg-honua-{env}-{location}
   ```
4. Check for failed deployments
5. Verify network/firewall rules
6. Restart Function App if necessary:
   ```bash
   az functionapp restart --name func-honua-{suffix} --resource-group rg-honua-{env}-{location}
   ```

**False Positive Scenarios**:
- Very low traffic environments during off-hours
- Just after deployment (2-5 minute warm-up)

**SLA Impact**: Critical - Complete service outage

---

## Performance Alerts

### 3. High Response Time Alert

**Alert Name**: `honua-high-response-time-{environment}`

**Threshold**: Average response time > 1000ms (1 second)

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Triggers when the average (P95) response time exceeds 1 second.

**Metric**: `requests/duration`

**What It Measures**:
- Server-side request processing time
- End-to-end latency excluding network

**Remediation Steps**:
1. Identify slow requests in Application Insights:
   - Navigate to: Performance → Server Response Time
   - Sort by Duration
2. Check for:
   - Slow database queries
   - OpenAI API latency
   - AI Search throttling
3. Review Application Map for slow dependencies
4. Check if caching is working properly
5. Consider scaling up if sustained

**Acceptable Ranges**:
- **Excellent**: < 500ms
- **Good**: 500ms - 1000ms
- **Warning**: 1000ms - 5000ms (alert)
- **Critical**: > 5000ms (critical alert)

**False Positive Scenarios**:
- Deployment operations (suppress during deployments)
- Cold start scenarios (Function App scale-out)
- Large AI/LLM requests (expected for complex queries)

---

### 4. Critical Response Time Alert

**Alert Name**: `honua-critical-response-time-{environment}`

**Threshold**: Average response time > 5000ms (5 seconds)

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)

**Description**: Critical alert when response times become unacceptable for users.

**Metric**: `requests/duration`

**Remediation Steps**:
1. **Immediate**: Check for resource exhaustion (CPU, memory)
2. Identify the slowest operations:
   ```kusto
   requests
   | where timestamp > ago(15m)
   | where duration > 5000
   | summarize count(), avg(duration) by name
   | order by avg_duration desc
   ```
3. Check OpenAI API status
4. Review database performance metrics
5. Consider emergency scale-up
6. Enable request throttling if necessary

---

### 5. Slow Dependencies Alert

**Alert Name**: `honua-slow-dependencies-{environment}`

**Threshold**: Dependency duration > 2000ms (2 seconds)

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Alerts when external dependencies (AI, Database, Storage) are responding slowly.

**Metric**: `dependencies/duration`

**What It Measures**:
- OpenAI API latency
- Azure AI Search latency
- PostgreSQL query execution time
- Key Vault access time

**Remediation Steps**:
1. Identify slow dependency:
   ```kusto
   dependencies
   | where timestamp > ago(15m)
   | where duration > 2000
   | summarize count(), avg(duration) by target, name
   | order by avg_duration desc
   ```
2. **If OpenAI is slow**:
   - Check Azure OpenAI service health
   - Review token usage (may be throttled)
   - Consider using multiple deployments
3. **If Database is slow**:
   - Check PostgreSQL metrics (CPU, connections)
   - Review slow query log
   - Analyze query plans
4. **If Storage is slow**:
   - Check storage account throttling
   - Verify network connectivity

---

## Error Rate Alerts

### 6. High Error Rate Alert

**Alert Name**: `honua-high-error-rate-{environment}`

**Threshold**: Dynamic (typically >5% failed requests)

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)
- **Type**: Dynamic (anomaly detection)

**Description**: Triggers when the percentage of failed requests (4xx, 5xx) exceeds baseline.

**Metric**: `requests/failed`

**What It Measures**:
- HTTP 4xx client errors
- HTTP 5xx server errors
- Request failure percentage

**Remediation Steps**:
1. Identify error types:
   ```kusto
   requests
   | where timestamp > ago(15m)
   | where success == false
   | summarize count() by resultCode, name
   | order by count_ desc
   ```
2. **For 4xx errors**:
   - Check for API contract changes
   - Review authentication issues
   - Validate request parameters
3. **For 5xx errors**:
   - Check recent deployments
   - Review exception logs
   - Check dependency availability
4. **For 500 Internal Server Error**:
   - Review exception traces
   - Check configuration errors
   - Verify database connectivity

**Acceptable Ranges**:
- **Excellent**: < 1% error rate
- **Good**: 1% - 2%
- **Warning**: 2% - 5%
- **Critical**: > 5% (alert)

---

### 7. High Exception Rate Alert

**Alert Name**: `honua-high-exception-rate-{environment}`

**Threshold**: > 10 exceptions in 15 minutes

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)

**Description**: Alerts when unhandled exceptions spike.

**Metric**: `exceptions/count`

**Remediation Steps**:
1. Review exception details:
   ```kusto
   exceptions
   | where timestamp > ago(15m)
   | summarize count() by type, outerMessage
   | order by count_ desc
   ```
2. Check for:
   - Null reference exceptions
   - Configuration errors
   - Dependency failures
3. Review stack traces
4. Correlate with deployments
5. Apply hotfix if necessary

---

### 8. Failed Dependencies Alert

**Alert Name**: `honua-failed-dependencies-{environment}`

**Threshold**: > 5 failed dependency calls in 15 minutes

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)

**Description**: Alerts when external services are failing.

**Metric**: `dependencies/failed`

**Remediation Steps**:
1. Identify failing dependency:
   ```kusto
   dependencies
   | where timestamp > ago(15m)
   | where success == false
   | summarize count() by target, name, resultCode
   ```
2. **Azure OpenAI failures**:
   - Check service quota and limits
   - Verify API key is valid
   - Check for rate limiting (429 errors)
3. **PostgreSQL failures**:
   - Check connection string
   - Verify firewall rules
   - Check server availability
4. **Key Vault failures**:
   - Verify managed identity permissions
   - Check for rotated secrets

---

## Resource Monitoring Alerts

### 9. High Memory Usage Alert

**Alert Name**: `honua-high-memory-usage-{environment}`

**Threshold**: Memory usage > 90%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Warns when Function App memory consumption is high.

**Metric**: `MemoryPercentage` (Function App)

**Remediation Steps**:
1. Check memory metrics:
   ```bash
   az monitor metrics list --resource {function-app-id} \
     --metric MemoryPercentage \
     --start-time 2025-10-18T00:00:00Z
   ```
2. Identify memory leaks:
   - Review Application Insights performance counters
   - Check for large object retention
   - Review caching strategy
3. **If sustained**:
   - Scale up to higher tier: Y1 → EP1
   - Review memory allocation in code
   - Optimize data structures

**Memory Thresholds**:
- **Green**: < 70%
- **Yellow**: 70% - 90%
- **Orange**: 90% - 95% (warning alert)
- **Red**: > 95% (critical alert)

---

### 10. Critical Memory Usage Alert

**Alert Name**: `honua-critical-memory-usage-{environment}`

**Threshold**: Memory usage > 95%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Critical (0)

**Description**: Critical alert when Function App is near out-of-memory condition.

**Remediation Steps**:
1. **Immediate**: Scale up Function App
   ```bash
   az functionapp update --name func-honua-{suffix} \
     --resource-group rg-honua-{env}-{location} \
     --plan-name asp-honua-{suffix} \
     --sku EP1
   ```
2. Restart Function App if needed
3. Review memory dumps if available
4. Emergency code review for memory leaks

---

### 11. High CPU Usage Alert

**Alert Name**: `honua-high-cpu-usage-{environment}`

**Threshold**: CPU usage > 80%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Warns when CPU consumption is high.

**Metric**: `CpuPercentage` (Function App)

**Remediation Steps**:
1. Identify CPU-intensive operations
2. Review recent code changes
3. Check for:
   - Inefficient algorithms
   - Excessive logging
   - Heavy AI/LLM processing
4. Consider scaling out (more instances)

---

### 12. HTTP Queue Length Alert

**Alert Name**: `honua-http-queue-length-{environment}`

**Threshold**: > 10 queued requests

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Alerts when requests are backing up (insufficient capacity).

**Metric**: `HttpQueueLength`

**Remediation Steps**:
1. Check current queue:
   ```kusto
   performanceCounters
   | where timestamp > ago(15m)
   | where name == "HTTP Queue Length"
   | summarize avg(value), max(value) by bin(timestamp, 1m)
   ```
2. Scale out Function App:
   ```bash
   az functionapp scale --name func-honua-{suffix} \
     --resource-group rg-honua-{env}-{location} \
     --maximum-instance-count 20
   ```
3. Review cold start issues
4. Consider premium plan for pre-warmed instances

---

## AI/LLM Monitoring Alerts

### 13. Azure OpenAI Rate Limit Alert

**Alert Name**: `honua-openai-rate-limit-{environment}`

**Threshold**: > 5 rate limit errors (429) in 15 minutes

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)

**Description**: Alerts when OpenAI API rate limits are being exceeded.

**Metric**: `RateLimitExceeded` (Cognitive Services)

**What It Measures**:
- HTTP 429 (Too Many Requests) errors
- Token-per-minute (TPM) limit violations
- Requests-per-minute (RPM) limit violations

**Remediation Steps**:
1. Check current quota usage:
   ```bash
   az cognitiveservices account show \
     --name openai-honua-{suffix} \
     --resource-group rg-honua-{env}-{location} \
     --query "properties.quotaLimit"
   ```
2. **Immediate**:
   - Implement exponential backoff (should already exist)
   - Queue non-urgent requests
   - Review concurrent request limits
3. **Short-term**:
   - Request quota increase from Azure
   - Implement request prioritization
   - Add caching for repeated queries
4. **Long-term**:
   - Deploy multiple OpenAI instances
   - Implement load balancing across deployments
   - Use GPT-3.5 for simpler queries

**Rate Limit Tiers** (GPT-4):
- **Basic**: 10K TPM, 20 RPM
- **Standard**: 80K TPM, 480 RPM (default)
- **Enterprise**: Custom limits

---

### 14. High Token Usage Alert

**Alert Name**: `honua-high-token-usage-{environment}`

**Threshold**: > 500,000 tokens per hour

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 1-hour rolling window
- **Severity**: Warning (2)

**Description**: Warns when approaching token quota limits.

**Metric**: `TokensGenerated` (Cognitive Services)

**Remediation Steps**:
1. Review token usage by operation:
   ```kusto
   dependencies
   | where target contains "openai"
   | extend tokens = tolong(customDimensions.tokens)
   | summarize totalTokens = sum(tokens) by name
   | order by totalTokens desc
   ```
2. Optimize prompts:
   - Reduce system message length
   - Use more concise instructions
   - Implement prompt caching
3. Consider using GPT-3.5 for simpler tasks
4. Implement user-based rate limiting

**Cost Impact**:
- GPT-4 Turbo: $0.01 per 1K input tokens, $0.03 per 1K output tokens
- 500K tokens/hour ≈ $7.50 - $15.00/hour
- Monthly projection: $5,400 - $10,800

---

### 15. AI Search Throttling Alert

**Alert Name**: `honua-search-throttling-{environment}`

**Threshold**: > 10% throttled queries

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Error (1)

**Description**: Alerts when AI Search service is throttling requests.

**Metric**: `ThrottledSearchQueriesPercentage`

**Remediation Steps**:
1. Check current tier limits:
   - **Basic**: 3 queries/second
   - **Standard**: 15 queries/second
2. Review query patterns:
   ```kusto
   dependencies
   | where target contains "search.windows.net"
   | summarize count(), avg(duration) by name
   ```
3. **Immediate**:
   - Implement client-side retry with backoff
   - Cache search results
4. **Short-term**:
   - Upgrade to Standard tier if on Basic
   - Add more replicas (Standard S1+)
5. **Long-term**:
   - Review search index design
   - Implement request queuing

---

## Database Monitoring Alerts

### 16. High Database CPU Alert

**Alert Name**: `honua-database-high-cpu-{environment}`

**Threshold**: CPU usage > 80%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Warns when PostgreSQL CPU usage is high.

**Metric**: `cpu_percent` (PostgreSQL Flexible Server)

**Remediation Steps**:
1. Identify expensive queries:
   ```sql
   SELECT query, calls, total_time, mean_time
   FROM pg_stat_statements
   ORDER BY total_time DESC
   LIMIT 10;
   ```
2. Check for:
   - Missing indexes
   - Full table scans
   - N+1 query patterns
3. **Immediate**:
   - Kill long-running queries if necessary
   - Implement connection pooling
4. **Short-term**:
   - Add indexes
   - Optimize queries
5. **Long-term**:
   - Scale up SKU: B_Standard_B1ms → GP_Standard_D2s_v3
   - Consider read replicas

---

### 17. High Database Memory Alert

**Alert Name**: `honua-database-high-memory-{environment}`

**Threshold**: Memory usage > 90%

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Warns when PostgreSQL memory usage is high.

**Metric**: `memory_percent` (PostgreSQL Flexible Server)

**Remediation Steps**:
1. Check buffer cache hit ratio:
   ```sql
   SELECT
     sum(heap_blks_read) as heap_read,
     sum(heap_blks_hit)  as heap_hit,
     sum(heap_blks_hit) / (sum(heap_blks_hit) + sum(heap_blks_read)) as ratio
   FROM pg_statio_user_tables;
   ```
   - Target: > 99% cache hit ratio
2. Review `shared_buffers` configuration
3. Scale up to higher memory tier if sustained

---

### 18. High Active Connections Alert

**Alert Name**: `honua-database-high-connections-{environment}`

**Threshold**: > 80 active connections (adjust based on max_connections)

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Warning (2)

**Description**: Warns when approaching connection pool limit.

**Metric**: `active_connections` (PostgreSQL Flexible Server)

**Remediation Steps**:
1. Check current connections:
   ```sql
   SELECT count(*), state, wait_event_type
   FROM pg_stat_activity
   GROUP BY state, wait_event_type
   ORDER BY count DESC;
   ```
2. Identify connection leaks:
   - Check for idle connections
   - Review application connection pool settings
3. **Immediate**:
   - Increase `max_connections` (requires restart)
   - Kill idle connections
4. **Short-term**:
   - Implement connection pooling (PgBouncer)
   - Review connection timeout settings
5. **Long-term**:
   - Scale up database tier
   - Implement read replicas

**Connection Limits by SKU**:
- **B_Standard_B1ms**: 50 connections
- **GP_Standard_D2s_v3**: 859 connections

---

### 19. Database Connection Failed Alert

**Alert Name**: `honua-database-connection-failed-{environment}`

**Threshold**: > 5 failed connection attempts in 15 minutes

**Evaluation**:
- **Frequency**: Every 5 minutes
- **Window**: 15-minute rolling window
- **Severity**: Critical (0)

**Description**: Critical alert when database connections are failing.

**Metric**: `connections_failed` (PostgreSQL Flexible Server)

**Remediation Steps**:
1. **Immediate**: Check database status
   ```bash
   az postgres flexible-server show \
     --name postgres-honua-{suffix} \
     --resource-group rg-honua-{env}-{location}
   ```
2. Verify firewall rules:
   ```bash
   az postgres flexible-server firewall-rule list \
     --name postgres-honua-{suffix} \
     --resource-group rg-honua-{env}-{location}
   ```
3. Check for:
   - Connection string errors
   - Authentication failures
   - Network issues
   - Connection pool exhaustion
4. Review recent configuration changes
5. Check Azure Service Health

---

## Smart Detection Rules

### 20. Failure Anomalies

**Rule**: Automatic detection of abnormal failure rate increases

**Description**: Machine learning-based detection of unusual spikes in failure rates compared to historical baseline.

**What It Detects**:
- Sudden increases in exceptions
- Request failure rate anomalies
- Dependency failure spikes

**Response**:
- Email notification to admin
- Investigate recent deployments
- Review correlated metrics

---

### 21. Slow Page Load Time

**Rule**: Automatic detection of page load degradation

**Description**: Detects when page load times deviate significantly from baseline.

**Response**:
- Review performance traces
- Check for recent code changes
- Analyze dependency latency

---

### 22. Slow Server Response Time

**Rule**: Automatic detection of server response time degradation

**Description**: ML-based detection of unusual increases in server response times.

**Response**:
- Check resource utilization
- Review database query performance
- Analyze slow operations

---

## Notification Configuration

### Action Group: Critical Alerts

**Name**: `ag-honua-critical-{environment}`

**Notification Channels**:
1. **Email**: Sent to admin email with detailed alert context
2. **SMS**: (Optional) For 24/7 on-call support
3. **Azure Mobile App**: Push notifications
4. **Webhook**: Integration with Slack/Teams/PagerDuty

**Alert Schema**: Common alert schema (standardized JSON format)

**Example Notification**:
```json
{
  "schemaId": "azureMonitorCommonAlertSchema",
  "data": {
    "essentials": {
      "alertId": "/subscriptions/.../alertrules/honua-service-down",
      "alertRule": "honua-service-down-prod",
      "severity": "Sev0",
      "signalType": "Metric",
      "monitorCondition": "Fired",
      "firedDateTime": "2025-10-18T12:00:00Z"
    },
    "alertContext": {
      "condition": {
        "metricName": "requests/count",
        "metricValue": 0,
        "threshold": 1,
        "operator": "LessThan"
      }
    }
  }
}
```

---

### Action Group: Warning Alerts

**Name**: `ag-honua-warning-{environment}`

**Notification Channels**:
1. **Email**: Sent to admin email
2. **Webhook**: (Optional) For tracking/ticketing systems

**Alert Schema**: Common alert schema

---

## Alert Response Procedures

### Critical Alert Response (Severity 0)

**Response Time**: Immediate (24/7 if production)

**Steps**:
1. **Acknowledge** alert within 5 minutes
2. **Assess** impact:
   - Is service completely down?
   - How many users affected?
   - What is the business impact?
3. **Triage**:
   - Check Azure Service Health
   - Review recent deployments
   - Check for infrastructure changes
4. **Escalate** if needed (on-call manager)
5. **Resolve**:
   - Apply fix
   - Monitor for stability
6. **Document**:
   - Root cause
   - Resolution steps
   - Prevention measures

---

### Error Alert Response (Severity 1)

**Response Time**: Within 1 hour (business hours), 2 hours (after hours)

**Steps**:
1. **Acknowledge** alert
2. **Investigate** using Application Insights
3. **Plan** resolution (immediate vs scheduled)
4. **Implement** fix
5. **Verify** resolution
6. **Document** in incident log

---

### Warning Alert Response (Severity 2)

**Response Time**: Within 4 hours (business hours), next business day (after hours)

**Steps**:
1. **Review** alert details
2. **Assess** if action needed now or can wait
3. **Schedule** fix if non-urgent
4. **Monitor** for escalation
5. **Document** in backlog

---

## Threshold Tuning Guidelines

### When to Adjust Thresholds

1. **Too Many False Positives**:
   - Increase threshold gradually (10% increments)
   - Extend evaluation window
   - Consider dynamic thresholds

2. **Missing Real Issues**:
   - Decrease threshold
   - Add additional alert rule with tighter threshold
   - Enable Smart Detection

3. **Environment Differences**:
   - Dev: Higher thresholds, longer windows
   - Staging: Medium thresholds
   - Production: Strictest thresholds

---

### Threshold Adjustment Process

1. **Collect Data**: Gather 2 weeks of baseline metrics
2. **Analyze Patterns**:
   - Calculate P50, P95, P99 values
   - Identify normal operational ranges
3. **Set Thresholds**:
   - Warning: P95 + 20%
   - Error: P99 + 10%
   - Critical: Absolute operational limits
4. **Test**: Monitor for 1 week
5. **Tune**: Adjust based on false positive rate
6. **Document**: Record threshold changes

---

### Example Threshold Tuning

**Scenario**: High Response Time alert firing too often

**Analysis**:
```kusto
requests
| where timestamp > ago(14d)
| summarize
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
| project
    p50_ms = p50,
    p95_ms = p95,
    p99_ms = p99
```

**Results**:
- P50: 250ms
- P95: 800ms
- P99: 1500ms

**Recommendation**:
- Warning Threshold: 1200ms (P95 + 50%)
- Error Threshold: 2000ms (P99 + 33%)

**Implementation**:
```hcl
resource "azurerm_monitor_metric_alert" "high_response_time" {
  # ... existing config ...

  criteria {
    threshold = 1200  # Updated from 1000ms
  }
}
```

---

## Alert Testing

### Testing Alert Rules

**1. Manual Testing**:
```bash
# Test high error rate alert
for i in {1..20}; do
  curl -X POST https://func-honua-{suffix}.azurewebsites.net/api/test-error
done
```

**2. Load Testing**:
```bash
# Test high latency alert
artillery quick --count 100 --num 50 https://func-honua-{suffix}.azurewebsites.net
```

**3. Verify Alert Fired**:
```bash
az monitor metrics alert show \
  --name honua-high-error-rate-prod \
  --resource-group rg-honua-prod-eastus
```

---

## Maintenance Windows

### Suppressing Alerts During Maintenance

**1. Create Alert Processing Rule**:
```hcl
resource "azurerm_monitor_alert_processing_rule_suppression" "maintenance_window" {
  name                = "maintenance-suppression"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_resource_group.main.id]

  schedule {
    recurrence {
      weekly {
        days_of_week = ["Sunday"]
      }
    }
    time_zone = "Eastern Standard Time"
  }

  condition {
    severity {
      operator = "Equals"
      values   = ["Sev2", "Sev3"]  # Suppress warning and info
    }
  }
}
```

**2. Pre-Maintenance Notification**:
- Send notification 24 hours before
- Include maintenance window details
- Specify which alerts will be suppressed

---

## Cost Optimization

### Alert Cost Considerations

**Metric Alert Pricing** (Azure Monitor):
- First 10 alert rules: Free
- Additional alerts: $0.10 per alert rule per month
- Dynamic threshold alerts: $1.00 per alert rule per month

**Current Configuration**:
- 22 alert rules
- 1 dynamic threshold alert
- **Estimated Cost**: ~$2.20/month

**Recommendations**:
- Consolidate similar alerts where possible
- Use dynamic thresholds only when necessary
- Review and disable unused alerts quarterly

---

## Summary

This alert configuration provides comprehensive monitoring across:
- ✅ Availability (99% SLA target)
- ✅ Performance (1s response time target)
- ✅ Error rates (5% threshold)
- ✅ Resource utilization (90% memory, 80% CPU)
- ✅ AI/LLM operations (rate limits, token usage)
- ✅ Database health (connections, performance)
- ✅ Anomaly detection (smart detection rules)

**Total Coverage**: 22 alert rules + 3 smart detection rules = 25 monitoring rules

---

## References

- [Azure Monitor Metrics](https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/metrics-supported)
- [Application Insights Metrics](https://learn.microsoft.com/en-us/azure/azure-monitor/app/standard-metrics)
- [Alert Best Practices](https://learn.microsoft.com/en-us/azure/azure-monitor/best-practices-alerts)
- [PostgreSQL Metrics](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-monitoring)
- [Azure OpenAI Limits](https://learn.microsoft.com/en-us/azure/ai-services/openai/quotas-limits)

---

**Last Updated**: 2025-10-18
**Maintained By**: Honua DevOps Team
**Review Cycle**: Quarterly
