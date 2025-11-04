# Outputs for Azure Container Apps Serverless Module

# ==================== Container App ====================
output "container_app_name" {
  description = "Name of the Container App"
  value       = azurerm_container_app.honua.name
}

output "container_app_id" {
  description = "ID of the Container App"
  value       = azurerm_container_app.honua.id
}

output "container_app_fqdn" {
  description = "Fully qualified domain name of the Container App"
  value       = azurerm_container_app.honua.ingress[0].fqdn
}

output "container_app_url" {
  description = "URL of the Container App"
  value       = "https://${azurerm_container_app.honua.ingress[0].fqdn}"
}

# ==================== Front Door ====================
output "front_door_endpoint_url" {
  description = "Front Door endpoint URL"
  value       = var.create_front_door ? "https://${azurerm_cdn_frontdoor_endpoint.honua[0].host_name}" : null
}

output "front_door_profile_id" {
  description = "Front Door profile ID"
  value       = var.create_front_door ? azurerm_cdn_frontdoor_profile.honua[0].id : null
}

# ==================== Database ====================
output "database_fqdn" {
  description = "Fully qualified domain name of PostgreSQL server"
  value       = var.create_database ? azurerm_postgresql_flexible_server.honua[0].fqdn : null
}

output "database_name" {
  description = "Name of the database"
  value       = var.create_database ? azurerm_postgresql_flexible_server_database.honua[0].name : null
}

output "database_username" {
  description = "Database username"
  value       = var.create_database ? var.db_username : null
  sensitive   = true
}

# ==================== Key Vault ====================
output "key_vault_id" {
  description = "ID of the Key Vault"
  value       = var.create_key_vault ? azurerm_key_vault.honua[0].id : null
}

output "key_vault_uri" {
  description = "URI of the Key Vault"
  value       = var.create_key_vault ? azurerm_key_vault.honua[0].vault_uri : null
}

# ==================== Managed Identity ====================
output "managed_identity_id" {
  description = "ID of the managed identity"
  value       = azurerm_user_assigned_identity.honua.id
}

output "managed_identity_principal_id" {
  description = "Principal ID of the managed identity"
  value       = azurerm_user_assigned_identity.honua.principal_id
}

output "managed_identity_client_id" {
  description = "Client ID of the managed identity"
  value       = azurerm_user_assigned_identity.honua.client_id
}

# ==================== Resource Group ====================
output "resource_group_name" {
  description = "Name of the resource group"
  value       = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
}

output "resource_group_location" {
  description = "Location of the resource group"
  value       = var.create_resource_group ? azurerm_resource_group.honua[0].location : var.location
}

# ==================== Virtual Network ====================
output "vnet_id" {
  description = "ID of the virtual network"
  value       = var.create_vnet ? azurerm_virtual_network.honua[0].id : null
}

output "vnet_name" {
  description = "Name of the virtual network"
  value       = var.create_vnet ? azurerm_virtual_network.honua[0].name : null
}

# ==================== Log Analytics ====================
output "log_analytics_workspace_id" {
  description = "ID of the Log Analytics workspace"
  value       = var.create_log_analytics ? azurerm_log_analytics_workspace.honua[0].id : null
}

# ==================== Cost Estimation ====================
output "estimated_monthly_cost" {
  description = "Estimated monthly cost breakdown (USD, approximate)"
  value = {
    container_app = {
      description = "Container Apps (highly variable based on usage)"
      vcpu        = "$0.000024 per vCPU-second"
      memory      = "$0.000002667 per GiB-second"
      requests    = "$0.40 per million requests"
      free_tier   = "180,000 vCPU-seconds, 360,000 GiB-seconds, 2M requests/month free"
      note        = "First 180K vCPU-s and 360K GiB-s free monthly"
    }
    database = var.create_database ? {
      description = "Azure Database for PostgreSQL ${var.db_sku_name}"
      instance    = var.db_sku_name == "B_Standard_B1ms" ? "~$12/month" : "varies by SKU"
      storage     = "~$0.115/GB-month"
      backup      = "~$0.095/GB-month"
      ha_mode     = var.db_high_availability_mode != "Disabled" ? "Doubles compute cost" : "Single zone"
    } : null
    front_door = var.create_front_door ? {
      description = "Azure Front Door ${var.front_door_sku}"
      base        = var.front_door_sku == "Standard_AzureFrontDoor" ? "$35/month" : "$330/month"
      data        = "$0.06-0.12/GB depending on region"
      requests    = "$0.008 per 10K requests"
    } : null
    key_vault = var.create_key_vault ? {
      description = "Azure Key Vault"
      operations  = "$0.03 per 10K operations"
      note        = "First 5 operations per second included"
    } : null
    log_analytics = var.create_log_analytics ? {
      description = "Log Analytics"
      ingestion   = "$2.30/GB"
      retention   = "Free for 31 days, $0.10/GB-month after"
      free_tier   = "First 5GB/month free"
    } : null
    total_estimate_dev = "$50-80/month for low traffic"
    total_estimate_prod = "$200-400/month for moderate traffic with Front Door Standard"
    note = "Container Apps pricing is consumption-based. Actual costs vary significantly with usage."
  }
}

# ==================== Monitoring URLs ====================
output "monitoring_urls" {
  description = "Azure Portal URLs for monitoring"
  value = {
    container_app = "https://portal.azure.com/#resource${azurerm_container_app.honua.id}"
    database      = var.create_database ? "https://portal.azure.com/#resource${azurerm_postgresql_flexible_server.honua[0].id}" : null
    front_door    = var.create_front_door ? "https://portal.azure.com/#resource${azurerm_cdn_frontdoor_profile.honua[0].id}" : null
    log_analytics = var.create_log_analytics ? "https://portal.azure.com/#resource${azurerm_log_analytics_workspace.honua[0].id}" : null
  }
}

# ==================== Deployment Information ====================
output "deployment_info" {
  description = "Information for deployment and integration"
  value = {
    container_app_url = "https://${azurerm_container_app.honua.ingress[0].fqdn}"
    front_door_url    = var.create_front_door ? "https://${azurerm_cdn_frontdoor_endpoint.honua[0].host_name}" : null
    health_check_url  = "https://${azurerm_container_app.honua.ingress[0].fqdn}${var.health_check_path}"
    container_image   = var.container_image
    min_replicas      = var.min_replicas
    max_replicas      = var.max_replicas
    database_enabled  = var.create_database
    front_door_enabled = var.create_front_door
    environment       = var.environment
  }
}
