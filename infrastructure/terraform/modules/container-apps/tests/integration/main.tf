# Integration Test for Azure Container Apps Module
# Tests full production-like configuration

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# Provider configuration
provider "azurerm" {
  features {
    resource_group {
      prevent_deletion_if_contains_resources = false
    }
    key_vault {
      purge_soft_delete_on_destroy = true
    }
  }
  skip_provider_registration = var.skip_azure_validation
}

provider "azuread" {}

# Random suffix to avoid naming conflicts
resource "random_id" "test" {
  byte_length = 4
}

# Test module with full configuration
module "container_apps_integration_test" {
  source = "../.."

  # General configuration
  environment  = "dev"
  service_name = "honua-int-${random_id.test.hex}"
  location     = var.location

  # Resource group
  create_resource_group = true

  # Container configuration
  container_image        = var.container_image
  aspnetcore_environment = "Development"
  container_cpu          = 1
  container_memory       = "2Gi"
  additional_env_vars = {
    TEST_MODE    = "true"
    LOG_LEVEL    = "Debug"
    FEATURE_FLAG = "integration-testing"
  }

  # Scaling configuration
  min_replicas               = 0
  max_replicas               = 10
  scale_concurrent_requests  = 10

  # Virtual Network
  create_vnet        = true
  vnet_address_space = "10.0.0.0/16"

  # Database configuration
  create_database              = true
  database_name                = "honua_test"
  db_username                  = "honua_test_admin"
  postgres_version             = "15"
  db_sku_name                  = "B_Standard_B1ms"
  db_storage_mb                = 32768
  db_backup_retention_days     = 7
  db_geo_redundant_backup      = false
  db_high_availability_mode    = "Disabled"

  # Key Vault
  create_key_vault = true

  # Azure Front Door
  create_front_door   = true
  front_door_sku      = "Standard_AzureFrontDoor"
  custom_domains      = []

  # Logging and Monitoring
  create_log_analytics = true
  log_retention_days   = 30

  # Security
  enable_storage_access = true

  # Health check
  health_check_path = "/health"

  # CORS configuration
  cors_origins = ["http://localhost:3000", "https://test.honua.io"]

  # Tags
  tags = {
    test_type    = "integration"
    terraform    = "true"
    ephemeral    = "true"
    auto_cleanup = "true"
  }
}

# Test all outputs
output "container_app_name" {
  description = "Container App name"
  value       = module.container_apps_integration_test.container_app_name
}

output "container_app_id" {
  description = "Container App ID"
  value       = module.container_apps_integration_test.container_app_id
}

output "container_app_fqdn" {
  description = "Container App FQDN"
  value       = module.container_apps_integration_test.container_app_fqdn
}

output "container_app_url" {
  description = "Container App URL"
  value       = module.container_apps_integration_test.container_app_url
}

output "database_fqdn" {
  description = "PostgreSQL server FQDN"
  value       = module.container_apps_integration_test.database_fqdn
}

output "database_name" {
  description = "Database name"
  value       = module.container_apps_integration_test.database_name
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = module.container_apps_integration_test.key_vault_name
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = module.container_apps_integration_test.key_vault_uri
}

output "front_door_endpoint_url" {
  description = "Front Door endpoint URL"
  value       = module.container_apps_integration_test.front_door_endpoint_url
}

output "managed_identity_id" {
  description = "Managed Identity ID"
  value       = module.container_apps_integration_test.managed_identity_id
}

output "managed_identity_principal_id" {
  description = "Managed Identity Principal ID"
  value       = module.container_apps_integration_test.managed_identity_principal_id
}

output "vnet_id" {
  description = "Virtual Network ID"
  value       = module.container_apps_integration_test.vnet_id
}

output "container_apps_subnet_id" {
  description = "Container Apps subnet ID"
  value       = module.container_apps_integration_test.vnet_id != null ? "${module.container_apps_integration_test.vnet_id}/subnets/container-apps" : null
}

output "log_analytics_workspace_id" {
  description = "Log Analytics Workspace ID"
  value       = module.container_apps_integration_test.log_analytics_workspace_id
}

output "key_vault_name" {
  description = "Key Vault name"
  value       = var.create_key_vault ? module.container_apps_integration_test.key_vault_id != null ? split("/", module.container_apps_integration_test.key_vault_id)[8] : null : null
}

output "resource_group_name" {
  description = "Resource Group name"
  value       = module.container_apps_integration_test.resource_group_name
}

output "monitoring_urls" {
  description = "Console URLs for monitoring"
  value       = module.container_apps_integration_test.monitoring_urls
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.container_apps_integration_test.deployment_info
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value       = module.container_apps_integration_test.estimated_monthly_cost
}

# Integration test validations
output "integration_test_results" {
  description = "Integration test validation results"
  value = {
    container_app_created     = module.container_apps_integration_test.container_app_name != null
    database_created          = module.container_apps_integration_test.database_fqdn != null
    key_vault_created         = module.container_apps_integration_test.key_vault_name != null
    front_door_created        = module.container_apps_integration_test.front_door_endpoint_url != null
    vnet_created              = module.container_apps_integration_test.vnet_id != null
    log_analytics_created     = module.container_apps_integration_test.log_analytics_workspace_id != null
    managed_identity_created  = module.container_apps_integration_test.managed_identity_id != null
    environment_correct       = module.container_apps_integration_test.deployment_info.environment == "dev"
    min_replicas_correct      = module.container_apps_integration_test.deployment_info.min_replicas == 0
    max_replicas_correct      = module.container_apps_integration_test.deployment_info.max_replicas == 10
    database_enabled          = module.container_apps_integration_test.deployment_info.database_enabled == true
    front_door_enabled        = module.container_apps_integration_test.deployment_info.front_door_enabled == true
  }
}

# DNS configuration for reference
output "dns_configuration" {
  description = "Required DNS records if custom domain is used"
  value       = module.container_apps_integration_test.dns_records_required
}

# Connection information
output "connection_info" {
  description = "Connection information for applications"
  value       = module.container_apps_integration_test.connection_info
  sensitive   = true
}
