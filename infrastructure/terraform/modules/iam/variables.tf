# Variables for IAM Module

variable "cloud_provider" {
  description = "Cloud provider (aws, azure, gcp)"
  type        = string
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
}

variable "service_name" {
  description = "Service name"
  type        = string
  default     = "honua"
}

variable "cluster_name" {
  description = "Kubernetes cluster name"
  type        = string
  default     = "honua"
}

# Kubernetes Service Accounts
variable "k8s_service_accounts" {
  description = "Map of Kubernetes service accounts to create IAM roles for"
  type = map(object({
    namespace       = string
    service_account = string
    policy          = string
  }))
  default = {}
}

# Customer IAM
variable "enable_customer_iam" {
  description = "Enable customer IAM resources"
  type        = bool
  default     = false
}

variable "customer_users" {
  description = "Map of customer usernames to customer IDs"
  type        = map(string)
  default     = {}
}

# AWS-specific
variable "kms_key_arn" {
  description = "KMS key ARN"
  type        = string
  default     = ""
}

variable "oidc_provider_arn" {
  description = "OIDC provider ARN for IRSA"
  type        = string
  default     = ""
}

variable "enable_github_oidc" {
  description = "Enable GitHub Actions OIDC"
  type        = bool
  default     = false
}

variable "github_org" {
  description = "GitHub organization"
  type        = string
  default     = ""
}

variable "github_repo" {
  description = "GitHub repository"
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

variable "acr_id" {
  description = "Azure Container Registry ID"
  type        = string
  default     = ""
}

variable "key_vault_id" {
  description = "Azure Key Vault ID"
  type        = string
  default     = ""
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
