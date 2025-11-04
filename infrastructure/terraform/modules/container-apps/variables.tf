# Variables for Azure Container Apps Serverless Module

# ==================== General Configuration ====================
variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "service_name" {
  description = "Name of the service"
  type        = string
  default     = "honua"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "eastus"
}

# ==================== Resource Group ====================
variable "create_resource_group" {
  description = "Create new resource group"
  type        = bool
  default     = true
}

variable "resource_group_name" {
  description = "Name of existing resource group (if not creating new)"
  type        = string
  default     = ""
}

# ==================== Container Configuration ====================
variable "container_image" {
  description = "Full container image path (e.g., myregistry.azurecr.io/honua:latest)"
  type        = string
}

variable "aspnetcore_environment" {
  description = "ASP.NET Core environment (Development, Staging, Production)"
  type        = string
  default     = "Production"
}

variable "container_cpu" {
  description = "CPU cores (0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, or 2)"
  type        = number
  default     = 1
  validation {
    condition     = contains([0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2], var.container_cpu)
    error_message = "container_cpu must be 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, or 2."
  }
}

variable "container_memory" {
  description = "Memory in GB (0.5Gi, 1Gi, 1.5Gi, 2Gi, 3Gi, 3.5Gi, or 4Gi)"
  type        = string
  default     = "2Gi"
}

variable "additional_env_vars" {
  description = "Additional environment variables"
  type        = map(string)
  default     = {}
}

variable "cors_origins" {
  description = "Allowed CORS origins"
  type        = list(string)
  default     = ["*"]
}

variable "gdal_cache_max" {
  description = "GDAL cache size in MB"
  type        = string
  default     = "512"
}

# ==================== Scaling Configuration ====================
variable "min_replicas" {
  description = "Minimum number of replicas (0 for serverless)"
  type        = number
  default     = 0
  validation {
    condition     = var.min_replicas >= 0 && var.min_replicas <= 30
    error_message = "min_replicas must be between 0 and 30."
  }
}

variable "max_replicas" {
  description = "Maximum number of replicas"
  type        = number
  default     = 30
  validation {
    condition     = var.max_replicas >= 1 && var.max_replicas <= 30
    error_message = "max_replicas must be between 1 and 30."
  }
}

variable "scale_concurrent_requests" {
  description = "Concurrent requests threshold for scaling"
  type        = number
  default     = 10
}

# ==================== Virtual Network ====================
variable "create_vnet" {
  description = "Create new virtual network"
  type        = bool
  default     = true
}

variable "vnet_address_space" {
  description = "Address space for virtual network"
  type        = string
  default     = "10.0.0.0/16"
}

variable "container_apps_subnet_id" {
  description = "Existing subnet ID for Container Apps (if not creating VNet)"
  type        = string
  default     = ""
}

variable "postgresql_subnet_id" {
  description = "Existing subnet ID for PostgreSQL (if not creating VNet)"
  type        = string
  default     = ""
}

# ==================== Database Configuration ====================
variable "create_database" {
  description = "Create Azure Database for PostgreSQL"
  type        = bool
  default     = true
}

variable "database_name" {
  description = "Name of the PostgreSQL database"
  type        = string
  default     = "honua"
}

variable "db_username" {
  description = "Database administrator username"
  type        = string
  default     = "honua"
}

variable "postgres_version" {
  description = "PostgreSQL version"
  type        = string
  default     = "15"
  validation {
    condition     = contains(["11", "12", "13", "14", "15", "16"], var.postgres_version)
    error_message = "postgres_version must be 11, 12, 13, 14, 15, or 16."
  }
}

variable "db_sku_name" {
  description = "Database SKU (e.g., B_Standard_B1ms, GP_Standard_D2s_v3)"
  type        = string
  default     = "B_Standard_B1ms"
}

variable "db_storage_mb" {
  description = "Database storage in MB"
  type        = number
  default     = 32768 # 32 GB
}

variable "db_backup_retention_days" {
  description = "Backup retention in days"
  type        = number
  default     = 7
}

variable "db_geo_redundant_backup" {
  description = "Enable geo-redundant backup"
  type        = bool
  default     = false
}

variable "db_high_availability_mode" {
  description = "High availability mode (Disabled, ZoneRedundant, or SameZone)"
  type        = string
  default     = "Disabled"
  validation {
    condition     = contains(["Disabled", "ZoneRedundant", "SameZone"], var.db_high_availability_mode)
    error_message = "db_high_availability_mode must be Disabled, ZoneRedundant, or SameZone."
  }
}

variable "db_standby_zone" {
  description = "Standby availability zone for HA"
  type        = string
  default     = "2"
}

variable "db_max_connections" {
  description = "Maximum database connections"
  type        = string
  default     = "100"
}

variable "db_shared_buffers" {
  description = "Shared buffers configuration"
  type        = string
  default     = "32000"
}

# ==================== Key Vault ====================
variable "create_key_vault" {
  description = "Create Azure Key Vault for secrets"
  type        = bool
  default     = true
}

# ==================== Azure Front Door ====================
variable "create_front_door" {
  description = "Create Azure Front Door (CDN + Global LB)"
  type        = bool
  default     = true
}

variable "front_door_sku" {
  description = "Front Door SKU (Standard_AzureFrontDoor or Premium_AzureFrontDoor)"
  type        = string
  default     = "Standard_AzureFrontDoor"
  validation {
    condition     = contains(["Standard_AzureFrontDoor", "Premium_AzureFrontDoor"], var.front_door_sku)
    error_message = "front_door_sku must be Standard_AzureFrontDoor or Premium_AzureFrontDoor."
  }
}

variable "custom_domains" {
  description = "Custom domains for Front Door"
  type        = list(string)
  default     = []
}

variable "dns_zone_id" {
  description = "Azure DNS Zone ID for custom domains"
  type        = string
  default     = ""
}

# ==================== Logging and Monitoring ====================
variable "create_log_analytics" {
  description = "Create Log Analytics workspace"
  type        = bool
  default     = true
}

variable "log_analytics_workspace_id" {
  description = "Existing Log Analytics workspace ID (if not creating)"
  type        = string
  default     = ""
}

variable "log_retention_days" {
  description = "Log retention in days"
  type        = number
  default     = 30
}

# ==================== Security ====================
variable "enable_storage_access" {
  description = "Grant managed identity Storage Blob Data Reader role"
  type        = bool
  default     = true
}

# ==================== Health Check ====================
variable "health_check_path" {
  description = "HTTP path for health checks"
  type        = string
  default     = "/health"
}

# ==================== Tags ====================
variable "tags" {
  description = "Additional tags for all resources"
  type        = map(string)
  default     = {}
}
