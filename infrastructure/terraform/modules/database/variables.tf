# Variables for Database Module

variable "cloud_provider" {
  description = "Cloud provider (aws, azure, gcp)"
  type        = string
  validation {
    condition     = contains(["aws", "azure", "gcp"], var.cloud_provider)
    error_message = "Cloud provider must be aws, azure, or gcp."
  }
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "db_name" {
  description = "Database instance name"
  type        = string
  default     = "honua"
}

variable "database_name" {
  description = "Name of the database to create"
  type        = string
  default     = "honua_db"
}

variable "master_username" {
  description = "Master username for database"
  type        = string
  default     = "honua_admin"
}

variable "postgres_version" {
  description = "PostgreSQL version"
  type        = string
  default     = "15"
}

# Instance sizing
variable "db_instance_class" {
  description = "Database instance class"
  type        = string
  default     = "db.t4g.large"
}

variable "replica_instance_class" {
  description = "Read replica instance class"
  type        = string
  default     = "db.t4g.large"
}

variable "allocated_storage" {
  description = "Allocated storage in GB"
  type        = number
  default     = 100
}

variable "max_allocated_storage" {
  description = "Maximum allocated storage for autoscaling in GB"
  type        = number
  default     = 500
}

# Backup and HA
variable "backup_retention_days" {
  description = "Number of days to retain backups"
  type        = number
  default     = 7
}

variable "read_replica_count" {
  description = "Number of read replicas"
  type        = number
  default     = 2
}

# AWS-specific
variable "vpc_id" {
  description = "VPC ID"
  type        = string
  default     = ""
}

variable "vpc_cidr" {
  description = "VPC CIDR block"
  type        = string
  default     = ""
}

variable "subnet_ids" {
  description = "Subnet IDs for database"
  type        = list(string)
  default     = []
}

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

variable "private_dns_zone_id" {
  description = "Private DNS zone ID for Azure"
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

# Features
variable "enable_pgbouncer" {
  description = "Enable PgBouncer connection pooling"
  type        = bool
  default     = true
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default     = {}
}
