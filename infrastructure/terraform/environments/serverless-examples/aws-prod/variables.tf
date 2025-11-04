# Variables for AWS Production Environment

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "container_image_uri" {
  description = "Lambda container image URI"
  type        = string
}

variable "ssl_certificate_arn" {
  description = "ACM certificate ARN for ALB"
  type        = string
}

variable "cloudfront_certificate_arn" {
  description = "ACM certificate ARN for CloudFront (must be in us-east-1)"
  type        = string
}

variable "custom_domains" {
  description = "Custom domains"
  type        = list(string)
}

variable "cors_origins" {
  description = "CORS origins"
  type        = list(string)
  default     = []
}

variable "alb_logs_bucket" {
  description = "S3 bucket for ALB logs"
  type        = string
}

variable "cloudfront_logs_bucket" {
  description = "S3 bucket for CloudFront logs"
  type        = string
}

variable "waf_web_acl_arn" {
  description = "WAF Web ACL ARN"
  type        = string
  default     = ""
}
