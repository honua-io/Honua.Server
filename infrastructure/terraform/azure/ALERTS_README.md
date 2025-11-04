# Application Insights Alert Rules - README

**Last Updated**: 2025-10-18
**Version**: 1.0.0
**Status**: Production Ready

---

## Overview

This directory contains comprehensive Application Insights alert rules for monitoring the Honua AI Deployment Consultant Azure infrastructure. The alert system provides multi-layered monitoring across availability, performance, error rates, resource utilization, AI/LLM operations, and database health.

---

## Files in This Directory

### 📄 Terraform Configuration

| File | Purpose | LOC |
|------|---------|-----|
| `app-insights-alerts.tf` | Main alert rules and action groups | 800+ |
| `main.tf` | Core infrastructure (includes App Insights) | 873 |

### 📚 Documentation

| File | Purpose | Audience |
|------|---------|----------|
| `ALERT_THRESHOLDS.md` | Detailed alert thresholds and response procedures | DevOps, SRE |
| `ALERT_NOTIFICATION_SAMPLES.md` | Sample notifications and integration guides | DevOps, Integrations |
| `ALERT_QUICK_REFERENCE.md` | Quick reference for on-call engineers | On-Call Team |
| `ALERTS_README.md` | This file - overview and deployment guide | All |

---

## Alert Coverage

### 📊 Alert Statistics

- **Total Alert Rules**: 22
- **Action Groups**: 2 (Critical, Warning)
- **Smart Detection Rules**: 3
- **Estimated Monthly Cost**: ~$2.20

### 🎯 Alert Categories

| Category | Count | Severity Levels |
|----------|-------|-----------------|
| Availability | 2 | Critical |
| Performance | 3 | Warning, Error |
| Error Rate | 3 | Error |
| Resource | 4 | Warning, Critical |
| AI/LLM | 3 | Warning, Error |
| Database | 4 | Warning, Critical |
| Smart Detection | 3 | Various |

---

## Quick Start

### Prerequisites

1. **Terraform** >= 1.5.0
2. **Azure CLI** logged in (`az login`)
3. **Azure Subscription** with appropriate permissions
4. **Admin Email** for alert notifications

### Deployment

**1. Initialize Terraform**:
```bash
cd infrastructure/terraform/azure
terraform init
```

**2. Configure Variables**:

Create `terraform.tfvars`:
```hcl
# Required variables
admin_email                = "alerts@yourdomain.com"
postgres_admin_username    = "honuaadmin"
postgres_admin_password    = "YourSecurePassword123!"

# Optional variables
location    = "eastus"
environment = "dev"  # dev, staging, or prod
```

**3. Review Plan**:
```bash
terraform plan -out=tfplan
```

Expected resources:
- 22 metric alert rules
- 2 action groups
- 3 smart detection rules
- Application Insights (if not exists)

**4. Apply Configuration**:
```bash
terraform apply tfplan
```

**5. Verify Deployment**:
```bash
# List all alert rules
az monitor metrics alert list \
  --resource-group rg-honua-dev-eastus \
  --query "[].name" -o table

# Check action groups
az monitor action-group list \
  --resource-group rg-honua-dev-eastus \
  --query "[].name" -o table
```

---

## Configuration

### Action Groups

#### Critical Alerts Action Group

**Name**: `ag-honua-critical-{environment}`

**Notification Channels**:
- ✅ Email (admin)
- ✅ Azure Mobile App push
- ✅ Webhook (Slack/Teams)
- 🔲 SMS (optional, uncomment in Terraform)

**When to Use**:
- Service outages (Sev0)
- Database failures (Sev0)
- Critical resource exhaustion (Sev0)

#### Warning Alerts Action Group

**Name**: `ag-honua-warning-{environment}`

**Notification Channels**:
- ✅ Email (admin)
- ✅ Webhook (optional)

**When to Use**:
- High latency (Sev2)
- Resource warnings (Sev2)
- Non-critical issues (Sev2)

---

### Customizing Alerts

#### Adjusting Thresholds

Edit `app-insights-alerts.tf`:

```hcl
# Example: Change high response time threshold
resource "azurerm_monitor_metric_alert" "high_response_time" {
  # ... existing config ...

  criteria {
    threshold = 1500  # Changed from 1000ms to 1500ms
  }
}
```

Apply changes:
```bash
terraform apply
```

---

#### Adding New Alert Rule

```hcl
# Example: Add alert for high request rate
resource "azurerm_monitor_metric_alert" "high_request_rate" {
  name                = "honua-high-request-rate-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_application_insights.main.id]
  description         = "Alert when request rate is abnormally high"
  severity            = 2 # Warning
  frequency           = "PT5M"
  window_size         = "PT15M"

  criteria {
    metric_namespace = "microsoft.insights/components"
    metric_name      = "requests/rate"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 1000 # 1000 requests per second
  }

  action {
    action_group_id = azurerm_monitor_action_group.warning_alerts.id
  }

  tags = merge(local.tags, {
    AlertType = "Performance"
    Severity  = "Warning"
  })
}
```

---

#### Environment-Specific Thresholds

```hcl
locals {
  # Define thresholds by environment
  response_time_threshold = var.environment == "prod" ? 1000 : 2000
  error_rate_threshold    = var.environment == "prod" ? 5 : 10
  memory_threshold        = var.environment == "prod" ? 90 : 95
}

resource "azurerm_monitor_metric_alert" "high_response_time" {
  # ... config ...

  criteria {
    threshold = local.response_time_threshold
  }
}
```

---

## Integration Examples

### Slack Integration

**1. Get Slack Webhook URL**:
- Go to https://api.slack.com/apps
- Create app → Incoming Webhooks
- Copy webhook URL

**2. Update Terraform**:
```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ... existing config ...

  webhook_receiver {
    name        = "slack-alerts"
    service_uri = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
    use_common_alert_schema = true
  }
}
```

**3. Apply**:
```bash
terraform apply
```

See [ALERT_NOTIFICATION_SAMPLES.md](ALERT_NOTIFICATION_SAMPLES.md) for message formatting.

---

### Microsoft Teams Integration

**1. Get Teams Webhook**:
- Open Teams channel → Connectors
- Add "Incoming Webhook"
- Copy webhook URL

**2. Update Terraform**:
```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ... existing config ...

  webhook_receiver {
    name        = "teams-alerts"
    service_uri = "https://outlook.office.com/webhook/YOUR/WEBHOOK/URL"
    use_common_alert_schema = true
  }
}
```

---

### PagerDuty Integration

See detailed setup in [ALERT_NOTIFICATION_SAMPLES.md](ALERT_NOTIFICATION_SAMPLES.md#pagerduty-integration).

**Summary**:
1. Get PagerDuty integration key
2. Create Azure Logic App
3. Configure Logic App receiver in action group

---

## Testing

### Test Alert Rule

**1. Test High Error Rate Alert**:
```bash
# Generate errors
for i in {1..50}; do
  curl -X POST https://func-honua-dev-{suffix}.azurewebsites.net/api/test-error
done

# Wait 15-20 minutes for alert to fire
```

**2. Verify Alert Fired**:
```bash
az monitor metrics alert show \
  --name honua-high-error-rate-dev \
  --resource-group rg-honua-dev-eastus \
  --query "properties.condition"
```

**3. Check Notification**:
- Verify email received
- Check Slack/Teams channel
- Review Azure Portal → Alerts

---

### Test Notification Channels

**Send Test Notification**:

Azure Portal:
1. Go to Monitor → Alerts → Action Groups
2. Select `ag-honua-critical-dev`
3. Click "Test action group"
4. Select "Email/SMS/Push/Voice"
5. Click "Test"

CLI:
```bash
az monitor action-group test-notifications \
  --action-group-id /subscriptions/.../actionGroups/ag-honua-critical-dev \
  --notification-type Email
```

---

## Monitoring Alert Health

### Alert Firing Rate

**Query** (Application Insights):
```kusto
AzureDiagnostics
| where Category == "Alert"
| where TimeGenerated > ago(7d)
| summarize count() by bin(TimeGenerated, 1h), alertName_s
| render timechart
```

### False Positive Rate

Track alerts that were fired but resolved without action:
```bash
az monitor activity-log list \
  --resource-group rg-honua-prod-eastus \
  --caller "Azure Monitor Alerts" \
  --start-time 2025-10-01T00:00:00Z \
  --end-time 2025-10-18T00:00:00Z \
  --query "[?contains(properties.status.value, 'Resolved')]" \
  -o table
```

**Target**: < 10% false positive rate

---

## Maintenance

### Suppressing Alerts During Maintenance

**Create Suppression Rule**:
```hcl
resource "azurerm_monitor_alert_processing_rule_suppression" "maintenance_window" {
  name                = "maintenance-sunday-3am"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_resource_group.main.id]

  schedule {
    recurrence {
      weekly {
        days_of_week = ["Sunday"]
        start_time   = "03:00:00"
        end_time     = "05:00:00"
      }
    }
  }

  condition {
    severity {
      operator = "Equals"
      values   = ["Sev2", "Sev3"]  # Suppress warnings only
    }
  }
}
```

**Manual Suppression** (temporary):
```bash
# Disable alert
az monitor metrics alert update \
  --name honua-high-response-time-prod \
  --resource-group rg-honua-prod-eastus \
  --enabled false

# Re-enable after maintenance
az monitor metrics alert update \
  --name honua-high-response-time-prod \
  --resource-group rg-honua-prod-eastus \
  --enabled true
```

---

### Quarterly Review Process

**1. Analyze Alert History** (last 90 days):
```bash
az monitor activity-log list \
  --resource-group rg-honua-prod-eastus \
  --caller "Azure Monitor Alerts" \
  --start-time 2025-07-18T00:00:00Z \
  --query "[].{Alert:properties.activityName, Status:properties.status.value, Time:eventTimestamp}" \
  -o table > alert-history-q3.txt
```

**2. Calculate Metrics**:
- Total alerts fired
- False positive rate
- Average time to resolution
- Most frequent alerts

**3. Adjust Thresholds**:
- Increase threshold if > 20% false positives
- Decrease threshold if missing real issues
- Update evaluation windows

**4. Document Changes**:
- Update `ALERT_THRESHOLDS.md`
- Add notes to Terraform comments
- Update runbooks

---

## Troubleshooting

### Alert Not Firing

**Checklist**:
1. ✅ Alert rule is enabled
2. ✅ Evaluation frequency and window size correct
3. ✅ Metric exists and has recent data
4. ✅ Threshold is appropriate for current conditions
5. ✅ Action group is configured

**Verify Alert Configuration**:
```bash
az monitor metrics alert show \
  --name honua-high-error-rate-prod \
  --resource-group rg-honua-prod-eastus \
  --query "{Enabled:enabled, Threshold:criteria.allOf[0].threshold, Frequency:evaluationFrequency, Window:windowSize}"
```

**Check Metric Data**:
```bash
az monitor metrics list \
  --resource /subscriptions/.../components/appi-honua-{suffix} \
  --metric "requests/failed" \
  --start-time 2025-10-18T00:00:00Z \
  --interval PT5M
```

---

### Notification Not Received

**Checklist**:
1. ✅ Action group has correct email/webhook
2. ✅ Email not in spam folder
3. ✅ Webhook endpoint is reachable
4. ✅ Alert rule references correct action group

**Test Action Group**:
```bash
az monitor action-group test-notifications \
  --action-group-id /subscriptions/.../actionGroups/ag-honua-critical-prod
```

**Check Action Group Logs**:
```bash
az monitor activity-log list \
  --resource-group rg-honua-prod-eastus \
  --caller "Azure Monitor Action Groups" \
  --start-time 2025-10-18T00:00:00Z
```

---

### Too Many False Positives

**Solutions**:

1. **Increase Threshold** (10-20%):
```hcl
criteria {
  threshold = 1200  # Increased from 1000
}
```

2. **Extend Evaluation Window**:
```hcl
window_size = "PT30M"  # Changed from PT15M
```

3. **Use Dynamic Thresholds**:
```hcl
dynamic_criteria {
  metric_namespace = "microsoft.insights/components"
  metric_name      = "requests/failed"
  aggregation      = "Count"
  operator         = "GreaterThan"
  alert_sensitivity = "Medium"  # Low, Medium, High
}
```

4. **Add Dimensions** (filter specific operations):
```hcl
criteria {
  # ... existing config ...

  dimension {
    name     = "operation_Name"
    operator = "Include"
    values   = ["POST /api/critical-operation"]
  }
}
```

---

## Cost Optimization

### Current Costs

| Component | Count | Unit Cost | Total |
|-----------|-------|-----------|-------|
| Static Metric Alerts | 21 | $0.10/month | $2.10 |
| Dynamic Metric Alerts | 1 | $1.00/month | $1.00 |
| Action Groups | 2 | Free (first 1000/month) | $0.00 |
| Smart Detection | 3 | Free | $0.00 |
| **Total** | | | **$3.10/month** |

### Optimization Tips

1. **Consolidate Similar Alerts**: Combine related alerts into multi-criteria rules
2. **Use Smart Detection**: Free ML-based anomaly detection
3. **Review Unused Alerts**: Disable alerts that never fire
4. **Optimize Frequency**: Longer evaluation intervals = fewer alert evaluations

---

## Best Practices

### ✅ Do's

1. **Use Common Alert Schema**: Enable for all webhook receivers
2. **Tag Everything**: Use consistent tags (Environment, Project, AlertType)
3. **Document Thresholds**: Explain why threshold was chosen
4. **Test Regularly**: Monthly test of critical alerts
5. **Review Quarterly**: Analyze alert effectiveness
6. **Version Control**: All alert configs in Terraform
7. **Environment Parity**: Same alerts across dev/staging/prod (different thresholds)
8. **Include Runbooks**: Link to remediation docs in alert description

### ❌ Don'ts

1. **Don't Ignore Warnings**: They're early indicators
2. **Don't Over-Alert**: Too many alerts = alert fatigue
3. **Don't Use Same Thresholds**: Dev ≠ Staging ≠ Prod
4. **Don't Hardcode**: Use variables for environment-specific values
5. **Don't Skip Testing**: Test before deploying to production
6. **Don't Forget Cleanup**: Remove alerts for deprecated features
7. **Don't Duplicate**: Use existing action groups
8. **Don't Skip Documentation**: Update docs when changing thresholds

---

## Support & Resources

### Documentation

- **Detailed Thresholds**: [ALERT_THRESHOLDS.md](ALERT_THRESHOLDS.md)
- **Notification Samples**: [ALERT_NOTIFICATION_SAMPLES.md](ALERT_NOTIFICATION_SAMPLES.md)
- **Quick Reference**: [ALERT_QUICK_REFERENCE.md](ALERT_QUICK_REFERENCE.md)
- **Monitoring Setup**: [/docs/MONITORING_SETUP.md](../../../docs/MONITORING_SETUP.md)

### External Resources

- [Azure Monitor Alerts Documentation](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-overview)
- [Application Insights Metrics](https://learn.microsoft.com/en-us/azure/azure-monitor/app/standard-metrics)
- [Common Alert Schema](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-common-schema)
- [Action Groups](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/action-groups)

### Getting Help

1. **Check Quick Reference**: [ALERT_QUICK_REFERENCE.md](ALERT_QUICK_REFERENCE.md)
2. **Review Runbooks**: [/docs/operations/RUNBOOKS.md](../../../docs/operations/RUNBOOKS.md)
3. **Azure Support**: Open ticket in Azure Portal
4. **Internal Slack**: #honua-alerts channel

---

## Changelog

### Version 1.0.0 (2025-10-18)

**Initial Release**:
- ✅ 22 metric alert rules
- ✅ 2 action groups (Critical, Warning)
- ✅ 3 smart detection rules
- ✅ Comprehensive documentation
- ✅ Integration samples (Slack, Teams, PagerDuty)
- ✅ Quick reference guide
- ✅ Testing procedures

**Alert Categories**:
- Availability monitoring (2 rules)
- Performance monitoring (3 rules)
- Error rate monitoring (3 rules)
- Resource monitoring (4 rules)
- AI/LLM monitoring (3 rules)
- Database monitoring (4 rules)
- Smart detection (3 rules)

---

## Contributing

### Adding New Alert Rule

1. **Define in Terraform**:
   - Add to `app-insights-alerts.tf`
   - Use consistent naming: `honua-{alert-type}-{environment}`
   - Add appropriate tags
   - Link to correct action group

2. **Document**:
   - Add to `ALERT_THRESHOLDS.md` (detailed documentation)
   - Add to `ALERT_QUICK_REFERENCE.md` (operations summary)
   - Update this README (alert count, categories)

3. **Test**:
   - Deploy to dev environment first
   - Trigger alert manually
   - Verify notification delivery
   - Document testing procedure

4. **Review**:
   - Code review by SRE team
   - Approval by DevOps lead
   - Deploy to staging
   - Deploy to production

---

## License

Internal use only - Honua DevOps Team

---

**Last Updated**: 2025-10-18
**Maintained By**: Honua DevOps Team
**Review Cycle**: Quarterly
**Next Review**: 2026-01-18
