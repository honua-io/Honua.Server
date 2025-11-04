# Alert Setup and Configuration Guide

## Overview

Honua uses a multi-layered alerting system:
1. **Prometheus** - Metrics collection and alert rule evaluation
2. **AlertManager** - Alert routing, grouping, and deduplication
3. **Alert Receiver** - Webhook service that publishes to multiple providers
4. **Alert Providers** - Notification delivery (AWS SNS, Azure, PagerDuty, Slack, Teams, Opsgenie)

**Note**: See [ALERT_PROVIDERS.md](ALERT_PROVIDERS.md) for detailed provider-specific configuration.

## Architecture

```
┌─────────────┐      ┌──────────────┐      ┌─────────────┐      ┌─────────────────┐
│  Prometheus │─────>│ AlertManager │─────>│   Alert     │─────>│   Providers     │
│             │      │              │      │  Receiver   │      │                 │
└─────────────┘      └──────────────┘      └─────────────┘      │ • AWS SNS       │
      │                                            │              │ • Azure Events  │
      │ Scrapes                                    │              │ • PagerDuty     │
      │ metrics                                    │              │ • Slack         │
      ▼                                            │              │ • Teams         │
┌─────────────┐                                    │              │ • Opsgenie      │
│ Honua API   │                                    ▼              └─────────────────┐
│ (Metrics)   │                              ┌─────────────┐              │
└─────────────┘                              │ Structured  │              │ Delivers
                                             │ Logs        │              │ notifications
                                             └─────────────┘              ▼
                                                                   ┌──────────────┐
                                                                   │ Email/SMS    │
                                                                   │ Mobile Apps  │
                                                                   │ Webhooks     │
                                                                   └──────────────┘
```

## Setup Instructions

### 1. AWS SNS Configuration

#### Create SNS Topics

```bash
# Critical alerts
aws sns create-topic --name honua-alerts-critical

# Warning alerts
aws sns create-topic --name honua-alerts-warning

# Database alerts
aws sns create-topic --name honua-alerts-database

# Storage alerts
aws sns create-topic --name honua-alerts-storage

# Default/info alerts
aws sns create-topic --name honua-alerts-default
```

#### Subscribe to Topics

```bash
# Email subscription for critical alerts
aws sns subscribe \
  --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
  --protocol email \
  --notification-endpoint oncall@example.com

# SMS subscription for critical alerts
aws sns subscribe \
  --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
  --protocol sms \
  --notification-endpoint +1234567890

# Lambda subscription for automated response
aws sns subscribe \
  --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
  --protocol lambda \
  --notification-endpoint arn:aws:lambda:us-west-2:123456789012:function:alert-handler
```

### 2. Alert Receiver Deployment

#### Environment Variables

Create `.env` file or configure environment:

```bash
# AWS Configuration
AWS_REGION=us-west-2
AWS_ACCESS_KEY_ID=<your-access-key>
AWS_SECRET_ACCESS_KEY=<your-secret-key>

# Authentication
ALERTMANAGER_WEBHOOK_TOKEN=<generate-secure-random-token>

# SNS Topic ARNs
Alerts__SNS__CriticalTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-critical
Alerts__SNS__WarningTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-warning
Alerts__SNS__DatabaseTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-database
Alerts__SNS__StorageTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-storage
Alerts__SNS__DefaultTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-default
```

#### Docker Deployment

```bash
# Build alert receiver
cd src/Honua.Server.AlertReceiver
docker build -t honua-alert-receiver .

# Run alert receiver
docker run -d \
  --name honua-alert-receiver \
  --env-file .env \
  -p 8080:8080 \
  honua-alert-receiver
```

#### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-alert-receiver
spec:
  replicas: 2
  selector:
    matchLabels:
      app: honua-alert-receiver
  template:
    metadata:
      labels:
        app: honua-alert-receiver
    spec:
      containers:
      - name: alert-receiver
        image: honua-alert-receiver:latest
        ports:
        - containerPort: 8080
        env:
        - name: AWS_REGION
          value: "us-west-2"
        - name: ALERTMANAGER_WEBHOOK_TOKEN
          valueFrom:
            secretKeyRef:
              name: alert-receiver-secrets
              key: webhook-token
        envFrom:
        - configMapRef:
            name: alert-receiver-config
---
apiVersion: v1
kind: ConfigMap
metadata:
  name: alert-receiver-config
data:
  Alerts__SNS__CriticalTopicArn: "arn:aws:sns:us-west-2:123456789012:honua-alerts-critical"
  Alerts__SNS__WarningTopicArn: "arn:aws:sns:us-west-2:123456789012:honua-alerts-warning"
  Alerts__SNS__DatabaseTopicArn: "arn:aws:sns:us-west-2:123456789012:honua-alerts-database"
  Alerts__SNS__StorageTopicArn: "arn:aws:sns:us-west-2:123456789012:honua-alerts-storage"
  Alerts__SNS__DefaultTopicArn: "arn:aws:sns:us-west-2:123456789012:honua-alerts-default"
---
apiVersion: v1
kind: Service
metadata:
  name: honua-alert-receiver
spec:
  selector:
    app: honua-alert-receiver
  ports:
  - port: 8080
    targetPort: 8080
```

### 3. AlertManager Configuration

Update `docker/alertmanager/alertmanager.yml` with environment variables:

```bash
# Set webhook URL
export ALERTMANAGER_SNS_WEBHOOK_URL=http://honua-alert-receiver:8080/alert

# Set authentication token (same as Alert Receiver)
export ALERTMANAGER_WEBHOOK_TOKEN=<your-secure-token>

# Start AlertManager with environment variable substitution
docker run -d \
  --name alertmanager \
  -v $(pwd)/docker/alertmanager:/etc/alertmanager \
  -e ALERTMANAGER_SNS_WEBHOOK_URL \
  -e ALERTMANAGER_WEBHOOK_TOKEN \
  -p 9093:9093 \
  prom/alertmanager:latest \
  --config.file=/etc/alertmanager/alertmanager.yml
```

### 4. Prometheus Configuration

Ensure Prometheus loads alert rules:

```yaml
# prometheus.yml
rule_files:
  - '/etc/prometheus/alerts/critical.yml'
  - '/etc/prometheus/alerts/warnings.yml'

alerting:
  alertmanagers:
  - static_configs:
    - targets:
      - alertmanager:9093
```

## Alert Types and Routing

### Critical Alerts
**Severity**: P0
**Response Time**: < 5 minutes
**Delivery**: SNS → Email + SMS + PagerDuty
**Examples**:
- HonuaServiceDown
- PostgresDatabaseDown
- HighErrorRate (>5%)
- OutOfMemory
- DiskAlmostFull (<10%)

### Warning Alerts
**Severity**: P2
**Response Time**: < 1 hour
**Delivery**: SNS → Email
**Examples**:
- HighLatency (>2000ms p95)
- LowCacheHitRatio (<70%)
- DiskSpaceWarning (<20%)
- HighCPUUsage (>80%)

### Database Alerts
**Routing**: Database team
**Examples**:
- DatabaseConnectionsHigh (>70%)
- LongRunningQueries (>5 min)
- ReplicationLagHigh

### Storage Alerts
**Routing**: Infrastructure team
**Examples**:
- S3UploadFailures
- DiskSpaceWarning

## Testing Alerts

### Manual Alert Testing

```bash
# Send test alert to Alert Receiver
curl -X POST http://localhost:8080/alert/critical \
  -H "Authorization: Bearer ${ALERTMANAGER_WEBHOOK_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "version": "4",
    "groupKey": "test",
    "status": "firing",
    "receiver": "critical-alerts",
    "groupLabels": {
      "alertname": "TestAlert"
    },
    "commonAnnotations": {
      "summary": "This is a test alert",
      "description": "Testing alert pipeline"
    },
    "alerts": [{
      "status": "firing",
      "labels": {
        "alertname": "TestAlert",
        "severity": "critical"
      },
      "annotations": {
        "description": "Testing alert delivery"
      },
      "startsAt": "2024-01-01T00:00:00Z"
    }]
  }'
```

### Trigger Prometheus Alert

```bash
# Temporarily stop service to trigger HonuaServiceDown
docker stop honua-api

# Wait 1-2 minutes for alert to fire
# Check AlertManager UI
open http://localhost:9093

# Check logs
docker logs honua-alert-receiver

# Restart service
docker start honua-api
```

## Monitoring Alert System Health

### Check Alert Receiver Status

```bash
# Health check
curl http://localhost:8080/alert/health

# View logs
docker logs honua-alert-receiver --tail=100 -f
```

### Check AlertManager Status

```bash
# View active alerts
curl http://localhost:9093/api/v1/alerts

# View AlertManager config
curl http://localhost:9093/api/v1/status
```

### Check SNS Metrics

```bash
# View SNS metrics in CloudWatch
aws cloudwatch get-metric-statistics \
  --namespace AWS/SNS \
  --metric-name NumberOfMessagesPublished \
  --dimensions Name=TopicName,Value=honua-alerts-critical \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Sum
```

## Structured Error Logging

Application errors are automatically logged with structured context for alerting:

```csharp
// In your API code
try
{
    // API operation
}
catch (Exception ex)
{
    // This automatically records metrics AND logs structured error
    _apiMetrics.RecordError("wfs", "my-service", "my-layer", ex,
        additionalContext: "GetFeature request failed");
}
```

Error log format:
```json
{
  "timestamp": "2024-01-01T12:00:00Z",
  "level": "Error",
  "message": "API Error",
  "apiProtocol": "wfs",
  "serviceId": "my-service",
  "layerId": "my-layer",
  "errorType": "database_error",
  "errorCategory": "database",
  "severity": "High",
  "context": "GetFeature request failed",
  "exception": "..."
}
```

These logs can be:
- Shipped to CloudWatch Logs
- Analyzed with CloudWatch Insights
- Trigger CloudWatch Alarms → SNS
- Indexed in Elasticsearch/OpenSearch
- Sent to Loki for correlation with metrics

## Advanced: CloudWatch Integration

### Ship Logs to CloudWatch

```bash
# Install CloudWatch agent
docker run -d \
  --name cloudwatch-logs \
  --log-driver=awslogs \
  --log-opt awslogs-region=us-west-2 \
  --log-opt awslogs-group=/honua/api \
  --log-opt awslogs-stream=honua-api \
  honua-api
```

### Create CloudWatch Alarms

```bash
# Alert on high error rate in logs
aws cloudwatch put-metric-alarm \
  --alarm-name honua-high-error-rate \
  --alarm-description "Alert when error rate exceeds 5%" \
  --metric-name ErrorRate \
  --namespace Honua/API \
  --statistic Average \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 0.05 \
  --comparison-operator GreaterThanThreshold \
  --alarm-actions arn:aws:sns:us-west-2:123456789012:honua-alerts-critical
```

## Troubleshooting

### Alerts Not Firing

1. Check Prometheus is scraping metrics:
   ```bash
   curl http://localhost:9090/api/v1/targets
   ```

2. Check alert rules are loaded:
   ```bash
   curl http://localhost:9090/api/v1/rules
   ```

3. Check AlertManager is receiving alerts:
   ```bash
   curl http://localhost:9093/api/v1/alerts
   ```

### Alerts Not Reaching SNS

1. Check Alert Receiver logs:
   ```bash
   docker logs honua-alert-receiver
   ```

2. Verify AWS credentials:
   ```bash
   aws sns list-topics
   ```

3. Test SNS publish manually:
   ```bash
   aws sns publish \
     --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
     --message "Test message"
   ```

### Alert Fatigue

If receiving too many alerts:

1. Adjust alert thresholds in `docker/prometheus/alerts/*.yml`
2. Increase `repeat_interval` in AlertManager config
3. Add more inhibition rules to suppress redundant alerts
4. Review and consolidate similar alerts

## Security Considerations

1. **Webhook Authentication**: Always set `ALERTMANAGER_WEBHOOK_TOKEN` to a strong random value
2. **AWS Credentials**: Use IAM roles instead of access keys when possible
3. **SNS Permissions**: Limit IAM permissions to only required SNS topics
4. **Network Security**: Run Alert Receiver in private subnet, only accessible from AlertManager
5. **Encryption**: Use HTTPS/TLS for all alert traffic in production

## Cost Optimization

AWS SNS pricing (as of 2024):
- First 1,000 email notifications/month: Free
- Additional email notifications: $2.00 per 100,000
- SMS: Varies by region (~$0.50-1.00 per message)

Tips:
- Use email for warnings, SMS only for critical
- Consolidate alerts with grouping (`group_by` in AlertManager)
- Set appropriate `repeat_interval` to avoid duplicate notifications
- Use Lambda for automated responses instead of human intervention
