# Cloud-Agnostic CDN Module

Terraform module for deploying Content Delivery Network (CDN) for Honua GIS Platform across AWS CloudFront, Google Cloud CDN, and Azure Front Door.

## Overview

This module provides CDN capabilities optimized for GIS workloads with intelligent caching policies for:
- **Raster/Vector Tiles**: 1 hour default cache
- **Feature Data**: 5 minutes default cache
- **Metadata**: 24 hours default cache

Supports all three major cloud providers with provider-specific optimizations.

## Features

- **Multi-Cloud Support**: AWS CloudFront, Google Cloud CDN, Azure Front Door
- **GIS-Optimized**: Cache policies for tiles, features, and metadata
- **Global Performance**: Edge locations worldwide
- **SSL/TLS**: Automatic HTTPS with custom domains
- **Compression**: Automatic gzip/brotli compression
- **Security**: WAF integration, geo-blocking support

## Usage

### AWS CloudFront

```hcl
provider "aws" {
  region = "us-east-1"  # CloudFront requires us-east-1 for certificates
}

module "cdn" {
  source = "../../modules/cdn"

  cloud_provider = "aws"
  environment    = "production"
  origin_domain  = "honua-prod-alb-123456.us-east-1.elb.amazonaws.com"

  # SSL certificate
  custom_domains      = ["api.honua.io"]
  ssl_certificate_arn = "arn:aws:acm:us-east-1:123456789012:certificate/..."

  # Cache TTLs
  tiles_ttl    = 3600   # 1 hour
  features_ttl = 300    # 5 minutes
  metadata_ttl = 86400  # 24 hours

  # Logging
  cloudfront_logging_bucket = "my-cloudfront-logs.s3.amazonaws.com"

  # WAF (optional)
  waf_web_acl_arn = "arn:aws:wafv2:us-east-1:123456789012:global/webacl/..."
}
```

### Google Cloud CDN

```hcl
# Note: GCP Cloud CDN is configured in the Load Balancer, not as standalone resource
# This module provides configuration values to apply to your backend service

module "cdn_config" {
  source = "../../modules/cdn"

  cloud_provider = "gcp"
  environment    = "production"
  origin_domain  = "cloud-run-service.run.app"  # Not used, for documentation

  tiles_ttl    = 3600
  features_ttl = 300
  metadata_ttl = 86400
}

# Apply the configuration to your Cloud Run backend service:
resource "google_compute_backend_service" "honua" {
  # ... other config ...

  enable_cdn = module.cdn_config.gcp_cdn_configuration.enable_cdn

  cdn_policy {
    cache_mode                   = module.cdn_config.gcp_cdn_configuration.cdn_policy.cache_mode
    default_ttl                  = module.cdn_config.gcp_cdn_configuration.cdn_policy.default_ttl
    max_ttl                      = module.cdn_config.gcp_cdn_configuration.cdn_policy.max_ttl
    client_ttl                   = module.cdn_config.gcp_cdn_configuration.cdn_policy.client_ttl
    negative_caching             = module.cdn_config.gcp_cdn_configuration.cdn_policy.negative_caching
    serve_while_stale            = module.cdn_config.gcp_cdn_configuration.cdn_policy.serve_while_stale

    cache_key_policy {
      include_host           = module.cdn_config.gcp_cdn_configuration.cdn_policy.cache_key_policy.include_host
      include_protocol       = module.cdn_config.gcp_cdn_configuration.cdn_policy.cache_key_policy.include_protocol
      include_query_string   = module.cdn_config.gcp_cdn_configuration.cdn_policy.cache_key_policy.include_query_string
      query_string_whitelist = module.cdn_config.gcp_cdn_configuration.cdn_policy.cache_key_policy.query_string_whitelist
    }
  }
}
```

### Azure Front Door

```hcl
provider "azurerm" {
  features {}
}

module "cdn" {
  source = "../../modules/cdn"

  cloud_provider = "azure"
  environment    = "production"
  origin_domain  = "honua-prod.azurecontainerapps.io"

  # Create standalone Front Door (if not created in container-apps module)
  create_azure_front_door   = true
  azure_resource_group_name = "honua-prod-rg"
  azure_front_door_sku      = "Standard_AzureFrontDoor"

  # Custom domains
  custom_domains = ["api.honua.io"]

  # Cache TTLs
  tiles_ttl    = 3600
  features_ttl = 300
  metadata_ttl = 86400
}
```

## Cache Policies

### Tiles (WMS, WMTS, XYZ)
- **Paths**: `/tiles/*`, `/wms*`, `/wmts/*`
- **Default TTL**: 1 hour (3600s)
- **Max TTL**: 24 hours (86400s)
- **Query String Keys**: bbox, width, height, layers, srs, crs, format, time

### Features (OGC API Features)
- **Paths**: `/features/*`, `/items/*`
- **Default TTL**: 5 minutes (300s)
- **Max TTL**: 1 hour (3600s)
- **Query String Keys**: bbox, limit, offset, filter, crs

### Metadata (Capabilities, Collections)
- **Paths**: `/collections*`, `/api*`, `/conformance*`
- **Default TTL**: 24 hours (86400s)
- **Max TTL**: 1 week (604800s)
- **Query String**: All parameters included

## Invalidation

### AWS CloudFront
```bash
aws cloudfront create-invalidation \
  --distribution-id E1234567890ABC \
  --paths "/*"
```

### Google Cloud CDN
```bash
gcloud compute url-maps invalidate-cdn-cache honua-urlmap \
  --path "/*"
```

### Azure Front Door
```bash
az afd endpoint purge \
  --resource-group honua-prod-rg \
  --profile-name honua-production-cdn \
  --endpoint-name honua-production \
  --content-paths "/*"
```

## Cost Optimization

1. **Set appropriate TTLs**: Longer cache = lower origin load
2. **Use query string whitelisting**: Cache only relevant parameters
3. **Enable compression**: Reduce bandwidth costs
4. **Monitor cache hit ratio**: Target >70% for tiles
5. **Use Origin Shield** (AWS): Reduce origin requests by 30-50%

## Monitoring

### Key Metrics
- **Cache Hit Ratio**: Should be >70% for tiles
- **Origin Latency**: Monitor backend response times
- **4xx/5xx Errors**: Watch for client/server errors
- **Bandwidth**: Track data transfer costs

### CloudWatch (AWS)
```bash
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name CacheHitRate \
  --dimensions Name=DistributionId,Value=E1234567890ABC \
  --start-time 2024-01-01T00:00:00Z \
  --end-time 2024-01-01T23:59:59Z \
  --period 3600 \
  --statistics Average
```

## Best Practices

1. **Set Cache-Control headers** at origin:
   ```csharp
   // In ASP.NET Core
   Response.Headers["Cache-Control"] = "public, max-age=3600";
   ```

2. **Use versioned URLs** for static assets:
   ```
   /tiles/v2/layer1/{z}/{x}/{y}.png
   ```

3. **Implement stale-while-revalidate**:
   ```
   Cache-Control: max-age=3600, stale-while-revalidate=86400
   ```

4. **Monitor and optimize**:
   - Review cache analytics weekly
   - Adjust TTLs based on usage patterns
   - Identify uncacheable endpoints

## Troubleshooting

### Low Cache Hit Ratio

```
Problem: Cache hit ratio < 50%
Solutions:
  - Check if Cache-Control headers are set at origin
  - Verify query string parameters are normalized
  - Review cache policies for path patterns
  - Check if too many unique URLs (e.g., random parameters)
```

### High Origin Load

```
Problem: Origin receiving too many requests
Solutions:
  - Increase cache TTLs
  - Enable Origin Shield (AWS CloudFront)
  - Implement stale-while-revalidate
  - Pre-warm cache for popular tiles
```

## Variables

| Name | Description | Default |
|------|-------------|---------|
| `cloud_provider` | aws, gcp, or azure | required |
| `origin_domain` | Origin domain name | required |
| `tiles_ttl` | Tile cache TTL (seconds) | `3600` |
| `features_ttl` | Feature cache TTL (seconds) | `300` |
| `metadata_ttl` | Metadata cache TTL (seconds) | `86400` |
| `custom_domains` | Custom domains | `[]` |
| `ssl_certificate_arn` | SSL certificate ARN (AWS) | `""` |

## Outputs

- `cdn_url` - CDN URL (cloud-agnostic)
- `cdn_domain` - CDN domain name
- `cache_policies` - Applied cache policies
- `dns_records_required` - Required DNS records

## Support

- [AWS CloudFront Docs](https://docs.aws.amazon.com/cloudfront/)
- [Google Cloud CDN Docs](https://cloud.google.com/cdn/docs)
- [Azure Front Door Docs](https://learn.microsoft.com/en-us/azure/frontdoor/)

## License

Part of Honua platform, licensed under Elastic License 2.0.
