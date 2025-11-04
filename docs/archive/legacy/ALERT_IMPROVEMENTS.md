# Alert Architecture Improvements

## Overview

The alert system has been enhanced with enterprise-grade features for reliability, observability, and operations.

## Improvements Implemented

### 1. Alert Deduplication & Throttling

**Problem**: Alert storms can overwhelm notification channels when the same issue triggers hundreds of alerts.

**Solution**: Time-based deduplication and rate limiting per alert fingerprint.

**Features**:
- **Deduplication Windows**: Same alert within N minutes → suppressed
  - Critical: 5 minutes
  - High: 10 minutes
  - Warning: 15 minutes
  - Default: 30 minutes
- **Rate Limiting**: Maximum alerts per hour per fingerprint
  - Critical: 20/hour
  - High: 10/hour
  - Warning: 5/hour
  - Default: 3/hour
- **Automatic Cleanup**: Old entries removed after 24 hours
- **Metrics**: Track suppressed alerts by reason

**Configuration**:
```json
{
  "Alerts": {
    "Deduplication": {
      "CriticalWindowMinutes": 5,
      "WarningWindowMinutes": 15
    },
    "RateLimit": {
      "CriticalPerHour": 20,
      "WarningPerHour": 5
    }
  }
}
```

**Example**:
```
12:00 - Alert fires → Sent
12:02 - Same alert → Suppressed (within 5min window)
12:04 - Same alert → Suppressed (within 5min window)
12:06 - Same alert → Sent (5min window expired)
```

---

### 2. Alert Persistence & Audit Trail

**Problem**: No history of alerts sent, making troubleshooting and compliance difficult.

**Solution**: PostgreSQL database with complete alert history.

**Features**:
- **Full Alert History**: Every alert (sent + suppressed) stored
- **Queryable API**: REST endpoints for history queries
- **Metadata**: Source, severity, timestamps, providers, suppression reasons
- **Labels & Context**: JSON storage for structured data
- **Retention**: Configurable (default: indefinite)

**Database Schema**:
- `AlertHistory`: All alerts with full details
- `Acknowledgements`: User acknowledgements
- `SilencingRules`: Active silencing rules

**API Endpoints**:
```bash
# Get recent alerts
GET /api/alerts/history?limit=100&severity=critical

# Get specific alert
GET /api/alerts/history/{fingerprint}

# Query shows:
# - When alert was received
# - Whether it was sent or suppressed
# - Which providers received it
# - Suppression reason (deduplication, silenced, acknowledged)
```

---

### 3. Circuit Breaker Per Provider

**Problem**: If a provider is down, we keep failing and wasting resources on every alert.

**Solution**: Circuit breaker pattern with automatic recovery.

**Features**:
- **Failure Threshold**: Open circuit after N failures (default: 5)
- **Break Duration**: Cool-down period before retry (default: 60s)
- **States**:
  - **Closed**: Normal operation
  - **Open**: Provider disabled, requests fail fast
  - **Half-Open**: Testing if provider recovered
- **Per-Provider**: Each provider has its own circuit breaker
- **Metrics**: Track circuit breaker state changes

**Behavior**:
```
Attempt 1-5: Call provider → Fail
Attempt 6: Circuit OPENS → Fast fail for 60s
After 60s: Circuit HALF-OPEN → Try one request
  Success → Circuit CLOSED (recovered)
  Failure → Circuit OPEN again for 60s
```

**Configuration**:
```json
{
  "Alerts": {
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "BreakDurationSeconds": 60
    }
  }
}
```

---

### 4. Retry Logic with Exponential Backoff

**Problem**: Transient failures (network blips) cause alerts to fail permanently.

**Solution**: Automatic retry with exponential backoff.

**Features**:
- **Max Retries**: Configurable attempts (default: 3)
- **Exponential Backoff**: 1s → 2s → 4s delays
- **Per-Provider**: Each provider retries independently
- **Logging**: Each retry logged with attempt number

**Retry Sequence**:
```
Attempt 1: Immediate
Attempt 2: After 1000ms
Attempt 3: After 2000ms
Attempt 4: After 4000ms (if max=4)
```

**Configuration**:
```json
{
  "Alerts": {
    "Retry": {
      "MaxAttempts": 3,
      "BaseDelayMs": 1000
    }
  }
}
```

**Stacking with Circuit Breaker**:
```
Alert → Retry(Circuit(Provider))
1. Retry wraps circuit breaker wraps provider
2. Retry handles transient failures
3. Circuit breaker handles persistent failures
```

---

### 5. Alert Acknowledgement & Silencing

**Problem**: No way to silence known issues or acknowledge alerts during maintenance.

**Solution**: AlertManager-style acknowledgements and silencing rules.

**Acknowledgements**:
- Acknowledge specific alert by fingerprint
- Optional expiration time
- Comment field for notes
- Acknowledged alerts are suppressed

**Example**:
```bash
POST /api/alerts/acknowledge
{
  "fingerprint": "abc123",
  "acknowledgedBy": "oncall@example.com",
  "comment": "Working on fix, ETA 2 hours",
  "expiresInMinutes": 120
}
```

**Silencing Rules**:
- Match alerts by labels/fields
- Time-based (start/end)
- Regex support
- Created by user with comment

**Example**:
```bash
POST /api/alerts/silence
{
  "name": "Maintenance Window",
  "matchers": {
    "service": "database",
    "severity": "~(warning|medium)"
  },
  "createdBy": "admin",
  "startsAt": "2024-01-15T02:00:00Z",
  "endsAt": "2024-01-15T04:00:00Z",
  "comment": "Scheduled database maintenance"
}
```

**API Endpoints**:
```bash
# Acknowledge alert
POST /api/alerts/acknowledge

# Create silencing rule
POST /api/alerts/silence

# List active silences
GET /api/alerts/silences

# Delete silence
DELETE /api/alerts/silences/{id}
```

---

### 6. Alerting System Metrics

**Problem**: No visibility into the alerting system itself.

**Solution**: OpenTelemetry metrics for alert pipeline observability.

**Metrics**:
- `honua.alerts.received` - Alerts received by source & severity
- `honua.alerts.sent` - Alerts sent by provider & severity
- `honua.alerts.suppressed` - Alerts suppressed by reason (deduplication, silenced, acknowledged)
- `honua.alerts.errors` - Alert delivery failures by provider
- `honua.alerts.latency` - Alert processing time by provider
- `honua.alerts.circuit_breaker_state` - Circuit breaker state by provider (0=Closed, 1=Open, 2=HalfOpen)

**Prometheus Queries**:
```promql
# Alert suppression rate
rate(honua_alerts_suppressed_total[5m]) / rate(honua_alerts_received_total[5m])

# Provider success rate
rate(honua_alerts_sent_total{success="true"}[5m]) / rate(honua_alerts_received_total[5m])

# Circuit breaker open count
count(honua_alerts_circuit_breaker_state == 1)

# P95 latency by provider
histogram_quantile(0.95, rate(honua_alerts_latency_bucket[5m]))
```

**Grafana Dashboard**:
- Alert volume by severity
- Suppression rate by reason
- Provider health (success rate, circuit breaker state)
- Latency percentiles

---

## Alert Processing Flow

**Enhanced Pipeline**:
```
1. Alert Received
   ↓
2. Record Metrics (alerts_received)
   ↓
3. Check Silencing Rules → If matched, suppress & persist
   ↓
4. Check Acknowledgements → If acknowledged, suppress & persist
   ↓
5. Check Deduplication → If duplicate, suppress & persist
   ↓
6. For each enabled provider:
   - Retry Logic (3 attempts with exponential backoff)
     ↓
   - Circuit Breaker (fail fast if provider down)
     ↓
   - Publish to Provider (AWS, Azure, PagerDuty, etc.)
   ↓
7. Record Metrics (alerts_sent, latency, errors)
   ↓
8. Record Deduplication State
   ↓
9. Persist to Database (history, providers, status)
   ↓
10. Return Response
```

**Suppression Priority**:
1. Silencing Rules (highest priority)
2. Acknowledgements
3. Deduplication (lowest priority)

---

## Configuration Example

**Complete appsettings.json**:
```json
{
  "ConnectionStrings": {
    "AlertHistory": "Host=db;Database=honua_alerts;Username=honua"
  },
  "Alerts": {
    "Deduplication": {
      "CriticalWindowMinutes": 5,
      "HighWindowMinutes": 10,
      "WarningWindowMinutes": 15,
      "DefaultWindowMinutes": 30
    },
    "RateLimit": {
      "CriticalPerHour": 20,
      "HighPerHour": 10,
      "WarningPerHour": 5,
      "DefaultPerHour": 3
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "BreakDurationSeconds": 60
    },
    "Retry": {
      "MaxAttempts": 3,
      "BaseDelayMs": 1000
    },
    "SNS": {
      "CriticalTopicArn": "arn:aws:sns:...",
      ...
    },
    ...
  }
}
```

---

## Deployment

**Database Setup**:
```bash
# Create database
createdb honua_alerts

# Migrations run automatically on startup
# Or manually:
cd src/Honua.Server.AlertReceiver
dotnet ef database update
```

**Docker Compose**:
```yaml
services:
  alert-receiver:
    image: honua-alert-receiver
    environment:
      - ConnectionStrings__AlertHistory=Host=postgres;Database=honua_alerts
      - Alerts__Deduplication__CriticalWindowMinutes=5
      - Alerts__CircuitBreaker__FailureThreshold=5
    depends_on:
      - postgres

  postgres:
    image: postgres:16
    environment:
      - POSTGRES_DB=honua_alerts
      - POSTGRES_USER=honua
      - POSTGRES_PASSWORD=honua
```

---

## Benefits Summary

| Feature | Benefit |
|---------|---------|
| **Deduplication** | Prevents alert storms, reduces noise |
| **Persistence** | Audit trail, compliance, troubleshooting |
| **Circuit Breaker** | Protects against cascading failures |
| **Retry Logic** | Handles transient failures automatically |
| **Acknowledgement** | Operator control, reduce alert fatigue |
| **Metrics** | Observability of alerting system itself |

**Before**:
```
100 errors/sec → 100 alerts/sec → Slack rate limit → Alerts dropped
```

**After**:
```
100 errors/sec → Dedup → 1 alert (first)
               → Retry → 3 attempts if failed
               → Circuit breaker → Fast fail if provider down
               → Metrics → Track all suppressed alerts
               → DB → Audit trail of all 100 alerts
```

**Alert Storm Protection**:
- Same error 1000 times in 1 minute
- Old system: 1000 notifications
- New system: 1 notification (first), 999 suppressed, all logged

---

## Monitoring the Alerting System

**Key Metrics to Watch**:
1. **Suppression Rate**: Should be 10-30% (higher = potential config issue)
2. **Circuit Breaker Opens**: Should be rare (frequent = provider problems)
3. **Retry Success Rate**: Should be >90% (lower = provider instability)
4. **Alert Latency**: Should be <500ms p95 (higher = performance issue)

**Alerts on Alerts** (Meta-alerting):
```promql
# Alert if too many alerts suppressed (>50%)
rate(honua_alerts_suppressed_total[10m]) / rate(honua_alerts_received_total[10m]) > 0.5

# Alert if circuit breaker stuck open
honua_alerts_circuit_breaker_state{state="open"} == 1 for 5m

# Alert if high error rate
rate(honua_alerts_errors_total[5m]) / rate(honua_alerts_received_total[5m]) > 0.1
```

---

## Testing the Improvements

**Test Deduplication**:
```bash
# Send same alert 5 times rapidly
for i in {1..5}; do
  curl -X POST http://localhost:8080/api/alerts \
    -H "Content-Type: application/json" \
    -d '{
      "name": "TestAlert",
      "severity": "critical",
      "source": "test",
      "summary": "Test deduplication"
    }'
done

# Expected: First sent, rest suppressed (deduplication)
```

**Test Circuit Breaker**:
```bash
# Stop a provider (e.g., disable Slack webhook)
# Send 6 critical alerts
# Expected: First 5 fail, 6th triggers circuit breaker open
# Check metrics: circuit_breaker_state == 1 (open)
```

**Test Acknowledgement**:
```bash
# Send alert
curl -X POST http://localhost:8080/api/alerts \
  -H "Content-Type: application/json" \
  -d '{"name": "DiskFull", "severity": "critical", "source": "system"}'

# Acknowledge it
curl -X POST http://localhost:8080/api/alerts/acknowledge \
  -H "Content-Type: application/json" \
  -d '{
    "fingerprint": "<fingerprint_from_first_response>",
    "acknowledgedBy": "oncall@example.com",
    "comment": "Working on cleanup",
    "expiresInMinutes": 60
  }'

# Send same alert again
# Expected: Suppressed (acknowledged)
```

**Test Silencing Rules**:
```bash
# Create silence for maintenance window
curl -X POST http://localhost:8080/api/alerts/silence \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Maintenance",
    "matchers": {"service": "database"},
    "createdBy": "admin",
    "startsAt": "2025-01-15T02:00:00Z",
    "endsAt": "2025-01-15T04:00:00Z",
    "comment": "Database upgrade"
  }'

# Send database alert during window
# Expected: Suppressed (silenced)
```

---

## Migration Guide

**From Basic Alert System**:

1. **Add Database Connection**:
```bash
# Create PostgreSQL database
createdb honua_alerts

# Update appsettings.json
"ConnectionStrings": {
  "AlertHistory": "Host=localhost;Database=honua_alerts;Username=honua;Password=honua"
}
```

2. **Configure Deduplication** (optional, has defaults):
```json
"Alerts": {
  "Deduplication": {
    "CriticalWindowMinutes": 5,
    "WarningWindowMinutes": 15
  }
}
```

3. **Configure Rate Limiting** (optional, has defaults):
```json
"RateLimit": {
  "CriticalPerHour": 20,
  "WarningPerHour": 5
}
```

4. **Restart Application**:
- Migrations run automatically
- Circuit breaker and retry are enabled by default
- All existing alerts work unchanged

5. **Verify**:
```bash
# Check database
psql honua_alerts -c "SELECT COUNT(*) FROM \"AlertHistory\";"

# Check metrics
curl http://localhost:8080/metrics | grep honua_alerts

# Query history
curl http://localhost:8080/api/alerts/history?limit=10
```

**No Breaking Changes**:
- All existing alert endpoints work unchanged
- New features are opt-in (acknowledgements, silencing)
- Deduplication/circuit breaker work automatically
- Database persistence is transparent

---

## Performance Considerations

**Deduplication Memory Usage**:
- Stores ~200 bytes per unique alert fingerprint
- Automatic cleanup after 24 hours
- 10,000 unique alerts = ~2MB memory

**Database Write Volume**:
- Every alert (sent + suppressed) = 1 DB write
- 100 alerts/sec = 8.6M rows/day
- Recommend partitioning AlertHistory by timestamp

**Metrics Overhead**:
- 6 metrics with 2-4 dimensions each
- ~20 time series per provider
- Negligible overhead (<1ms per alert)

**Circuit Breaker State**:
- Per-provider state (6 providers = 6 circuit breakers)
- Minimal memory (~100 bytes per provider)

**Optimization Tips**:
```sql
-- Partition AlertHistory by month
CREATE TABLE AlertHistory_2025_01 PARTITION OF AlertHistory
  FOR VALUES FROM ('2025-01-01') TO ('2025-02-01');

-- Index for common queries
CREATE INDEX idx_alert_history_recent
  ON AlertHistory (Timestamp DESC)
  WHERE Timestamp > NOW() - INTERVAL '7 days';
```

---

## Troubleshooting

**Problem: Too many alerts suppressed**

Check suppression rate:
```promql
rate(honua_alerts_suppressed_total[10m]) / rate(honua_alerts_received_total[10m])
```

If >50%, investigate:
- Are deduplication windows too aggressive?
- Are silencing rules too broad?
- Is this expected (e.g., known issue)?

**Problem: Circuit breaker stuck open**

Check circuit breaker state:
```bash
curl http://localhost:8080/metrics | grep circuit_breaker_state
```

If stuck open:
- Check provider health (network, credentials)
- Check logs for failures
- Manually test provider endpoint

**Problem: Alerts not persisting**

Check database connection:
```bash
# Test connection
psql "Host=localhost;Database=honua_alerts;Username=honua" -c "SELECT 1;"

# Check migrations
dotnet ef database update --project src/Honua.Server.AlertReceiver
```

Check logs:
```bash
docker logs alert-receiver | grep "Database migrations"
```

**Problem: High alert latency**

Check metrics:
```promql
histogram_quantile(0.95, rate(honua_alerts_latency_bucket[5m]))
```

If >500ms:
- Check provider latency (network)
- Check database write performance
- Consider async persistence (queue-based)

---

## Security Considerations

**Database Credentials**:
- Use strong passwords
- Rotate credentials regularly
- Use connection pooling with max connections

**Provider Keys**:
- Store in environment variables or secrets manager
- Never commit to version control
- Use different keys per environment

**API Authentication**:
- Use bearer token authentication
- Rotate tokens regularly
- Consider OAuth2 for production

**Alert Content**:
- Avoid PII in alert descriptions
- Use fingerprints instead of user IDs
- Sanitize context data before persistence

**Example Secure Configuration**:
```bash
# Environment variables
export ALERT_DB_PASSWORD=$(aws secretsmanager get-secret-value --secret-id alert-db-password)
export PAGERDUTY_KEY=$(aws secretsmanager get-secret-value --secret-id pagerduty-key)
export BEARER_TOKEN=$(openssl rand -hex 32)

# appsettings.json
{
  "ConnectionStrings": {
    "AlertHistory": "Host=db;Database=honua_alerts;Username=honua;Password=${ALERT_DB_PASSWORD}"
  },
  "Authentication": {
    "BearerToken": "${BEARER_TOKEN}"
  },
  "Alerts": {
    "PagerDuty": {
      "CriticalRoutingKey": "${PAGERDUTY_KEY}"
    }
  }
}
```
