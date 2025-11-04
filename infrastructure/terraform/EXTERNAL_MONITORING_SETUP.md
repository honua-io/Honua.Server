# External Monitoring Setup Guide

Quick reference for deploying external uptime monitoring for Honua across cloud providers.

## Quick Start

### Azure Monitor Availability Tests

```bash
cd infrastructure/terraform/azure

# Configure variables
cat > terraform.tfvars <<EOF
environment = "production"
app_url = "https://honua.example.com"
admin_email = "alerts@example.com"

# Enable availability tests
enable_availability_tests = true
availability_test_frequency = 300  # 5 minutes
availability_test_timeout = 30

# Optional: Slack webhook
# slack_webhook_url = "https://hooks.slack.com/services/YOUR/WEBHOOK"
EOF

# Deploy
terraform init
terraform plan
terraform apply
```

**View Results**:
```bash
# Get Application Insights ID from Terraform output
terraform output -json | jq -r '.availability_tests.value.dashboards.azure_portal'

# Or navigate to Azure Portal → Application Insights → Availability
```

### AWS CloudWatch Synthetics

```bash
cd infrastructure/terraform/aws

# Package canary scripts first
cd canary-scripts
./package.sh
cd ..

# Configure variables
cat > terraform.tfvars <<EOF
environment = "production"
app_url = "https://honua.example.com"
alert_email = "alerts@example.com"

# Enable synthetics
enable_synthetics = true
canary_schedule_expression = "rate(5 minutes)"
canary_timeout_seconds = 60
canary_retention_days = 31
EOF

# Deploy
terraform init
terraform plan
terraform apply
```

**View Results**:
```bash
# Open CloudWatch Synthetics console
aws cloudwatch list-canaries --output table

# Get canary details
terraform output -json synthetics_canaries
```

### GCP Cloud Monitoring Uptime Checks

```bash
cd infrastructure/terraform/gcp

# Configure variables
cat > terraform.tfvars <<EOF
project_id = "my-gcp-project"
environment = "production"
app_url = "https://honua.example.com"

# Enable uptime checks
enable_uptime_checks = true
uptime_check_period = "300s"
uptime_check_timeout = "10s"

# Notification channels (create these first in GCP Console)
alert_notification_channels = [
  "projects/my-gcp-project/notificationChannels/1234567890"
]
EOF

# Deploy
terraform init
terraform plan
terraform apply
```

**View Results**:
```bash
# List uptime checks
gcloud monitoring uptime-check-configs list

# Get uptime check details
terraform output -json uptime_checks
```

## Multi-Cloud Setup

Use the unified external monitoring module for multi-cloud deployments:

```hcl
# main.tf
module "external_monitoring_azure" {
  source = "./modules/external-monitoring"

  cloud_provider = "azure"
  environment    = var.environment
  app_url        = var.app_url

  azure_resource_group_name = azurerm_resource_group.main.name
  azure_app_insights_id     = azurerm_application_insights.main.id

  alert_email = var.alert_email
}

module "external_monitoring_aws" {
  source = "./modules/external-monitoring"

  cloud_provider = "aws"
  environment    = var.environment
  app_url        = var.app_url

  aws_region = "us-east-1"

  alert_email = var.alert_email
}

module "external_monitoring_gcp" {
  source = "./modules/external-monitoring"

  cloud_provider = "gcp"
  environment    = var.environment
  app_url        = var.app_url

  gcp_project_id = var.gcp_project_id

  alert_email = var.alert_email
}
```

## Monitored Endpoints

| Endpoint | Path | Expected Content | Severity |
|----------|------|------------------|----------|
| Liveness | `/healthz/live` | "Healthy" | Critical |
| Readiness | `/healthz/ready` | "Healthy" | Error |
| OGC API | `/ogc` | "links" | Error |
| OGC Conformance | `/ogc/conformance` | "conformsTo" | Warning |
| STAC Catalog | `/stac` | "type" | Warning |

## Alert Configuration

### Email Alerts

**Azure**:
```hcl
variable "admin_email" {
  default = "alerts@example.com"
}
```

**AWS**:
```hcl
variable "alert_email" {
  default = "alerts@example.com"
}
```

**GCP**:
```bash
# Create notification channel
gcloud alpha monitoring channels create \
  --display-name="Email Alerts" \
  --type=email \
  --channel-labels=email_address=alerts@example.com
```

### Slack Alerts

**Azure**:
```hcl
webhook_receiver {
  name        = "slack-webhook"
  service_uri = "https://hooks.slack.com/services/T00/B00/XXXX"
  use_common_alert_schema = true
}
```

**AWS**:
```bash
# Set up AWS Chatbot
aws chatbot create-slack-channel-configuration \
  --configuration-name honua-alerts \
  --slack-channel-id C0123456789 \
  --slack-workspace-id T0123456789 \
  --sns-topic-arns arn:aws:sns:us-east-1:123456789012:honua-alerts
```

**GCP**:
```bash
# Create Slack notification channel
gcloud alpha monitoring channels create \
  --display-name="Slack Alerts" \
  --type=slack \
  --channel-labels=channel_name=#honua-alerts \
  --auth-token=${SLACK_TOKEN}
```

### PagerDuty Integration

**Azure**:
```hcl
webhook_receiver {
  name        = "pagerduty"
  service_uri = "https://events.pagerduty.com/integration/${var.pagerduty_key}/enqueue"
}
```

**AWS**:
```bash
# Create SNS topic
aws sns create-topic --name honua-pagerduty-alerts

# Subscribe PagerDuty endpoint
aws sns subscribe \
  --topic-arn arn:aws:sns:us-east-1:123456789012:honua-pagerduty-alerts \
  --protocol https \
  --notification-endpoint https://events.pagerduty.com/integration/${PAGERDUTY_KEY}/enqueue
```

**GCP**:
```bash
# Create PagerDuty notification channel
gcloud alpha monitoring channels create \
  --display-name="PagerDuty" \
  --type=pagerduty \
  --channel-labels=service_key=${PAGERDUTY_SERVICE_KEY}
```

## Testing

### Test Endpoint Manually

```bash
# Liveness check
curl -v https://honua.example.com/healthz/live

# Readiness check
curl -v https://honua.example.com/healthz/ready

# OGC API
curl -H "Accept: application/json" https://honua.example.com/ogc

# OGC Conformance
curl -H "Accept: application/json" https://honua.example.com/ogc/conformance

# STAC Catalog
curl -H "Accept: application/json" https://honua.example.com/stac
```

### Trigger Test Alert

**Azure**:
```bash
az monitor metrics alert update \
  --name honua-liveness-failed-production \
  --resource-group honua-production \
  --enabled true

# Temporarily disable endpoint to trigger alert
# Then re-enable
```

**AWS**:
```bash
# Set alarm to ALARM state
aws cloudwatch set-alarm-state \
  --alarm-name honua-liveness-failed-production \
  --state-value ALARM \
  --state-reason "Testing alert notification"
```

**GCP**:
```bash
# Force alert condition
gcloud alpha monitoring policies update \
  projects/my-gcp-project/alertPolicies/1234567890 \
  --enabled
```

## Monitoring Regions

### Azure (5 regions)
- East US (us-va-ash-azr)
- West Europe (emea-nl-ams-azr)
- Southeast Asia (apac-sg-sin-azr)
- Australia East (apac-au-syd-azr)
- UK South (emea-gb-db3-azr)

### AWS (5 regions - configurable)
- us-east-1 (N. Virginia)
- us-west-1 (N. California)
- eu-west-1 (Ireland)
- ap-southeast-1 (Singapore)
- ap-northeast-1 (Tokyo)

### GCP (4 regions)
- USA
- EUROPE
- SOUTH_AMERICA
- ASIA_PACIFIC

## Troubleshooting

### Tests Not Running

**Azure**:
```bash
# Check availability test status
az monitor app-insights web-test list \
  --resource-group honua-production \
  --output table

# View test results
az monitor app-insights web-test show \
  --resource-group honua-production \
  --name honua-liveness-production
```

**AWS**:
```bash
# List canaries
aws synthetics describe-canaries

# Get canary last run
aws synthetics get-canary-runs \
  --name honua-liveness-production \
  --max-results 5
```

**GCP**:
```bash
# List uptime checks
gcloud monitoring uptime-check-configs list

# Describe specific check
gcloud monitoring uptime-check-configs describe honua-liveness-production
```

### Alerts Not Firing

**Check notification channels**:
```bash
# Azure
az monitor action-group list --output table

# AWS
aws sns list-subscriptions

# GCP
gcloud alpha monitoring channels list
```

**Test notifications directly**:
```bash
# Azure
az monitor action-group test-notifications create \
  --action-group-name honua-critical-alerts \
  --notification-type Email

# AWS
aws sns publish \
  --topic-arn arn:aws:sns:us-east-1:123456789012:honua-alerts \
  --message "Test alert notification"

# GCP (send test alert)
gcloud alpha monitoring policies test \
  --policy-id honua-liveness-failed-production
```

### High Latency Alerts

1. Check CDN configuration
2. Review database performance
3. Check application logs
4. Verify network routing

```bash
# Trace route to application
traceroute honua.example.com

# Check DNS resolution time
dig honua.example.com

# Test latency from different regions
for region in us-east-1 eu-west-1 ap-southeast-1; do
  echo "Testing from ${region}..."
  # Use region-specific test endpoint
done
```

## Cleanup

### Remove All Monitoring

**Azure**:
```bash
cd infrastructure/terraform/azure
terraform destroy -auto-approve
```

**AWS**:
```bash
cd infrastructure/terraform/aws
terraform destroy -auto-approve
```

**GCP**:
```bash
cd infrastructure/terraform/gcp
terraform destroy -auto-approve
```

### Remove Specific Tests

**Azure**:
```bash
terraform destroy \
  -target=azurerm_application_insights_standard_web_test.liveness
```

**AWS**:
```bash
terraform destroy \
  -target=aws_synthetics_canary.liveness
```

**GCP**:
```bash
terraform destroy \
  -target=google_monitoring_uptime_check_config.liveness
```

## Cost Estimate

| Provider | Tests | Regions | Frequency | Monthly Cost |
|----------|-------|---------|-----------|--------------|
| Azure | 5 | 5 | 5 min | ~$25 |
| AWS | 4 | Regional | 5 min | ~$42 |
| GCP | 5 | 4 | 5 min | Free (< 1M) |

**Total**: ~$50-70/month for comprehensive multi-cloud monitoring

## Next Steps

1. Review [Full Documentation](../../docs/observability/external-monitoring.md)
2. Set up [Alert Runbooks](../../docs/operations/RUNBOOKS.md)
3. Configure [Performance Baselines](../../docs/observability/performance-baselines.md)
4. Test failover scenarios
5. Document incident response procedures

## Support

- Documentation: `/docs/observability/external-monitoring.md`
- Issues: Create GitHub issue with `monitoring` label
- Emergency: Follow incident response runbook
