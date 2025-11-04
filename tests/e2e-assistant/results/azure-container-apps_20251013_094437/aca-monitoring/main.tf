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

# Application Insights
resource "azurerm_application_insights" "honua" {
  name                = "honua-insights-${var.environment}"
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  workspace_id        = azurerm_log_analytics_workspace.honua.id
  application_type    = "web"

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