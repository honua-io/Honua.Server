# Variables for Monitoring Module

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

# Prometheus Configuration
variable "prometheus_retention" {
  description = "Prometheus data retention period"
  type        = string
  default     = "30d"
}

variable "prometheus_storage_size" {
  description = "Prometheus storage size"
  type        = string
  default     = "50Gi"
}

variable "storage_class" {
  description = "Storage class for persistent volumes"
  type        = string
  default     = "gp3"
}

variable "grafana_admin_password" {
  description = "Grafana admin password"
  type        = string
  sensitive   = true
}

# Logging
variable "log_retention_days" {
  description = "Log retention period in days"
  type        = number
  default     = 30
}

# Alerting
variable "alarm_emails" {
  description = "List of email addresses for alarms"
  type        = list(string)
  default     = []
}

variable "create_sns_topic" {
  description = "Create SNS topic for alarms (AWS)"
  type        = bool
  default     = true
}

variable "sns_topic_arn" {
  description = "Existing SNS topic ARN (AWS)"
  type        = string
  default     = ""
}

# Budget Alerts
variable "enable_budget_alerts" {
  description = "Enable budget alerts"
  type        = bool
  default     = true
}

variable "monthly_budget_limit" {
  description = "Monthly budget limit in USD"
  type        = number
  default     = 5000
}

# AWS-specific
variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
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

variable "resource_group_id" {
  description = "Azure resource group ID for budget"
  type        = string
  default     = ""
}

variable "aks_cluster_id" {
  description = "AKS cluster ID for monitoring"
  type        = string
  default     = ""
}

# GCP-specific
variable "gcp_project_id" {
  description = "GCP project ID"
  type        = string
  default     = ""
}

variable "gcp_billing_account" {
  description = "GCP billing account ID"
  type        = string
  default     = ""
}

variable "api_endpoint" {
  description = "API endpoint for uptime checks"
  type        = string
  default     = ""
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default     = {}
}
