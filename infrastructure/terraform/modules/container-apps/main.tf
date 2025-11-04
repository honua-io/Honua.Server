# Azure Container Apps Serverless Module for Honua GIS Platform
# Deploys Honua as Azure Container App with Azure Database for PostgreSQL

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

# ==================== Local Variables ====================
locals {
  name_prefix = "${var.service_name}-${var.environment}"
  location    = var.location

  # Environment variables for container
  env_vars = merge(
    {
      ASPNETCORE_ENVIRONMENT      = var.aspnetcore_environment
      ASPNETCORE_URLS             = "http://+:8080"
      DOTNET_RUNNING_IN_CONTAINER = "true"
      DOTNET_TieredPGO            = "1"
      DOTNET_ReadyToRun           = "1"
      GDAL_CACHEMAX               = var.gdal_cache_max
      CORS_ORIGINS                = join(",", var.cors_origins)
    },
    var.additional_env_vars
  )

  common_tags = merge(
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
      Application = "Honua"
      Tier        = "Serverless"
    },
    var.tags
  )
}

# ==================== Resource Group ====================
resource "azurerm_resource_group" "honua" {
  count    = var.create_resource_group ? 1 : 0
  name     = "${local.name_prefix}-rg"
  location = local.location
  tags     = local.common_tags
}

# ==================== Virtual Network ====================
resource "azurerm_virtual_network" "honua" {
  count               = var.create_vnet ? 1 : 0
  name                = "${local.name_prefix}-vnet"
  location            = local.location
  resource_group_name = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  address_space       = [var.vnet_address_space]
  tags                = local.common_tags
}

# Subnet for Container Apps
resource "azurerm_subnet" "container_apps" {
  count                = var.create_vnet ? 1 : 0
  name                 = "${local.name_prefix}-ca-subnet"
  resource_group_name  = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  virtual_network_name = azurerm_virtual_network.honua[0].name
  address_prefixes     = [cidrsubnet(var.vnet_address_space, 4, 0)]

  delegation {
    name = "container-apps-delegation"

    service_delegation {
      name    = "Microsoft.App/environments"
      actions = ["Microsoft.Network/virtualNetworks/subnets/join/action"]
    }
  }
}

# Subnet for PostgreSQL
resource "azurerm_subnet" "postgresql" {
  count                = var.create_vnet && var.create_database ? 1 : 0
  name                 = "${local.name_prefix}-db-subnet"
  resource_group_name  = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  virtual_network_name = azurerm_virtual_network.honua[0].name
  address_prefixes     = [cidrsubnet(var.vnet_address_space, 4, 1)]

  delegation {
    name = "postgresql-delegation"

    service_delegation {
      name = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }

  service_endpoints = ["Microsoft.Storage"]
}

# Private DNS Zone for PostgreSQL
resource "azurerm_private_dns_zone" "postgresql" {
  count               = var.create_database ? 1 : 0
  name                = "${local.name_prefix}.postgres.database.azure.com"
  resource_group_name = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  tags                = local.common_tags
}

resource "azurerm_private_dns_zone_virtual_network_link" "postgresql" {
  count                 = var.create_database && var.create_vnet ? 1 : 0
  name                  = "${local.name_prefix}-db-vnet-link"
  resource_group_name   = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  private_dns_zone_name = azurerm_private_dns_zone.postgresql[0].name
  virtual_network_id    = azurerm_virtual_network.honua[0].id
  tags                  = local.common_tags
}

# ==================== Azure Database for PostgreSQL ====================
resource "random_password" "db_password" {
  count   = var.create_database ? 1 : 0
  length  = 32
  special = true
}

resource "azurerm_postgresql_flexible_server" "honua" {
  count                  = var.create_database ? 1 : 0
  name                   = local.name_prefix
  resource_group_name    = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  location               = local.location
  version                = var.postgres_version
  delegated_subnet_id    = var.create_vnet ? azurerm_subnet.postgresql[0].id : var.postgresql_subnet_id
  private_dns_zone_id    = azurerm_private_dns_zone.postgresql[0].id
  administrator_login    = var.db_username
  administrator_password = random_password.db_password[0].result

  sku_name   = var.db_sku_name
  storage_mb = var.db_storage_mb

  backup_retention_days        = var.db_backup_retention_days
  geo_redundant_backup_enabled = var.db_geo_redundant_backup

  high_availability {
    mode                      = var.db_high_availability_mode
    standby_availability_zone = var.db_high_availability_mode != "Disabled" ? var.db_standby_zone : null
  }

  maintenance_window {
    day_of_week  = 0
    start_hour   = 3
    start_minute = 0
  }

  tags = local.common_tags

  depends_on = [azurerm_private_dns_zone_virtual_network_link.postgresql]
}

# Database
resource "azurerm_postgresql_flexible_server_database" "honua" {
  count     = var.create_database ? 1 : 0
  name      = var.database_name
  server_id = azurerm_postgresql_flexible_server.honua[0].id
  collation = "en_US.utf8"
  charset   = "UTF8"
}

# PostgreSQL Configuration
resource "azurerm_postgresql_flexible_server_configuration" "max_connections" {
  count     = var.create_database ? 1 : 0
  name      = "max_connections"
  server_id = azurerm_postgresql_flexible_server.honua[0].id
  value     = var.db_max_connections
}

resource "azurerm_postgresql_flexible_server_configuration" "shared_buffers" {
  count     = var.create_database ? 1 : 0
  name      = "shared_buffers"
  server_id = azurerm_postgresql_flexible_server.honua[0].id
  value     = var.db_shared_buffers
}

# ==================== Key Vault ====================
data "azurerm_client_config" "current" {}

resource "azurerm_key_vault" "honua" {
  count                      = var.create_key_vault ? 1 : 0
  name                       = "${substr(local.name_prefix, 0, 20)}-kv"
  location                   = local.location
  resource_group_name        = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = var.environment == "production"

  network_acls {
    bypass         = "AzureServices"
    default_action = "Allow"  # Restrict in production
  }

  tags = local.common_tags
}

# JWT Secret
resource "random_password" "jwt_secret" {
  length  = 64
  special = true
}

resource "azurerm_key_vault_secret" "jwt_secret" {
  count        = var.create_key_vault ? 1 : 0
  name         = "jwt-secret"
  value        = random_password.jwt_secret.result
  key_vault_id = azurerm_key_vault.honua[0].id
  tags         = local.common_tags
}

# Database Connection String
resource "azurerm_key_vault_secret" "db_connection" {
  count        = var.create_database && var.create_key_vault ? 1 : 0
  name         = "database-connection-string"
  value        = "Host=${azurerm_postgresql_flexible_server.honua[0].fqdn};Database=${var.database_name};Username=${var.db_username};Password=${random_password.db_password[0].result};SslMode=Require"
  key_vault_id = azurerm_key_vault.honua[0].id
  tags         = local.common_tags
}

# ==================== Managed Identity ====================
resource "azurerm_user_assigned_identity" "honua" {
  name                = "${local.name_prefix}-identity"
  resource_group_name = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  location            = local.location
  tags                = local.common_tags
}

# Grant Key Vault access to managed identity
resource "azurerm_key_vault_access_policy" "honua" {
  count        = var.create_key_vault ? 1 : 0
  key_vault_id = azurerm_key_vault.honua[0].id
  tenant_id    = data.azurerm_client_config.current.tenant_id
  object_id    = azurerm_user_assigned_identity.honua.principal_id

  secret_permissions = [
    "Get",
    "List"
  ]
}

# Grant Storage Blob Data Reader for raster data
resource "azurerm_role_assignment" "storage_blob_reader" {
  count                = var.enable_storage_access ? 1 : 0
  scope                = var.create_resource_group ? azurerm_resource_group.honua[0].id : "/subscriptions/${data.azurerm_client_config.current.subscription_id}/resourceGroups/${var.resource_group_name}"
  role_definition_name = "Storage Blob Data Reader"
  principal_id         = azurerm_user_assigned_identity.honua.principal_id
}

# ==================== Log Analytics Workspace ====================
resource "azurerm_log_analytics_workspace" "honua" {
  count               = var.create_log_analytics ? 1 : 0
  name                = "${local.name_prefix}-law"
  location            = local.location
  resource_group_name = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = var.log_retention_days
  tags                = local.common_tags
}

# ==================== Container Apps Environment ====================
resource "azurerm_container_app_environment" "honua" {
  name                       = "${local.name_prefix}-env"
  location                   = local.location
  resource_group_name        = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  log_analytics_workspace_id = var.create_log_analytics ? azurerm_log_analytics_workspace.honua[0].id : var.log_analytics_workspace_id
  infrastructure_subnet_id   = var.create_vnet ? azurerm_subnet.container_apps[0].id : var.container_apps_subnet_id

  tags = local.common_tags
}

# ==================== Container App ====================
resource "azurerm_container_app" "honua" {
  name                         = local.name_prefix
  container_app_environment_id = azurerm_container_app_environment.honua.id
  resource_group_name          = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.honua.id]
  }

  template {
    min_replicas = var.min_replicas
    max_replicas = var.max_replicas

    container {
      name   = "honua"
      image  = var.container_image
      cpu    = var.container_cpu
      memory = var.container_memory

      # Environment variables
      dynamic "env" {
        for_each = local.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      # JWT Secret from Key Vault
      dynamic "env" {
        for_each = var.create_key_vault ? [1] : []
        content {
          name        = "JWT_SECRET"
          secret_name = "jwt-secret"
        }
      }

      # Database connection from Key Vault
      dynamic "env" {
        for_each = var.create_database && var.create_key_vault ? [1] : []
        content {
          name        = "DATABASE_CONNECTION_STRING"
          secret_name = "database-connection-string"
        }
      }

      # Liveness probe
      liveness_probe {
        transport = "HTTP"
        port      = 8080
        path      = var.health_check_path

        initial_delay           = 10
        interval_seconds        = 30
        timeout                 = 5
        failure_count_threshold = 3
      }

      # Readiness probe
      readiness_probe {
        transport = "HTTP"
        port      = 8080
        path      = var.health_check_path

        interval_seconds        = 10
        timeout                 = 3
        failure_count_threshold = 3
        success_count_threshold = 1
      }
    }

    # HTTP scale rule
    http_scale_rule {
      name                = "http-scale"
      concurrent_requests = var.scale_concurrent_requests
    }
  }

  # Secrets from Key Vault
  dynamic "secret" {
    for_each = var.create_key_vault ? [1] : []
    content {
      name                = "jwt-secret"
      key_vault_secret_id = azurerm_key_vault_secret.jwt_secret[0].versionless_id
      identity            = azurerm_user_assigned_identity.honua.id
    }
  }

  dynamic "secret" {
    for_each = var.create_database && var.create_key_vault ? [1] : []
    content {
      name                = "database-connection-string"
      key_vault_secret_id = azurerm_key_vault_secret.db_connection[0].versionless_id
      identity            = azurerm_user_assigned_identity.honua.id
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "http"

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }

  tags = local.common_tags

  depends_on = [
    azurerm_key_vault_access_policy.honua
  ]
}

# ==================== Azure Front Door (CDN + Global Load Balancer) ====================
resource "azurerm_cdn_frontdoor_profile" "honua" {
  count               = var.create_front_door ? 1 : 0
  name                = "${local.name_prefix}-fd"
  resource_group_name = var.create_resource_group ? azurerm_resource_group.honua[0].name : var.resource_group_name
  sku_name            = var.front_door_sku
  tags                = local.common_tags
}

# Front Door endpoint
resource "azurerm_cdn_frontdoor_endpoint" "honua" {
  count                    = var.create_front_door ? 1 : 0
  name                     = "${local.name_prefix}-endpoint"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id
  tags                     = local.common_tags
}

# Origin group
resource "azurerm_cdn_frontdoor_origin_group" "honua" {
  count                    = var.create_front_door ? 1 : 0
  name                     = "${local.name_prefix}-origin-group"
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id

  load_balancing {
    sample_size                 = 4
    successful_samples_required = 3
  }

  health_probe {
    path                = var.health_check_path
    request_type        = "GET"
    protocol            = "Https"
    interval_in_seconds = 30
  }
}

# Origin (Container App)
resource "azurerm_cdn_frontdoor_origin" "honua" {
  count                         = var.create_front_door ? 1 : 0
  name                          = "${local.name_prefix}-origin"
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua[0].id
  enabled                       = true

  certificate_name_check_enabled = true
  host_name                      = azurerm_container_app.honua.ingress[0].fqdn
  http_port                      = 80
  https_port                     = 443
  origin_host_header             = azurerm_container_app.honua.ingress[0].fqdn
  priority                       = 1
  weight                         = 1000
}

# Route
resource "azurerm_cdn_frontdoor_route" "honua" {
  count                         = var.create_front_door ? 1 : 0
  name                          = "${local.name_prefix}-route"
  cdn_frontdoor_endpoint_id     = azurerm_cdn_frontdoor_endpoint.honua[0].id
  cdn_frontdoor_origin_group_id = azurerm_cdn_frontdoor_origin_group.honua[0].id
  cdn_frontdoor_origin_ids      = [azurerm_cdn_frontdoor_origin.honua[0].id]

  supported_protocols    = ["Http", "Https"]
  patterns_to_match      = ["/*"]
  forwarding_protocol    = "HttpsOnly"
  link_to_default_domain = true
  https_redirect_enabled = true

  cache {
    query_string_caching_behavior = "IncludeSpecifiedQueryStrings"
    query_strings                 = ["bbox", "width", "height", "layers", "srs", "crs", "format"]
    compression_enabled           = true
    content_types_to_compress     = ["application/json", "application/xml", "image/png", "image/jpeg"]
  }
}

# Custom domain (optional)
resource "azurerm_cdn_frontdoor_custom_domain" "honua" {
  count                    = var.create_front_door && length(var.custom_domains) > 0 ? length(var.custom_domains) : 0
  name                     = replace(var.custom_domains[count.index], ".", "-")
  cdn_frontdoor_profile_id = azurerm_cdn_frontdoor_profile.honua[0].id
  dns_zone_id              = var.dns_zone_id
  host_name                = var.custom_domains[count.index]

  tls {
    certificate_type    = "ManagedCertificate"
    minimum_tls_version = "TLS12"
  }
}

# ==================== Monitoring and Alerts ====================
resource "azurerm_monitor_diagnostic_setting" "container_app" {
  count                      = var.create_log_analytics ? 1 : 0
  name                       = "${local.name_prefix}-diagnostics"
  target_resource_id         = azurerm_container_app.honua.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.honua[0].id

  enabled_log {
    category = "ContainerAppConsoleLogs"
  }

  enabled_log {
    category = "ContainerAppSystemLogs"
  }

  metric {
    category = "AllMetrics"
  }
}
