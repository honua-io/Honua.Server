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

# Outputs
output "honua_server_url" {
  value = "http://${azurerm_container_group.honua.ip_address}:8080"
}