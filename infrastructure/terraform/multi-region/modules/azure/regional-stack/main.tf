# Placeholder Azure Regional Stack Module
variable "location" { type = string }
variable "environment" { type = string }
variable "project_name" { type = string }
variable "unique_suffix" { type = string }
variable "is_primary" { type = bool }
variable "instance_count" { type = number }
variable "instance_type" { type = string }
variable "container_image" { type = string }
variable "container_port" { type = number }
variable "cpu_limit" { type = string }
variable "memory_limit" { type = string }
variable "db_sku_name" { type = string }
variable "db_version" { type = string }
variable "db_storage_size_gb" { type = number }
variable "db_admin_username" { type = string
  default = "honuaadmin"
}
variable "db_admin_password" {
  type = string
  sensitive = true
  default = null
}
variable "db_backup_retention_days" { type = number
  default = 30
}
variable "enable_zone_redundant" { type = bool
  default = false
}
variable "enable_geo_backup" { type = bool
  default = false
}
variable "enable_read_replica" { type = bool
  default = false
}
variable "primary_server_id" { type = string
  default = null
}
variable "storage_replication_type" { type = string
  default = "LRS"
}
variable "enable_storage_encryption" { type = bool
  default = true
}
variable "enable_redis" { type = bool
  default = true
}
variable "redis_sku_name" { type = string
  default = "Standard"
}
variable "redis_capacity" { type = string
  default = "C2"
}
variable "enable_monitoring" { type = bool
  default = true
}
variable "enable_logging" { type = bool
  default = true
}
variable "log_retention_days" { type = number
  default = 30
}
variable "alert_email" { type = string
  default = null
}
variable "enable_waf" { type = bool
  default = true
}
variable "enable_ddos_protection" { type = bool
  default = true
}
variable "tags" {
  type = map(string)
  default = {}
}

output "resource_group_name" { value = "rg-honua-${var.environment}-${var.location}" }
output "app_fqdn" { value = "honua-${var.environment}-${var.location}.azurecontainerapps.io" }
output "db_server_id" { value = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-honua-${var.environment}-${var.location}/providers/Microsoft.DBforPostgreSQL/flexibleServers/honua-${var.environment}-${var.location}-db" }
output "db_server_name" { value = "honua-${var.environment}-${var.location}-db" }
output "db_fqdn" { value = "honua-${var.environment}-${var.location}-db.postgres.database.azure.com" }
output "storage_account_name" { value = "sthonua${var.unique_suffix}" }
output "storage_primary_endpoint" { value = "https://sthonua${var.unique_suffix}.blob.core.windows.net" }
output "storage_secondary_endpoint" { value = "https://sthonua${var.unique_suffix}-secondary.blob.core.windows.net" }
output "redis_hostname" { value = "honua-${var.environment}-${var.location}-redis.redis.cache.windows.net" }
