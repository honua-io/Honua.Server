# Application Insights Alerts - Deployment Guide

**Quick Start**: Deploy Application Insights alert rules in 5 minutes

---

## Prerequisites

- ✅ Terraform >= 1.5.0 installed
- ✅ Azure CLI installed and authenticated (`az login`)
- ✅ Azure subscription with permissions to create alert rules
- ✅ Admin email address for notifications

---

## Quick Deployment

### Step 1: Configure Variables (2 minutes)

Create or update `terraform.tfvars`:

```hcl
# Required
admin_email             = "your-email@yourdomain.com"
postgres_admin_username = "honuaadmin"
postgres_admin_password = "YourSecurePassword123!"

# Optional (defaults shown)
location    = "eastus"
environment = "dev"  # dev, staging, or prod
```

### Step 2: Initialize Terraform (1 minute)

```bash
cd /home/mike/projects/HonuaIO/infrastructure/terraform/azure
terraform init
```

Expected output:
```
Initializing provider plugins...
- Finding hashicorp/azurerm versions matching "~> 3.80"...
- Installing hashicorp/azurerm v3.80.0...

Terraform has been successfully initialized!
```

### Step 3: Review Plan (1 minute)

```bash
terraform plan -out=tfplan
```

Look for:
```
Plan: 27 to add, 0 to change, 0 to destroy.

Changes:
  + azurerm_monitor_action_group.critical_alerts
  + azurerm_monitor_action_group.warning_alerts
  + azurerm_monitor_metric_alert.service_down
  + azurerm_monitor_metric_alert.high_error_rate
  ...
  + 22 alert rules total
```

### Step 4: Deploy (1 minute)

```bash
terraform apply tfplan
```

Wait for completion:
```
Apply complete! Resources: 27 added, 0 changed, 0 destroyed.

Outputs:

alert_configuration = {
  "action_groups" = {
    "critical" = "/subscriptions/.../ag-honua-critical-dev"
    "warning" = "/subscriptions/.../ag-honua-warning-dev"
  }
  "alert_counts" = {
    "total_alerts" = 22
    ...
  }
}
```

### Step 5: Verify (1 minute)

```bash
# List alert rules
az monitor metrics alert list \
  --resource-group rg-honua-dev-eastus \
  --query "[].{Name:name, Enabled:enabled, Severity:severity}" \
  -o table

# Test notification
az monitor action-group test-notifications \
  --action-group-id $(terraform output -raw alert_configuration | jq -r '.action_groups.critical')
```

Check your email for test notification!

---

## What Gets Deployed

### 22 Alert Rules

**Availability (2)**:
- Service down (0 requests)
- Low availability (<99%)

**Performance (3)**:
- High response time (>1s)
- Critical response time (>5s)
- Slow dependencies (>2s)

**Error Rate (3)**:
- High error rate (>5%)
- High exception rate
- Failed dependencies

**Resource (4)**:
- High memory (>90%, >95%)
- High CPU (>80%)
- HTTP queue length

**AI/LLM (3)**:
- OpenAI rate limits
- High token usage
- AI Search throttling

**Database (4)**:
- High CPU/memory
- High connections
- Connection failures

**Smart Detection (3)**:
- Failure anomalies
- Slow page load
- Slow server response

### 2 Action Groups

**Critical** (Sev0):
- Email notification
- Azure App push
- Webhook (Slack/Teams/PagerDuty)

**Warning** (Sev2):
- Email notification
- Webhook (optional)

---

## Configuration Options

### Slack Integration

Add to action group in `app-insights-alerts.tf`:

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

### Environment-Specific Thresholds

```hcl
# In app-insights-alerts.tf
locals {
  response_time_threshold = var.environment == "prod" ? 1000 : 2000
  memory_threshold        = var.environment == "prod" ? 90 : 95
}

resource "azurerm_monitor_metric_alert" "high_response_time" {
  # ...
  threshold = local.response_time_threshold
}
```

### SMS Notifications

Uncomment in `app-insights-alerts.tf`:

```hcl
resource "azurerm_monitor_action_group" "critical_alerts" {
  # ...

  sms_receiver {
    name         = "admin-sms"
    country_code = "1"
    phone_number = "5551234567"
  }
}
```

---

## Testing

### Test Critical Alert

```bash
# Stop Function App (simulates service down)
az functionapp stop \
  --name func-honua-dev-a1b2c3 \
  --resource-group rg-honua-dev-eastus

# Wait 15 minutes for alert to fire
# Check email for notification

# Restart
az functionapp start \
  --name func-honua-dev-a1b2c3 \
  --resource-group rg-honua-dev-eastus
```

### Test Error Rate Alert

```bash
# Generate errors
for i in {1..100}; do
  curl -X POST https://func-honua-dev.azurewebsites.net/api/test-error &
done

# Wait 15-20 minutes
# Check email for alert
```

---

## Troubleshooting

### Alert Not Created

**Check Terraform state**:
```bash
terraform state list | grep alert
```

**Re-apply if missing**:
```bash
terraform apply -target=azurerm_monitor_metric_alert.service_down
```

### No Email Received

**Check spam folder first!**

**Test action group**:
```bash
az monitor action-group test-notifications \
  --action-group-id /subscriptions/.../ag-honua-critical-dev
```

**Verify email address**:
```bash
terraform output alert_configuration | jq '.notification_channels.email'
```

### Permission Errors

**Grant required permissions**:
```bash
az role assignment create \
  --assignee $(az account show --query user.name -o tsv) \
  --role "Monitoring Contributor" \
  --scope /subscriptions/YOUR_SUBSCRIPTION_ID
```

---

## Cost

**Monthly Cost**: ~$3.10
- 21 static metric alerts: $2.10
- 1 dynamic metric alert: $1.00
- Action groups: Free (first 1000)
- Smart detection: Free

**Annual Cost**: ~$37/year

---

## Next Steps

1. ✅ Deploy to dev (done above)
2. 📧 Configure Slack/Teams webhook
3. 🧪 Test alerts
4. 📊 Monitor for false positives
5. 🔧 Tune thresholds if needed
6. 🚀 Deploy to staging
7. 🚀 Deploy to production

---

## Resources

- **Alert Thresholds**: [ALERT_THRESHOLDS.md](ALERT_THRESHOLDS.md)
- **Notification Samples**: [ALERT_NOTIFICATION_SAMPLES.md](ALERT_NOTIFICATION_SAMPLES.md)
- **Quick Reference**: [ALERT_QUICK_REFERENCE.md](ALERT_QUICK_REFERENCE.md)
- **Full README**: [ALERTS_README.md](ALERTS_README.md)

---

## Support

- **Documentation**: See files above
- **Azure Docs**: https://learn.microsoft.com/azure/azure-monitor/alerts/
- **Issues**: Review [Troubleshooting](#troubleshooting) section

---

**Deployment Time**: 5 minutes
**Cost**: $3.10/month
**Alert Rules**: 22
**Ready to Deploy**: ✅ Yes
