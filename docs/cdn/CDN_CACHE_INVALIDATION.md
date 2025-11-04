# CDN Cache Invalidation Procedures

**Version**: 1.0
**Last Updated**: 2025-10-18
**Status**: Production Ready

## Table of Contents

1. [Overview](#overview)
2. [When to Invalidate Cache](#when-to-invalidate-cache)
3. [Invalidation Methods](#invalidation-methods)
4. [Provider-Specific Procedures](#provider-specific-procedures)
5. [Automation](#automation)
6. [Cost Considerations](#cost-considerations)
7. [Testing and Verification](#testing-and-verification)
8. [Troubleshooting](#troubleshooting)

## Overview

Cache invalidation removes cached content from CDN edge locations, forcing fresh content to be fetched from the origin. This is critical when:

- Publishing updated map tiles
- Changing layer metadata
- Fixing data errors
- Rolling back problematic changes

**Trade-offs:**
- **Invalidation**: Fast but costs money (especially at scale)
- **TTL Expiration**: Free but slower (wait for natural expiry)
- **Versioned URLs**: Best practice (no invalidation needed)

## When to Invalidate Cache

### Automatic Invalidation Triggers

These scenarios should trigger automatic cache invalidation:

1. **Data Ingestion Complete**
   - New raster/vector data uploaded
   - Affects specific layers/zoom levels
   - Invalidate: Affected layer paths

2. **Metadata Reload**
   - Layer configuration changed
   - Styles updated
   - Invalidate: Metadata endpoints + affected layer

3. **Emergency Data Fix**
   - Incorrect data published
   - Security vulnerability
   - Invalidate: Everything (purge all)

4. **Deployment**
   - New Honua version deployed
   - Breaking API changes
   - Invalidate: API endpoints

### Manual Invalidation Scenarios

1. **Scheduled Updates**
   - Monthly satellite imagery refresh
   - Quarterly boundary updates
   - Invalidate during maintenance window

2. **Testing**
   - Verify new tile rendering
   - Validate style changes
   - Invalidate specific test tiles

3. **Customer Request**
   - Data correction
   - Style modification
   - Invalidate specific collection/layer

## Invalidation Methods

### 1. Full Invalidation (Purge Everything)

**When to Use:**
- Major deployment
- Emergency data fix
- Complete system update

**Pros:**
- Guaranteed fresh content everywhere
- Simple (one command)

**Cons:**
- Expensive (especially on CloudFront)
- Cache hit ratio drops to 0%
- Increased origin load

**Example:**
```bash
# CloudFront
aws cloudfront create-invalidation --distribution-id E1234567890ABC --paths "/*"

# Azure Front Door
az afd endpoint purge --resource-group honua-prod --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint --content-paths "/*"

# Cloudflare
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"purge_everything":true}'
```

### 2. Path-Based Invalidation

**When to Use:**
- Single layer updated
- Specific zoom level changed
- Metadata refresh

**Pros:**
- Surgical (only affects needed paths)
- Lower cost than full purge
- Preserves other cached content

**Cons:**
- Need to know exact paths
- Wildcard support varies by provider

**Examples:**

**Invalidate Single Layer:**
```bash
# CloudFront (wildcard supported)
aws cloudfront create-invalidation --distribution-id E1234567890ABC \
  --paths "/ogc/collections/cities/*"

# Azure Front Door
az afd endpoint purge --resource-group honua-prod --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint --content-paths "/ogc/collections/cities/*"

# Cloudflare
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"files":["https://tiles.honua.io/ogc/collections/cities/*"]}'
```

**Invalidate Specific Zoom Levels:**
```bash
# WMS tiles for zoom 10-14
aws cloudfront create-invalidation --distribution-id E1234567890ABC \
  --paths "/wms*BBOX=*" "/wmts*TILEMATRIX=10*" "/wmts*TILEMATRIX=11*" \
          "/wmts*TILEMATRIX=12*" "/wmts*TILEMATRIX=13*" "/wmts*TILEMATRIX=14*"
```

**Invalidate Metadata Only:**
```bash
# STAC catalog and OGC collections
aws cloudfront create-invalidation --distribution-id E1234567890ABC \
  --paths "/stac/*" "/ogc/collections" "/ogc/conformance"
```

### 3. Tag-Based Invalidation

**When to Use:**
- CloudFront with cache key tags
- Logical grouping of content

**Pros:**
- Invalidate by business logic (not paths)
- Flexible grouping

**Cons:**
- Requires tag setup in advance
- CloudFront only

**Setup:**
```json
{
  "CachePolicy": {
    "ParametersInCacheKeyAndForwardedToOrigin": {
      "EnableAcceptEncodingGzip": true,
      "HeadersConfig": {
        "HeaderBehavior": "whitelist",
        "Headers": {
          "Items": ["CloudFront-Is-Mobile-Viewer", "CloudFront-Is-Desktop-Viewer"]
        }
      }
    }
  }
}
```

### 4. Versioned URLs (Best Practice)

**Strategy:**
Include version in URL path or query string.

**Examples:**
```
/v1/tiles/cities/10/512/342.mvt
/tiles/cities/10/512/342.mvt?v=20250118
/tiles/cities/10/512/342.mvt?hash=abc123
```

**Benefits:**
- No invalidation needed
- Immutable caching (max TTL)
- Instant updates (change version)
- No CDN costs

**Implementation:**
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

**Tile URL:**
```
https://tiles.honua.io/v2025-01-18/ogc/collections/cities/tiles/10/512/342
```

## Provider-Specific Procedures

### AWS CloudFront

#### CLI Commands

**Create Invalidation:**
```bash
# Single invalidation
aws cloudfront create-invalidation \
  --distribution-id E1234567890ABC \
  --paths "/wms*" "/wmts*" "/ogc/collections/cities/*"

# Multiple paths (up to 3,000 per request)
aws cloudfront create-invalidation \
  --distribution-id E1234567890ABC \
  --paths "/*"
```

**Check Invalidation Status:**
```bash
aws cloudfront get-invalidation \
  --distribution-id E1234567890ABC \
  --id I1234567890DEF
```

**List Recent Invalidations:**
```bash
aws cloudfront list-invalidations \
  --distribution-id E1234567890ABC
```

#### Terraform

```hcl
resource "null_resource" "invalidate_cache" {
  triggers = {
    always_run = timestamp()
  }

  provisioner "local-exec" {
    command = <<EOF
      aws cloudfront create-invalidation \
        --distribution-id ${aws_cloudfront_distribution.honua_cdn.id} \
        --paths "/*"
    EOF
  }
}
```

#### Cost

- **First 1,000 paths/month**: FREE
- **Additional paths**: $0.005 per path
- **Wildcard counts as 1 path**

**Examples:**
- `/wms*` = 1 path (FREE if <1,000/month)
- `/ogc/collections/cities/*` = 1 path
- `/ogc/collections/*/tiles/*` = 1 path
- Invalidating 10 specific files = 10 paths

#### Limits

- **Max 3,000 paths per request**
- **Max 15 concurrent invalidations**
- **Wildcard limit**: 15 wildcards per distribution

#### Best Practices

1. Use wildcards to reduce path count
2. Batch invalidations (combine related changes)
3. Schedule during low-traffic periods
4. Monitor invalidation status

### Azure Front Door

#### CLI Commands

**Purge Content:**
```bash
# Purge all
az afd endpoint purge \
  --resource-group honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint \
  --content-paths "/*"

# Purge specific paths
az afd endpoint purge \
  --resource-group honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint \
  --content-paths "/wms/*" "/wmts/*" "/ogc/collections/cities/*"

# Purge specific domains
az afd endpoint purge \
  --resource-group honua-prod \
  --profile-name honua-fd-prod \
  --endpoint-name honua-endpoint \
  --content-paths "/*" \
  --domains "tiles.honua.io"
```

**PowerShell:**
```powershell
# Purge endpoint
Clear-AzFrontDoorCdnEndpointContent `
  -ResourceGroupName "honua-prod" `
  -ProfileName "honua-fd-prod" `
  -EndpointName "honua-endpoint" `
  -ContentPath @("/*")
```

#### REST API

```bash
POST https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Cdn/profiles/{profileName}/endpoints/{endpointName}/purge?api-version=2023-05-01

{
  "contentPaths": [
    "/wms/*",
    "/wmts/*",
    "/ogc/collections/cities/*"
  ]
}
```

#### Terraform

```hcl
resource "null_resource" "purge_frontdoor" {
  triggers = {
    always_run = timestamp()
  }

  provisioner "local-exec" {
    command = <<EOF
      az afd endpoint purge \
        --resource-group ${var.resource_group_name} \
        --profile-name ${azurerm_cdn_frontdoor_profile.honua.name} \
        --endpoint-name ${azurerm_cdn_frontdoor_endpoint.honua.name} \
        --content-paths "/*"
    EOF
  }
}
```

#### Cost

- **Standard Tier**: $0.02 per purge request
- **Premium Tier**: $0.02 per purge request
- **No per-path charges**

**Examples:**
- Purge all (`/*`) = $0.02
- Purge 100 paths = $0.02 (same cost)

#### Limits

- **Max 100 paths per request**
- **No documented request limit**

#### Best Practices

1. Purge is cheaper than CloudFront (flat fee)
2. Can purge multiple paths in one request
3. Use wildcards for efficiency
4. Supports per-domain purging

### Cloudflare

#### CLI Commands

**Purge Everything:**
```bash
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{"purge_everything":true}'
```

**Purge by Files:**
```bash
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "files": [
      "https://tiles.honua.io/wms?LAYERS=cities",
      "https://tiles.honua.io/wmts?LAYER=cities",
      "https://tiles.honua.io/ogc/collections/cities"
    ]
  }'
```

**Purge by Tags:**
```bash
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "tags": ["cities-layer", "vector-tiles"]
  }'
```

**Purge by Host:**
```bash
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "hosts": ["tiles.honua.io"]
  }'
```

**Purge by Prefix (Enterprise):**
```bash
curl -X POST "https://api.cloudflare.com/client/v4/zones/$ZONE_ID/purge_cache" \
  -H "Authorization: Bearer $CF_API_TOKEN" \
  -H "Content-Type: application/json" \
  --data '{
    "prefixes": ["tiles.honua.io/wms", "tiles.honua.io/wmts"]
  }'
```

#### Terraform

```hcl
resource "null_resource" "purge_cloudflare" {
  triggers = {
    always_run = timestamp()
  }

  provisioner "local-exec" {
    command = <<EOF
      curl -X POST "https://api.cloudflare.com/client/v4/zones/${data.cloudflare_zone.honua.id}/purge_cache" \
        -H "Authorization: Bearer ${var.cloudflare_api_token}" \
        -H "Content-Type: application/json" \
        --data '{"purge_everything":true}'
    EOF
  }
}
```

#### Cost

**All plans: FREE (unlimited)**

#### Limits

- **Purge Everything**: 1 per 30 seconds
- **Purge by Files**: Max 30 URLs per request
- **Purge by Tags**: Max 30 tags per request (Enterprise)
- **Purge by Prefix**: Enterprise only

#### Best Practices

1. Free unlimited purging (huge advantage)
2. Use tags for logical grouping (Enterprise)
3. Use prefix purging for efficiency (Enterprise)
4. Rate limit: Wait 30s between "purge everything"

## Automation

### GitHub Actions Workflow

**Invalidate on Deployment:**

```yaml
name: Deploy and Invalidate CDN

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Deploy to Production
        run: |
          # Deploy Honua server
          kubectl apply -f k8s/

      - name: Invalidate CloudFront
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
        run: |
          aws cloudfront create-invalidation \
            --distribution-id ${{ secrets.CLOUDFRONT_DISTRIBUTION_ID }} \
            --paths "/*"

      - name: Invalidate Azure Front Door
        run: |
          az login --service-principal -u ${{ secrets.AZURE_CLIENT_ID }} \
            -p ${{ secrets.AZURE_CLIENT_SECRET }} \
            --tenant ${{ secrets.AZURE_TENANT_ID }}

          az afd endpoint purge \
            --resource-group honua-prod \
            --profile-name honua-fd-prod \
            --endpoint-name honua-endpoint \
            --content-paths "/*"

      - name: Invalidate Cloudflare
        run: |
          curl -X POST "https://api.cloudflare.com/client/v4/zones/${{ secrets.CF_ZONE_ID }}/purge_cache" \
            -H "Authorization: Bearer ${{ secrets.CF_API_TOKEN }}" \
            -H "Content-Type: application/json" \
            --data '{"purge_everything":true}'
```

### Honua CLI Integration

**Automatic Invalidation on Data Ingestion:**

```bash
# Configure CDN invalidation
honua config set cdn.provider cloudfront
honua config set cdn.distributionId E1234567890ABC

# Ingest data with auto-invalidate
honua data ingest --collection cities --file cities.geojson --invalidate-cache

# Manual invalidation
honua cdn invalidate --layer cities
honua cdn invalidate --all
```

**CLI Configuration:**

```json
{
  "cdn": {
    "provider": "cloudfront",
    "distributionId": "E1234567890ABC",
    "autoInvalidate": true,
    "invalidatePatterns": [
      "/ogc/collections/{layerId}/*",
      "/stac/*"
    ]
  }
}
```

### Kubernetes CronJob

**Scheduled Cache Warming:**

```yaml
apiVersion: batch/v1
kind: CronJob
metadata:
  name: cdn-cache-warm
spec:
  schedule: "0 2 * * *" # 2 AM daily
  jobTemplate:
    spec:
      template:
        spec:
          containers:
          - name: cache-warm
            image: honua/cli:latest
            command:
            - /bin/sh
            - -c
            - |
              # Invalidate old cache
              aws cloudfront create-invalidation \
                --distribution-id $CLOUDFRONT_DISTRIBUTION_ID \
                --paths "/*"

              # Wait for invalidation
              sleep 60

              # Preseed new tiles
              honua vector-cache preseed --service-id main --layer-id cities --min-zoom 0 --max-zoom 12
            env:
            - name: AWS_ACCESS_KEY_ID
              valueFrom:
                secretKeyRef:
                  name: aws-credentials
                  key: access-key-id
            - name: AWS_SECRET_ACCESS_KEY
              valueFrom:
                secretKeyRef:
                  name: aws-credentials
                  key: secret-access-key
            - name: CLOUDFRONT_DISTRIBUTION_ID
              value: "E1234567890ABC"
          restartPolicy: OnFailure
```

## Cost Considerations

### Provider Comparison

| Provider | Purge Cost | Paths/Request | Free Tier | Notes |
|----------|------------|---------------|-----------|-------|
| **Cloudflare** | FREE | 30 URLs | Unlimited | Best value |
| **Azure Front Door** | $0.02/request | 100 paths | None | Flat fee |
| **CloudFront** | $0.005/path | 3,000 paths | 1,000/month | Can get expensive |

### Cost Optimization Strategies

**1. Use Versioned URLs (Best)**
- Cost: $0 (no invalidation needed)
- Complexity: Medium (URL rewriting)
- Benefit: Instant updates, immutable caching

**2. Batch Invalidations**
```bash
# BAD: 100 separate invalidations
for layer in $(cat layers.txt); do
  aws cloudfront create-invalidation --distribution-id E123 --paths "/ogc/collections/$layer/*"
done
# Cost: 100 × $0.005 = $0.50 (if >1,000/month)

# GOOD: 1 batched invalidation
aws cloudfront create-invalidation --distribution-id E123 --paths "/ogc/collections/*"
# Cost: 1 × $0.005 = $0.005 (or FREE if <1,000/month)
```

**3. Use Wildcards**
```bash
# BAD: Specific paths
aws cloudfront create-invalidation --distribution-id E123 \
  --paths "/wms?LAYERS=cities" "/wms?LAYERS=roads" "/wms?LAYERS=parcels"
# Cost: 3 paths

# GOOD: Wildcard
aws cloudfront create-invalidation --distribution-id E123 --paths "/wms*"
# Cost: 1 path
```

**4. Schedule During Low Traffic**
- Purge at night (e.g., 2-4 AM)
- Reduces origin load spike
- Better cache repopulation

**5. Use Cloudflare for High-Frequency Purging**
- Unlimited free purging
- Good for development/staging
- Consider multi-CDN strategy

## Testing and Verification

### Verify Invalidation Worked

**1. Check Cache Headers:**
```bash
curl -I https://tiles.honua.io/wms?LAYERS=cities

# Before invalidation:
X-Cache: HIT
Age: 3600

# After invalidation:
X-Cache: MISS
Age: 0
```

**2. Check Invalidation Status:**

**CloudFront:**
```bash
aws cloudfront get-invalidation --distribution-id E123 --id I456
# Status: Completed
```

**Azure:**
```bash
# No status endpoint - purge is near-instant
```

**Cloudflare:**
```bash
# No status endpoint - purge is instant (30s propagation)
```

**3. Test from Multiple Locations:**
```bash
# Use global edge location testing
curl -I -H "CloudFront-Viewer-Country: US" https://tiles.honua.io/wms?LAYERS=cities
curl -I -H "CloudFront-Viewer-Country: JP" https://tiles.honua.io/wms?LAYERS=cities
curl -I -H "CloudFront-Viewer-Country: GB" https://tiles.honua.io/wms?LAYERS=cities
```

### Pre-Production Testing

**Staging Environment:**
```bash
# 1. Deploy to staging
kubectl apply -f k8s/staging/

# 2. Invalidate staging CDN
aws cloudfront create-invalidation --distribution-id E789 --paths "/*"

# 3. Test tile rendering
curl https://staging-tiles.honua.io/wms?LAYERS=cities

# 4. Verify correct tile version
curl -I https://staging-tiles.honua.io/wms?LAYERS=cities | grep ETag
```

## Troubleshooting

### Issue: Invalidation Not Taking Effect

**Symptoms:**
- Old content still being served
- Cache headers show old Age
- ETag unchanged

**Diagnosis:**
```bash
# Check cache status from multiple locations
curl -I https://tiles.honua.io/wms?LAYERS=cities

# Check origin response directly
curl -I --resolve tiles.honua.io:443:$ORIGIN_IP https://tiles.honua.io/wms?LAYERS=cities
```

**Solutions:**

1. **Wait for Propagation:**
   - CloudFront: 5-10 minutes
   - Azure: 1-2 minutes
   - Cloudflare: 30 seconds

2. **Check Invalidation Status:**
   ```bash
   aws cloudfront list-invalidations --distribution-id E123
   ```

3. **Verify Path Matching:**
   ```bash
   # Check if path matches invalidation pattern
   # /wms?LAYERS=cities matches /wms*
   # /WMS?LAYERS=cities does NOT match /wms* (case-sensitive)
   ```

4. **Bypass Cache for Testing:**
   ```bash
   curl -H "Cache-Control: no-cache" https://tiles.honua.io/wms?LAYERS=cities
   ```

### Issue: High Invalidation Costs

**Symptoms:**
- AWS bill shows high CloudFront invalidation charges
- Frequent invalidations in CloudWatch

**Solutions:**

1. **Switch to Versioned URLs:**
   ```
   /v1/tiles/... → /v2/tiles/...
   ```

2. **Batch Invalidations:**
   ```bash
   # Combine related changes into single request
   ```

3. **Use Wildcard Patterns:**
   ```bash
   /ogc/collections/* instead of /ogc/collections/layer1/*, /ogc/collections/layer2/*, ...
   ```

4. **Consider Azure or Cloudflare:**
   - Azure: Flat $0.02/request
   - Cloudflare: FREE unlimited

### Issue: Partial Invalidation

**Symptoms:**
- Some edge locations serve new content
- Others still serve old content

**Diagnosis:**
```bash
# Test from different regions
curl -I --resolve tiles.honua.io:443:$EDGE_IP_US https://tiles.honua.io/wms
curl -I --resolve tiles.honua.io:443:$EDGE_IP_EU https://tiles.honua.io/wms
```

**Solutions:**

1. **Wait Longer:**
   - Full global propagation: 15-30 minutes

2. **Force Full Invalidation:**
   ```bash
   aws cloudfront create-invalidation --distribution-id E123 --paths "/*"
   ```

3. **Check Edge Location Health:**
   - CloudFront: Check CloudWatch metrics
   - Azure: Check diagnostic logs
   - Cloudflare: Check dashboard

## Related Documentation

- [CDN Caching Policies](./CDN_CACHING_POLICIES.md)
- [CDN Deployment Guide](./CDN_DEPLOYMENT_GUIDE.md)
- [CDN Integration](../CDN_INTEGRATION.md)
- [Monitoring and Observability](../observability/README.md)
