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

# Container Instance for Honua Server
resource "azurerm_container_group" "honua" {
  name                = "honua-server-${var.environment}"
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  os_type             = "Linux"

  container {
    name   = "honua-server"
    image  = "honuaio/honua-server:latest"
    cpu    = "1.0"
    memory = "2.0"

    ports {
      port     = 8080
      protocol = "TCP"
    }

    environment_variables = {
      ASPNETCORE_ENVIRONMENT = var.environment
      HONUA__DATABASE__HOST = azurerm_postgresql_flexible_server.honua.fqdn
      HONUA__DATABASE__PORT = "5432"
      HONUA__DATABASE__DATABASE = azurerm_postgresql_flexible_server_database.honua.name
    }
  }

  ip_address {
    type  = "Public"
    ports {
      protocol = "TCP"
      port     = 8080
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
  value = "http://${azurerm_container_group.honua.ip_address}:8080"
}

output "database_fqdn" {
  value = azurerm_postgresql_flexible_server.honua.fqdn
}

output "database_password" {
  value     = random_password.db_password.result
  sensitive = true
}