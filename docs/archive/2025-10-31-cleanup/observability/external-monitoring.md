# External Uptime Monitoring

This document describes Honua's external uptime monitoring setup, which uses cloud-native synthetic monitoring services to detect outages and performance issues from multiple geographic locations.

## Overview

External uptime monitoring provides:

- **Independent validation**: Tests run outside the application infrastructure
- **Multi-region probing**: Detects region-specific outages
- **SSL/TLS validation**: Monitors certificate expiration
- **Content validation**: Verifies response correctness, not just HTTP status
- **Performance tracking**: Measures response times globally
- **Proactive alerting**: Detects issues before users report them

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     External Monitoring                      │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Azure      │  │     AWS      │  │     GCP      │      │
│  │  Monitor     │  │  CloudWatch  │  │    Cloud     │      │
│  │ Availability │  │  Synthetics  │  │  Monitoring  │      │
│  │    Tests     │  │   Canaries   │  │    Uptime    │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                 │                 │               │
│         └─────────────────┴─────────────────┘               │
│                           │                                  │
│                           ▼                                  │
│         ┌─────────────────────────────────┐                 │
│         │    Honua Application            │                 │
│         │  ┌────────────────────────┐     │                 │
│         │  │ /healthz/live          │     │                 │
│         │  │ /healthz/ready         │     │                 │
│         │  │ /ogc                   │     │                 │
│         │  │ /ogc/conformance       │     │                 │
│         │  │ /stac                  │     │                 │
│         │  └────────────────────────┘     │                 │
│         └─────────────────────────────────┘                 │
│                           │                                  │
│                           ▼                                  │
│         ┌─────────────────────────────────┐                 │
│         │      Alert Channels             │                 │
│         │  • Email                        │                 │
│         │  • Slack/Teams                  │                 │
│         │  • PagerDuty                    │                 │
│         │  • SMS (critical only)          │                 │
│         └─────────────────────────────────┘                 │
└─────────────────────────────────────────────────────────────┘
```

## Monitored Endpoints

### 1. Liveness Endpoint (`/healthz/live`)

**Purpose**: Basic availability check
**Severity**: Critical
**Check Frequency**: 5 minutes
**Expected Response**: 200 OK with "Healthy" in body

This is the most critical check. If this fails, the application is completely unavailable.

```bash
curl https://honua.example.com/healthz/live
# Expected: {"status": "Healthy"}
```

### 2. Readiness Endpoint (`/healthz/ready`)

**Purpose**: Full stack health including dependencies
**Severity**: Error
**Check Frequency**: 5 minutes
**Expected Response**: 200 OK with "Healthy" in body

Validates:
- Database connectivity
- Redis cache availability
- External service dependencies
- Configuration validity

```bash
curl https://honua.example.com/healthz/ready
# Expected: {"status": "Healthy", "checks": {...}}
```

### 3. OGC API Landing Page (`/ogc`)

**Purpose**: Core API functionality
**Severity**: Error
**Check Frequency**: 5 minutes
**Expected Response**: 200 OK with JSON containing "links" array

```bash
curl -H "Accept: application/json" https://honua.example.com/ogc
# Expected: {"links": [...], "title": "Honua OGC API"}
```

### 4. OGC Conformance Endpoint (`/ogc/conformance`)

**Purpose**: Standards compliance verification
**Severity**: Warning
**Check Frequency**: 5 minutes
**Expected Response**: 200 OK with JSON containing "conformsTo" array

```bash
curl -H "Accept: application/json" https://honua.example.com/ogc/conformance
# Expected: {"conformsTo": ["http://www.opengis.net/spec/..."]}
```

### 5. STAC Catalog Endpoint (`/stac`)

**Purpose**: Metadata catalog availability
**Severity**: Warning
**Check Frequency**: 5 minutes
**Expected Response**: 200 OK with JSON containing "type" field

```bash
curl -H "Accept: application/json" https://honua.example.com/stac
# Expected: {"type": "Catalog", "id": "honua-stac", ...}
```

## Cloud Provider Implementations

### Azure Monitor Availability Tests

**Configuration File**: `/infrastructure/terraform/azure/availability-tests.tf`

**Features**:
- Standard web tests with content validation
- Multi-region testing from 5+ Azure locations
- Integrated with Application Insights
- Smart Detection for anomalies
- SSL certificate expiration monitoring

**Test Locations**:
- East US (us-va-ash-azr)
- West Europe (emea-nl-ams-azr)
- Southeast Asia (apac-sg-sin-azr)
- Australia East (apac-au-syd-azr)
- UK South (emea-gb-db3-azr)

**Alert Configuration**:
```hcl
# Critical: Liveness check failed from 2+ locations
# Error: Readiness check failed from 2+ locations
# Warning: High latency (>2s average)
```

**Terraform Usage**:
```hcl
module "azure_deployment" {
  source = "./infrastructure/terraform/azure"

  # Enable availability tests
  enable_availability_tests = true
  availability_test_frequency = 300  # 5 minutes
  availability_test_timeout = 30     # 30 seconds

  app_url = "https://honua.example.com"
  admin_email = "alerts@example.com"
}
```

**View Results**:
- Azure Portal: Navigate to Application Insights → Availability
- URL: `https://portal.azure.com/#@/resource/{app-insights-id}/availability`

### AWS CloudWatch Synthetics

**Configuration File**: `/infrastructure/terraform/aws/synthetics-canaries.tf`
**Canary Scripts**: `/infrastructure/terraform/aws/canary-scripts/`

**Features**:
- Node.js Puppeteer-based canaries
- Custom validation logic
- Results stored in S3
- CloudWatch metrics and alarms
- X-Ray tracing integration

**Canary Scripts**:
- `liveness-canary.js`: Validates /healthz/live
- `readiness-canary.js`: Validates /healthz/ready
- `ogc-api-canary.js`: Validates OGC API structure
- `ogc-conformance-canary.js`: Validates conformance classes

**Alert Configuration**:
```hcl
# Critical: Success rate < 90% over 2 evaluation periods
# Warning: Average duration > 2000ms
```

**Terraform Usage**:
```hcl
module "aws_deployment" {
  source = "./infrastructure/terraform/aws"

  # Enable synthetics
  enable_synthetics = true
  canary_schedule_expression = "rate(5 minutes)"
  canary_timeout_seconds = 60

  app_url = "https://honua.example.com"
  alert_email = "alerts@example.com"
}
```

**View Results**:
- AWS Console: CloudWatch → Synthetics → Canaries
- URL: `https://console.aws.amazon.com/cloudwatch/home#synthetics:canary/list`

**Package Canaries**:
```bash
cd infrastructure/terraform/aws/canary-scripts
./package.sh
# Uploads to S3 or deploys via Terraform
```

### GCP Cloud Monitoring Uptime Checks

**Configuration File**: `/infrastructure/terraform/gcp/uptime-checks.tf`

**Features**:
- HTTP/HTTPS uptime checks
- Content matching with regex support
- Multi-region testing from 4 GCP regions
- Alert policies with auto-close
- Integration with Notification Channels

**Test Regions**:
- USA
- EUROPE
- SOUTH_AMERICA
- ASIA_PACIFIC

**Alert Configuration**:
```hcl
# Critical: Check failed for 5 minutes
# Warning: Average latency > 2000ms over 10 minutes
```

**Terraform Usage**:
```hcl
module "gcp_deployment" {
  source = "./infrastructure/terraform/gcp"

  # Enable uptime checks
  enable_uptime_checks = true
  uptime_check_period = "300s"
  uptime_check_timeout = "10s"

  app_url = "https://honua.example.com"
  gcp_project_id = "my-gcp-project"
}
```

**View Results**:
- GCP Console: Monitoring → Uptime checks
- URL: `https://console.cloud.google.com/monitoring/uptime?project={project-id}`

## Alert Configuration

### Alert Severity Levels

| Severity | Description | Example | Response Time |
|----------|-------------|---------|---------------|
| **Critical** | Application completely unavailable | Liveness check failed from multiple regions | Immediate (< 5 min) |
| **Error** | Core functionality impaired | Readiness check failed, OGC API down | < 30 minutes |
| **Warning** | Degraded performance or non-critical issues | High latency, conformance endpoint slow | < 2 hours |
| **Info** | Informational only | Certificate expiring in 30 days | Best effort |

### Alert Thresholds

#### Availability Thresholds

```yaml
Liveness Endpoint:
  Critical: Failed from 2+ regions for 5+ minutes
  Success Rate: < 90%

Readiness Endpoint:
  Error: Failed from 2+ regions for 5+ minutes
  Success Rate: < 90%

OGC API Endpoints:
  Warning: Failed from 3+ regions for 10+ minutes
  Success Rate: < 80%
```

#### Performance Thresholds

```yaml
Response Time:
  Warning: Average > 2000ms over 10 minutes
  Error: Average > 5000ms over 5 minutes
  Critical: Average > 10000ms over 5 minutes

SSL Certificate:
  Warning: Expires in < 30 days
  Error: Expires in < 7 days
  Critical: Expired or invalid
```

### Notification Channels

#### Email Notifications

**Azure**:
```hcl
email_receiver {
  name          = "admin-email"
  email_address = "alerts@example.com"
  use_common_alert_schema = true
}
```

**AWS**:
```hcl
resource "aws_sns_topic_subscription" "alerts_email" {
  topic_arn = aws_sns_topic.alerts.arn
  protocol  = "email"
  endpoint  = "alerts@example.com"
}
```

**GCP**:
```hcl
resource "google_monitoring_notification_channel" "email" {
  display_name = "Email Alerts"
  type         = "email"
  labels = {
    email_address = "alerts@example.com"
  }
}
```

#### Slack Notifications

**Azure**:
```hcl
webhook_receiver {
  name        = "slack-webhook"
  service_uri = var.slack_webhook_url
  use_common_alert_schema = true
}
```

**AWS**:
```bash
# Use AWS Chatbot for Slack integration
aws chatbot create-slack-channel-configuration \
  --configuration-name honua-alerts \
  --slack-channel-id C0123456789 \
  --slack-workspace-id T0123456789 \
  --sns-topic-arns arn:aws:sns:us-east-1:123456789012:honua-alerts
```

**GCP**:
```hcl
resource "google_monitoring_notification_channel" "slack" {
  display_name = "Slack Alerts"
  type         = "slack"
  labels = {
    channel_name = "#honua-alerts"
  }
  sensitive_labels {
    auth_token = var.slack_token
  }
}
```

#### PagerDuty Integration

**Azure**:
```hcl
webhook_receiver {
  name        = "pagerduty"
  service_uri = "https://events.pagerduty.com/integration/${var.pagerduty_key}/enqueue"
}
```

**AWS**:
```bash
# Use EventBridge to PagerDuty
aws events put-rule --name honua-critical-alerts \
  --event-pattern '{"source": ["aws.cloudwatch"]}'

aws events put-targets --rule honua-critical-alerts \
  --targets "Id"="1","Arn"="${PAGERDUTY_ENDPOINT}"
```

**GCP**:
```hcl
resource "google_monitoring_notification_channel" "pagerduty" {
  display_name = "PagerDuty"
  type         = "pagerduty"
  sensitive_labels {
    service_key = var.pagerduty_service_key
  }
}
```

## Monitoring Coverage Summary

| Endpoint | Azure | AWS | GCP | Multi-Region | SSL Check | Content Validation |
|----------|-------|-----|-----|--------------|-----------|-------------------|
| /healthz/live | ✅ | ✅ | ✅ | ✅ (5+ regions) | ✅ | ✅ (Healthy) |
| /healthz/ready | ✅ | ✅ | ✅ | ✅ (5+ regions) | ✅ | ✅ (Healthy) |
| /ogc | ✅ | ✅ | ✅ | ✅ (5+ regions) | ✅ | ✅ (links) |
| /ogc/conformance | ✅ | ✅ | ✅ | ✅ (5+ regions) | ✅ | ✅ (conformsTo) |
| /stac | ✅ | ⚠️ | ✅ | ✅ (4+ regions) | ✅ | ✅ (type) |

✅ = Fully implemented
⚠️ = Partially implemented
❌ = Not implemented

## Best Practices

### 1. Multi-Region Coverage

Always test from at least 3 geographic regions to:
- Detect region-specific outages
- Identify CDN/routing issues
- Measure global performance

### 2. Content Validation

Never rely solely on HTTP status codes:
- Validate response body content
- Check JSON structure for API endpoints
- Verify critical fields are present

### 3. Alert Fatigue Prevention

Configure alerts to avoid false positives:
- Require failures from multiple regions
- Use evaluation periods (not single data points)
- Set appropriate thresholds for your SLA

### 4. SSL Monitoring

Monitor certificate expiration:
- Warning at 30 days
- Error at 7 days
- Automate renewal before expiration

### 5. Performance Baselines

Establish and monitor against baselines:
- P50, P95, P99 latency
- Regional performance variations
- Time-of-day patterns

## Troubleshooting

### Tests Failing After Deployment

1. **Verify endpoint accessibility**:
   ```bash
   curl -v https://honua.example.com/healthz/live
   ```

2. **Check SSL certificate**:
   ```bash
   openssl s_client -connect honua.example.com:443 -servername honua.example.com
   ```

3. **Review firewall rules**: Ensure monitoring service IPs are allowed

4. **Check DNS resolution**:
   ```bash
   dig honua.example.com
   nslookup honua.example.com
   ```

### False Positive Alerts

1. **Increase failure threshold**: Require more regions to fail
2. **Extend evaluation period**: Use longer time windows
3. **Review content matchers**: Ensure they're not too strict
4. **Check for scheduled maintenance**: Disable tests during known maintenance

### Missing Alerts

1. **Verify notification channels**:
   - Test email delivery
   - Validate webhook URLs
   - Check API keys/tokens

2. **Review alert policies**:
   - Confirm conditions are correctly configured
   - Check evaluation logic
   - Verify action groups are attached

3. **Check alert suppression**:
   - Review maintenance windows
   - Check alert rules status (enabled/disabled)

## Maintenance

### Adding New Endpoints

1. Add endpoint to monitoring configuration:
   ```hcl
   # In availability-tests.tf or equivalent
   monitored_endpoints = {
     new_endpoint = {
       path            = "/api/new"
       description     = "New API endpoint"
       expected_status = 200
       content_match   = "expected content"
       severity        = "warning"
     }
   }
   ```

2. Create monitoring test for the endpoint

3. Configure alerts

4. Update documentation

### Updating Alert Thresholds

1. Review historical performance data
2. Calculate new baseline
3. Update Terraform variables:
   ```hcl
   latency_threshold_ms = 2000
   success_rate_threshold = 90
   ```
4. Apply changes: `terraform apply`
5. Monitor for false positives

### Testing Alerts

#### Azure
```bash
# Trigger test alert
az monitor metrics alert create \
  --name test-alert \
  --resource-group honua-rg \
  --condition "..."
```

#### AWS
```bash
# Set canary to ALARM state
aws cloudwatch set-alarm-state \
  --alarm-name honua-liveness-failed \
  --state-value ALARM \
  --state-reason "Testing alert notification"
```

#### GCP
```bash
# Create test incident
gcloud alpha monitoring policies test \
  --policy-id=honua-liveness-failed \
  --project=my-gcp-project
```

## Cost Optimization

### Azure Monitor
- Standard web tests: ~$1.00 per test per month
- 5 tests × 5 regions = ~$25/month

### AWS CloudWatch Synthetics
- Canary runs: $0.0012 per run
- 4 canaries × 12 runs/hour × 730 hours = ~$42/month
- S3 storage: Minimal (~$1/month)

### GCP Cloud Monitoring
- Uptime checks: First 1M checks free, then $0.30 per 1K checks
- 5 checks × 12/hour × 730 hours = ~43,800 checks = Free

**Total Estimated Cost**: $50-70/month for comprehensive multi-cloud monitoring

## Related Documentation

- [Observability Overview](./README.md)
- [Performance Baselines](./performance-baselines.md)
- [Alert Runbooks](../operations/RUNBOOKS.md)
- [Azure Availability Tests Configuration](../../infrastructure/terraform/azure/availability-tests.tf)
- [AWS Synthetics Configuration](../../infrastructure/terraform/aws/synthetics-canaries.tf)
- [GCP Uptime Checks Configuration](../../infrastructure/terraform/gcp/uptime-checks.tf)
