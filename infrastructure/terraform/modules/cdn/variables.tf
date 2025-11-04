# Variables for Cloud-Agnostic CDN Module

# ==================== General Configuration ====================
variable "cloud_provider" {
  description = "Cloud provider (aws, gcp, or azure)"
  type        = string
  validation {
    condition     = contains(["aws", "gcp", "azure"], var.cloud_provider)
    error_message = "cloud_provider must be aws, gcp, or azure."
  }
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
}

variable "service_name" {
  description = "Name of the service"
  type        = string
  default     = "honua"
}

# ==================== Origin Configuration ====================
variable "origin_domain" {
  description = "Origin domain name (e.g., api.example.com or ALB DNS)"
  type        = string
}

variable "origin_type" {
  description = "Origin type (custom or s3 for AWS)"
  type        = string
  default     = "custom"
  validation {
    condition     = contains(["custom", "s3"], var.origin_type)
    error_message = "origin_type must be custom or s3."
  }
}

variable "origin_protocol_policy" {
  description = "Origin protocol policy (http-only, https-only, match-viewer)"
  type        = string
  default     = "https-only"
}

# ==================== Cache TTL Configuration ====================
variable "tiles_ttl" {
  description = "Default TTL for tiles in seconds"
  type        = number
  default     = 3600 # 1 hour
}

variable "tiles_min_ttl" {
  description = "Minimum TTL for tiles in seconds"
  type        = number
  default     = 0
}

variable "tiles_max_ttl" {
  description = "Maximum TTL for tiles in seconds"
  type        = number
  default     = 86400 # 24 hours
}

variable "features_ttl" {
  description = "Default TTL for features in seconds"
  type        = number
  default     = 300 # 5 minutes
}

variable "features_min_ttl" {
  description = "Minimum TTL for features in seconds"
  type        = number
  default     = 0
}

variable "features_max_ttl" {
  description = "Maximum TTL for features in seconds"
  type        = number
  default     = 3600 # 1 hour
}

variable "metadata_ttl" {
  description = "Default TTL for metadata in seconds"
  type        = number
  default     = 86400 # 24 hours
}

variable "metadata_min_ttl" {
  description = "Minimum TTL for metadata in seconds"
  type        = number
  default     = 0
}

variable "metadata_max_ttl" {
  description = "Maximum TTL for metadata in seconds"
  type        = number
  default     = 604800 # 1 week
}

# ==================== SSL Configuration ====================
variable "custom_domains" {
  description = "Custom domains for CDN"
  type        = list(string)
  default     = []
}

variable "ssl_certificate_arn" {
  description = "ARN of SSL certificate (AWS ACM)"
  type        = string
  default     = ""
}

# ==================== AWS CloudFront Specific ====================
variable "aws_region" {
  description = "AWS region for CloudFront"
  type        = string
  default     = "us-east-1"
}

variable "cloudfront_price_class" {
  description = "CloudFront price class"
  type        = string
  default     = "PriceClass_All"
  validation {
    condition     = contains(["PriceClass_100", "PriceClass_200", "PriceClass_All"], var.cloudfront_price_class)
    error_message = "cloudfront_price_class must be PriceClass_100, PriceClass_200, or PriceClass_All."
  }
}

variable "cloudfront_logging_bucket" {
  description = "S3 bucket for CloudFront logs (must end with .s3.amazonaws.com)"
  type        = string
  default     = ""
}

variable "waf_web_acl_arn" {
  description = "ARN of AWS WAF web ACL"
  type        = string
  default     = ""
}

variable "geo_restriction_type" {
  description = "Geo restriction type (none, whitelist, blacklist)"
  type        = string
  default     = "none"
}

variable "geo_restriction_locations" {
  description = "Country codes for geo restriction"
  type        = list(string)
  default     = []
}

variable "enable_origin_shield" {
  description = "Enable CloudFront Origin Shield"
  type        = bool
  default     = false
}

variable "origin_shield_region" {
  description = "AWS region for Origin Shield"
  type        = string
  default     = ""
}

variable "forwarded_headers" {
  description = "Headers to forward to origin"
  type        = list(string)
  default     = ["Host", "CloudFront-Forwarded-Proto", "CloudFront-Is-Desktop-Viewer", "CloudFront-Is-Mobile-Viewer"]
}

# ==================== Azure Front Door Specific ====================
variable "create_azure_front_door" {
  description = "Create Azure Front Door (false if already created in container-apps module)"
  type        = bool
  default     = false
}

variable "azure_resource_group_name" {
  description = "Azure resource group name"
  type        = string
  default     = ""
}

variable "azure_front_door_sku" {
  description = "Azure Front Door SKU"
  type        = string
  default     = "Standard_AzureFrontDoor"
  validation {
    condition     = contains(["Standard_AzureFrontDoor", "Premium_AzureFrontDoor"], var.azure_front_door_sku)
    error_message = "azure_front_door_sku must be Standard_AzureFrontDoor or Premium_AzureFrontDoor."
  }
}

variable "health_check_path" {
  description = "Health check path"
  type        = string
  default     = "/health"
}

# ==================== Tags ====================
variable "tags" {
  description = "Additional tags"
  type        = map(string)
  default     = {}
}
