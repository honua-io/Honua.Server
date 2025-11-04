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

      env {
        name  = "HONUA__DATABASE__HOST"
        value = azurerm_postgresql_flexible_server.honua.fqdn
      }

      env {
        name  = "HONUA__DATABASE__PORT"
        value = "5432"
      }

      env {
        name  = "HONUA__DATABASE__DATABASE"
        value = azurerm_postgresql_flexible_server_database.honua.name
      }

      env {
        name  = "HONUA__DATABASE__USERNAME"
        value = azurerm_postgresql_flexible_server.honua.administrator_login
      }

      env {
        name  = "HONUA__DATABASE__PASSWORD"
        value = azurerm_postgresql_flexible_server.honua.administrator_password
        secret_name = "db-password"
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

# PostgreSQL Flexible Server
resource "azurerm_postgresql_flexible_server" "honua" {
  name                   = "honua-psql-${var.environment}"
  resource_group_name    = azurerm_resource_group.honua.name
  location               = azurerm_resource_group.honua.location
  version                = "15"
  administrator_login    = "postgres"
  administrator_password = random_password.db_password.result
  zone                   = "1"

  storage_mb = 32768
  sku_name   = "B_Standard_B1ms"

  tags = {
    Environment = var.environment
    Application = "honua"
  }
}

resource "azurerm_postgresql_flexible_server_database" "honua" {
  name      = "honua"
  server_id = azurerm_postgresql_flexible_server.honua.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

resource "azurerm_postgresql_flexible_server_firewall_rule" "allow_azure" {
  name             = "allow-azure-services"
  server_id        = azurerm_postgresql_flexible_server.honua.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "random_password" "db_password" {
  length  = 32
  special = true
}

# Outputs
output "honua_server_url" {
  value = "https://${azurerm_container_app.honua.latest_revision_fqdn}"
  description = "URL to access the Honua server"
}

output "database_fqdn" {
  value = azurerm_postgresql_flexible_server.honua.fqdn
}

output "database_password" {
  value     = random_password.db_password.result
  sensitive = true
}