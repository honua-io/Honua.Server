# Alert Provider Configuration Guide

## Overview

Honua Alert Receiver supports multiple alert notification providers. You can enable one or more providers simultaneously, and alerts will be sent to all configured providers.

**Supported Providers:**
- AWS SNS (Simple Notification Service)
- Azure Event Grid
- PagerDuty
- Slack
- Microsoft Teams
- Opsgenie

## Provider Configuration

### AWS SNS

Amazon SNS provides reliable, scalable notification delivery via email, SMS, mobile push, and more.

#### Setup

1. **Create SNS Topics:**
   ```bash
   aws sns create-topic --name honua-alerts-critical
   aws sns create-topic --name honua-alerts-warning
   aws sns create-topic --name honua-alerts-database
   aws sns create-topic --name honua-alerts-storage
   aws sns create-topic --name honua-alerts-default
   ```

2. **Subscribe to Topics:**
   ```bash
   # Email subscription
   aws sns subscribe \
     --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
     --protocol email \
     --notification-endpoint oncall@example.com

   # SMS subscription
   aws sns subscribe \
     --topic-arn arn:aws:sns:us-west-2:123456789012:honua-alerts-critical \
     --protocol sms \
     --notification-endpoint +1234567890
   ```

3. **Configure Environment Variables:**
   ```bash
   export AWS_REGION=us-west-2
   export AWS_ACCESS_KEY_ID=<your-access-key>
   export AWS_SECRET_ACCESS_KEY=<your-secret-key>
   export Alerts__SNS__CriticalTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-critical
   export Alerts__SNS__WarningTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-warning
   export Alerts__SNS__DatabaseTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-database
   export Alerts__SNS__StorageTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-storage
   export Alerts__SNS__DefaultTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-default
   ```

#### Features
- ✅ Multiple delivery protocols (email, SMS, Lambda, SQS, HTTP)
- ✅ Message attributes for filtering
- ✅ Full JSON payload included
- ✅ Automatic retry and dead-letter queues
- ✅ Regional redundancy

#### Pricing (as of 2024)
- First 1,000 email notifications/month: Free
- Additional emails: $2.00 per 100,000
- SMS: $0.50-$1.00 per message (varies by region)

---

### Azure Event Grid

Azure Event Grid provides event-driven architecture with advanced routing and filtering.

#### Setup

1. **Create Event Grid Topic:**
   ```bash
   az eventgrid topic create \
     --name honua-alerts \
     --resource-group honua-rg \
     --location westus2
   ```

2. **Get Endpoint and Access Key:**
   ```bash
   ENDPOINT=$(az eventgrid topic show \
     --name honua-alerts \
     --resource-group honua-rg \
     --query "endpoint" -o tsv)

   ACCESS_KEY=$(az eventgrid topic key list \
     --name honua-alerts \
     --resource-group honua-rg \
     --query "key1" -o tsv)
   ```

3. **Create Subscriptions:**
   ```bash
   # Email subscription
   az eventgrid event-subscription create \
     --name critical-email \
     --source-resource-id /subscriptions/.../honua-alerts \
     --endpoint-type webhook \
     --endpoint https://your-webhook-url

   # Azure Function subscription
   az eventgrid event-subscription create \
     --name critical-function \
     --source-resource-id /subscriptions/.../honua-alerts \
     --endpoint-type azurefunction \
     --endpoint /subscriptions/.../functions/alert-handler
   ```

4. **Configure Environment Variables:**
   ```bash
   export Alerts__Azure__EventGridEndpoint=$ENDPOINT
   export Alerts__Azure__EventGridAccessKey=$ACCESS_KEY
   ```

#### Features
- ✅ Event filtering by subject, event type
- ✅ Delivery to Azure Functions, Logic Apps, Webhooks
- ✅ Advanced retry policies
- ✅ Dead-letter event handling
- ✅ CloudEvents schema support

#### Event Schema
```json
{
  "subject": "honua/alerts/critical/HonuaServiceDown",
  "eventType": "Honua.Alert.firing",
  "dataVersion": "1.0",
  "data": {
    "alertName": "HonuaServiceDown",
    "severity": "critical",
    "status": "firing",
    "description": "...",
    "labels": {...},
    "annotations": {...}
  }
}
```

#### Pricing (as of 2024)
- First 100,000 operations/month: Free
- Additional operations: $0.60 per million

---

### PagerDuty

PagerDuty provides on-call management and incident response.

#### Setup

1. **Create Integration:**
   - Go to **Services** → Your Service → **Integrations**
   - Add **Events API V2** integration
   - Copy the **Integration Key** (routing key)

2. **Configure Environment Variables:**
   ```bash
   export Alerts__PagerDuty__CriticalRoutingKey=<your-critical-integration-key>
   export Alerts__PagerDuty__WarningRoutingKey=<your-warning-integration-key>
   export Alerts__PagerDuty__DatabaseRoutingKey=<your-database-integration-key>
   export Alerts__PagerDuty__DefaultRoutingKey=<your-default-integration-key>
   ```

3. **Configure Escalation Policies** (in PagerDuty UI):
   - Set up on-call schedules
   - Define escalation rules
   - Configure notification methods

#### Features
- ✅ Automatic incident creation and resolution
- ✅ On-call scheduling
- ✅ Escalation policies
- ✅ Mobile app notifications
- ✅ Incident analytics and reports
- ✅ Deduplication by fingerprint

#### Alert Lifecycle
- **Firing** → Creates PagerDuty incident (triggers notifications)
- **Resolved** → Closes PagerDuty incident (sends resolution notification)

#### Priority Mapping
| Severity | PagerDuty Priority |
|----------|-------------------|
| critical | P1 (Critical)     |
| warning  | P3 (Warning)      |
| database | P2 (High)         |
| storage  | P2 (High)         |

---

### Slack

Slack provides team messaging with rich formatting and threading.

#### Setup

1. **Create Incoming Webhook:**
   - Go to https://api.slack.com/apps
   - Create new app or select existing
   - Enable **Incoming Webhooks**
   - Click **Add New Webhook to Workspace**
   - Select channel and authorize
   - Copy webhook URL

2. **Configure Environment Variables:**
   ```bash
   export Alerts__Slack__CriticalWebhookUrl=https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX
   export Alerts__Slack__WarningWebhookUrl=https://hooks.slack.com/services/...
   export Alerts__Slack__DatabaseWebhookUrl=https://hooks.slack.com/services/...
   export Alerts__Slack__StorageWebhookUrl=https://hooks.slack.com/services/...
   export Alerts__Slack__DefaultWebhookUrl=https://hooks.slack.com/services/...
   ```

3. **Optional: Create Multiple Webhooks** for different channels:
   - `#alerts-critical` - Critical alerts (service down, data loss)
   - `#alerts-warning` - Warning alerts (performance issues)
   - `#alerts-database` - Database team channel
   - `#alerts-storage` - Storage/infrastructure team

#### Features
- ✅ Rich message formatting with attachments
- ✅ Color-coded severity (red=critical, yellow=warning)
- ✅ Emoji icons for quick identification
- ✅ Clickable links to AlertManager
- ✅ Grouped alerts (shows up to 5 alerts per message)

#### Message Format
```
🚨 FIRING: HonuaServiceDown

Alert: HonuaServiceDown
Status: firing
Severity: critical
Protocol: wfs
Service: my-service
Description: Honua API service has been down for more than 1 minute
```

---

### Microsoft Teams

Microsoft Teams provides enterprise messaging with adaptive cards.

#### Setup

1. **Create Incoming Webhook:**
   - Open Teams → Select channel
   - Click **...** → **Connectors**
   - Search for **Incoming Webhook**
   - Configure webhook (name, icon)
   - Copy webhook URL

2. **Configure Environment Variables:**
   ```bash
   export Alerts__Teams__CriticalWebhookUrl=https://outlook.office.com/webhook/...
   export Alerts__Teams__WarningWebhookUrl=https://outlook.office.com/webhook/...
   export Alerts__Teams__DatabaseWebhookUrl=https://outlook.office.com/webhook/...
   export Alerts__Teams__StorageWebhookUrl=https://outlook.office.com/webhook/...
   export Alerts__Teams__DefaultWebhookUrl=https://outlook.office.com/webhook/...
   ```

#### Features
- ✅ MessageCard format with sections
- ✅ Color-coded by severity
- ✅ Fact-based display (key-value pairs)
- ✅ Action buttons (View in AlertManager)
- ✅ Timestamp display
- ✅ Duration calculation for resolved alerts

#### Message Format
Teams alerts use the MessageCard format with:
- Title: Alert status and name
- Color bar: Red (critical), Orange (warning), Blue (info)
- Facts: Status, Severity, Protocol, Service, Started, Resolved, Duration
- Actions: Link to AlertManager UI

---

### Opsgenie

Opsgenie provides advanced alerting, on-call management, and incident tracking.

#### Setup

1. **Get API Key:**
   - Go to **Settings** → **API Key Management**
   - Create new API key with **Create and Update Access**
   - Copy API key

2. **Configure Environment Variables:**
   ```bash
   export Alerts__Opsgenie__ApiKey=<your-api-key>
   export Alerts__Opsgenie__ApiUrl=https://api.opsgenie.com  # or https://api.eu.opsgenie.com for EU
   ```

3. **Configure Teams and Schedules** (in Opsgenie UI):
   - Create teams (database, infrastructure, etc.)
   - Set up on-call schedules
   - Define escalation policies

#### Features
- ✅ Automatic alert creation and closure
- ✅ Priority-based routing (P1-P5)
- ✅ On-call scheduling
- ✅ Escalation policies
- ✅ Mobile app notifications
- ✅ Alert tagging for organization
- ✅ Custom alert fields (details)

#### Alert Lifecycle
- **Firing** → Creates Opsgenie alert
- **Resolved** → Closes Opsgenie alert

#### Priority Mapping
| Severity | Opsgenie Priority |
|----------|------------------|
| critical | P1               |
| database | P2               |
| storage  | P2               |
| warning  | P3               |
| default  | P4               |

#### Alert Details
Each alert includes:
- **Tags**: `severity:critical`, `protocol:wfs`, `service:my-service`
- **Details**: All labels and annotations as key-value pairs
- **Alias**: Alert fingerprint (for deduplication)
- **Source**: "Honua Server"

---

## Multi-Provider Configuration

You can enable multiple providers simultaneously. The Alert Receiver will publish to all configured providers in parallel.

### Example: AWS + Slack + PagerDuty

```bash
# AWS SNS for email/SMS
export Alerts__SNS__CriticalTopicArn=arn:aws:sns:us-west-2:123456789012:honua-alerts-critical

# Slack for team visibility
export Alerts__Slack__CriticalWebhookUrl=https://hooks.slack.com/services/...

# PagerDuty for on-call
export Alerts__PagerDuty__CriticalRoutingKey=<your-integration-key>
```

With this configuration:
1. Critical alert fires in Prometheus
2. AlertManager sends webhook to Alert Receiver
3. Alert Receiver publishes to:
   - AWS SNS → Email + SMS
   - Slack → `#alerts-critical` channel
   - PagerDuty → Creates incident, pages on-call engineer

### Error Handling

The composite publisher uses a "best effort" approach:
- Publishes to all providers in parallel
- Continues even if one provider fails
- Logs failures but doesn't fail the entire operation
- Only fails if ALL providers fail

## Provider Selection Guide

| Provider | Use Case | Best For |
|----------|----------|----------|
| **AWS SNS** | General notifications, multi-channel | AWS-centric infrastructure, email/SMS delivery |
| **Azure Event Grid** | Event-driven automation | Azure environments, serverless workflows |
| **PagerDuty** | On-call management | 24/7 operations, incident response teams |
| **Slack** | Team collaboration | Development teams, real-time visibility |
| **Teams** | Enterprise messaging | Microsoft 365 organizations |
| **Opsgenie** | Advanced alerting | Complex on-call rotations, escalations |

### Recommended Combinations

**Startup/Small Team:**
- Slack (team visibility) + PagerDuty (on-call)

**AWS Environment:**
- AWS SNS (notifications) + Slack (visibility) + PagerDuty (on-call)

**Azure Environment:**
- Azure Event Grid (events) + Teams (collaboration) + Opsgenie (on-call)

**Enterprise:**
- All providers enabled with severity-based routing

## Testing Alerts

Test each provider with a manual alert:

```bash
curl -X POST http://localhost:8080/alert/critical \
  -H "Authorization: Bearer ${ALERTMANAGER_WEBHOOK_TOKEN}" \
  -H "Content-Type: application/json" \
  -d '{
    "version": "4",
    "groupKey": "test",
    "status": "firing",
    "receiver": "critical-alerts",
    "groupLabels": {"alertname": "TestAlert"},
    "commonAnnotations": {
      "summary": "Test alert",
      "description": "Testing provider integration"
    },
    "alerts": [{
      "status": "firing",
      "labels": {
        "alertname": "TestAlert",
        "severity": "critical"
      },
      "annotations": {
        "description": "This is a test alert to verify provider configuration"
      },
      "startsAt": "2024-01-01T00:00:00Z",
      "fingerprint": "test-alert-123"
    }]
  }'
```

Check logs to verify delivery:
```bash
docker logs honua-alert-receiver
```

Expected output:
```
Published alert to AWS SNS, MessageId: ...
Published alert to Slack - Alert: TestAlert, Status: FIRING
Published alert to PagerDuty - Alert: TestAlert, Action: trigger
Successfully published alert to 3 providers
```

## Troubleshooting

### Provider Not Receiving Alerts

1. **Check Configuration:**
   ```bash
   # View environment variables
   docker exec honua-alert-receiver env | grep Alerts
   ```

2. **Check Logs:**
   ```bash
   docker logs honua-alert-receiver --tail=100
   ```

3. **Verify Provider Registration:**
   Look for startup log:
   ```
   Registered 3 alert publishers
   ```

### Provider-Specific Issues

**AWS SNS:**
- Verify IAM permissions: `sns:Publish`
- Check topic ARN is correct
- Verify AWS credentials

**Azure Event Grid:**
- Verify endpoint URL and access key
- Check firewall rules
- Test with Azure CLI: `az eventgrid topic show`

**PagerDuty:**
- Verify integration key (routing key)
- Check service is active
- Test with API: `curl -X POST https://events.pagerduty.com/v2/enqueue`

**Slack:**
- Verify webhook URL is valid
- Check webhook is not revoked
- Verify channel still exists

**Teams:**
- Verify webhook URL is valid
- Check connector is not removed
- Verify channel access

**Opsgenie:**
- Verify API key has correct permissions
- Check API URL (US vs EU)
- Verify teams and schedules exist

## Cost Considerations

| Provider | Free Tier | Pricing Model |
|----------|-----------|---------------|
| AWS SNS | 1,000 emails/mo | Per notification |
| Azure Event Grid | 100K ops/mo | Per operation |
| PagerDuty | Limited | Per user/month |
| Slack | Unlimited | Free for basic webhooks |
| Teams | Unlimited | Free for webhooks |
| Opsgenie | 5 users | Per user/month |

**Cost Optimization Tips:**
- Use Slack/Teams for high-volume alerts (free)
- Use PagerDuty/Opsgenie only for critical alerts
- Consolidate alerts with AlertManager grouping
- Set appropriate `repeat_interval` to avoid duplicates
