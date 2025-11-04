# Application Insights Alerts - Quick Reference

**Last Updated**: 2025-10-18
**For**: Operations Team / On-Call Engineers

---

## 🚨 Alert Priority Matrix

| Severity | Response Time | On-Call? | Action Required |
|----------|---------------|----------|-----------------|
| **Critical (Sev0)** | Immediate | YES | Immediate investigation & resolution |
| **Error (Sev1)** | 1 hour | YES | Investigation required |
| **Warning (Sev2)** | 4 hours | NO | Review during business hours |
| **Info (Sev3)** | Next day | NO | Review in daily standup |

---

## 🔴 Critical Alerts (Sev0)

### 1. Service Down
**Alert Name**: `honua-service-down-{env}`
**Condition**: No requests in 15 minutes
**Quick Fix**:
```bash
# Check status
az functionapp show -n func-honua-{suffix} -g rg-honua-{env}-{location} --query state

# Restart if needed
az functionapp restart -n func-honua-{suffix} -g rg-honua-{env}-{location}
```

---

### 2. Low Availability
**Alert Name**: `honua-low-availability-{env}`
**Condition**: Availability < 99%
**Quick Fix**:
- Check Azure Service Health: https://status.azure.com
- Review Application Insights Live Metrics
- Check recent deployments

---

### 3. Critical Memory Usage
**Alert Name**: `honua-critical-memory-usage-{env}`
**Condition**: Memory > 95%
**Quick Fix**:
```bash
# Scale up immediately
az functionapp update -n func-honua-{suffix} -g rg-honua-{env}-{location} --set sku.name=EP1
```

---

### 4. Database Connection Failed
**Alert Name**: `honua-database-connection-failed-{env}`
**Condition**: > 5 failed connections in 15 min
**Quick Fix**:
```bash
# Check database status
az postgres flexible-server show -n postgres-honua-{suffix} -g rg-honua-{env}-{location}

# Check firewall rules
az postgres flexible-server firewall-rule list -s postgres-honua-{suffix} -g rg-honua-{env}-{location}
```

---

## 🟠 Error Alerts (Sev1)

### 5. High Error Rate
**Alert Name**: `honua-high-error-rate-{env}`
**Condition**: Error rate > 5%
**Investigation Query**:
```kusto
requests
| where timestamp > ago(15m)
| where success == false
| summarize count() by resultCode, name
| order by count_ desc
```

---

### 6. High Exception Rate
**Alert Name**: `honua-high-exception-rate-{env}`
**Condition**: > 10 exceptions in 15 min
**Investigation Query**:
```kusto
exceptions
| where timestamp > ago(15m)
| summarize count() by type, outerMessage
| order by count_ desc
```

---

### 7. Failed Dependencies
**Alert Name**: `honua-failed-dependencies-{env}`
**Condition**: > 5 failed dependency calls
**Investigation Query**:
```kusto
dependencies
| where timestamp > ago(15m)
| where success == false
| summarize count() by target, name, resultCode
```

---

### 8. OpenAI Rate Limit
**Alert Name**: `honua-openai-rate-limit-{env}`
**Condition**: > 5 rate limit errors
**Quick Fix**:
- Check quota: https://portal.azure.com → Cognitive Services → Quotas
- Request increase if needed
- Implement request queuing

---

### 9. AI Search Throttling
**Alert Name**: `honua-search-throttling-{env}`
**Condition**: > 10% throttled queries
**Quick Fix**:
- Check tier: Basic (3 QPS) vs Standard (15 QPS)
- Consider upgrade or add replicas

---

## 🟡 Warning Alerts (Sev2)

### 10. High Response Time
**Alert Name**: `honua-high-response-time-{env}`
**Condition**: P95 > 1 second
**Investigation**:
- Check Application Insights → Performance
- Review slow operations
- Check dependency latency

---

### 11. Critical Response Time
**Alert Name**: `honua-critical-response-time-{env}`
**Condition**: P95 > 5 seconds
**Investigation Query**:
```kusto
requests
| where timestamp > ago(15m)
| where duration > 5000
| summarize count(), avg(duration) by name
| order by avg_duration desc
```

---

### 12. Slow Dependencies
**Alert Name**: `honua-slow-dependencies-{env}`
**Condition**: Dependency duration > 2 seconds
**Investigation Query**:
```kusto
dependencies
| where timestamp > ago(15m)
| where duration > 2000
| summarize count(), avg(duration) by target, name
| order by avg_duration desc
```

---

### 13-15. Resource Alerts
**High Memory**: > 90%
**High CPU**: > 80%
**HTTP Queue**: > 10 queued requests

**Quick Actions**:
- Scale up: Increase SKU
- Scale out: Add instances
- Optimize: Review resource-intensive code

---

### 16. High Token Usage
**Alert Name**: `honua-high-token-usage-{env}`
**Condition**: > 500K tokens/hour
**Cost Impact**: ~$7.50 - $15.00/hour
**Actions**:
- Review token usage by operation
- Optimize prompts
- Use GPT-3.5 for simpler queries

---

### 17-19. Database Alerts
**High CPU**: > 80%
**High Memory**: > 90%
**High Connections**: > 80 active

**Investigation**:
```sql
-- Slow queries
SELECT query, calls, total_time, mean_time
FROM pg_stat_statements
ORDER BY total_time DESC
LIMIT 10;

-- Active connections
SELECT count(*), state, wait_event_type
FROM pg_stat_activity
GROUP BY state, wait_event_type;
```

---

## 🔧 Common Commands

### Check Alert Status
```bash
az monitor metrics alert show \
  --name {alert-name} \
  --resource-group rg-honua-{env}-{location}
```

### List Recent Alerts
```bash
az monitor activity-log list \
  --resource-group rg-honua-{env}-{location} \
  --caller "Azure Monitor Alerts" \
  --start-time 2025-10-18T00:00:00Z
```

### Disable Alert (Maintenance)
```bash
az monitor metrics alert update \
  --name {alert-name} \
  --resource-group rg-honua-{env}-{location} \
  --enabled false
```

### Re-enable Alert
```bash
az monitor metrics alert update \
  --name {alert-name} \
  --resource-group rg-honua-{env}-{location} \
  --enabled true
```

---

## 📊 Application Insights Queries

### Error Rate (Last Hour)
```kusto
requests
| where timestamp > ago(1h)
| summarize
    total = count(),
    failed = countif(success == false),
    error_rate = 100.0 * countif(success == false) / count()
| project error_rate, total, failed
```

### Response Time Percentiles
```kusto
requests
| where timestamp > ago(1h)
| summarize
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    p99 = percentile(duration, 99)
```

### Top Slow Operations
```kusto
requests
| where timestamp > ago(1h)
| summarize avg(duration), count() by name
| order by avg_duration desc
| take 10
```

### Dependency Failures
```kusto
dependencies
| where timestamp > ago(1h)
| where success == false
| summarize count() by target, name, resultCode
| order by count_ desc
```

### Exception Summary
```kusto
exceptions
| where timestamp > ago(1h)
| summarize count() by type, outerMessage
| order by count_ desc
```

---

## 🔗 Quick Links

### Azure Portal
- **Resource Group**: https://portal.azure.com/#resource/subscriptions/.../resourceGroups/rg-honua-{env}-{location}
- **Application Insights**: https://portal.azure.com/#resource/.../components/appi-honua-{suffix}
- **Function App**: https://portal.azure.com/#resource/.../sites/func-honua-{suffix}
- **PostgreSQL**: https://portal.azure.com/#resource/.../flexibleServers/postgres-honua-{suffix}
- **Azure OpenAI**: https://portal.azure.com/#resource/.../accounts/openai-honua-{suffix}

### Dashboards
- **Live Metrics**: Application Insights → Live Metrics
- **Performance**: Application Insights → Performance
- **Failures**: Application Insights → Failures
- **Application Map**: Application Insights → Application Map

### Service Health
- **Azure Status**: https://status.azure.com
- **Service Health**: https://portal.azure.com/#blade/Microsoft_Azure_Health/AzureHealthBrowseBlade

---

## 📞 Escalation Path

### Level 1: On-Call Engineer
- **Response**: All Sev0 and Sev1 alerts
- **Escalate if**: Cannot resolve in 30 minutes (Sev0) or 2 hours (Sev1)

### Level 2: Senior SRE
- **Response**: Escalated incidents
- **Escalate if**: Requires code changes or architecture decisions

### Level 3: Engineering Manager
- **Response**: Major incidents, multiple system failures
- **Escalate if**: Requires business decisions or external vendor engagement

---

## 🛠️ Auto-Remediation Scripts

### Restart Function App
```bash
#!/bin/bash
RESOURCE_GROUP="rg-honua-prod-eastus"
FUNCTION_APP="func-honua-a1b2c3"

echo "Restarting Function App: $FUNCTION_APP"
az functionapp restart --name $FUNCTION_APP --resource-group $RESOURCE_GROUP

echo "Waiting 60 seconds for startup..."
sleep 60

echo "Checking status..."
az functionapp show --name $FUNCTION_APP --resource-group $RESOURCE_GROUP --query state
```

### Scale Out Function App
```bash
#!/bin/bash
RESOURCE_GROUP="rg-honua-prod-eastus"
FUNCTION_APP="func-honua-a1b2c3"
MAX_INSTANCES=20

echo "Scaling out Function App to $MAX_INSTANCES instances"
az functionapp scale --name $FUNCTION_APP \
  --resource-group $RESOURCE_GROUP \
  --maximum-instance-count $MAX_INSTANCES
```

### Kill Long-Running DB Queries
```sql
-- Find long-running queries
SELECT pid, now() - query_start as duration, query
FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > interval '5 minutes'
ORDER BY duration DESC;

-- Kill specific query (replace PID)
SELECT pg_terminate_backend(12345);
```

---

## 📋 Alert Checklist

### When Alert Fires
- [ ] Acknowledge alert in notification channel
- [ ] Check Azure Service Health
- [ ] Review Application Insights Live Metrics
- [ ] Check recent deployments (last 1 hour)
- [ ] Review relevant logs/traces
- [ ] Take immediate action if Sev0
- [ ] Document actions taken
- [ ] Update incident ticket

### After Resolution
- [ ] Verify metrics returned to normal
- [ ] Monitor for 15 minutes to ensure stability
- [ ] Document root cause
- [ ] Create follow-up task if needed
- [ ] Update runbook if new issue
- [ ] Close incident ticket

---

## 💡 Pro Tips

1. **Use Application Map**: Quickly identify failing dependencies
2. **Check Recent Deployments**: Most incidents correlate with deployments
3. **Compare Time Periods**: Use "Compare to last week" in metrics
4. **Enable Smart Detection**: ML-based anomaly detection
5. **Create Custom Dashboards**: Pin frequently-used queries
6. **Set Up Mobile App**: Get alerts on phone (Azure mobile app)
7. **Use Workbooks**: Pre-built investigation templates
8. **Correlate Metrics**: Look at CPU + Memory + Latency together
9. **Check Logs First**: Often faster than metrics for root cause
10. **Document Everything**: Update runbooks as you learn

---

## 📚 Reference Documents

- **Full Alert Documentation**: [ALERT_THRESHOLDS.md](ALERT_THRESHOLDS.md)
- **Notification Samples**: [ALERT_NOTIFICATION_SAMPLES.md](ALERT_NOTIFICATION_SAMPLES.md)
- **Monitoring Setup**: [/docs/MONITORING_SETUP.md](../../../docs/MONITORING_SETUP.md)
- **Alert Architecture**: [/docs/ALERT_ARCHITECTURE.md](../../../docs/ALERT_ARCHITECTURE.md)

---

## 🆘 Emergency Contacts

| Role | Name | Email | Phone | Slack |
|------|------|-------|-------|-------|
| On-Call Engineer | (Rotation) | oncall@honua.io | +1-555-0100 | @oncall |
| Senior SRE | TBD | sre-lead@honua.io | +1-555-0101 | @sre-lead |
| Engineering Manager | TBD | eng-manager@honua.io | +1-555-0102 | @eng-manager |
| Azure Support | Microsoft | - | - | Open ticket in portal |

---

**Print This Guide** and keep it handy for on-call rotations!

**Last Updated**: 2025-10-18
**Maintained By**: Honua DevOps Team
