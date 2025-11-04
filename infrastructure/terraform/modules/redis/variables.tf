# Variables for Redis Module

variable "cloud_provider" {
  description = "Cloud provider (aws, azure, gcp)"
  type        = string
}

variable "environment" {
  description = "Environment (dev, staging, production)"
  type        = string
}

variable "redis_name" {
  description = "Redis instance name"
  type        = string
  default     = "honua-redis"
}

variable "redis_version" {
  description = "Redis version"
  type        = string
  default     = "7.0"
}

# AWS-specific
variable "redis_node_type" {
  description = "ElastiCache node type"
  type        = string
  default     = "cache.r7g.large"
}

variable "enable_cluster_mode" {
  description = "Enable Redis cluster mode"
  type        = bool
  default     = true
}

variable "num_cache_nodes" {
  description = "Number of cache nodes (non-cluster mode)"
  type        = number
  default     = 2
}

variable "num_node_groups" {
  description = "Number of node groups (shards) for cluster mode"
  type        = number
  default     = 3
}

variable "replicas_per_node_group" {
  description = "Number of replicas per node group"
  type        = number
  default     = 2
}

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
  description = "Subnet IDs for Redis"
  type        = list(string)
  default     = []
}

variable "kms_key_arn" {
  description = "KMS key ARN for encryption"
  type        = string
  default     = ""
}

variable "backup_retention_days" {
  description = "Number of days to retain backups"
  type        = number
  default     = 7
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

variable "azure_redis_sku" {
  description = "Azure Redis SKU (Basic, Standard, Premium)"
  type        = string
  default     = "Premium"
}

variable "azure_redis_family" {
  description = "Azure Redis family (C, P)"
  type        = string
  default     = "P"
}

variable "azure_redis_capacity" {
  description = "Azure Redis capacity"
  type        = number
  default     = 1
}

variable "azure_storage_connection_string" {
  description = "Azure Storage connection string for backups"
  type        = string
  default     = ""
  sensitive   = true
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

variable "gcp_redis_memory_size" {
  description = "Redis memory size in GB for GCP"
  type        = number
  default     = 5
}

variable "tags" {
  description = "Common tags for all resources"
  type        = map(string)
  default     = {}
}
