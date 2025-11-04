# Variables for CDN Integration Tests

variable "aws_region" {
  description = "AWS region for integration testing"
  type        = string
  default     = "us-east-1"
}

variable "origin_domain" {
  description = "Origin domain for testing"
  type        = string
  default     = "test-origin.example.com"
}

variable "origin_type" {
  description = "Origin type for testing"
  type        = string
  default     = "custom"
}

variable "cloudfront_logging_bucket" {
  description = "S3 bucket for CloudFront logs"
  type        = string
  default     = ""  # Empty for testing
}

variable "skip_aws_validation" {
  description = "Skip AWS validation for testing without credentials"
  type        = bool
  default     = true
}
