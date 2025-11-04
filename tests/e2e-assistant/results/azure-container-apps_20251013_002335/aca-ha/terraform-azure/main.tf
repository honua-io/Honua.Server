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
      HONUA__CACHE__PROVIDER = "redis"
      HONUA__CACHE__REDIS__HOST = azurerm_redis_cache.honua.hostname
      HONUA__CACHE__REDIS__PORT = "6380"
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

# Redis Cache
resource "azurerm_redis_cache" "honua" {
  name                = "honua-redis-${var.environment}"
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  capacity            = 0
  family              = "C"
  sku_name            = "Basic"
  enable_non_ssl_port = false

  redis_configuration {
    maxmemory_policy = "allkeys-lru"
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

output "redis_hostname" {
  value = azurerm_redis_cache.honua.hostname
}