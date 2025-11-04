# Unit Test for Azure Container Apps Module
# Tests basic Terraform validation with minimal configuration

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

# Provider configuration (backend disabled for testing)
provider "azurerm" {
  features {}
  skip_provider_registration = true
}

provider "azuread" {}

# Test module with minimal configuration
module "container_apps_test" {
  source = "../.."

  # Required variables
  environment      = "dev"
  service_name     = "honua-unit-test"
  location         = var.location
  container_image  = var.container_image

  # Resource group - use existing for unit test
  create_resource_group = false
  resource_group_name   = "test-rg"

  # VNet - use existing for unit test
  create_vnet             = false
  container_apps_subnet_id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/test-rg/providers/Microsoft.Network/virtualNetworks/test-vnet/subnets/container-apps"

  # Database - disabled for unit test
  create_database = false

  # Key Vault - disabled for unit test
  create_key_vault = false

  # Front Door - disabled for unit test
  create_front_door = false

  # Log Analytics - use existing
  create_log_analytics      = false
  log_analytics_workspace_id = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/test-rg/providers/Microsoft.OperationalInsights/workspaces/test-law"

  # Container configuration
  container_cpu    = 0.25
  container_memory = "0.5Gi"

  # Minimal scaling for testing
  min_replicas = 0
  max_replicas = 1

  # Security
  enable_storage_access = false

  # Tags
  tags = {
    test_type = "unit"
    terraform = "true"
  }
}

# Test outputs are generated correctly
output "container_app_name" {
  description = "Container App name from test module"
  value       = module.container_apps_test.container_app_name
}

output "container_app_fqdn" {
  description = "Container App FQDN from test module"
  value       = module.container_apps_test.container_app_fqdn
}

output "container_app_url" {
  description = "Container App URL from test module"
  value       = module.container_apps_test.container_app_url
}

output "managed_identity_id" {
  description = "Managed Identity ID from test module"
  value       = module.container_apps_test.managed_identity_id
}

output "deployment_info" {
  description = "Deployment information"
  value       = module.container_apps_test.deployment_info
}

# Validation tests
output "test_validation" {
  description = "Test validation checks"
  value = {
    environment_valid     = module.container_apps_test.deployment_info.environment == "dev"
    min_replicas_valid    = module.container_apps_test.deployment_info.min_replicas == 0
    max_replicas_valid    = module.container_apps_test.deployment_info.max_replicas == 1
    database_disabled     = module.container_apps_test.deployment_info.database_enabled == false
    app_url_generated     = module.container_apps_test.container_app_url != null
  }
}
