# Application Insights Alert Notification Samples

**Last Updated**: 2025-10-18
**Version**: 1.0.0

## Table of Contents

1. [Overview](#overview)
2. [Email Notification Samples](#email-notification-samples)
3. [Webhook Payload Samples](#webhook-payload-samples)
4. [Slack Integration](#slack-integration)
5. [Microsoft Teams Integration](#microsoft-teams-integration)
6. [PagerDuty Integration](#pagerduty-integration)
7. [Custom Webhook Processing](#custom-webhook-processing)

---

## Overview

This document provides sample alert notifications for all configured alert types. These samples help you understand what to expect when alerts fire and how to integrate with external systems.

---

## Email Notification Samples

### Critical Alert: Service Down

**Subject**: `[Critical] Honua Service Down - prod`

**Body**:
```
Alert: honua-service-down-prod
Severity: Critical (Sev0)
Status: Fired
Time: 2025-10-18 12:00:00 UTC

Description:
Alert when no requests are being processed (service down)

Condition:
- Metric: requests/count
- Aggregation: Count
- Operator: LessThan
- Threshold: 1
- Current Value: 0
- Evaluation Window: 15 minutes

Affected Resource:
- Name: appi-honua-a1b2c3
- Type: Microsoft.Insights/components
- Resource Group: rg-honua-prod-eastus
- Subscription: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx

Recommended Actions:
1. Check if Function App is running
2. Review Azure Service Health
3. Check recent deployments
4. Review function app logs
5. Verify network connectivity

View in Azure Portal:
https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/alertDetails

Application Insights:
https://portal.azure.com/#resource/.../overview
```

---

### Error Alert: High Error Rate

**Subject**: `[Error] High Error Rate Detected - prod`

**Body**:
```
Alert: honua-high-error-rate-prod
Severity: Error (Sev1)
Status: Fired
Time: 2025-10-18 12:00:00 UTC

Description:
Error rate exceeds 5% threshold

Condition:
- Metric: requests/failed
- Aggregation: Count
- Threshold: Dynamic (5% baseline)
- Current Value: 127 failed requests
- Total Requests: 1542
- Error Rate: 8.23%
- Evaluation Window: 15 minutes

Top Error Codes:
- 500 Internal Server Error: 89 requests (70.1%)
- 502 Bad Gateway: 23 requests (18.1%)
- 503 Service Unavailable: 15 requests (11.8%)

Recommended Actions:
1. Review exception logs in Application Insights
2. Check recent deployments
3. Verify dependency availability (Database, OpenAI, etc.)
4. Review Application Map for failing dependencies

Query to investigate:
requests
| where timestamp > ago(15m)
| where success == false
| summarize count() by resultCode, name
| order by count_ desc

View Alert:
https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/alertDetails
```

---

### Warning Alert: High Response Time

**Subject**: `[Warning] High Response Time Detected - prod`

**Body**:
```
Alert: honua-high-response-time-prod
Severity: Warning (Sev2)
Status: Fired
Time: 2025-10-18 12:00:00 UTC

Description:
P95 response time exceeds 1 second

Condition:
- Metric: requests/duration
- Aggregation: Average
- Threshold: 1000ms
- Current Value: 1847ms (P95)
- Evaluation Window: 15 minutes

Performance Breakdown:
- P50: 234ms
- P75: 567ms
- P95: 1847ms
- P99: 3421ms

Slowest Operations:
1. POST /api/deployment/generate (avg: 2.3s)
2. POST /api/agent/coordinate (avg: 1.9s)
3. GET /api/metadata/extract (avg: 1.4s)

Recommended Actions:
1. Review Performance blade in Application Insights
2. Check dependency latency (Database, OpenAI)
3. Review Application Map
4. Consider scaling if load is high
5. Check for slow database queries

View Performance Data:
https://portal.azure.com/#resource/.../performance
```

---

### Resolved Alert Sample

**Subject**: `[Resolved] High Response Time - prod`

**Body**:
```
Alert: honua-high-response-time-prod
Severity: Warning (Sev2)
Status: Resolved
Fired: 2025-10-18 12:00:00 UTC
Resolved: 2025-10-18 12:23:00 UTC
Duration: 23 minutes

Description:
Response time has returned to normal levels

Condition:
- Metric: requests/duration
- Current Value: 678ms (P95)
- Threshold: 1000ms
- Status: Below threshold

Resolution Summary:
Alert was active for 23 minutes and has now been resolved.
Performance has returned to acceptable levels.

No action required unless issue recurs.

View Alert History:
https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/alertDetails
```

---

## Webhook Payload Samples

### Common Alert Schema Format

All webhook notifications use the Azure Monitor Common Alert Schema for consistency.

### Critical Alert Webhook Payload

```json
{
  "schemaId": "azureMonitorCommonAlertSchema",
  "data": {
    "essentials": {
      "alertId": "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-honua-prod-eastus/providers/Microsoft.Insights/metricAlerts/honua-service-down-prod",
      "alertRule": "honua-service-down-prod",
      "severity": "Sev0",
      "signalType": "Metric",
      "monitorCondition": "Fired",
      "monitoringService": "Platform",
      "alertTargetIDs": [
        "/subscriptions/12345678-1234-1234-1234-123456789012/resourceGroups/rg-honua-prod-eastus/providers/Microsoft.Insights/components/appi-honua-a1b2c3"
      ],
      "configurationItems": [
        "appi-honua-a1b2c3"
      ],
      "originAlertId": "12345678-1234-1234-1234-123456789012_rg-honua-prod-eastus_microsoft.insights_metricalerts_honua-service-down-prod_1234567890",
      "firedDateTime": "2025-10-18T12:00:00.0000000Z",
      "description": "Alert when no requests are being processed (service down)",
      "essentialsVersion": "1.0",
      "alertContextVersion": "1.0"
    },
    "alertContext": {
      "properties": null,
      "conditionType": "SingleResourceMultipleMetricCriteria",
      "condition": {
        "windowSize": "PT15M",
        "allOf": [
          {
            "metricName": "requests/count",
            "metricNamespace": "microsoft.insights/components",
            "operator": "LessThan",
            "threshold": "1",
            "timeAggregation": "Count",
            "dimensions": [],
            "metricValue": 0.0,
            "webTestName": null
          }
        ],
        "windowStartTime": "2025-10-18T11:45:00.0000000Z",
        "windowEndTime": "2025-10-18T12:00:00.0000000Z"
      }
    },
    "customProperties": {
      "Environment": "prod",
      "Project": "HonuaIO",
      "ManagedBy": "Terraform",
      "AlertType": "Availability",
      "Severity": "Critical"
    }
  }
}
```

---

### Warning Alert Webhook Payload

```json
{
  "schemaId": "azureMonitorCommonAlertSchema",
  "data": {
    "essentials": {
      "alertId": "/subscriptions/.../honua-high-response-time-prod",
      "alertRule": "honua-high-response-time-prod",
      "severity": "Sev2",
      "signalType": "Metric",
      "monitorCondition": "Fired",
      "monitoringService": "Platform",
      "alertTargetIDs": [
        "/subscriptions/.../components/appi-honua-a1b2c3"
      ],
      "firedDateTime": "2025-10-18T12:00:00.0000000Z",
      "description": "Alert when P95 response time exceeds 1 second"
    },
    "alertContext": {
      "conditionType": "SingleResourceMultipleMetricCriteria",
      "condition": {
        "windowSize": "PT15M",
        "allOf": [
          {
            "metricName": "requests/duration",
            "metricNamespace": "microsoft.insights/components",
            "operator": "GreaterThan",
            "threshold": "1000",
            "timeAggregation": "Average",
            "metricValue": 1847.23
          }
        ]
      }
    },
    "customProperties": {
      "AlertType": "Performance",
      "Threshold": "1000ms"
    }
  }
}
```

---

## Slack Integration

### Configure Slack Webhook

**1. Create Incoming Webhook in Slack**:
- Go to https://api.slack.com/apps
- Create new app → "From scratch"
- Enable "Incoming Webhooks"
- Add webhook to channel (e.g., #honua-alerts)
- Copy webhook URL

**2. Update Terraform**:
```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ... existing config ...

  webhook_receiver {
    name        = "slack-webhook"
    service_uri = "https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX"
    use_common_alert_schema = true
  }
}
```

---

### Slack Message Format

**Critical Alert**:
```
🔴 *CRITICAL ALERT* 🔴

*Alert*: Honua Service Down - prod
*Severity*: Sev0 (Critical)
*Status*: 🔥 FIRED
*Time*: 2025-10-18 12:00:00 UTC

*Condition*:
• Metric: `requests/count`
• Current: 0 requests
• Threshold: < 1 request
• Window: 15 minutes

*Quick Actions*:
1. Check Function App status
2. Review Azure Service Health
3. Check recent deployments

<https://portal.azure.com/#resource/.../overview|View in Azure Portal>
```

---

### Custom Slack Webhook Handler

If you want custom formatting, create an Azure Function to transform the payload:

```csharp
[FunctionName("SlackAlertWebhook")]
public static async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req,
    ILogger log)
{
    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    var alert = JsonConvert.DeserializeObject<CommonAlertSchema>(requestBody);

    var slackMessage = new
    {
        text = FormatAlertForSlack(alert),
        attachments = new[]
        {
            new
            {
                color = GetSeverityColor(alert.Data.Essentials.Severity),
                fields = new[]
                {
                    new { title = "Alert Rule", value = alert.Data.Essentials.AlertRule, @short = true },
                    new { title = "Severity", value = alert.Data.Essentials.Severity, @short = true },
                    new { title = "Status", value = alert.Data.Essentials.MonitorCondition, @short = true },
                    new { title = "Time", value = alert.Data.Essentials.FiredDateTime, @short = true }
                },
                actions = new[]
                {
                    new
                    {
                        type = "button",
                        text = "View in Azure",
                        url = GetAzurePortalUrl(alert)
                    }
                }
            }
        }
    };

    var slackWebhookUrl = Environment.GetEnvironmentVariable("SLACK_WEBHOOK_URL");
    using var client = new HttpClient();
    await client.PostAsJsonAsync(slackWebhookUrl, slackMessage);

    return new OkResult();
}

private static string GetSeverityColor(string severity)
{
    return severity switch
    {
        "Sev0" => "#FF0000", // Red
        "Sev1" => "#FF6600", // Orange
        "Sev2" => "#FFCC00", // Yellow
        _ => "#0099FF"       // Blue
    };
}
```

---

## Microsoft Teams Integration

### Configure Teams Webhook

**1. Create Incoming Webhook in Teams**:
- Open Teams channel
- Click "..." → Connectors
- Search for "Incoming Webhook"
- Configure and copy webhook URL

**2. Update Terraform**:
```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ... existing config ...

  webhook_receiver {
    name        = "teams-webhook"
    service_uri = "https://outlook.office.com/webhook/..."
    use_common_alert_schema = true
  }
}
```

---

### Teams Adaptive Card Format

```json
{
  "@type": "MessageCard",
  "@context": "https://schema.org/extensions",
  "summary": "Critical Alert: Honua Service Down",
  "themeColor": "FF0000",
  "title": "🔴 CRITICAL ALERT: Honua Service Down",
  "sections": [
    {
      "activityTitle": "honua-service-down-prod",
      "activitySubtitle": "Fired at 2025-10-18 12:00:00 UTC",
      "facts": [
        {
          "name": "Severity:",
          "value": "Sev0 (Critical)"
        },
        {
          "name": "Environment:",
          "value": "Production"
        },
        {
          "name": "Metric:",
          "value": "requests/count"
        },
        {
          "name": "Current Value:",
          "value": "0 requests"
        },
        {
          "name": "Threshold:",
          "value": "< 1 request"
        }
      ],
      "text": "Alert when no requests are being processed (service down)"
    }
  ],
  "potentialAction": [
    {
      "@type": "OpenUri",
      "name": "View in Azure Portal",
      "targets": [
        {
          "os": "default",
          "uri": "https://portal.azure.com/#resource/.../overview"
        }
      ]
    },
    {
      "@type": "OpenUri",
      "name": "View Alert Details",
      "targets": [
        {
          "os": "default",
          "uri": "https://portal.azure.com/#blade/Microsoft_Azure_Monitoring/AzureMonitoringBrowseBlade/alertDetails"
        }
      ]
    }
  ]
}
```

---

## PagerDuty Integration

### Configure PagerDuty

**1. Get PagerDuty Integration Key**:
- Log in to PagerDuty
- Go to Services → Select service
- Integrations → Add Integration
- Select "Azure Monitor"
- Copy Integration Key

**2. Create Azure Logic App for PagerDuty**:

```hcl
resource "azurerm_logic_app_workflow" "pagerduty_integration" {
  name                = "honua-pagerduty-integration"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  tags = local.tags
}

resource "azurerm_logic_app_trigger_http_request" "webhook" {
  name         = "When_Azure_Monitor_alert_is_received"
  logic_app_id = azurerm_logic_app_workflow.pagerduty_integration.id

  schema = jsonencode({
    type = "object"
    properties = {
      schemaId = { type = "string" }
      data = { type = "object" }
    }
  })
}

resource "azurerm_logic_app_action_http" "send_to_pagerduty" {
  name         = "Send_to_PagerDuty"
  logic_app_id = azurerm_logic_app_workflow.pagerduty_integration.id

  method = "POST"
  uri    = "https://events.pagerduty.com/v2/enqueue"

  body = jsonencode({
    routing_key  = "@{parameters('pagerduty_integration_key')}"
    event_action = "trigger"
    payload = {
      summary        = "@{triggerBody()?['data']?['essentials']?['description']}"
      severity       = "@{if(equals(triggerBody()?['data']?['essentials']?['severity'], 'Sev0'), 'critical', if(equals(triggerBody()?['data']?['essentials']?['severity'], 'Sev1'), 'error', 'warning'))}"
      source         = "Azure Monitor"
      component      = "Honua"
      custom_details = "@triggerBody()"
    }
  })

  headers = {
    "Content-Type" = "application/json"
  }

  depends_on = [azurerm_logic_app_trigger_http_request.webhook]
}
```

**3. Update Action Group**:
```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ... existing config ...

  logic_app_receiver {
    name                    = "pagerduty-integration"
    resource_id             = azurerm_logic_app_workflow.pagerduty_integration.id
    callback_url            = azurerm_logic_app_trigger_http_request.webhook.callback_url
    use_common_alert_schema = true
  }
}
```

---

### PagerDuty Incident Format

```json
{
  "routing_key": "R023...",
  "event_action": "trigger",
  "payload": {
    "summary": "Honua Service Down - Production",
    "severity": "critical",
    "source": "Azure Monitor",
    "component": "Honua AI Deployment Consultant",
    "group": "honua-production",
    "class": "availability",
    "custom_details": {
      "alert_rule": "honua-service-down-prod",
      "metric_name": "requests/count",
      "metric_value": "0",
      "threshold": "1",
      "environment": "prod",
      "resource_group": "rg-honua-prod-eastus",
      "subscription": "12345678-1234-1234-1234-123456789012"
    }
  },
  "links": [
    {
      "href": "https://portal.azure.com/#resource/.../overview",
      "text": "View in Azure Portal"
    }
  ],
  "images": [
    {
      "src": "https://portal.azure.com/.../chart.png",
      "alt": "Alert Graph"
    }
  ]
}
```

---

## Custom Webhook Processing

### Generic Webhook Handler

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public static class AlertWebhookHandler
{
    [FunctionName("AlertWebhook")]
    public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
        ILogger log)
    {
        log.LogInformation("Alert webhook received");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var alert = JsonConvert.DeserializeObject<CommonAlertSchema>(requestBody);

        // Process based on severity
        switch (alert.Data.Essentials.Severity)
        {
            case "Sev0":
                await HandleCriticalAlert(alert, log);
                break;
            case "Sev1":
                await HandleErrorAlert(alert, log);
                break;
            case "Sev2":
                await HandleWarningAlert(alert, log);
                break;
            default:
                log.LogInformation($"Alert received: {alert.Data.Essentials.AlertRule}");
                break;
        }

        return new OkObjectResult("Alert processed successfully");
    }

    private static async Task HandleCriticalAlert(CommonAlertSchema alert, ILogger log)
    {
        log.LogCritical($"CRITICAL ALERT: {alert.Data.Essentials.AlertRule}");

        // Send to multiple channels
        await SendToSlack(alert, "critical");
        await SendToPagerDuty(alert);
        await SendSMS(alert);

        // Create incident ticket
        await CreateIncidentTicket(alert);

        // Trigger auto-remediation if applicable
        if (IsAutoRemediationEnabled(alert))
        {
            await TriggerAutoRemediation(alert);
        }
    }

    private static async Task HandleErrorAlert(CommonAlertSchema alert, ILogger log)
    {
        log.LogError($"ERROR ALERT: {alert.Data.Essentials.AlertRule}");

        // Send to team channels
        await SendToSlack(alert, "error");
        await CreateJiraTicket(alert);
    }

    private static async Task HandleWarningAlert(CommonAlertSchema alert, ILogger log)
    {
        log.LogWarning($"WARNING ALERT: {alert.Data.Essentials.AlertRule}");

        // Send to monitoring channel only
        await SendToSlack(alert, "warning");
    }

    // Implementation details...
}

public class CommonAlertSchema
{
    public string SchemaId { get; set; }
    public AlertData Data { get; set; }
}

public class AlertData
{
    public AlertEssentials Essentials { get; set; }
    public AlertContext AlertContext { get; set; }
    public Dictionary<string, string> CustomProperties { get; set; }
}

public class AlertEssentials
{
    public string AlertId { get; set; }
    public string AlertRule { get; set; }
    public string Severity { get; set; }
    public string SignalType { get; set; }
    public string MonitorCondition { get; set; }
    public DateTime FiredDateTime { get; set; }
    public string Description { get; set; }
}

public class AlertContext
{
    public AlertCondition Condition { get; set; }
}

public class AlertCondition
{
    public string WindowSize { get; set; }
    public List<MetricCriteria> AllOf { get; set; }
}

public class MetricCriteria
{
    public string MetricName { get; set; }
    public string MetricNamespace { get; set; }
    public string Operator { get; set; }
    public string Threshold { get; set; }
    public double MetricValue { get; set; }
}
```

---

## Testing Alert Notifications

### Test Critical Alert

```bash
# Simulate service down
az functionapp stop \
  --name func-honua-dev-a1b2c3 \
  --resource-group rg-honua-dev-eastus

# Wait 15 minutes for alert to fire

# Restart service
az functionapp start \
  --name func-honua-dev-a1b2c3 \
  --resource-group rg-honua-dev-eastus
```

---

### Test High Error Rate

```bash
# Generate errors
for i in {1..100}; do
  curl -X POST https://func-honua-dev-a1b2c3.azurewebsites.net/api/test-error &
done

# Wait 15 minutes for alert evaluation
```

---

### Verify Alert Notification

```bash
# Check alert status
az monitor metrics alert show \
  --name honua-high-error-rate-dev \
  --resource-group rg-honua-dev-eastus \
  --query "properties.enabled"

# List recent alert activations
az monitor activity-log list \
  --resource-group rg-honua-dev-eastus \
  --caller "Azure Monitor Alerts" \
  --start-time 2025-10-18T00:00:00Z
```

---

## Alert Notification Best Practices

1. **Use Common Alert Schema**: Enables consistent parsing across all integrations
2. **Include Context**: Add custom properties for faster triage
3. **Provide Action Links**: Direct links to Azure Portal, runbooks, dashboards
4. **Test Regularly**: Monthly test of critical alert notifications
5. **Document Runbooks**: Include remediation steps in notification
6. **Rate Limiting**: Implement deduplication to avoid alert storms
7. **Escalation Policies**: Define clear escalation paths for each severity
8. **Maintenance Windows**: Suppress non-critical alerts during maintenance
9. **Review Quarterly**: Audit alert effectiveness and false positive rate
10. **Monitor Notification Delivery**: Track webhook success rates

---

## Summary

This document provides comprehensive samples for:
- ✅ Email notifications (formatted, actionable)
- ✅ Webhook payloads (Common Alert Schema)
- ✅ Slack integration (formatted messages)
- ✅ Microsoft Teams integration (Adaptive Cards)
- ✅ PagerDuty integration (incident creation)
- ✅ Custom webhook handlers (multi-channel processing)

All samples use the Azure Monitor Common Alert Schema for consistency and ease of integration.

---

## References

- [Azure Monitor Common Alert Schema](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-common-schema)
- [Action Groups](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/action-groups)
- [Slack Incoming Webhooks](https://api.slack.com/messaging/webhooks)
- [Microsoft Teams Webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook)
- [PagerDuty Events API](https://developer.pagerduty.com/docs/ZG9jOjExMDI5NTgw-events-api-v2-overview)

---

**Last Updated**: 2025-10-18
**Maintained By**: Honua DevOps Team
