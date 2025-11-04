# Outputs for Cloud-Agnostic CDN Module

# ==================== AWS CloudFront ====================
output "cloudfront_distribution_id" {
  description = "ID of CloudFront distribution"
  value       = var.cloud_provider == "aws" ? aws_cloudfront_distribution.honua[0].id : null
}

output "cloudfront_distribution_arn" {
  description = "ARN of CloudFront distribution"
  value       = var.cloud_provider == "aws" ? aws_cloudfront_distribution.honua[0].arn : null
}

output "cloudfront_domain_name" {
  description = "Domain name of CloudFront distribution"
  value       = var.cloud_provider == "aws" ? aws_cloudfront_distribution.honua[0].domain_name : null
}

output "cloudfront_hosted_zone_id" {
  description = "Route 53 hosted zone ID of CloudFront distribution"
  value       = var.cloud_provider == "aws" ? aws_cloudfront_distribution.honua[0].hosted_zone_id : null
}

# ==================== Azure Front Door ====================
output "azure_front_door_endpoint_url" {
  description = "Azure Front Door endpoint URL"
  value       = var.cloud_provider == "azure" && var.create_azure_front_door ? "https://${azurerm_cdn_frontdoor_endpoint.honua[0].host_name}" : null
}

output "azure_front_door_profile_id" {
  description = "Azure Front Door profile ID"
  value       = var.cloud_provider == "azure" && var.create_azure_front_door ? azurerm_cdn_frontdoor_profile.honua[0].id : null
}

# ==================== Google Cloud CDN ====================
output "gcp_cdn_configuration" {
  description = "GCP Cloud CDN configuration (apply to backend service)"
  value       = var.cloud_provider == "gcp" ? {
    enable_cdn = true
    cdn_policy = {
      cache_mode                   = "CACHE_ALL_STATIC"
      default_ttl                  = var.tiles_ttl
      max_ttl                      = var.tiles_max_ttl
      client_ttl                   = var.tiles_ttl
      negative_caching             = true
      serve_while_stale            = 86400
      cache_key_policy = {
        include_host           = true
        include_protocol       = true
        include_query_string   = true
        query_string_whitelist = ["bbox", "width", "height", "layers", "srs", "crs", "format", "time"]
      }
    }
  } : null
}

# ==================== Common Outputs ====================
output "cdn_url" {
  description = "CDN URL (cloud-agnostic)"
  value = var.cloud_provider == "aws" ? (
    length(var.custom_domains) > 0 ? "https://${var.custom_domains[0]}" : "https://${aws_cloudfront_distribution.honua[0].domain_name}"
  ) : var.cloud_provider == "azure" && var.create_azure_front_door ? (
    "https://${azurerm_cdn_frontdoor_endpoint.honua[0].host_name}"
  ) : null
}

output "cdn_domain" {
  description = "CDN domain name"
  value = var.cloud_provider == "aws" ? aws_cloudfront_distribution.honua[0].domain_name : (
    var.cloud_provider == "azure" && var.create_azure_front_door ? azurerm_cdn_frontdoor_endpoint.honua[0].host_name : null
  )
}

# ==================== DNS Configuration ====================
output "dns_records_required" {
  description = "DNS records required for custom domains"
  value = length(var.custom_domains) > 0 ? {
    for domain in var.custom_domains :
    domain => var.cloud_provider == "aws" ? {
      type   = "CNAME"
      name   = domain
      value  = aws_cloudfront_distribution.honua[0].domain_name
      note   = "Or use A/AAAA alias record if using Route 53"
    } : var.cloud_provider == "azure" ? {
      type   = "CNAME"
      name   = domain
      value  = var.create_azure_front_door ? azurerm_cdn_frontdoor_endpoint.honua[0].host_name : "N/A"
      note   = "CNAME validation required for Azure Front Door custom domains"
    } : {
      type   = "CNAME"
      name   = domain
      value  = "Configure in Cloud Run / Cloud Load Balancer module"
      note   = "GCP Cloud CDN uses Load Balancer configuration"
    }
  } : null
}

# ==================== Cache Configuration ====================
output "cache_policies" {
  description = "Cache policies applied"
  value = {
    tiles = {
      ttl     = var.tiles_ttl
      min_ttl = var.tiles_min_ttl
      max_ttl = var.tiles_max_ttl
      paths   = ["/tiles/*", "/wms*", "/wmts/*"]
    }
    features = {
      ttl     = var.features_ttl
      min_ttl = var.features_min_ttl
      max_ttl = var.features_max_ttl
      paths   = ["/features/*", "/items/*"]
    }
    metadata = {
      ttl     = var.metadata_ttl
      min_ttl = var.metadata_min_ttl
      max_ttl = var.metadata_max_ttl
      paths   = ["/collections*", "/api*", "/conformance*"]
    }
  }
}

# ==================== Performance Recommendations ====================
output "performance_recommendations" {
  description = "Recommendations for optimizing CDN performance"
  value = {
    compression = "Ensure origin sends appropriate Cache-Control headers"
    query_strings = "Tile requests should include bbox, width, height, layers parameters"
    cache_invalidation = var.cloud_provider == "aws" ? "aws cloudfront create-invalidation --distribution-id ${aws_cloudfront_distribution.honua[0].id} --paths '/*'" : (
      var.cloud_provider == "azure" ? "az afd endpoint purge --resource-group ${var.azure_resource_group_name} --profile-name ${local.cdn_name} --endpoint-name ${var.service_name}-${var.environment} --domains all --content-paths '/*'" : (
        "gcloud compute url-maps invalidate-cdn-cache URL_MAP_NAME --path '/*'"
      )
    )
    monitoring = "Monitor cache hit ratio - target >70% for tiles"
  }
}
