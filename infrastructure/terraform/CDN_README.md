# CDN Infrastructure for Honua Server

Complete Terraform configurations for deploying Content Delivery Networks (CDN) in front of Honua Server.

## Overview

This directory contains production-ready Terraform modules for three major CDN providers:

- **AWS CloudFront** - Comprehensive caching, integrated with AWS services
- **Azure Front Door** - Global load balancing with integrated WAF
- **Cloudflare** - Cost-effective with unlimited bandwidth

## Quick Start

### 1. Choose Your Provider

```bash
# AWS CloudFront
cd aws/

# Azure Front Door
cd azure/

# Cloudflare
cd cloudflare/
```

### 2. Copy Example Variables

```bash
# Copy and customize variables file
cp cdn-*.tfvars.example cdn-*.tfvars

# Edit with your configuration
vi cdn-*.tfvars
```

### 3. Deploy

```bash
# Initialize Terraform
terraform init

# Preview changes
terraform plan -var-file="cdn-*.tfvars"

# Deploy
terraform apply -var-file="cdn-*.tfvars"
```

### 4. Configure DNS

```bash
# Get CDN endpoint from outputs
terraform output

# Create DNS CNAME record pointing to CDN endpoint
```

## Directory Structure

```
infrastructure/terraform/
├── aws/
│   ├── cdn-cloudfront.tf              # CloudFront configuration
│   ├── cdn-cloudfront.tfvars.example  # Example variables
│   └── README.md                      # AWS-specific guide
├── azure/
│   ├── cdn-frontdoor.tf               # Front Door configuration
│   ├── cdn-frontdoor.tfvars.example   # Example variables
│   └── README.md                      # Azure-specific guide
├── cloudflare/
│   ├── cdn-cloudflare.tf              # Cloudflare configuration
│   ├── cdn-cloudflare.tfvars.example  # Example variables
│   └── README.md                      # Cloudflare-specific guide
└── CDN_README.md                      # This file
```

## Provider Comparison

### AWS CloudFront

**Best For**: AWS-native deployments, S3 integration, granular control

**Pros**:
- Deep AWS integration (ALB, S3, Lambda@Edge)
- 450+ global edge locations
- Sophisticated cache policies
- Tag-based invalidation

**Cons**:
- Invalidation costs ($0.005/path after 1,000 free)
- Complex configuration
- Requires ACM certificate in us-east-1

**Monthly Cost** (10M requests, 1TB transfer):
- Base: ~$93
- With WAF: ~$108

**Terraform Module**: `/aws/cdn-cloudfront.tf`

### Azure Front Door

**Best For**: Azure deployments, integrated WAF, global load balancing

**Pros**:
- Integrated WAF (Premium tier)
- Health probes and failover
- Azure-native integration
- Managed SSL certificates

**Cons**:
- Higher cost than alternatives
- Premium tier required for WAF
- Less granular cache control

**Monthly Cost** (10M requests, 1TB transfer):
- Standard: ~$126
- Premium (WAF): ~$431

**Terraform Module**: `/azure/cdn-frontdoor.tf`

### Cloudflare

**Best For**: All deployments, cost optimization, free tier available

**Pros**:
- **Unlimited bandwidth (FREE!)**
- Free cache purging (unlimited)
- Easy setup and configuration
- Integrated DDoS protection
- 200+ global PoPs

**Cons**:
- Less granular cache control than CloudFront
- WAF requires Pro plan ($20/month)
- Argo requires Business plan ($200/month)

**Monthly Cost** (10M requests, 1TB transfer):
- Free Plan: $0
- Pro Plan: $20
- Business Plan: $200

**Terraform Module**: `/cloudflare/cdn-cloudflare.tf`

## Features by Provider

| Feature | CloudFront | Front Door | Cloudflare |
|---------|-----------|------------|------------|
| **Global PoPs** | 450+ | 118+ | 200+ |
| **DDoS Protection** | AWS Shield | Azure DDoS | Included (all plans) |
| **WAF** | Extra cost | Premium tier | Pro+ plan |
| **Cache Purge Cost** | $0.005/path | $0.02/req | FREE |
| **SSL Certificate** | ACM (free) | Managed (free) | Universal (free) |
| **Health Checks** | Via Route53 | Built-in | Via Load Balancer |
| **Edge Compute** | Lambda@Edge | Not supported | Workers |
| **Geo-blocking** | Included | Included | Included |
| **Rate Limiting** | WAF required | WAF required | All plans |
| **Analytics** | CloudWatch | Azure Monitor | Dashboard |
| **API** | AWS CLI | Azure CLI | REST API |
| **Terraform Support** | Excellent | Excellent | Excellent |

## Caching Strategy

All configurations implement a three-tier caching strategy:

### 1. Tile Endpoints (Long Cache)

**Paths**: `/wms*`, `/wmts*`, `/ogc/collections/*/tiles/*`

**Cache Policy**:
- Browser TTL: 1 day
- CDN Edge TTL: 30 days
- Stale-while-revalidate: 1 hour
- Stale-if-error: 7 days

**Why**: Tiles are immutable or rarely change. Aggressive caching reduces origin load by 95%+.

### 2. Metadata Endpoints (Short Cache)

**Paths**: `/stac/*`, `/ogc/collections`, `/ogc/conformance`

**Cache Policy**:
- Browser TTL: 5 minutes
- CDN Edge TTL: 5 minutes

**Why**: Metadata changes when layers are added/removed. Short TTL ensures freshness.

### 3. Admin Endpoints (No Cache)

**Paths**: `/admin/*`

**Cache Policy**:
- No caching
- Bypass CDN

**Why**: Admin operations require real-time data and authentication.

## Security Features

### DDoS Protection

**CloudFront**:
- AWS Shield Standard (automatic, free)
- AWS Shield Advanced (optional, $3,000/month)

**Azure Front Door**:
- Azure DDoS Protection (included)
- Automatic mitigation

**Cloudflare**:
- Unmetered DDoS protection (all plans)
- 121 Tbps network capacity

### Web Application Firewall (WAF)

**CloudFront**:
```hcl
enable_waf = true  # Creates WAF Web ACL
# - Rate limiting: 2000 req/min per IP
# - AWS Managed Rules (Common, Known Bad Inputs)
# - Custom rules supported
```

**Azure Front Door**:
```hcl
enable_waf = true  # Requires Premium tier
# - Default Rule Set (DRS 1.0)
# - Bot Manager Rule Set
# - Rate limiting: 2000 req/min per IP
```

**Cloudflare**:
```hcl
enable_waf = true  # Requires Pro plan ($20/month)
# - Cloudflare Managed Ruleset
# - OWASP Core Ruleset
# - Custom rules
```

### SSL/TLS

All configurations enforce:
- HTTPS-only (redirect HTTP to HTTPS)
- Minimum TLS 1.2
- Strong cipher suites
- HSTS headers

**Certificate Management**:
- **CloudFront**: AWS Certificate Manager (ACM) - free, auto-renewal
- **Azure**: Front Door managed certificate - free, auto-renewal
- **Cloudflare**: Universal SSL - free, auto-renewal

## Cost Optimization

### 1. Use Versioned URLs (Best Practice)

```json
{
  "rasters": [
    {
      "id": "cities",
      "version": "2025-01-18",
      "cdn": {
        "enabled": true,
        "policy": "Immutable"
      }
    }
  ]
}
```

**Benefits**:
- No invalidation costs
- Immutable caching (max TTL)
- Instant updates (change version)

### 2. Choose Right Price Class (CloudFront)

```hcl
# Global coverage (most expensive)
price_class = "PriceClass_All"

# US, Canada, Europe, Asia (recommended)
price_class = "PriceClass_200"

# US, Canada, Europe only (cheapest)
price_class = "PriceClass_100"
```

### 3. Batch Invalidations

```bash
# Bad: Multiple separate invalidations
aws cloudfront create-invalidation --distribution-id E123 --paths "/layer1/*"
aws cloudfront create-invalidation --distribution-id E123 --paths "/layer2/*"
# Cost: 2 paths

# Good: Single batched invalidation
aws cloudfront create-invalidation --distribution-id E123 --paths "/layer1/*" "/layer2/*"
# Cost: 1 request (2 paths counted)
```

### 4. Consider Cloudflare for High-Frequency Purging

Cloudflare offers unlimited free cache purging, making it ideal for:
- Development environments
- Frequently updated data
- CI/CD pipelines with automatic purging

## Multi-CDN Strategy

For maximum reliability, deploy multiple CDNs with DNS-based failover:

```
Route53 Geolocation Routing:
  ├─ North America → CloudFront
  ├─ Europe → Azure Front Door
  └─ Asia/Pacific → Cloudflare
```

**Benefits**:
- Geographic optimization
- Provider redundancy
- Cost optimization (use cheapest per region)
- Performance testing (A/B comparison)

**Implementation**: See `/docs/cdn/CDN_DEPLOYMENT_GUIDE.md` for complete multi-CDN setup.

## Deployment Workflow

### Standard Deployment

```bash
# 1. Review configuration
vi cdn-*.tfvars

# 2. Initialize
terraform init

# 3. Plan (verify resources)
terraform plan -var-file="cdn-*.tfvars"

# 4. Apply
terraform apply -var-file="cdn-*.tfvars"

# 5. Get outputs
terraform output

# 6. Configure DNS
# Create CNAME: tiles.honua.io → <cdn-endpoint>

# 7. Test
curl -I https://tiles.honua.io/health
```

### CI/CD Deployment

**GitHub Actions Example**:

```yaml
name: Deploy CDN

on:
  push:
    branches: [main]
    paths: ['infrastructure/terraform/aws/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v2

      - name: Terraform Init
        working-directory: infrastructure/terraform/aws
        run: terraform init

      - name: Terraform Apply
        working-directory: infrastructure/terraform/aws
        run: terraform apply -auto-approve -var-file="cdn-cloudfront.tfvars"
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
```

## Cache Invalidation

### CloudFront

```bash
# Invalidate all
aws cloudfront create-invalidation \
  --distribution-id $(terraform output -raw cloudfront_distribution_id) \
  --paths "/*"

# Invalidate specific layer
aws cloudfront create-invalidation \
  --distribution-id $(terraform output -raw cloudfront_distribution_id) \
  --paths "/ogc/collections/cities/*"
```

### Azure Front Door

```bash
# Invalidate all
az afd endpoint purge \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint \
  --content-paths "/*"
```

### Cloudflare

```bash
# Invalidate all (FREE)
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"purge_everything":true}'
```

**Complete Guide**: See `/docs/cdn/CDN_CACHE_INVALIDATION.md`

## Monitoring

### CloudFront (CloudWatch)

```bash
# Cache hit rate
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name CacheHitRate \
  --dimensions Name=DistributionId,Value=$(terraform output -raw cloudfront_distribution_id) \
  --start-time 2025-01-18T00:00:00Z \
  --end-time 2025-01-18T23:59:59Z \
  --period 3600 \
  --statistics Average
```

### Azure Front Door (Azure Monitor)

```bash
# View metrics
az monitor metrics list \
  --resource $(terraform output -raw frontdoor_id) \
  --metric "RequestCount,CacheHitRatio,OriginLatency"
```

### Cloudflare (Analytics API)

```bash
# Get analytics
curl "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/analytics/dashboard" \
  -H "Authorization: Bearer $CF_API_TOKEN"
```

**Recommended Dashboards**: Grafana dashboards for all providers available in `/docs/observability/`

## Testing

### Verify Cache Headers

```bash
# Test cache miss
curl -I https://tiles.honua.io/wms?LAYERS=cities

# Expected:
# X-Cache: MISS from cloudfront
# Age: 0

# Test cache hit (repeat same request)
curl -I https://tiles.honua.io/wms?LAYERS=cities

# Expected:
# X-Cache: HIT from cloudfront
# Age: <seconds>
```

### Test from Multiple Locations

```bash
# Use online tools:
# - https://www.webpagetest.org/
# - https://tools.pingdom.com/
# - https://www.dotcom-tools.com/website-speed-test.aspx

# Or use VPN/proxy to test from different regions
```

### Load Testing

```bash
# Apache Bench
ab -n 10000 -c 100 https://tiles.honua.io/wms?LAYERS=cities

# Expected cache hit rate: >95%
```

## Troubleshooting

### Issue: 502 Bad Gateway

**Diagnosis**:
```bash
# Check origin health
curl -I https://origin.example.com/health

# Check SSL certificate
openssl s_client -connect origin.example.com:443
```

**Solutions**:
- Verify origin is running
- Check origin SSL certificate
- Increase origin timeout in CDN config

### Issue: Cache Not Working

**Diagnosis**:
```bash
# Check cache headers
curl -I https://tiles.honua.io/wms?LAYERS=cities | grep -i cache
```

**Solutions**:
- Verify Cache-Control headers from origin
- Check cache policy includes query strings
- Disable cookie forwarding

### Issue: Slow Performance

**Diagnosis**:
```bash
# Measure response time
time curl -so /dev/null https://tiles.honua.io/wms?LAYERS=cities
```

**Solutions**:
- Preseed cache for critical tiles
- Enable compression (brotli/gzip)
- Use HTTP/2 or HTTP/3
- Check origin performance

**Complete Troubleshooting Guide**: See `/docs/cdn/CDN_DEPLOYMENT_GUIDE.md#troubleshooting`

## Documentation

### Complete Documentation Suite

1. **[CDN Caching Policies](../../docs/cdn/CDN_CACHING_POLICIES.md)**
   - Cache strategy by content type
   - Query string handling
   - HTTP headers
   - Performance optimization

2. **[CDN Cache Invalidation](../../docs/cdn/CDN_CACHE_INVALIDATION.md)**
   - When to invalidate
   - Provider-specific procedures
   - Automation strategies
   - Cost considerations

3. **[CDN Deployment Guide](../../docs/cdn/CDN_DEPLOYMENT_GUIDE.md)**
   - Step-by-step deployment
   - Multi-CDN strategy
   - Post-deployment verification
   - Troubleshooting

4. **[CDN Integration](../../docs/CDN_INTEGRATION.md)**
   - Honua server configuration
   - Cache-Control header setup
   - Provider comparison

## Support

### Community

- GitHub Issues: https://github.com/honuaio/honua/issues
- Discussions: https://github.com/honuaio/honua/discussions
- Documentation: https://docs.honua.io

### Commercial Support

- Email: support@honua.io
- Enterprise consulting available

## License

This Terraform configuration is part of the Honua Server project and is licensed under the MIT License.

## Contributing

Contributions welcome! Please:

1. Test thoroughly before submitting
2. Update documentation
3. Follow existing code style
4. Add examples for new features

---

**Last Updated**: 2025-10-18
**Version**: 1.0
**Status**: Production Ready
