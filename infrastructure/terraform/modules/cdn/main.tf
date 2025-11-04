# Cloud-Agnostic CDN Module for Honua GIS Platform
# Supports AWS CloudFront, Google Cloud CDN, and Azure Front Door

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
      configuration_aliases = [aws]
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
      configuration_aliases = [google]
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
      configuration_aliases = [azurerm]
    }
  }
}

# ==================== Local Variables ====================
locals {
  cdn_name = "${var.service_name}-${var.environment}-cdn"

  # Common cache behaviors for GIS content
  cache_policies = {
    tiles = {
      ttl         = var.tiles_ttl
      min_ttl     = var.tiles_min_ttl
      max_ttl     = var.tiles_max_ttl
      description = "Cache policy for raster/vector tiles"
    }
    features = {
      ttl         = var.features_ttl
      min_ttl     = var.features_min_ttl
      max_ttl     = var.features_max_ttl
      description = "Cache policy for feature data (GeoJSON, etc.)"
    }
    metadata = {
      ttl         = var.metadata_ttl
      min_ttl     = var.metadata_min_ttl
      max_ttl     = var.metadata_max_ttl
      description = "Cache policy for layer metadata and capabilities"
    }
  }

  common_tags = merge(
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
      Application = "Honua"
      Component   = "CDN"
    },
    var.tags
  )
}

# ==================== AWS CloudFront ====================
resource "aws_cloudfront_distribution" "honua" {
  count   = var.cloud_provider == "aws" ? 1 : 0
  enabled = true
  comment = "Honua GIS CDN - ${var.environment}"

  origin {
    domain_name = var.origin_domain
    origin_id   = "honua-origin"

    dynamic "custom_origin_config" {
      for_each = var.origin_type == "custom" ? [1] : []
      content {
        http_port              = 80
        https_port             = 443
        origin_protocol_policy = var.origin_protocol_policy
        origin_ssl_protocols   = ["TLSv1.2"]
      }
    }

    dynamic "s3_origin_config" {
      for_each = var.origin_type == "s3" ? [1] : []
      content {
        origin_access_identity = aws_cloudfront_origin_access_identity.honua[0].cloudfront_access_identity_path
      }
    }

    origin_shield {
      enabled              = var.enable_origin_shield
      origin_shield_region = var.origin_shield_region != "" ? var.origin_shield_region : var.aws_region
    }
  }

  # Default cache behavior (for API endpoints)
  default_cache_behavior {
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD", "OPTIONS"]
    target_origin_id       = "honua-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    forwarded_values {
      query_string = true
      headers      = var.forwarded_headers

      cookies {
        forward = "none"
      }
    }

    min_ttl     = 0
    default_ttl = 300  # 5 minutes for general API
    max_ttl     = 3600
  }

  # Cache behavior for tiles (e.g., /tiles/*)
  ordered_cache_behavior {
    path_pattern           = "/tiles/*"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "honua-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    forwarded_values {
      query_string = true
      query_string_cache_keys = ["bbox", "width", "height", "layers", "srs", "crs", "format", "time"]

      cookies {
        forward = "none"
      }
    }

    min_ttl     = local.cache_policies.tiles.min_ttl
    default_ttl = local.cache_policies.tiles.ttl
    max_ttl     = local.cache_policies.tiles.max_ttl
  }

  # Cache behavior for WMS/WMTS (e.g., /wms, /wmts/*)
  ordered_cache_behavior {
    path_pattern           = "/wms*"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "honua-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    forwarded_values {
      query_string = true
      query_string_cache_keys = ["bbox", "width", "height", "layers", "srs", "crs", "format", "request", "service", "version"]

      cookies {
        forward = "none"
      }
    }

    min_ttl     = local.cache_policies.tiles.min_ttl
    default_ttl = local.cache_policies.tiles.ttl
    max_ttl     = local.cache_policies.tiles.max_ttl
  }

  # Cache behavior for features (e.g., /features/*)
  ordered_cache_behavior {
    path_pattern           = "/features/*"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "honua-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    forwarded_values {
      query_string = true
      query_string_cache_keys = ["bbox", "limit", "offset", "filter", "crs"]

      cookies {
        forward = "none"
      }
    }

    min_ttl     = local.cache_policies.features.min_ttl
    default_ttl = local.cache_policies.features.ttl
    max_ttl     = local.cache_policies.features.max_ttl
  }

  # Cache behavior for metadata (e.g., /collections, /api)
  ordered_cache_behavior {
    path_pattern           = "/collections*"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    target_origin_id       = "honua-origin"
    viewer_protocol_policy = "redirect-to-https"
    compress               = true

    forwarded_values {
      query_string = true

      cookies {
        forward = "none"
      }
    }

    min_ttl     = local.cache_policies.metadata.min_ttl
    default_ttl = local.cache_policies.metadata.ttl
    max_ttl     = local.cache_policies.metadata.max_ttl
  }

  restrictions {
    geo_restriction {
      restriction_type = var.geo_restriction_type
      locations        = var.geo_restriction_locations
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = length(var.custom_domains) == 0
    acm_certificate_arn           = length(var.custom_domains) > 0 ? var.ssl_certificate_arn : null
    ssl_support_method            = length(var.custom_domains) > 0 ? "sni-only" : null
    minimum_protocol_version      = "TLSv1.2_2021"
  }

  aliases = var.custom_domains

  price_class = var.cloudfront_price_class

  logging_config {
    include_cookies = false
    bucket          = var.cloudfront_logging_bucket
    prefix          = "${var.service_name}-${var.environment}/"
  }

  web_acl_id = var.waf_web_acl_arn

  tags = local.common_tags
}

# Origin Access Identity for S3
resource "aws_cloudfront_origin_access_identity" "honua" {
  count   = var.cloud_provider == "aws" && var.origin_type == "s3" ? 1 : 0
  comment = "OAI for Honua ${var.environment}"
}

# ==================== Google Cloud CDN ====================
# Note: Cloud CDN is typically configured as part of the Load Balancer
# This resource is for reference and would be created in the cloud-run module
# Include here for completeness and documentation

output "gcp_cdn_config" {
  description = "Configuration to enable Cloud CDN in GCP Load Balancer"
  value = var.cloud_provider == "gcp" ? {
    enable_cdn = true
    cdn_policy = {
      cache_mode                   = "CACHE_ALL_STATIC"
      default_ttl                  = local.cache_policies.tiles.ttl
      max_ttl                      = local.cache_policies.tiles.max_ttl
      client_ttl                   = local.cache_policies.tiles.ttl
      negative_caching             = true
      serve_while_stale            = 86400
      cache_key_policy = {
        include_host           = true
        include_protocol       = true
        include_query_string   = true
        query_string_whitelist = ["bbox", "width", "height", "layers", "srs", "crs", "format", "time", "request", "service", "version"]
      }
    }
  } : null
}

# ==================== Azure Front Door ====================
# Note: Azure Front Door is typically created in the container-apps module
# This provides standalone configuration for existing origins

resource "azurerm_cdn_frontdoor_profile" "honua" {
  count               = var.cloud_provider == "azure" && var.create_azure_front_door ? 1 : 0
  name                = local.cdn_name
  resource_group_name = var.azure_resource_group_name
  sku_name            = var.azure_front_door_sku
  tags                = local.common_tags
}

resource "azurerm_cdn_frontdoor_endpoint" "honua" {
  count                    = var.cloud_provider == "azure" && var.create_azure_front_door ? 1 : 0
  name                     = "${var.service_name}-${var.environment}"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id
  tags                     = local.common_tags
}

resource "azurerm_cdn_frontdoor_origin_group" "honua" {
  count                    = var.cloud_provider == "azure" && var.create_azure_front_door ? 1 : 0
  name                     = "${var.service_name}-origin-group"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  health_probe {
    path                = var.health_check_path
    request_type        = "GET"
    protocol            = "Https"
    interval_in_seconds = 30
  }
}

resource "azurerm_cdn_frontdoor_origin" "honua" {
  count                         = var.cloud_provider == "azure" && var.create_azure_front_door ? 1 : 0
  name                          = "${var.service_name}-origin"
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua[0].id
  enabled                       = true

  certificate_name_check_enabled = true
  host_name                      = var.origin_domain
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = var.origin_domain
  priority                       = 1
  weight                         = 1000
}

resource "azurerm_cdn_frontdoor_route" "honua" {
  count                         = var.cloud_provider == "azure" && var.create_azure_front_door ? 1 : 0
  name                          = "${var.service_name}-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua[0].id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.honua[0].id]

  supported_protocols    = ["Http", "Https"]
  patterns_to_match      = ["/*"]
  forwarding_protocol    = "HttpsOnly"
  link_to_default_domain = true
  https_redirect_enabled = true

  cache {
    query_string_caching_behavior = "IncludeSpecifiedQueryStrings"
    query_strings                 = ["bbox", "width", "height", "layers", "srs", "crs", "format", "time", "request", "service", "version", "filter", "limit", "offset"]
    compression_enabled           = true
    content_types_to_compress     = ["application/json", "application/xml", "application/geo+json", "image/png", "image/jpeg", "image/webp"]
  }
}

# Custom domain for Azure Front Door
resource "azurerm_cdn_frontdoor_custom_domain" "honua" {
  count                    = var.cloud_provider == "azure" && var.create_azure_front_door && length(var.custom_domains) > 0 ? length(var.custom_domains) : 0
  name                     = replace(var.custom_domains[count.index], ".", "-")
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id
  host_name                = var.custom_domains[count.index]

  tls {
    certificate_type    = "ManagedCertificate"
    minimum_tls_version = "TLS12"
  }
}
