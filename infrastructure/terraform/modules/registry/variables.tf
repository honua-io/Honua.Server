# Variables for Registry Module

variable "cloud_provider" {
  description = "Cloud provider (aws, azure, gcp)"
  type        = string
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
}

variable "registry_prefix" {
  description = "Prefix for registry names"
  type        = string
  default     = "honua-"
}

variable "repository_names" {
  description = "List of repository names to create"
  type        = list(string)
  default = [
    "orchestrator",
    "agent",
    "api-server",
    "web-ui",
    "build-worker",
    "monitor"
  ]
}

# Lifecycle policies
variable "image_retention_count" {
  description = "Number of images to retain"
  type        = number
  default     = 30
}

variable "untagged_retention_days" {
  description = "Days to retain untagged images"
  type        = number
  default     = 7
}

# Customer access
variable "customer_account_ids" {
  description = "AWS account IDs for customer access"
  type        = list(string)
  default     = []
}

variable "customer_service_accounts" {
  description = "GCP service accounts for customer access"
  type        = list(string)
  default     = []
}

variable "enable_customer_registries" {
  description = "Create customer-specific registries"
  type        = bool
  default     = false
}

variable "customer_repositories" {
  description = "Map of customer repository names to account IDs"
  type        = map(string)
  default     = {}
}

# Replication
variable "enable_replication" {
  description = "Enable cross-region replication"
  type        = bool
  default     = true
}

variable "replication_regions" {
  description = "Regions for replication"
  type        = list(string)
  default     = []
}

# AWS-specific
variable "kms_key_arn" {
  description = "KMS key ARN for encryption"
  type        = string
  default     = ""
}

# Azure-specific
variable "resource_group_name" {
  description = "Azure resource group name"
  type        = string
  default     = ""
}

variable "azure_location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

variable "azure_acr_sku" {
  description = "Azure ACR SKU (Basic, Standard, Premium)"
  type        = string
  default     = "Premium"
}

variable "azure_key_vault_key_id" {
  description = "Azure Key Vault key ID for encryption"
  type        = string
  default     = ""
}

variable "azure_replication_location" {
  description = "Azure replication location"
  type        = string
  default     = "westus2"
}

variable "allowed_ip_ranges" {
  description = "Allowed IP ranges for registry access"
  type        = list(string)
  default     = []
}

variable "subnet_ids" {
  description = "Subnet IDs for private access"
  type        = list(string)
  default     = []
}

# GCP-specific
variable "gcp_project_id" {
  description = "GCP project ID"
  type        = string
  default     = ""
}

variable "gcp_region" {
  description = "GCP region"
  type        = string
  default     = "us-central1"
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default     = {}
}
