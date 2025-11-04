# Tests for CDN Module

This directory contains automated tests for the Honua cloud-agnostic CDN Terraform module.

## Test Structure

```
tests/
├── unit/           # Unit tests with minimal configuration
├── integration/    # Integration tests with full configuration
└── README.md       # This file
```

## Module Overview

The CDN module is cloud-agnostic and supports:
- **AWS CloudFront** - Full implementation with cache behaviors
- **Google Cloud CDN** - Configuration output for Cloud Load Balancer
- **Azure Front Door** - Standalone or integrated configuration

## Running Tests

### Prerequisites

- Terraform >= 1.5.0
- Cloud provider credentials (optional for validation-only testing)
- Provider CLI tools (optional)

### Unit Tests

Unit tests validate Terraform syntax with minimal AWS CloudFront configuration.

```bash
cd tests/unit
terraform init -backend=false
terraform validate
terraform plan
```

**What Unit Tests Check:**
- Terraform syntax is valid
- Variables have proper types and validation
- Outputs are correctly defined
- Required providers are specified
- Module compiles without errors
- Basic CloudFront configuration works

### Integration Tests

Integration tests validate full configurations for multiple cloud providers.

```bash
cd tests/integration
terraform init -backend=false
terraform validate
terraform plan
```

**What Integration Tests Check:**
- Full AWS CloudFront configuration with Origin Shield
- GCP Cloud CDN configuration output
- Cache policies for GIS workloads (tiles, features, metadata)
- Multi-provider compatibility
- All outputs generated correctly
- Performance recommendations

### Testing with Real Resources (Optional)

To test with actual AWS CloudFront (will incur costs):

```bash
cd tests/integration

# Ensure AWS credentials are configured
aws sts get-caller-identity

# Set skip validation to false
export TF_VAR_skip_aws_validation=false

# Set origin domain
export TF_VAR_origin_domain="your-origin.example.com"

# Optional: Set logging bucket (must end with .s3.amazonaws.com)
export TF_VAR_cloudfront_logging_bucket="my-logs-bucket.s3.amazonaws.com"

# Initialize with a backend
terraform init

# Plan the deployment
terraform plan -out=tfplan

# Review the plan carefully!
terraform show tfplan

# Apply (WARNING: Creates real resources and incurs costs)
terraform apply tfplan

# Test the CDN
curl -I https://$(terraform output -raw cloudfront_domain_name)/health

# Don't forget to destroy afterwards
terraform destroy
```

## Test Coverage

- [x] Terraform syntax validation
- [x] Variable type checking and validation rules
- [x] Output generation
- [x] Required provider configuration
- [x] AWS CloudFront distribution
- [x] Origin configuration (custom and S3)
- [x] Cache behaviors for GIS content
  - [x] Tiles (/tiles/*, /wms*, /wmts/*)
  - [x] Features (/features/*)
  - [x] Metadata (/collections*, /api*)
- [x] Query string caching for GIS parameters
- [x] Origin Shield configuration
- [x] SSL/TLS configuration
- [x] Geo-restriction configuration
- [x] Custom domain support
- [x] Logging configuration
- [x] WAF integration
- [x] GCP Cloud CDN configuration output
- [x] Azure Front Door configuration
- [x] Performance recommendations
- [x] Cache invalidation commands

## CI/CD Integration

Tests run automatically on:
- Pull requests modifying Terraform code in this module
- Commits to `main` or `dev` branches

See `.github/workflows/terraform-test.yml` for CI/CD configuration.

## Test Configuration

### Unit Test Configuration

The unit test uses minimal settings:
- AWS CloudFront only
- No custom domains
- No SSL certificate
- No logging
- No WAF
- Basic cache policies
- PriceClass_100 (US, Canada, Europe)

### Integration Test Configuration

The integration test uses production-like settings:
- **AWS CloudFront:**
  - Origin Shield enabled
  - Global distribution (PriceClass_All)
  - GIS-optimized cache behaviors
  - Comprehensive header forwarding
  - All cache policies configured

- **GCP Cloud CDN:**
  - Configuration output validation
  - Backend service integration settings

## Cache Policy Testing

The module implements three cache policies optimized for GIS workloads:

### Tiles Cache Policy
- **TTL:** 1 hour (default), up to 24 hours
- **Paths:** `/tiles/*`, `/wms*`, `/wmts/*`
- **Parameters:** bbox, width, height, layers, srs, crs, format, time
- **Rationale:** Tiles are static and cacheable

### Features Cache Policy
- **TTL:** 5 minutes (default), up to 1 hour
- **Paths:** `/features/*`, `/items/*`
- **Parameters:** bbox, limit, offset, filter, crs
- **Rationale:** Features are more dynamic

### Metadata Cache Policy
- **TTL:** 24 hours (default), up to 1 week
- **Paths:** `/collections*`, `/api*`, `/conformance*`
- **Rationale:** Metadata rarely changes

## Validation Outputs

Integration tests validate:

```hcl
integration_test_results = {
  cloudfront_created        = true
  cdn_url_generated         = true
  cdn_domain_generated      = true
  cache_policies_configured = true
  tiles_ttl_correct         = true   # 3600 seconds
  features_ttl_correct      = true   # 300 seconds
  metadata_ttl_correct      = true   # 86400 seconds
  origin_shield_enabled     = true
}
```

## Security Testing

Run security scans with `tfsec`:

```bash
# Install tfsec
brew install tfsec

# Scan the module
tfsec ../..

# Scan with specific checks
tfsec ../.. --minimum-severity HIGH
```

## Cost Estimation

Estimate costs with Infracost:

```bash
# Install infracost
brew install infracost

# Generate cost estimate
cd tests/integration
infracost breakdown --path .
```

### CloudFront Pricing (Approximate)

- **Data Transfer Out:** $0.085/GB (first 10 TB/month in US)
- **HTTPS Requests:** $0.010 per 10,000 requests
- **Origin Shield:** $0.01 per 10,000 requests
- **Invalidations:** First 1,000 paths free per month
- **SSL Certificate:** Free with ACM

Example monthly cost for moderate traffic:
- 1 TB data transfer: ~$85
- 10M requests: ~$10
- Origin Shield: ~$10
- **Total:** ~$105/month

## Troubleshooting

### Backend Errors

```bash
terraform init -backend=false
```

### AWS Authentication

```bash
aws configure
# Or for validation-only testing:
export TF_VAR_skip_aws_validation=true
```

### Custom Domain Issues

Custom domains require:
1. ACM certificate in `us-east-1` (CloudFront requirement)
2. DNS validation for the certificate
3. CNAME record pointing to CloudFront distribution

Example:
```bash
# Request certificate
aws acm request-certificate \
  --domain-name cdn.honua.io \
  --validation-method DNS \
  --region us-east-1

# Get validation DNS records
aws acm describe-certificate \
  --certificate-arn <cert-arn> \
  --region us-east-1

# After DNS validation, update module
export TF_VAR_custom_domains='["cdn.honua.io"]'
export TF_VAR_ssl_certificate_arn="arn:aws:acm:us-east-1:..."
```

### S3 Origin Configuration

For S3 origins, use:
```hcl
origin_type = "s3"
origin_domain = "my-bucket.s3.amazonaws.com"
```

The module automatically creates an Origin Access Identity.

## Cache Invalidation

After deploying changes, you may need to invalidate the cache:

```bash
# Get distribution ID from outputs
DISTRIBUTION_ID=$(terraform output -raw cloudfront_distribution_id)

# Invalidate all paths
aws cloudfront create-invalidation \
  --distribution-id $DISTRIBUTION_ID \
  --paths "/*"

# Invalidate specific paths
aws cloudfront create-invalidation \
  --distribution-id $DISTRIBUTION_ID \
  --paths "/tiles/*" "/api/*"

# Check invalidation status
aws cloudfront get-invalidation \
  --distribution-id $DISTRIBUTION_ID \
  --id <invalidation-id>
```

**Note:** First 1,000 invalidation paths are free per month.

## Performance Testing

Test cache performance:

```bash
# Get CDN domain
CDN_DOMAIN=$(terraform output -raw cdn_domain)

# Test cache miss (first request)
curl -I "https://$CDN_DOMAIN/tiles/test.png"
# Look for: X-Cache: Miss from cloudfront

# Test cache hit (second request)
curl -I "https://$CDN_DOMAIN/tiles/test.png"
# Look for: X-Cache: Hit from cloudfront

# Test different cache behaviors
curl -I "https://$CDN_DOMAIN/wms?bbox=..." # Tiles policy
curl -I "https://$CDN_DOMAIN/features/123" # Features policy
curl -I "https://$CDN_DOMAIN/collections"  # Metadata policy
```

## Monitoring

Monitor CloudFront metrics:

```bash
# Cache hit rate
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name CacheHitRate \
  --dimensions Name=DistributionId,Value=$DISTRIBUTION_ID \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 3600 \
  --statistics Average

# Requests
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name Requests \
  --dimensions Name=DistributionId,Value=$DISTRIBUTION_ID \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 3600 \
  --statistics Sum
```

Target metrics:
- **Cache Hit Rate:** >70% for tiles
- **Origin Response Time:** <100ms
- **Total Response Time:** <500ms

## Cleanup

```bash
cd tests/integration

# Destroy CloudFront distribution
terraform destroy -auto-approve

# Note: CloudFront distributions take 15-20 minutes to fully delete
```

## Best Practices

1. **Use Origin Shield** for improved cache hit ratio
2. **Enable compression** at origin (CloudFront compresses automatically)
3. **Set proper Cache-Control headers** at origin
4. **Monitor cache hit ratio** - target >70%
5. **Use query string caching** for GIS parameters
6. **Test invalidations** in dev before production
7. **Review costs regularly** - data transfer can be significant
8. **Use appropriate price class** for your user base

## Known Limitations

- CloudFront custom domains require certificates in `us-east-1`
- Invalidations can take 10-15 minutes to complete
- Origin Shield adds cost but improves cache hit ratio
- GCP Cloud CDN requires configuration in Cloud Load Balancer module
- Azure Front Door requires separate resource group configuration

## Support

For issues or questions about testing:
- Check existing GitHub issues
- Review Terraform documentation: https://developer.hashicorp.com/terraform
- Review AWS CloudFront documentation: https://docs.aws.amazon.com/cloudfront/
- Review GCP Cloud CDN documentation: https://cloud.google.com/cdn/docs
- Review Azure Front Door documentation: https://learn.microsoft.com/azure/frontdoor/
