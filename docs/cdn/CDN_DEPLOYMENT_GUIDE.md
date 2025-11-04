# CDN Deployment Guide for Honua Server

**Version**: 1.0
**Last Updated**: 2025-10-18
**Status**: Production Ready

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [AWS CloudFront Deployment](#aws-cloudfront-deployment)
4. [Azure Front Door Deployment](#azure-front-door-deployment)
5. [Cloudflare Deployment](#cloudflare-deployment)
6. [Multi-CDN Strategy](#multi-cdn-strategy)
7. [Post-Deployment Verification](#post-deployment-verification)
8. [Performance Tuning](#performance-tuning)
9. [Troubleshooting](#troubleshooting)

## Overview

This guide provides step-by-step instructions for deploying Honua Server behind a Content Delivery Network (CDN). CDN deployment offers:

- **95%+ cache hit rate** - Reduced origin load
- **Sub-100ms response times** - Global edge caching
- **DDoS protection** - Built-in security
- **Auto-scaling** - Handle traffic spikes
- **Cost savings** - Reduced bandwidth costs

### Provider Comparison

| Feature | AWS CloudFront | Azure Front Door | Cloudflare |
|---------|----------------|------------------|------------|
| **Price** | Moderate | Higher | Lower |
| **Global PoPs** | 450+ | 118+ | 200+ |
| **Setup Complexity** | Medium | Medium | Easy |
| **Invalidation Cost** | $0.005/path | $0.02/request | FREE |
| **WAF** | Extra cost | Included (Premium) | Included (Pro+) |
| **Best For** | AWS users | Azure users | All users |

## Prerequisites

### General Requirements

1. **Honua Server Deployed**
   - Running on AWS, Azure, GCP, or on-premises
   - Accessible via public IP or hostname
   - SSL/TLS certificate configured

2. **Domain Name**
   - Own a domain (e.g., honua.io)
   - Access to DNS management

3. **SSL Certificate** (optional)
   - CloudFront: ACM certificate in us-east-1
   - Azure: Front Door managed certificate
   - Cloudflare: Universal SSL (automatic)

4. **Terraform** (optional)
   - Version 1.5.0+
   - For infrastructure-as-code deployment

### Provider-Specific Requirements

**AWS:**
- AWS account with CloudFront access
- IAM user with permissions: `cloudfront:*`, `acm:*`, `s3:*`, `waf:*`
- AWS CLI configured

**Azure:**
- Azure subscription
- Service principal with Contributor role
- Azure CLI configured

**Cloudflare:**
- Cloudflare account (Free, Pro, Business, or Enterprise)
- Domain added to Cloudflare
- API token with Zone.Edit permissions

## AWS CloudFront Deployment

### Option 1: Terraform (Recommended)

**1. Clone Honua Infrastructure:**
```bash
cd /path/to/honua
cd infrastructure/terraform/aws
```

**2. Create terraform.tfvars:**
```hcl
# terraform.tfvars
domain_name              = "tiles.honua.io"
origin_domain_name       = "honua-alb-123456789.us-east-1.elb.amazonaws.com"
s3_raster_cache_bucket   = "honua-raster-cache-prod"
price_class              = "PriceClass_100"
enable_waf               = true
environment              = "prod"
```

**3. Initialize and Deploy:**
```bash
# Initialize Terraform
terraform init

# Plan deployment
terraform plan

# Apply configuration
terraform apply
```

**4. Configure DNS:**
```bash
# Get CloudFront distribution domain
CLOUDFRONT_DOMAIN=$(terraform output -raw cloudfront_domain_name)

# Create CNAME record (in Route53 or your DNS provider)
# tiles.honua.io → $CLOUDFRONT_DOMAIN
```

**5. Validate Custom Domain:**
```bash
# Wait for DNS propagation
dig tiles.honua.io

# Test CDN
curl -I https://tiles.honua.io/health
```

### Option 2: AWS Console

**1. Create CloudFront Distribution:**

Navigate to CloudFront Console → Create Distribution:

**Origin Settings:**
- Origin Domain: `honua-alb-123456789.us-east-1.elb.amazonaws.com`
- Protocol: HTTPS only
- Origin Path: (leave blank)
- Custom Headers:
  - `X-Origin-Verify`: `<random-secret>`

**Default Cache Behavior:**
- Viewer Protocol Policy: Redirect HTTP to HTTPS
- Allowed HTTP Methods: GET, HEAD, OPTIONS
- Cache Policy: CachingOptimized
- Origin Request Policy: AllViewerExceptHostHeader
- Response Headers Policy: SimpleCORS

**Ordered Cache Behaviors:**

1. **Pattern**: `/wms*`
   - Cache Policy: Create new (see below)
   - Origin: honua-server

2. **Pattern**: `/wmts*`
   - Cache Policy: TilesCachePolicy
   - Origin: honua-server

3. **Pattern**: `/ogc/collections/*/tiles/*`
   - Cache Policy: TilesCachePolicy
   - Origin: honua-server

4. **Pattern**: `/admin/*`
   - Cache Policy: CachingDisabled
   - Origin: honua-server

**Custom Cache Policy (TilesCachePolicy):**
```
Name: HonuaTilesCachePolicy
Min TTL: 0
Max TTL: 31536000 (1 year)
Default TTL: 86400 (1 day)

Query Strings: Include specified
- TIME
- datetime
- LAYERS
- STYLES
- FORMAT
- CRS
- BBOX
- WIDTH
- HEIGHT
- TILEMATRIX
- TILEMATRIXSET
- TILEROW
- TILECOL
- f
- styleId

Headers: Include specified
- Accept-Encoding

Compression: Enable gzip and brotli
```

**2. Configure SSL:**
- SSL Certificate: Custom SSL Certificate
- ACM Certificate: Select your ACM cert (us-east-1)
- Security Policy: TLSv1.2_2021

**3. Add Alternate Domain Names (CNAMEs):**
- Add: `tiles.honua.io`

**4. Enable Logging (Optional):**
- S3 Bucket: `honua-cloudfront-logs`
- Prefix: `cloudfront/prod/`

**5. Create Distribution:**
- Click "Create Distribution"
- Wait 5-10 minutes for deployment

### Option 3: AWS CLI

**1. Create Distribution Configuration:**

```json
{
  "CallerReference": "honua-cdn-prod-20250118",
  "Comment": "Honua Server CDN - Production",
  "Enabled": true,
  "Origins": {
    "Quantity": 1,
    "Items": [
      {
        "Id": "honua-server",
        "DomainName": "honua-alb-123456789.us-east-1.elb.amazonaws.com",
        "CustomOriginConfig": {
          "HTTPPort": 80,
          "HTTPSPort": 443,
          "OriginProtocolPolicy": "https-only",
          "OriginSslProtocols": {
            "Quantity": 1,
            "Items": ["TLSv1.2"]
          }
        }
      }
    ]
  },
  "DefaultCacheBehavior": {
    "TargetOriginId": "honua-server",
    "ViewerProtocolPolicy": "redirect-to-https",
    "AllowedMethods": {
      "Quantity": 3,
      "Items": ["GET", "HEAD", "OPTIONS"]
    },
    "CachePolicyId": "658327ea-f89d-4fab-a63d-7e88639e58f6",
    "Compress": true
  },
  "ViewerCertificate": {
    "ACMCertificateArn": "arn:aws:acm:us-east-1:123456789012:certificate/abc-def-123",
    "SSLSupportMethod": "sni-only",
    "MinimumProtocolVersion": "TLSv1.2_2021"
  }
}
```

**2. Create Distribution:**
```bash
aws cloudfront create-distribution --distribution-config file://distribution-config.json
```

### Post-Deployment Steps

**1. Update Origin to Validate Custom Header:**

In your Honua server configuration:

```json
{
  "cdn": {
    "originVerifyHeader": "X-Origin-Verify",
    "originVerifyValue": "<secret-from-terraform-output>",
    "rejectUnverified": true
  }
}
```

**2. Create DNS Record:**
```bash
# Route53
aws route53 change-resource-record-sets --hosted-zone-id Z123 --change-batch '{
  "Changes": [{
    "Action": "CREATE",
    "ResourceRecordSet": {
      "Name": "tiles.honua.io",
      "Type": "A",
      "AliasTarget": {
        "HostedZoneId": "Z2FDTNDATAQYW2",
        "DNSName": "d111111abcdef8.cloudfront.net",
        "EvaluateTargetHealth": false
      }
    }
  }]
}'
```

**3. Test CDN:**
```bash
# Test cache miss
curl -I https://tiles.honua.io/wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=cities&CRS=EPSG:3857&BBOX=-180,-90,180,90&WIDTH=256&HEIGHT=256&FORMAT=image/png
# X-Cache: Miss from cloudfront

# Test cache hit (same request)
curl -I https://tiles.honua.io/wms?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=cities&CRS=EPSG:3857&BBOX=-180,-90,180,90&WIDTH=256&HEIGHT=256&FORMAT=image/png
# X-Cache: Hit from cloudfront
```

## Azure Front Door Deployment

### Option 1: Terraform (Recommended)

**1. Navigate to Azure Infrastructure:**
```bash
cd infrastructure/terraform/azure
```

**2. Create terraform.tfvars:**
```hcl
# terraform.tfvars
resource_group_name  = "rg-honua-prod"
location             = "eastus"
custom_domain        = "tiles.honua.io"
origin_hostname      = "honua-app.azurecontainerapps.io"
storage_account_name = "honuarastercache"
enable_waf           = true
environment          = "prod"
```

**3. Deploy:**
```bash
terraform init
terraform plan
terraform apply
```

**4. Validate Custom Domain:**

After deployment, Azure will provide a validation token:

```bash
# Get validation token
VALIDATION_TOKEN=$(terraform output -raw custom_domain_validation_token)

# Create TXT record
# _dnsauth.tiles.honua.io TXT $VALIDATION_TOKEN
```

**5. Wait for Validation:**
```bash
az afd custom-domain show \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --custom-domain-name tiles-honua-io \
  --query validationProperties.validationState
```

### Option 2: Azure Portal

**1. Create Front Door Profile:**

Navigate to Front Door → Create:

**Basics:**
- Resource Group: rg-honua-prod
- Name: honua-fd-prod
- Tier: Premium (for WAF)

**Endpoint:**
- Name: honua-endpoint
- Status: Enabled

**Origin Groups:**
- Name: honua-server
- Health Probe: /health
- Load Balancing: Latency-based

**Origins:**
- Name: honua-server
- Origin Type: Custom
- Host Name: honua-app.azurecontainerapps.io
- HTTP Port: 80
- HTTPS Port: 443
- Priority: 1
- Weight: 1000

**Routes:**

1. **Default Route (Tiles):**
   - Name: tiles-route
   - Domains: Default endpoint
   - Patterns: `/*`
   - Protocol: HTTPS only
   - Caching: Enabled, 1 day TTL
   - Query String: Include specified
   - Rule Set: TilesCaching

2. **Metadata Route:**
   - Name: metadata-route
   - Patterns: `/stac/*`, `/ogc/collections`
   - Caching: 5 minutes
   - Rule Set: MetadataCaching

3. **Admin Route:**
   - Name: admin-route
   - Patterns: `/admin/*`
   - Caching: Disabled

**2. Configure WAF (Premium Tier):**

Security → Create WAF Policy:

**Managed Rules:**
- Default Rule Set: DRS 1.0
- Bot Manager Rule Set: 1.0

**Custom Rules:**
- Rate Limiting: 2000 requests/minute per IP

**3. Add Custom Domain:**

Domains → Add Custom Domain:
- Custom Domain: tiles.honua.io
- DNS Management: Use Azure DNS or external
- Certificate: Managed certificate

**4. Validate Domain:**
- Add TXT record to DNS
- Wait for validation (5-10 minutes)

### Option 3: Azure CLI

```bash
# Create Front Door profile
az afd profile create \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --sku Premium_AzureFrontDoor

# Create endpoint
az afd endpoint create \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint

# Create origin group
az afd origin-group create \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --origin-group-name honua-server \
  --probe-path /health \
  --probe-protocol Https \
  --probe-interval-in-seconds 30

# Create origin
az afd origin create \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --origin-group-name honua-server \
  --origin-name honua-server \
  --host-name honua-app.azurecontainerapps.io \
  --origin-host-header honua-app.azurecontainerapps.io \
  --http-port 80 \
  --https-port 443 \
  --priority 1 \
  --weight 1000

# Create route
az afd route create \
  --resource-group rg-honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint \
  --route-name default-route \
  --origin-group honua-server \
  --supported-protocols Https \
  --https-redirect Enabled \
  --forwarding-protocol HttpsOnly \
  --patterns-to-match "/*"
```

## Cloudflare Deployment

### Option 1: Terraform (Recommended)

**1. Set Up Cloudflare Provider:**

```bash
cd infrastructure/terraform/cloudflare
```

**2. Create terraform.tfvars:**
```hcl
# terraform.tfvars
cloudflare_api_token = "your-cloudflare-api-token"
zone_name            = "honua.io"
subdomain            = "tiles"
origin_hostname      = "honua-server.example.com"
enable_waf           = true
enable_argo          = false  # Requires Business plan
enable_rate_limiting = true
ssl_mode             = "full"
min_tls_version      = "1.2"
environment          = "prod"
```

**3. Deploy:**
```bash
terraform init
terraform plan
terraform apply
```

**4. Update Nameservers (if new zone):**
```bash
# Get Cloudflare nameservers
terraform output nameservers

# Update at your domain registrar:
# ns1.cloudflare.com
# ns2.cloudflare.com
```

### Option 2: Cloudflare Dashboard

**1. Add Site to Cloudflare:**
- Dashboard → Add Site
- Enter domain: honua.io
- Select plan: Free, Pro, Business, or Enterprise

**2. Update Nameservers:**
- Copy Cloudflare nameservers
- Update at domain registrar
- Wait for activation (can take 24-48 hours)

**3. Create DNS Record:**
- DNS → Add Record
- Type: CNAME
- Name: tiles
- Target: honua-server.example.com
- Proxy status: Proxied (orange cloud)

**4. Configure SSL/TLS:**
- SSL/TLS → Overview
- Encryption mode: Full (Strict)
- Edge Certificates → Always Use HTTPS: On
- Minimum TLS Version: 1.2
- TLS 1.3: On
- Automatic HTTPS Rewrites: On

**5. Configure Caching:**

Rules → Page Rules:

1. **Tiles Cache Rule:**
   - URL: `tiles.honua.io/wms*`
   - Settings:
     - Cache Level: Cache Everything
     - Edge Cache TTL: 1 month
     - Browser Cache TTL: 1 day

2. **WMTS Cache Rule:**
   - URL: `tiles.honua.io/wmts*`
   - Settings: Same as above

3. **Admin No Cache:**
   - URL: `tiles.honua.io/admin/*`
   - Settings:
     - Cache Level: Bypass

**6. Enable WAF (Pro+ Plan):**
- Security → WAF
- Managed Rules: Enable Cloudflare Managed Ruleset
- OWASP Core Ruleset: Enable

**7. Configure Rate Limiting:**
- Security → Rate Limiting Rules
- Create Rule:
  - Name: General Rate Limit
  - Expression: `(http.request.uri.path matches "^/.*")`
  - Action: Block
  - Duration: 60 seconds
  - Requests: 2000

### Option 3: Cloudflare API

```bash
# Get Zone ID
ZONE_ID=$(curl -X GET "https://api.cloudflare.com/client/v4/zones?name=honua.io" \
  -H "Authorization: Bearer $CF_API_TOKEN" | jq -r '.result[0].id')

# Create DNS record
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/dns_records" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "type": "CNAME",
    "name": "tiles",
    "content": "honua-server.example.com",
    "proxied": true
  }'

# Create page rule
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/pagerules" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "targets": [{
      "target": "url",
      "constraint": {
        "operator": "matches",
        "value": "tiles.honua.io/wms*"
      }
    }],
    "actions": [{
      "id": "cache_level",
      "value": "cache_everything"
    }, {
      "id": "edge_cache_ttl",
      "value": 2592000
    }],
    "priority": 1,
    "status": "active"
  }'
```

## Multi-CDN Strategy

For maximum reliability and performance, use multiple CDN providers:

### Architecture

```
                      ┌─────────────────┐
                      │  Global DNS     │
                      │  (Route53/      │
                      │   GeoDNS)       │
                      └────────┬────────┘
                               │
            ┌──────────────────┼──────────────────┐
            │                  │                  │
      ┌─────▼─────┐      ┌────▼────┐      ┌─────▼─────┐
      │CloudFront │      │  Azure  │      │Cloudflare │
      │  (Primary)│      │Front Door│      │ (Backup)  │
      └─────┬─────┘      └────┬────┘      └─────┬─────┘
            │                 │                  │
            └─────────────────┼──────────────────┘
                              │
                      ┌───────▼────────┐
                      │ Honua Server   │
                      └────────────────┘
```

### DNS-Based Routing

**Route53 Geolocation Routing:**

```hcl
# North America → CloudFront
resource "aws_route53_record" "tiles_na" {
  zone_id = aws_route53_zone.honua.zone_id
  name    = "tiles"
  type    = "A"

  set_identifier = "CloudFront-NA"
  geolocation_routing_policy {
    continent = "NA"
  }

  alias {
    name                   = aws_cloudfront_distribution.honua_cdn.domain_name
    zone_id                = aws_cloudfront_distribution.honua_cdn.hosted_zone_id
    evaluate_target_health = true
  }
}

# Europe → Azure Front Door
resource "aws_route53_record" "tiles_eu" {
  zone_id = aws_route53_zone.honua.zone_id
  name    = "tiles"
  type    = "A"

  set_identifier = "AzureFrontDoor-EU"
  geolocation_routing_policy {
    continent = "EU"
  }

  alias {
    name                   = azurerm_cdn_frontdoor_endpoint.honua.host_name
    zone_id                = "..." # Azure Front Door hosted zone
    evaluate_target_health = true
  }
}

# Default → Cloudflare
resource "aws_route53_record" "tiles_default" {
  zone_id = aws_route53_zone.honua.zone_id
  name    = "tiles"
  type    = "A"

  set_identifier = "Cloudflare-Default"
  geolocation_routing_policy {
    location = "*"
  }

  # Point to Cloudflare CNAME
}
```

### Failover Configuration

**CloudFront + Cloudflare Failover:**

```hcl
resource "aws_route53_record" "tiles_primary" {
  zone_id = aws_route53_zone.honua.zone_id
  name    = "tiles"
  type    = "A"

  set_identifier = "Primary"
  failover_routing_policy {
    type = "PRIMARY"
  }
  health_check_id = aws_route53_health_check.cloudfront.id

  alias {
    name                   = aws_cloudfront_distribution.honua_cdn.domain_name
    zone_id                = aws_cloudfront_distribution.honua_cdn.hosted_zone_id
    evaluate_target_health = false
  }
}

resource "aws_route53_record" "tiles_secondary" {
  zone_id = aws_route53_zone.honua.zone_id
  name    = "tiles"
  type    = "CNAME"

  set_identifier = "Secondary"
  failover_routing_policy {
    type = "SECONDARY"
  }

  ttl     = 60
  records = ["tiles-cf.honua.io.cdn.cloudflare.net"]
}
```

## Post-Deployment Verification

### 1. DNS Propagation

```bash
# Check DNS resolution
dig tiles.honua.io
nslookup tiles.honua.io

# Check from multiple locations
dig @8.8.8.8 tiles.honua.io  # Google DNS
dig @1.1.1.1 tiles.honua.io  # Cloudflare DNS
```

### 2. SSL Certificate

```bash
# Test SSL
curl -vI https://tiles.honua.io 2>&1 | grep -E "subject:|issuer:|expire"

# Check certificate chain
openssl s_client -connect tiles.honua.io:443 -showcerts
```

### 3. Cache Behavior

```bash
# Test cache miss
curl -I https://tiles.honua.io/wms?LAYERS=cities&BBOX=-180,-90,180,90&WIDTH=256&HEIGHT=256&FORMAT=image/png

# Expected headers:
# X-Cache: MISS from cloudfront (or similar)
# Age: 0

# Test cache hit (repeat same request)
curl -I https://tiles.honua.io/wms?LAYERS=cities&BBOX=-180,-90,180,90&WIDTH=256&HEIGHT=256&FORMAT=image/png

# Expected headers:
# X-Cache: HIT from cloudfront
# Age: <seconds since cached>
```

### 4. Compression

```bash
# Test gzip compression
curl -H "Accept-Encoding: gzip" -I https://tiles.honua.io/stac

# Expected:
# Content-Encoding: gzip

# Test brotli compression
curl -H "Accept-Encoding: br" -I https://tiles.honua.io/stac

# Expected:
# Content-Encoding: br
```

### 5. CORS Headers

```bash
# Test CORS
curl -H "Origin: https://example.com" -I https://tiles.honua.io/wms

# Expected:
# Access-Control-Allow-Origin: *
# Access-Control-Allow-Methods: GET, HEAD, OPTIONS
```

### 6. Performance

```bash
# Measure response time
time curl -so /dev/null https://tiles.honua.io/wms?LAYERS=cities

# Expected: <100ms from cache

# Test from multiple locations (use VPN or online tools)
```

## Performance Tuning

### 1. Optimize Cache Key

**Include only necessary query parameters:**

```hcl
# Bad: Cache all query strings (cache fragmentation)
query_string_caching_behavior = "all"

# Good: Cache specific parameters
query_string_caching_behavior = "include_specified"
query_strings = ["LAYERS", "BBOX", "WIDTH", "HEIGHT", "FORMAT"]
```

### 2. Enable Compression

**CloudFront:**
```hcl
compress = true

# Response headers policy for brotli
response_headers_policy {
  custom_headers_config {
    items {
      header   = "Content-Encoding"
      value    = "br"
      override = false
    }
  }
}
```

**Azure Front Door:**
```hcl
compression_enabled = true
```

**Cloudflare:**
```
# Automatic in dashboard: Speed → Optimization → Auto Minify
```

### 3. Preseed Cache

```bash
# Preseed critical tiles before launch
for z in {0..10}; do
  for x in $(seq 0 $((2**z - 1))); do
    for y in $(seq 0 $((2**z - 1))); do
      curl -s "https://tiles.honua.io/ogc/collections/cities/tiles/WebMercatorQuad/$z/$x/$y?f=mvt" > /dev/null &
    done
  done
  wait
done
```

### 4. Monitor Cache Hit Ratio

**CloudWatch (CloudFront):**
```bash
aws cloudwatch get-metric-statistics \
  --namespace AWS/CloudFront \
  --metric-name CacheHitRate \
  --dimensions Name=DistributionId,Value=E123 \
  --start-time 2025-01-18T00:00:00Z \
  --end-time 2025-01-18T23:59:59Z \
  --period 3600 \
  --statistics Average
```

**Target:** 95%+ cache hit rate

## Troubleshooting

### Issue: 502 Bad Gateway

**Causes:**
- Origin server down
- SSL certificate mismatch
- Origin timeout

**Solutions:**
1. Check origin health
2. Verify SSL certificate on origin
3. Increase origin timeout in CDN config

### Issue: Cache Not Working

**Symptoms:**
- X-Cache always shows MISS
- Age header always 0

**Solutions:**
1. Check Cache-Control headers from origin
2. Verify cache policy includes query strings
3. Check for cookies (disable cookie forwarding)

### Issue: Slow Initial Load

**Cause:**
- Cold cache (no content cached yet)

**Solution:**
- Preseed cache with critical tiles
- Consider using Cloudflare Always Online

### Issue: Different Content by Location

**Cause:**
- CDN serving different cached versions

**Solution:**
- Ensure consistent Vary headers
- Use same cache key across regions
- Purge cache and reseed

## Related Documentation

- [CDN Caching Policies](./CDN_CACHING_POLICIES.md)
- [CDN Cache Invalidation](./CDN_CACHE_INVALIDATION.md)
- [CDN Integration](../CDN_INTEGRATION.md)
- [Performance Tuning](../rag/04-operations/performance-tuning.md)
- [Deployment Guide](../deployment/README.md)
