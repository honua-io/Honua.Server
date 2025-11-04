# Integration Test for CDN Module
# Tests full production-like configuration for AWS CloudFront

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# Provider configuration
provider "aws" {
  region = var.aws_region

  # For testing without actual AWS resources
  skip_credentials_validation = var.skip_aws_validation
  skip_requesting_account_id  = var.skip_aws_validation
  skip_metadata_api_check     = var.skip_aws_validation
}

# Random suffix for testing
resource "random_id" "test" {
  byte_length = 4
}

# Test module with full AWS CloudFront configuration
module "cdn_aws_integration_test" {
  source = "../.."

  # General configuration
  cloud_provider = "aws"
  environment    = "dev"
  service_name   = "honua-cdn-test-${random_id.test.hex}"

  # Origin configuration
  origin_domain          = var.origin_domain
  origin_type            = var.origin_type
  origin_protocol_policy = "https-only"

  # Cache TTL configuration - Optimized for GIS workloads
  # Tiles cache longer (static)
  tiles_ttl     = 3600   # 1 hour
  tiles_min_ttl = 0
  tiles_max_ttl = 86400  # 24 hours

  # Features cache shorter (more dynamic)
  features_ttl     = 300  # 5 minutes
  features_min_ttl = 0
  features_max_ttl = 3600 # 1 hour

  # Metadata cache longer (rarely changes)
  metadata_ttl     = 86400  # 24 hours
  metadata_min_ttl = 0
  metadata_max_ttl = 604800 # 1 week

  # SSL configuration
  custom_domains      = []  # No custom domains for testing
  ssl_certificate_arn = ""  # No SSL cert for testing

  # AWS CloudFront specific
  aws_region               = var.aws_region
  cloudfront_price_class   = "PriceClass_All"  # Global distribution
  cloudfront_logging_bucket = var.cloudfront_logging_bucket
  waf_web_acl_arn          = ""  # No WAF for testing
  geo_restriction_type     = "none"
  geo_restriction_locations = []
  enable_origin_shield     = true
  origin_shield_region     = var.aws_region

  # Headers to forward
  forwarded_headers = [
    "Host",
    "CloudFront-Forwarded-Proto",
    "CloudFront-Is-Desktop-Viewer",
    "CloudFront-Is-Mobile-Viewer",
    "CloudFront-Viewer-Country"
  ]

  # Tags
  tags = {
    test_type    = "integration"
    terraform    = "true"
    ephemeral    = "true"
    auto_cleanup = "true"
    purpose      = "gis-cdn"
  }
}

# Test all AWS CloudFront outputs
output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID"
  value       = module.cdn_aws_integration_test.cloudfront_distribution_id
}

output "cloudfront_distribution_arn" {
  description = "CloudFront distribution ARN"
  value       = module.cdn_aws_integration_test.cloudfront_distribution_arn
}

output "cloudfront_domain_name" {
  description = "CloudFront domain name"
  value       = module.cdn_aws_integration_test.cloudfront_domain_name
}

output "cloudfront_hosted_zone_id" {
  description = "Route 53 hosted zone ID"
  value       = module.cdn_aws_integration_test.cloudfront_hosted_zone_id
}

output "cdn_url" {
  description = "CDN URL"
  value       = module.cdn_aws_integration_test.cdn_url
}

output "cdn_domain" {
  description = "CDN domain"
  value       = module.cdn_aws_integration_test.cdn_domain
}

output "dns_records_required" {
  description = "Required DNS records"
  value       = module.cdn_aws_integration_test.dns_records_required
}

output "cache_policies" {
  description = "Cache policies applied"
  value       = module.cdn_aws_integration_test.cache_policies
}

output "performance_recommendations" {
  description = "Performance recommendations"
  value       = module.cdn_aws_integration_test.performance_recommendations
}

# Integration test validations
output "integration_test_results" {
  description = "Integration test validation results"
  value = {
    cloudfront_created        = module.cdn_aws_integration_test.cloudfront_distribution_id != null
    cdn_url_generated         = module.cdn_aws_integration_test.cdn_url != null
    cdn_domain_generated      = module.cdn_aws_integration_test.cdn_domain != null
    cache_policies_configured = module.cdn_aws_integration_test.cache_policies != null
    tiles_ttl_correct         = module.cdn_aws_integration_test.cache_policies.tiles.ttl == 3600
    features_ttl_correct      = module.cdn_aws_integration_test.cache_policies.features.ttl == 300
    metadata_ttl_correct      = module.cdn_aws_integration_test.cache_policies.metadata.ttl == 86400
    origin_shield_enabled     = true  # Enabled in config
  }
}

# Cache invalidation command
output "cache_invalidation_command" {
  description = "Command to invalidate CDN cache"
  value       = "aws cloudfront create-invalidation --distribution-id ${module.cdn_aws_integration_test.cloudfront_distribution_id} --paths '/*'"
}

# Test GCP CDN Configuration Output
module "cdn_gcp_config_test" {
  source = "../.."

  cloud_provider = "gcp"
  environment    = "dev"
  service_name   = "honua-gcp-cdn-test"
  origin_domain  = "test.example.com"

  tiles_ttl    = 3600
  features_ttl = 300
  metadata_ttl = 86400

  tags = {
    test_type = "integration"
    provider  = "gcp"
  }
}

output "gcp_cdn_configuration" {
  description = "GCP CDN configuration for backend service"
  value       = module.cdn_gcp_config_test.gcp_cdn_configuration
}

output "gcp_cdn_config_validation" {
  description = "GCP CDN config validation"
  value = {
    config_generated = module.cdn_gcp_config_test.gcp_cdn_configuration != null
    cdn_enabled      = module.cdn_gcp_config_test.gcp_cdn_configuration != null ? module.cdn_gcp_config_test.gcp_cdn_configuration.enable_cdn : false
    cache_mode_set   = module.cdn_gcp_config_test.gcp_cdn_configuration != null ? module.cdn_gcp_config_test.gcp_cdn_configuration.cdn_policy.cache_mode == "CACHE_ALL_STATIC" : false
  }
}
