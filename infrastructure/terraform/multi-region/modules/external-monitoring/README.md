# External Monitoring Module

This Terraform module provides cloud-agnostic external uptime monitoring for Honua deployments. It abstracts the differences between Azure Monitor Availability Tests, AWS CloudWatch Synthetics, and GCP Cloud Monitoring Uptime Checks.

## Features

- **Multi-cloud support**: Works with Azure, AWS, and GCP
- **Standardized endpoints**: Monitors key Honua endpoints consistently
- **Multi-region probing**: Tests from multiple geographic locations
- **SSL validation**: Checks SSL certificate validity and expiration
- **Content validation**: Verifies response content matches expectations
- **Flexible alerting**: Supports email, webhooks, and cloud-native notifications

## Monitored Endpoints

The module monitors these standard Honua endpoints:

1. `/healthz/live` - Liveness probe (critical)
2. `/healthz/ready` - Readiness probe (error)
3. `/ogc` - OGC API landing page (error)
4. `/ogc/conformance` - OGC conformance endpoint (warning)

## Usage

### Azure Deployment

```hcl
module "external_monitoring" {
  source = "./modules/external-monitoring"

  cloud_provider = "azure"
  environment    = "production"
  project_name   = "honua"
  app_url        = "https://honua.example.com"

  # Monitoring configuration
  enable_monitoring       = true
  check_frequency_seconds = 300  # 5 minutes
  check_timeout_seconds   = 30
  enable_ssl_validation   = true
  ssl_cert_expiry_days    = 7

  # Azure-specific
  azure_resource_group_name = azurerm_resource_group.main.name
  azure_app_insights_id     = azurerm_application_insights.main.id
  azure_action_group_ids = {
    critical = azurerm_monitor_action_group.critical.id
    warning  = azurerm_monitor_action_group.warning.id
  }

  # Alert configuration
  alert_email       = "alerts@example.com"
  alert_webhook_url = "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"

  tags = {
    Team = "DevOps"
  }
}
```

### AWS Deployment

```hcl
module "external_monitoring" {
  source = "./modules/external-monitoring"

  cloud_provider = "aws"
  environment    = "production"
  project_name   = "honua"
  app_url        = "https://honua.example.com"

  # Monitoring configuration
  enable_monitoring       = true
  check_frequency_seconds = 300
  check_timeout_seconds   = 60
  enable_ssl_validation   = true
  ssl_cert_expiry_days    = 7

  # AWS-specific
  aws_region        = "us-east-1"
  aws_sns_topic_arn = aws_sns_topic.alerts.arn

  # Alert configuration
  alert_email = "alerts@example.com"

  tags = {
    Team = "DevOps"
  }
}
```

### GCP Deployment

```hcl
module "external_monitoring" {
  source = "./modules/external-monitoring"

  cloud_provider = "gcp"
  environment    = "production"
  project_name   = "honua"
  app_url        = "https://honua.example.com"

  # Monitoring configuration
  enable_monitoring       = true
  check_frequency_seconds = 300
  check_timeout_seconds   = 10
  enable_ssl_validation   = true
  ssl_cert_expiry_days    = 7

  # GCP-specific
  gcp_project_id           = "my-gcp-project"
  gcp_notification_channels = [
    google_monitoring_notification_channel.email.id
  ]

  # Alert configuration
  alert_email = "alerts@example.com"

  tags = {
    team = "devops"
  }
}
```

## Inputs

### Common Variables

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| `cloud_provider` | Cloud provider (azure, aws, gcp) | `string` | n/a | yes |
| `environment` | Environment name | `string` | n/a | yes |
| `app_url` | Application URL to monitor | `string` | n/a | yes |
| `enable_monitoring` | Enable external monitoring | `bool` | `true` | no |
| `check_frequency_seconds` | Check frequency in seconds | `number` | `300` | no |
| `check_timeout_seconds` | Check timeout in seconds | `number` | `30` | no |
| `alert_email` | Email for alerts | `string` | `null` | no |
| `enable_ssl_validation` | Enable SSL validation | `bool` | `true` | no |
| `ssl_cert_expiry_days` | SSL cert expiry warning threshold | `number` | `7` | no |

### Azure-Specific Variables

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| `azure_resource_group_name` | Resource group name | `string` | `null` | yes (for Azure) |
| `azure_app_insights_id` | Application Insights ID | `string` | `null` | yes (for Azure) |
| `azure_action_group_ids` | Action group IDs | `map(string)` | `{}` | no |

### AWS-Specific Variables

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| `aws_region` | AWS region | `string` | `"us-east-1"` | no |
| `aws_sns_topic_arn` | SNS topic ARN | `string` | `null` | no |

### GCP-Specific Variables

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| `gcp_project_id` | GCP project ID | `string` | `null` | yes (for GCP) |
| `gcp_notification_channels` | Notification channel IDs | `list(string)` | `[]` | no |

## Outputs

| Name | Description |
|------|-------------|
| `monitoring_configuration` | Complete monitoring configuration |
| `endpoints_monitored` | List of monitored endpoints |
| `alert_configuration` | Alert notification setup |
| `monitoring_dashboard_urls` | Cloud provider dashboard URLs |

## Monitoring Regions

### Azure Default Regions
- East US (us-va-ash-azr)
- West Europe (emea-nl-ams-azr)
- Southeast Asia (apac-sg-sin-azr)
- Australia East (apac-au-syd-azr)
- UK South (emea-gb-db3-azr)

### AWS Default Regions
- us-east-1 (N. Virginia)
- us-west-1 (N. California)
- eu-west-1 (Ireland)
- ap-southeast-1 (Singapore)
- ap-northeast-1 (Tokyo)

### GCP Default Regions
- USA
- EUROPE
- SOUTH_AMERICA
- ASIA_PACIFIC

## Alert Severity Levels

| Endpoint | Severity | Description |
|----------|----------|-------------|
| `/healthz/live` | Critical | Application completely down |
| `/healthz/ready` | Error | Dependencies unhealthy |
| `/ogc` | Error | Core API unavailable |
| `/ogc/conformance` | Warning | Standards endpoint issue |

## Best Practices

1. **Multi-region monitoring**: Use at least 3 geographic regions
2. **Alert thresholds**: Configure based on acceptable downtime SLA
3. **SSL monitoring**: Keep cert expiry threshold at 7 days minimum
4. **Content validation**: Always validate response content, not just HTTP status
5. **Webhook integration**: Use webhooks for real-time notifications (Slack, Teams, PagerDuty)

## Troubleshooting

### Tests Failing After Deployment

1. Verify the app URL is correct and accessible
2. Check SSL certificate is valid
3. Ensure health endpoints return expected content
4. Review firewall rules for monitoring IPs

### False Positives

1. Increase check frequency if tests are too sensitive
2. Adjust failure threshold (e.g., require 2+ regions to fail)
3. Verify content matchers are not too strict

### Missing Alerts

1. Confirm notification channels are configured
2. Check email/webhook endpoints are valid
3. Review alert policy conditions

## Related Documentation

- [Azure Monitor Availability Tests](../azure/availability-tests.tf)
- [AWS CloudWatch Synthetics](../aws/synthetics-canaries.tf)
- [GCP Cloud Monitoring](../gcp/uptime-checks.tf)
- [Monitoring Coverage Documentation](../../../../docs/observability/external-monitoring.md)
