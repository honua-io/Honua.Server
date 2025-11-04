# ============================================================================
# Cloud-Agnostic External Monitoring Module
# ============================================================================
# This module provides external uptime monitoring across cloud providers.
# It abstracts the differences between:
# - Azure Monitor Availability Tests
# - AWS CloudWatch Synthetics
# - GCP Cloud Monitoring Uptime Checks
#
# Usage:
#   module "external_monitoring" {
#     source = "./modules/external-monitoring"
#
#     cloud_provider = "azure" # or "aws" or "gcp"
#     environment    = "production"
#     app_url        = "https://honua.example.com"
#     ...
#   }
# ============================================================================

terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
      configuration_aliases = [azurerm.main]
    }
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
      configuration_aliases = [aws.main]
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
      configuration_aliases = [google.main]
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "cloud_provider" {
  description = "Cloud provider (azure, aws, gcp)"
  type        = string
  validation {
    condition     = contains(["azure", "aws", "gcp"], var.cloud_provider)
    error_message = "Cloud provider must be one of: azure, aws, gcp"
  }
}

variable "environment" {
  description = "Environment name (production, staging, development)"
  type        = string
}

variable "project_name" {
  description = "Project name"
  type        = string
  default     = "honua"
}

variable "app_url" {
  description = "Application URL to monitor"
  type        = string
}

variable "enable_monitoring" {
  description = "Enable external monitoring"
  type        = bool
  default     = true
}

variable "check_frequency_seconds" {
  description = "Frequency of checks in seconds (300 = 5 min)"
  type        = number
  default     = 300
}

variable "check_timeout_seconds" {
  description = "Timeout for checks in seconds"
  type        = number
  default     = 30
}

variable "alert_email" {
  description = "Email address for alerts"
  type        = string
  default     = null
}

variable "alert_webhook_url" {
  description = "Webhook URL for alerts (Slack, Teams, etc.)"
  type        = string
  default     = null
  sensitive   = true
}

variable "enable_ssl_validation" {
  description = "Enable SSL certificate validation"
  type        = bool
  default     = true
}

variable "ssl_cert_expiry_days" {
  description = "Alert if SSL cert expires within this many days"
  type        = number
  default     = 7
}

variable "multi_region_locations" {
  description = "List of regions to probe from (provider-specific)"
  type        = list(string)
  default     = []
}

# Azure-specific variables
variable "azure_resource_group_name" {
  description = "Azure resource group name"
  type        = string
  default     = null
}

variable "azure_app_insights_id" {
  description = "Azure Application Insights ID"
  type        = string
  default     = null
}

variable "azure_action_group_ids" {
  description = "Azure Monitor action group IDs for alerts"
  type        = map(string)
  default     = {}
}

# AWS-specific variables
variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "aws_sns_topic_arn" {
  description = "AWS SNS topic ARN for alerts"
  type        = string
  default     = null
}

# GCP-specific variables
variable "gcp_project_id" {
  description = "GCP project ID"
  type        = string
  default     = null
}

variable "gcp_notification_channels" {
  description = "GCP notification channel IDs"
  type        = list(string)
  default     = []
}

variable "tags" {
  description = "Additional tags to apply to resources"
  type        = map(string)
  default     = {}
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Normalize app URL
  app_hostname = replace(replace(var.app_url, "https://", ""), "http://", "")
  app_protocol = can(regex("^https://", var.app_url)) ? "https" : "http"

  # Standard endpoints to monitor
  endpoints = {
    liveness = {
      path            = "/healthz/live"
      description     = "Liveness probe - basic availability"
      expected_status = 200
      content_match   = "Healthy"
      severity        = "critical"
      priority        = 1
    }
    readiness = {
      path            = "/healthz/ready"
      description     = "Readiness probe - full stack health"
      expected_status = 200
      content_match   = "Healthy"
      severity        = "error"
      priority        = 2
    }
    ogc_landing = {
      path            = "/ogc"
      description     = "OGC API landing page"
      expected_status = 200
      content_match   = "\"links\""
      severity        = "error"
      priority        = 3
    }
    ogc_conformance = {
      path            = "/ogc/conformance"
      description     = "OGC conformance endpoint"
      expected_status = 200
      content_match   = "\"conformsTo\""
      severity        = "warning"
      priority        = 4
    }
  }

  # Default monitoring regions by cloud provider
  default_regions = {
    azure = [
      "us-va-ash-azr",      # East US
      "emea-nl-ams-azr",    # West Europe
      "apac-sg-sin-azr",    # Southeast Asia
      "apac-au-syd-azr",    # Australia East
      "emea-gb-db3-azr"     # UK South
    ]
    aws = [
      "us-east-1",
      "us-west-1",
      "eu-west-1",
      "ap-southeast-1",
      "ap-northeast-1"
    ]
    gcp = [
      "USA",
      "EUROPE",
      "SOUTH_AMERICA",
      "ASIA_PACIFIC"
    ]
  }

  # Use provided regions or defaults
  monitoring_regions = length(var.multi_region_locations) > 0 ? var.multi_region_locations : local.default_regions[var.cloud_provider]

  common_tags = merge(
    {
      Environment = var.environment
      Project     = var.project_name
      ManagedBy   = "Terraform"
      Component   = "ExternalMonitoring"
    },
    var.tags
  )
}

# ============================================================================
# Azure Monitor Availability Tests
# ============================================================================

# Note: These would be created via separate Azure-specific module
# Placeholder for module structure

# ============================================================================
# AWS CloudWatch Synthetics
# ============================================================================

# Note: These would be created via separate AWS-specific module
# Placeholder for module structure

# ============================================================================
# GCP Cloud Monitoring Uptime Checks
# ============================================================================

# Note: These would be created via separate GCP-specific module
# Placeholder for module structure

# ============================================================================
# Outputs
# ============================================================================

output "monitoring_configuration" {
  value = {
    enabled        = var.enable_monitoring
    cloud_provider = var.cloud_provider
    app_url        = var.app_url
    endpoints      = local.endpoints
    configuration = {
      frequency_seconds = var.check_frequency_seconds
      timeout_seconds   = var.check_timeout_seconds
      ssl_validation    = var.enable_ssl_validation
      cert_expiry_days  = var.ssl_cert_expiry_days
      regions           = local.monitoring_regions
    }
  }
  description = "External monitoring configuration summary"
}

output "endpoints_monitored" {
  value = {
    for key, endpoint in local.endpoints : key => {
      url         = "${var.app_url}${endpoint.path}"
      description = endpoint.description
      severity    = endpoint.severity
    }
  }
  description = "List of monitored endpoints with URLs"
}

output "alert_configuration" {
  value = {
    email_enabled   = var.alert_email != null
    webhook_enabled = var.alert_webhook_url != null
    cloud_specific = {
      azure_action_groups     = var.cloud_provider == "azure" ? var.azure_action_group_ids : null
      aws_sns_topic           = var.cloud_provider == "aws" ? var.aws_sns_topic_arn : null
      gcp_notification_channels = var.cloud_provider == "gcp" ? var.gcp_notification_channels : null
    }
  }
  description = "Alert notification configuration"
  sensitive   = true
}

output "monitoring_dashboard_urls" {
  value = {
    azure = var.cloud_provider == "azure" && var.azure_app_insights_id != null ? "https://portal.azure.com/#@/resource${var.azure_app_insights_id}/availability" : null
    aws   = var.cloud_provider == "aws" ? "https://console.aws.amazon.com/cloudwatch/home?region=${var.aws_region}#synthetics:canary/list" : null
    gcp   = var.cloud_provider == "gcp" && var.gcp_project_id != null ? "https://console.cloud.google.com/monitoring/uptime?project=${var.gcp_project_id}" : null
  }
  description = "Cloud provider monitoring dashboard URLs"
}
