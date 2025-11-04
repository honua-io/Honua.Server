# External Uptime Monitoring for Honua

This directory contains Terraform configurations for external uptime monitoring across Azure, AWS, and GCP cloud providers.

## Overview

External uptime monitoring provides independent validation of application availability and performance from multiple geographic locations worldwide. These synthetic tests run outside the application infrastructure to detect outages before users are impacted.

## Quick Links

- **Setup Guide**: [EXTERNAL_MONITORING_SETUP.md](./EXTERNAL_MONITORING_SETUP.md)
- **Full Documentation**: [/docs/observability/external-monitoring.md](../../docs/observability/external-monitoring.md)
- **Coverage Summary**: [/infrastructure/monitoring/MONITORING_COVERAGE.md](../monitoring/MONITORING_COVERAGE.md)

## Implementation Status

### ✅ Completed

- [x] Azure Monitor Availability Tests
- [x] AWS CloudWatch Synthetics Canaries
- [x] GCP Cloud Monitoring Uptime Checks
- [x] Multi-region probing (13+ global locations)
- [x] SSL certificate monitoring
- [x] Content validation for all endpoints
- [x] Alert configuration (email, Slack, PagerDuty)
- [x] Cloud-agnostic monitoring module
- [x] Comprehensive documentation

### 📋 Monitored Endpoints

| Endpoint | Path | Severity | Regions |
|----------|------|----------|---------|
| Liveness | `/healthz/live` | Critical | 13+ |
| Readiness | `/healthz/ready` | Error | 13+ |
| OGC API | `/ogc` | Error | 13+ |
| OGC Conformance | `/ogc/conformance` | Warning | 13+ |
| STAC Catalog | `/stac` | Warning | 9+ |

## Directory Structure

```
infrastructure/terraform/
├── azure/
│   ├── availability-tests.tf          # Azure Monitor configuration
│   └── app-insights-alerts.tf         # Existing alert rules
├── aws/
│   ├── synthetics-canaries.tf         # CloudWatch Synthetics config
│   └── canary-scripts/                # Canary JavaScript code
│       ├── liveness-canary.js
│       ├── readiness-canary.js
│       ├── ogc-api-canary.js
│       ├── ogc-conformance-canary.js
│       └── package.sh                 # Script to package canaries
├── gcp/
│   └── uptime-checks.tf               # Cloud Monitoring config
├── multi-region/
│   └── modules/
│       └── external-monitoring/       # Cloud-agnostic module
│           ├── main.tf
│           └── README.md
├── EXTERNAL_MONITORING_SETUP.md       # Quick start guide (this file)
└── MONITORING_README.md               # Overview (you are here)
```

## Cloud Provider Features

### Azure Monitor Availability Tests

**File**: `azure/availability-tests.tf`

**Features**:
- Standard web tests with custom validation
- 5 global test locations
- Application Insights integration
- SSL certificate expiration alerts
- Smart Detection for anomaly detection

**Cost**: ~$25/month

### AWS CloudWatch Synthetics

**Files**:
- `aws/synthetics-canaries.tf`
- `aws/canary-scripts/*.js`

**Features**:
- Node.js Puppeteer-based canaries
- Custom validation logic
- Results stored in S3
- CloudWatch alarms integration
- X-Ray tracing enabled

**Cost**: ~$42/month

### GCP Cloud Monitoring

**File**: `gcp/uptime-checks.tf`

**Features**:
- HTTP/HTTPS uptime checks
- Content matching with regex
- 4 global regions
- Alert policies with documentation
- Notification channels integration

**Cost**: Free (under 1M checks/month)

## Quick Start

### 1. Choose Your Cloud Provider

```bash
# Azure
cd infrastructure/terraform/azure

# AWS
cd infrastructure/terraform/aws

# GCP
cd infrastructure/terraform/gcp
```

### 2. Configure Variables

```bash
# Create terraform.tfvars
cat > terraform.tfvars <<EOF
environment = "production"
app_url = "https://honua.example.com"
alert_email = "alerts@example.com"

# Provider-specific settings
enable_availability_tests = true  # Azure
enable_synthetics = true          # AWS
enable_uptime_checks = true       # GCP
EOF
```

### 3. Deploy

```bash
terraform init
terraform plan
terraform apply
```

See [EXTERNAL_MONITORING_SETUP.md](./EXTERNAL_MONITORING_SETUP.md) for detailed instructions.

## Multi-Cloud Deployment

Use the unified monitoring module for deployments across multiple cloud providers:

```hcl
module "monitoring" {
  source = "./modules/external-monitoring"

  cloud_provider = "azure"  # or "aws" or "gcp"
  environment    = "production"
  app_url        = var.app_url
  alert_email    = var.alert_email

  # Cloud-specific configuration
  azure_resource_group_name = azurerm_resource_group.main.name
  azure_app_insights_id     = azurerm_application_insights.main.id
}
```

## Monitoring Coverage

### Geographic Distribution

**13+ Global Test Locations**:
- 🇺🇸 North America: 3 locations
- 🇪🇺 Europe: 3 locations
- 🇸🇬 Asia Pacific: 4 locations
- 🇧🇷 South America: 1 location
- 🇦🇺 Australia: 1 location

### Test Frequency

- **Interval**: Every 5 minutes
- **Timeout**: 10-60 seconds (provider-dependent)
- **Retry**: Enabled
- **SSL Validation**: Enabled
- **Content Validation**: Enabled

### Alert Thresholds

| Metric | Warning | Error | Critical |
|--------|---------|-------|----------|
| Uptime | N/A | <95% | <90% |
| Response Time | >2000ms | >5000ms | >10000ms |
| Failed Regions | N/A | 2+ | 3+ |
| SSL Cert Expiry | <30 days | <7 days | Expired |

## Alert Notification Channels

### Configured Channels

- ✅ **Email**: Immediate notifications
- ✅ **Slack**: Real-time team alerts
- ✅ **PagerDuty**: On-call escalation
- ⚠️ **SMS**: Critical alerts only (optional)
- ⚠️ **Teams**: Alternative to Slack (optional)

### Channel Configuration

See [EXTERNAL_MONITORING_SETUP.md](./EXTERNAL_MONITORING_SETUP.md#alert-configuration) for setup instructions.

## Validation

### Test Endpoints Manually

```bash
# Test all monitored endpoints
for endpoint in /healthz/live /healthz/ready /ogc /ogc/conformance /stac; do
  echo "Testing ${endpoint}..."
  curl -v "https://honua.example.com${endpoint}"
done
```

### Trigger Test Alerts

```bash
# Azure
az monitor metrics alert update --name honua-liveness-failed --enabled true

# AWS
aws cloudwatch set-alarm-state --alarm-name honua-liveness-failed \
  --state-value ALARM --state-reason "Testing"

# GCP
gcloud alpha monitoring policies test --policy-id honua-liveness-failed
```

## Dashboards

### View Monitoring Results

**Azure**:
```
https://portal.azure.com → Application Insights → Availability
```

**AWS**:
```
https://console.aws.amazon.com/cloudwatch → Synthetics → Canaries
```

**GCP**:
```
https://console.cloud.google.com/monitoring/uptime
```

## Troubleshooting

### Common Issues

1. **Tests not running**: Check Terraform outputs and provider-specific logs
2. **False positives**: Adjust failure thresholds or content matchers
3. **Missing alerts**: Verify notification channels are configured correctly
4. **High latency**: Review CDN configuration and database performance

See full troubleshooting guide in [docs/observability/external-monitoring.md](../../docs/observability/external-monitoring.md#troubleshooting).

## Cost Optimization

### Estimated Monthly Costs

| Provider | Tests | Cost |
|----------|-------|------|
| Azure Monitor | 5 tests × 5 regions | ~$25 |
| AWS Synthetics | 4 canaries | ~$42 |
| GCP Monitoring | 5 checks | Free |
| **Total** | | **~$67** |

### Cost Reduction Options

1. Reduce test frequency (5 min → 15 min): Save ~60%
2. Reduce test locations (5 → 3 regions): Save ~40%
3. Use GCP only for non-critical environments: Save 100% on dev/staging

## Security Considerations

- ✅ SSL/TLS validation enabled
- ✅ Certificate expiration monitoring
- ✅ Secure storage of canary scripts (S3 encrypted)
- ✅ IAM roles with minimum required permissions
- ✅ Secrets stored in Key Vault/Secrets Manager
- ⚠️ Consider IP whitelisting for monitoring sources

## Compliance

This monitoring setup supports compliance with:

- ✅ SOC 2 Type II (continuous monitoring)
- ✅ ISO 27001 (security monitoring)
- ✅ GDPR (availability requirements)
- ✅ HIPAA (system availability)

## Maintenance

### Regular Tasks

- **Weekly**: Review alert trends and adjust thresholds
- **Monthly**: Validate monitoring coverage for new endpoints
- **Quarterly**: Review geographic distribution and add new regions
- **Annually**: Audit alert runbooks and update procedures

### Update Procedures

1. Modify Terraform configuration
2. Run `terraform plan` to preview changes
3. Apply changes: `terraform apply`
4. Verify monitoring is functioning
5. Update documentation

## Support

### Documentation

- [External Monitoring Guide](../../docs/observability/external-monitoring.md)
- [Quick Setup Guide](./EXTERNAL_MONITORING_SETUP.md)
- [Coverage Summary](../monitoring/MONITORING_COVERAGE.md)
- [Alert Runbooks](../../docs/operations/RUNBOOKS.md)

### Getting Help

- **Issues**: Create GitHub issue with `monitoring` label
- **Questions**: Post in #devops Slack channel
- **Emergency**: Follow incident response runbook
- **Feature Requests**: Discuss in team meetings

## Contributing

When adding new monitored endpoints:

1. Update Terraform configuration for all providers
2. Add content validation rules
3. Configure appropriate alerts
4. Update documentation
5. Test from multiple regions
6. Create PR with monitoring changes

## License

See project root LICENSE file.

---

**Last Updated**: 2025-10-18
**Maintained By**: DevOps Team
**Version**: 1.0.0
