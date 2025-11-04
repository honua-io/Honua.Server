# ============================================================================
# AWS CloudFront CDN Configuration for Honua Server
# ============================================================================
# CloudFront distribution for caching tiles, metadata, and static assets.
# Features:
#   - Multi-origin support (ALB, S3 raster cache)
#   - Optimized caching policies for tiles vs metadata
#   - DDoS protection via AWS Shield Standard
#   - SSL/TLS with ACM certificates
#   - Custom domain support
#   - Geographic restrictions (optional)
#   - Real-time logs and monitoring
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "domain_name" {
  description = "Custom domain name for CloudFront distribution (e.g., tiles.honua.io)"
  type        = string
  default     = ""
}

variable "origin_domain_name" {
  description = "Origin domain name (ALB or Honua server endpoint)"
  type        = string
}

variable "s3_raster_cache_bucket" {
  description = "S3 bucket name for raster tile cache (optional)"
  type        = string
  default     = ""
}

variable "price_class" {
  description = "CloudFront price class (PriceClass_All, PriceClass_200, PriceClass_100)"
  type        = string
  default     = "PriceClass_100" # US, Canada, Europe
}

variable "enable_geo_restriction" {
  description = "Enable geographic restrictions"
  type        = bool
  default     = false
}

variable "allowed_countries" {
  description = "List of allowed country codes (ISO 3166-1 alpha-2)"
  type        = list(string)
  default     = ["US", "CA", "MX", "GB", "DE", "FR", "JP", "AU"]
}

variable "enable_waf" {
  description = "Enable AWS WAF web ACL"
  type        = bool
  default     = false
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  distribution_name = "honua-cdn-${var.environment}"

  tags = {
    Environment = var.environment
    Project     = "HonuaIO"
    ManagedBy   = "Terraform"
    Component   = "CDN"
  }

  # Origin IDs
  origin_honua_server = "honua-server"
  origin_s3_cache     = "s3-raster-cache"
}

# ============================================================================
# CloudFront Origin Access Identity (for S3)
# ============================================================================

resource "aws_cloudfront_origin_access_identity" "s3_oai" {
  count   = var.s3_raster_cache_bucket != "" ? 1 : 0
  comment = "OAI for Honua raster cache S3 bucket"
}

# ============================================================================
# Cache Policies
# ============================================================================

# Tile Cache Policy - Long-lived, optimized for tiles
resource "aws_cloudfront_cache_policy" "tiles" {
  name        = "honua-tiles-cache-policy-${var.environment}"
  comment     = "Cache policy for map tiles (WMS, WMTS, OGC Tiles)"
  default_ttl = 86400    # 1 day
  max_ttl     = 31536000 # 1 year
  min_ttl     = 0

  parameters_in_cache_key_and_forwarded_to_origin {
    cookies_config {
      cookie_behavior = "none"
    }

    headers_config {
      header_behavior = "whitelist"
      headers {
        items = ["Accept-Encoding"]
      }
    }

    query_strings_config {
      query_string_behavior = "whitelist"
      query_strings {
        # Forward tile-specific parameters
        items = [
          "TIME",           # Temporal dimension
          "datetime",       # OGC API temporal
          "LAYERS",         # WMS layers
          "STYLES",         # WMS styles
          "FORMAT",         # Image format
          "CRS",            # Coordinate system
          "BBOX",           # Bounding box
          "WIDTH",          # Image width
          "HEIGHT",         # Image height
          "TILEMATRIX",     # WMTS tile matrix
          "TILEMATRIXSET",  # WMTS tile matrix set
          "TILEROW",        # WMTS row
          "TILECOL",        # WMTS column
          "f",              # OGC API format
          "styleId"         # Custom styles
        ]
      }
    }

    enable_accept_encoding_gzip   = true
    enable_accept_encoding_brotli = true
  }
}

# Metadata Cache Policy - Shorter TTL, more dynamic
resource "aws_cloudfront_cache_policy" "metadata" {
  name        = "honua-metadata-cache-policy-${var.environment}"
  comment     = "Cache policy for metadata endpoints (GetCapabilities, collections, etc.)"
  default_ttl = 300   # 5 minutes
  max_ttl     = 3600  # 1 hour
  min_ttl     = 0

  parameters_in_cache_key_and_forwarded_to_origin {
    cookies_config {
      cookie_behavior = "none"
    }

    headers_config {
      header_behavior = "whitelist"
      headers {
        items = ["Accept", "Accept-Encoding"]
      }
    }

    query_strings_config {
      query_string_behavior = "whitelist"
      query_strings {
        items = [
          "SERVICE",
          "VERSION",
          "REQUEST",
          "f",
          "limit",
          "offset"
        ]
      }
    }

    enable_accept_encoding_gzip   = true
    enable_accept_encoding_brotli = true
  }
}

# No-Cache Policy - For authenticated/admin endpoints
resource "aws_cloudfront_cache_policy" "no_cache" {
  name        = "honua-no-cache-policy-${var.environment}"
  comment     = "No caching for admin and authenticated endpoints"
  default_ttl = 0
  max_ttl     = 0
  min_ttl     = 0

  parameters_in_cache_key_and_forwarded_to_origin {
    cookies_config {
      cookie_behavior = "all"
    }

    headers_config {
      header_behavior = "whitelist"
      headers {
        items = [
          "Authorization",
          "Accept",
          "Accept-Encoding"
        ]
      }
    }

    query_strings_config {
      query_string_behavior = "all"
    }

    enable_accept_encoding_gzip   = true
    enable_accept_encoding_brotli = false
  }
}

# ============================================================================
# Origin Request Policy
# ============================================================================

resource "aws_cloudfront_origin_request_policy" "honua_origin" {
  name    = "honua-origin-request-policy-${var.environment}"
  comment = "Forward necessary headers to Honua origin"

  cookies_config {
    cookie_behavior = "none"
  }

  headers_config {
    header_behavior = "whitelist"
    headers {
      items = [
        "Accept",
        "Accept-Encoding",
        "CloudFront-Viewer-Country",
        "CloudFront-Viewer-Country-Region"
      ]
    }
  }

  query_strings_config {
    query_string_behavior = "all"
  }
}

# ============================================================================
# Response Headers Policy
# ============================================================================

resource "aws_cloudfront_response_headers_policy" "security_headers" {
  name    = "honua-security-headers-${var.environment}"
  comment = "Security headers for Honua CDN"

  cors_config {
    access_control_allow_credentials = false

    access_control_allow_headers {
      items = ["*"]
    }

    access_control_allow_methods {
      items = ["GET", "HEAD", "OPTIONS"]
    }

    access_control_allow_origins {
      items = ["*"]
    }

    access_control_max_age_sec = 600
    origin_override            = false
  }

  security_headers_config {
    strict_transport_security {
      access_control_max_age_sec = 31536000
      include_subdomains         = true
      preload                    = true
      override                   = true
    }

    content_type_options {
      override = true
    }

    frame_options {
      frame_option = "SAMEORIGIN"
      override     = true
    }

    xss_protection {
      mode_block = true
      protection = true
      override   = true
    }

    referrer_policy {
      referrer_policy = "strict-origin-when-cross-origin"
      override        = true
    }
  }

  custom_headers_config {
    items {
      header   = "X-CDN-Provider"
      value    = "CloudFront"
      override = false
    }

    items {
      header   = "X-Honua-Version"
      value    = "1.0"
      override = false
    }
  }
}

# ============================================================================
# ACM Certificate (must be in us-east-1 for CloudFront)
# ============================================================================

resource "aws_acm_certificate" "cdn" {
  count             = var.domain_name != "" ? 1 : 0
  provider          = aws.us_east_1
  domain_name       = var.domain_name
  validation_method = "DNS"

  subject_alternative_names = [
    "*.${var.domain_name}"
  ]

  lifecycle {
    create_before_destroy = true
  }

  tags = local.tags
}

# ============================================================================
# CloudFront Distribution
# ============================================================================

resource "aws_cloudfront_distribution" "honua_cdn" {
  enabled             = true
  is_ipv6_enabled     = true
  comment             = "Honua Server CDN - ${var.environment}"
  default_root_object = ""
  price_class         = var.price_class
  web_acl_id          = var.enable_waf ? aws_wafv2_web_acl.cdn_waf[0].arn : null

  aliases = var.domain_name != "" ? [var.domain_name] : []

  # ============================================================================
  # Origin: Honua Server (ALB or direct)
  # ============================================================================
  origin {
    domain_name = var.origin_domain_name
    origin_id   = local.origin_honua_server

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
      origin_read_timeout    = 60
      origin_keepalive_timeout = 5
    }

    custom_header {
      name  = "X-Origin-Verify"
      value = random_password.origin_verify.result
    }
  }

  # ============================================================================
  # Origin: S3 Raster Cache (optional)
  # ============================================================================
  dynamic "origin" {
    for_each = var.s3_raster_cache_bucket != "" ? [1] : []
    content {
      domain_name = "${var.s3_raster_cache_bucket}.s3.amazonaws.com"
      origin_id   = local.origin_s3_cache

      s3_origin_config {
        origin_access_identity = aws_cloudfront_origin_access_identity.s3_oai[0].cloudfront_access_identity_path
      }
    }
  }

  # ============================================================================
  # Default Cache Behavior (Tiles)
  # ============================================================================
  default_cache_behavior {
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.tiles.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # ============================================================================
  # Ordered Cache Behaviors
  # ============================================================================

  # WMS Tiles - Long cache
  ordered_cache_behavior {
    path_pattern           = "/wms*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.tiles.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # WMTS Tiles - Long cache
  ordered_cache_behavior {
    path_pattern           = "/wmts*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.tiles.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # OGC API Tiles - Long cache
  ordered_cache_behavior {
    path_pattern           = "/ogc/collections/*/tiles/*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.tiles.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # STAC API - Metadata cache
  ordered_cache_behavior {
    path_pattern           = "/stac*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.metadata.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # OGC API Collections - Metadata cache
  ordered_cache_behavior {
    path_pattern           = "/ogc/collections*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    cache_policy_id            = aws_cloudfront_cache_policy.metadata.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # Admin endpoints - No cache
  ordered_cache_behavior {
    path_pattern           = "/admin/*"
    target_origin_id       = local.origin_honua_server
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = []
    compress               = false

    cache_policy_id            = aws_cloudfront_cache_policy.no_cache.id
    origin_request_policy_id   = aws_cloudfront_origin_request_policy.honua_origin.id
    response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
  }

  # S3 Raster Cache - Direct from S3 (if configured)
  dynamic "ordered_cache_behavior" {
    for_each = var.s3_raster_cache_bucket != "" ? [1] : []
    content {
      path_pattern           = "/raster-cache/*"
      target_origin_id       = local.origin_s3_cache
      viewer_protocol_policy = "redirect-to-https"
      allowed_methods        = ["GET", "HEAD"]
      cached_methods         = ["GET", "HEAD"]
      compress               = true

      cache_policy_id            = aws_cloudfront_cache_policy.tiles.id
      response_headers_policy_id = aws_cloudfront_response_headers_policy.security_headers.id
    }
  }

  # ============================================================================
  # Geographic Restrictions
  # ============================================================================
  restrictions {
    geo_restriction {
      restriction_type = var.enable_geo_restriction ? "whitelist" : "none"
      locations        = var.enable_geo_restriction ? var.allowed_countries : []
    }
  }

  # ============================================================================
  # SSL/TLS Configuration
  # ============================================================================
  viewer_certificate {
    cloudfront_default_certificate = var.domain_name == ""
    acm_certificate_arn            = var.domain_name != "" ? aws_acm_certificate.cdn[0].arn : null
    ssl_support_method             = var.domain_name != "" ? "sni-only" : null
    minimum_protocol_version       = "TLSv1.2_2021"
  }

  # ============================================================================
  # Logging
  # ============================================================================
  logging_config {
    include_cookies = false
    bucket          = aws_s3_bucket.cloudfront_logs.bucket_domain_name
    prefix          = "cloudfront/${var.environment}/"
  }

  tags = local.tags
}

# ============================================================================
# CloudFront Logging Bucket
# ============================================================================

resource "aws_s3_bucket" "cloudfront_logs" {
  bucket = "honua-cloudfront-logs-${var.environment}-${data.aws_caller_identity.current.account_id}"

  tags = local.tags
}

resource "aws_s3_bucket_ownership_controls" "cloudfront_logs" {
  bucket = aws_s3_bucket.cloudfront_logs.id

  rule {
    object_ownership = "BucketOwnerPreferred"
  }
}

resource "aws_s3_bucket_acl" "cloudfront_logs" {
  depends_on = [aws_s3_bucket_ownership_controls.cloudfront_logs]

  bucket = aws_s3_bucket.cloudfront_logs.id
  acl    = "log-delivery-write"
}

resource "aws_s3_bucket_lifecycle_configuration" "cloudfront_logs" {
  bucket = aws_s3_bucket.cloudfront_logs.id

  rule {
    id     = "delete-old-logs"
    status = "Enabled"

    expiration {
      days = 90
    }

    transition {
      days          = 30
      storage_class = "STANDARD_IA"
    }
  }
}

# ============================================================================
# AWS WAF (Optional)
# ============================================================================

resource "aws_wafv2_web_acl" "cdn_waf" {
  count       = var.enable_waf ? 1 : 0
  name        = "honua-cdn-waf-${var.environment}"
  description = "WAF rules for Honua CDN"
  scope       = "CLOUDFRONT"

  default_action {
    allow {}
  }

  # Rate limiting rule
  rule {
    name     = "rate-limit"
    priority = 1

    action {
      block {}
    }

    statement {
      rate_based_statement {
        limit              = 2000
        aggregate_key_type = "IP"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "RateLimitRule"
      sampled_requests_enabled   = true
    }
  }

  # AWS Managed Rules - Common Rule Set
  rule {
    name     = "aws-managed-rules-common"
    priority = 2

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        vendor_name = "AWS"
        name        = "AWSManagedRulesCommonRuleSet"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWSManagedRulesCommon"
      sampled_requests_enabled   = true
    }
  }

  # AWS Managed Rules - Known Bad Inputs
  rule {
    name     = "aws-managed-rules-known-bad-inputs"
    priority = 3

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        vendor_name = "AWS"
        name        = "AWSManagedRulesKnownBadInputsRuleSet"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "AWSManagedRulesKnownBadInputs"
      sampled_requests_enabled   = true
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "HonuaCDNWAF"
    sampled_requests_enabled   = true
  }

  tags = local.tags
}

# ============================================================================
# Origin Verification Secret
# ============================================================================

resource "random_password" "origin_verify" {
  length  = 32
  special = true
}

# ============================================================================
# Data Sources
# ============================================================================

data "aws_caller_identity" "current" {}

# ============================================================================
# Outputs
# ============================================================================

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID"
  value       = aws_cloudfront_distribution.honua_cdn.id
}

output "cloudfront_distribution_arn" {
  description = "CloudFront distribution ARN"
  value       = aws_cloudfront_distribution.honua_cdn.arn
}

output "cloudfront_domain_name" {
  description = "CloudFront distribution domain name"
  value       = aws_cloudfront_distribution.honua_cdn.domain_name
}

output "cloudfront_hosted_zone_id" {
  description = "CloudFront hosted zone ID for Route53 alias"
  value       = aws_cloudfront_distribution.honua_cdn.hosted_zone_id
}

output "custom_domain" {
  description = "Custom domain name (if configured)"
  value       = var.domain_name
}

output "origin_verify_header" {
  description = "Origin verification header value"
  value       = random_password.origin_verify.result
  sensitive   = true
}

output "cache_invalidation_command" {
  description = "AWS CLI command to invalidate cache"
  value       = "aws cloudfront create-invalidation --distribution-id ${aws_cloudfront_distribution.honua_cdn.id} --paths '/*'"
}

output "waf_web_acl_arn" {
  description = "WAF Web ACL ARN"
  value       = var.enable_waf ? aws_wafv2_web_acl.cdn_waf[0].arn : null
}

output "deployment_summary" {
  description = "CDN deployment summary"
  value = {
    distribution_id  = aws_cloudfront_distribution.honua_cdn.id
    domain_name      = aws_cloudfront_distribution.honua_cdn.domain_name
    custom_domain    = var.domain_name
    environment      = var.environment
    price_class      = var.price_class
    waf_enabled      = var.enable_waf
    geo_restrictions = var.enable_geo_restriction
  }
}
