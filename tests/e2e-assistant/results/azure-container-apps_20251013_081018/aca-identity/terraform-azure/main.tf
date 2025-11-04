terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

variable "location" {
  description = "Azure region"
  default     = "East US"
}

variable "environment" {
  description = "Environment name"
  default     = "development"
}

# Resource Group
resource "azurerm_resource_group" "honua" {
  name     = "honua-rg-${var.environment}"
  location = var.location

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# Container Registry
resource "azurerm_container_registry" "honua" {
  name                = "honuaacr${var.environment}"
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  sku                 = "Basic"
  admin_enabled       = true
}

# Container Apps Environment
resource "azurerm_log_analytics_workspace" "honua" {
  name                = "honua-logs-${var.environment}"
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

resource "azurerm_container_app_environment" "honua" {
  name                       = "honua-env-${var.environment}"
  location                   = azurerm_resource_group.honua.location
  resource_group_name        = azurerm_resource_group.honua.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.honua.id

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# Container App for Honua Server
resource "azurerm_container_app" "honua" {
  name                         = "honua-server-${var.environment}"
  container_app_environment_id = azurerm_container_app_environment.honua.id
  resource_group_name          = azurerm_resource_group.honua.name
  revision_mode                = "Single"

  template {
    container {
      name   = "honua-server"
      image  = "honuaio/honuaserver:latest"
      cpu    = 1.0
      memory = "2Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment
      }

    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  identity {
    type = "SystemAssigned"
  }

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# Storage Account for Blob Storage
resource "azurerm_storage_account" "honua" {
  name                     = "honuastorage${var.environment}"
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# Blob Container for tile caching
resource "azurerm_storage_container" "tiles" {
  name                  = "tiles"
  storage_account_name  = azurerm_storage_account.honua.name
  container_access_type = "private"
}

# CDN Profile
resource "azurerm_cdn_profile" "honua" {
  name                = "honua-cdn-${var.environment}"
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku                 = "Standard_Microsoft"

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# CDN Endpoint
resource "azurerm_cdn_endpoint" "honua" {
  name                = "honua-endpoint-${var.environment}"
  profile_name        = azurerm_cdn_profile.honua.name
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name

  origin {
    name      = "honua-storage"
    host_name = azurerm_storage_account.honua.primary_blob_host
  }

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

# Outputs
output "honua_server_url" {
  value = "https://${azurerm_container_app.honua.latest_revision_fqdn}"
  description = "URL to access the Honua server"
}