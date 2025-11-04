# Unit Test for CDN Module
# Tests basic Terraform validation with minimal configuration

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# Provider configuration (backend disabled for testing)
provider "aws" {
  region = "us-east-1"

  # Skip credentials for validation-only testing
  skip_credentials_validation = true
  skip_requesting_account_id  = true
  skip_metadata_api_check     = true
}

# Test module with minimal AWS CloudFront configuration
module "cdn_test" {
  source = "../.."

  # Required variables
  cloud_provider = "aws"
  environment    = "dev"
  service_name   = "honua-unit-test"
  origin_domain  = "test.example.com"

  # Cache TTL configuration
  tiles_ttl    = 3600
  features_ttl = 300
  metadata_ttl = 86400

  # Custom domains
  custom_domains = []

  # CloudFront specific
  cloudfront_price_class   = "PriceClass_100"
  cloudfront_logging_bucket = ""
  waf_web_acl_arn          = ""

  # Tags
  tags = {
    test_type = "unit"
    terraform = "true"
  }
}

# Test outputs are generated correctly
output "cdn_url" {
  description = "CDN URL from test module"
  value       = module.cdn_test.cdn_url
}

output "cdn_domain" {
  description = "CDN domain from test module"
  value       = module.cdn_test.cdn_domain
}

output "cloudfront_distribution_id" {
  description = "CloudFront distribution ID"
  value       = module.cdn_test.cloudfront_distribution_id
}

output "cache_policies" {
  description = "Cache policies applied"
  value       = module.cdn_test.cache_policies
}

# Validation tests
output "test_validation" {
  description = "Test validation checks"
  value = {
    provider_aws         = module.cdn_test.cloudfront_distribution_id != null
    cdn_url_generated    = module.cdn_test.cdn_url != null
    cdn_domain_generated = module.cdn_test.cdn_domain != null
    cache_policies_set   = module.cdn_test.cache_policies != null
  }
}
