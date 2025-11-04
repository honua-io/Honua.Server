// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Helper class containing Azure Terraform generation logic.
/// </summary>
internal static class TerraformAzureContent
{
    public static string GenerateContainerApps(DeploymentAnalysis analysis)
    {
        var hasDatabase = analysis.InfrastructureNeeds.NeedsDatabase ||
                         analysis.RequiredServices.Any(s => s.Contains("postgis", StringComparison.OrdinalIgnoreCase));
        var hasCache = analysis.InfrastructureNeeds.NeedsCache ||
                      analysis.RequiredServices.Any(s => s.Contains("redis", StringComparison.OrdinalIgnoreCase));

        // Detect storage needs
        var hasStorage = analysis.RequiredServices.Any(s =>
                            s.Contains("blob", StringComparison.OrdinalIgnoreCase) ||
                            s.Contains("storage", StringComparison.OrdinalIgnoreCase) ||
                            s.Contains("cdn", StringComparison.OrdinalIgnoreCase) ||
                            s.Contains("front door", StringComparison.OrdinalIgnoreCase));

        // Detect monitoring needs
        var hasMonitoring = analysis.RequiredServices.Any(s =>
                               s.Contains("insights", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("monitoring", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("application insights", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("observability", StringComparison.OrdinalIgnoreCase));

        // Detect managed identity needs
        var hasManagedIdentity = analysis.RequiredServices.Any(s =>
                                   s.Contains("managed identity", StringComparison.OrdinalIgnoreCase) ||
                                   s.Contains("identity", StringComparison.OrdinalIgnoreCase) ||
                                   s.Contains("rbac", StringComparison.OrdinalIgnoreCase));

        var tf = new System.Text.StringBuilder();
        tf.AppendLine(@"terraform {
  required_providers {
    azurerm = {
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }
  }
}

provider ""azurerm"" {
  features {}
}

variable ""location"" {
  description = ""Azure region""
  default     = ""East US""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# Resource Group
resource ""azurerm_resource_group"" ""honua"" {
  name     = ""honua-rg-${var.environment}""
  location = var.location

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Container Registry
resource ""azurerm_container_registry"" ""honua"" {
  name                = ""honuaacr${var.environment}""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  sku                 = ""Basic""
  admin_enabled       = true
}

# Container Apps Environment
resource ""azurerm_log_analytics_workspace"" ""honua"" {
  name                = ""honua-logs-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku                 = ""PerGB2018""
  retention_in_days   = 30

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");

        if (hasMonitoring)
        {
            tf.AppendLine(@"
# Application Insights
resource ""azurerm_application_insights"" ""honua"" {
  name                = ""honua-insights-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  workspace_id        = azurerm_log_analytics_workspace.honua.id
  application_type    = ""web""

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");
        }

        tf.AppendLine(@"
resource ""azurerm_container_app_environment"" ""honua"" {
  name                       = ""honua-env-${var.environment}""
  location                   = azurerm_resource_group.honua.location
  resource_group_name        = azurerm_resource_group.honua.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.honua.id

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Container App for Honua Server
resource ""azurerm_container_app"" ""honua"" {
  name                         = ""honua-server-${var.environment}""
  container_app_environment_id = azurerm_container_app_environment.honua.id
  resource_group_name          = azurerm_resource_group.honua.name
  revision_mode                = ""Single""

  template {
    container {
      name   = ""honua-server""
      image  = ""honuaio/honuaserver:latest""
      cpu    = 1.0
      memory = ""2Gi""

      env {
        name  = ""ASPNETCORE_ENVIRONMENT""
        value = var.environment
      }");

        if (hasDatabase)
        {
            tf.AppendLine(@"
      env {
        name  = ""HONUA__DATABASE__HOST""
        value = azurerm_postgresql_flexible_server.honua.fqdn
      }

      env {
        name  = ""HONUA__DATABASE__PORT""
        value = ""5432""
      }

      env {
        name  = ""HONUA__DATABASE__DATABASE""
        value = azurerm_postgresql_flexible_server_database.honua.name
      }

      env {
        name  = ""HONUA__DATABASE__USERNAME""
        value = azurerm_postgresql_flexible_server.honua.administrator_login
      }

      env {
        name  = ""HONUA__DATABASE__PASSWORD""
        value = azurerm_postgresql_flexible_server.honua.administrator_password
        secret_name = ""db-password""
      }");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
      env {
        name  = ""HONUA__CACHE__PROVIDER""
        value = ""redis""
      }

      env {
        name  = ""HONUA__CACHE__REDIS__HOST""
        value = azurerm_redis_cache.honua.hostname
      }

      env {
        name  = ""HONUA__CACHE__REDIS__PORT""
        value = ""6380""
      }

      env {
        name  = ""HONUA__CACHE__REDIS__PASSWORD""
        value = azurerm_redis_cache.honua.primary_access_key
        secret_name = ""redis-password""
      }");
        }

        tf.AppendLine(@"
    }
  }

  ingress {
    external_enabled = true
    target_port      = 8080

    traffic_weight {
      latest_revision = true
      percentage      = 100
    }
  }");

        if (hasManagedIdentity)
        {
            tf.AppendLine(@"
  identity { type = ""SystemAssigned"" }");
        }

        tf.AppendLine(@"
  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
# PostgreSQL Flexible Server
resource ""azurerm_postgresql_flexible_server"" ""honua"" {
  name                   = ""honua-psql-${var.environment}""
  resource_group_name    = azurerm_resource_group.honua.name
  location               = azurerm_resource_group.honua.location
  version                = ""15""
  administrator_login    = ""postgres""
  administrator_password = random_password.db_password.result
  zone                   = ""1""

  storage_mb = 32768
  sku_name   = ""B_Standard_B1ms""

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

resource ""azurerm_postgresql_flexible_server_database"" ""honua"" {
  name      = ""honua""
  server_id = azurerm_postgresql_flexible_server.honua.id
  charset   = ""UTF8""
  collation = ""en_US.utf8""
}

resource ""azurerm_postgresql_flexible_server_firewall_rule"" ""allow_azure"" {
  name             = ""allow-azure-services""
  server_id        = azurerm_postgresql_flexible_server.honua.id
  start_ip_address = ""0.0.0.0""
  end_ip_address   = ""0.0.0.0""
}

resource ""random_password"" ""db_password"" {
  length  = 32
  special = true
}");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
# Redis Cache
resource ""azurerm_redis_cache"" ""honua"" {
  name                = ""honua-redis-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  capacity            = 0
  family              = ""C""
  sku_name            = ""Basic""
  enable_non_ssl_port = false

  redis_configuration {
    maxmemory_policy = ""allkeys-lru""
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");
        }

        if (hasStorage)
        {
            tf.AppendLine(@"
# Storage Account for Blob Storage
resource ""azurerm_storage_account"" ""honua"" {
  name                     = ""honuastorage${var.environment}""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Blob Container for tile caching
resource ""azurerm_storage_container"" ""tiles"" {
  name                  = ""tiles""
  storage_account_name  = azurerm_storage_account.honua.name
  container_access_type = ""private""
}

# CDN Profile
resource ""azurerm_cdn_profile"" ""honua"" {
  name                = ""honua-cdn-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku                 = ""Standard_Microsoft""

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# CDN Endpoint
resource ""azurerm_cdn_endpoint"" ""honua"" {
  name                = ""honua-endpoint-${var.environment}""
  profile_name        = azurerm_cdn_profile.honua.name
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name

  origin {
    name      = ""honua-storage""
    host_name = azurerm_storage_account.honua.primary_blob_host
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");
        }

        tf.AppendLine(@"
# Outputs
output ""honua_server_url"" {
  value = ""https://${azurerm_container_app.honua.latest_revision_fqdn}""
  description = ""URL to access the Honua server""
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
output ""database_fqdn"" {
  value = azurerm_postgresql_flexible_server.honua.fqdn
}

output ""database_password"" {
  value     = random_password.db_password.result
  sensitive = true
}");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
output ""redis_hostname"" {
  value = azurerm_redis_cache.honua.hostname
}");
        }

        return tf.ToString();
    }


    public static string GenerateFunctions(DeploymentAnalysis analysis)
    {
        var hasDatabase = analysis.InfrastructureNeeds.NeedsDatabase ||
                         analysis.RequiredServices.Any(s => s.Contains("cosmos", StringComparison.OrdinalIgnoreCase) ||
                                                            s.Contains("database", StringComparison.OrdinalIgnoreCase));

        // Azure Functions typically use Cosmos DB for NoSQL scenarios
        var hasCosmosDb = analysis.RequiredServices.Any(s => s.Contains("cosmos", StringComparison.OrdinalIgnoreCase)) || hasDatabase;

        // Detect storage needs (always needed for Functions + blob storage for tiles)
        // Storage is always required for Azure Functions

        // Detect monitoring needs
        var hasMonitoring = analysis.RequiredServices.Any(s =>
                               s.Contains("insights", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("monitoring", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("application insights", StringComparison.OrdinalIgnoreCase) ||
                               s.Contains("observability", StringComparison.OrdinalIgnoreCase)) || true; // Always include monitoring

        var tf = new System.Text.StringBuilder();
        tf.AppendLine(@"terraform {
  required_providers {
    azurerm = {
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }
  }
}

provider ""azurerm"" {
  features {}
}

variable ""location"" {
  description = ""Azure region""
  default     = ""East US""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# Resource Group
resource ""azurerm_resource_group"" ""honua"" {
  name     = ""honua-functions-rg-${var.environment}""
  location = var.location

  tags = {
    Environment = var.environment
    Application = ""honua""
    DeploymentType = ""azure-functions""
  }
}

# Storage Account (required for Azure Functions)
resource ""azurerm_storage_account"" ""honua_functions"" {
  name                     = ""honuafunc${var.environment}""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Storage Account for Tiles (Blob Storage)
resource ""azurerm_storage_account"" ""honua_tiles"" {
  name                     = ""honuatiles${var.environment}""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  blob_properties {
    delete_retention_policy {
      days = 7
    }
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
    Purpose = ""tile-storage""
  }
}

# Blob Container for tiles
resource ""azurerm_storage_container"" ""tiles"" {
  name                  = ""tiles""
  storage_account_name  = azurerm_storage_account.honua_tiles.name
  container_access_type = ""private""
}");

        if (hasMonitoring)
        {
            tf.AppendLine(@"
# Log Analytics Workspace
resource ""azurerm_log_analytics_workspace"" ""honua"" {
  name                = ""honua-functions-logs-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku                 = ""PerGB2018""
  retention_in_days   = 30

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Application Insights
resource ""azurerm_application_insights"" ""honua"" {
  name                = ""honua-functions-insights-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  workspace_id        = azurerm_log_analytics_workspace.honua.id
  application_type    = ""web""

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");
        }

        // App Service Plan for Azure Functions (Premium for container support)
        tf.AppendLine(@"
# App Service Plan (Premium for container support and better performance)
resource ""azurerm_service_plan"" ""honua"" {
  name                = ""honua-functions-plan-${var.environment}""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  os_type             = ""Linux""
  sku_name            = ""EP1""  # Elastic Premium 1

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Azure Function App with Container Support
resource ""azurerm_linux_function_app"" ""honua"" {
  name                = ""honua-functions-${var.environment}""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location

  service_plan_id            = azurerm_service_plan.honua.id
  storage_account_name       = azurerm_storage_account.honua_functions.name
  storage_account_access_key = azurerm_storage_account.honua_functions.primary_access_key

  site_config {
    always_on = true

    application_stack {
      docker {
        registry_url = ""https://index.docker.io""
        image_name   = ""honuaio/honuaserver""
        image_tag    = ""latest""
      }
    }

    cors {
      allowed_origins = [""*""]
    }
  }

  app_settings = {
    ""FUNCTIONS_WORKER_RUNTIME""                   = ""dotnet-isolated""
    ""ASPNETCORE_ENVIRONMENT""                     = var.environment
    ""DOCKER_REGISTRY_SERVER_URL""                 = ""https://index.docker.io""
    ""WEBSITES_ENABLE_APP_SERVICE_STORAGE""        = ""false""
    ""HONUA__STORAGE__BLOB__CONNECTIONSTRING""     = azurerm_storage_account.honua_tiles.primary_connection_string
    ""HONUA__STORAGE__BLOB__CONTAINER""            = azurerm_storage_container.tiles.name");

        if (hasMonitoring)
        {
            tf.AppendLine(@"
    ""APPINSIGHTS_INSTRUMENTATIONKEY""             = azurerm_application_insights.honua.instrumentation_key
    ""APPLICATIONINSIGHTS_CONNECTION_STRING""      = azurerm_application_insights.honua.connection_string");
        }

        if (hasCosmosDb)
        {
            tf.AppendLine(@"
    ""HONUA__DATABASE__COSMOSDB__ENDPOINT""        = azurerm_cosmosdb_account.honua.endpoint
    ""HONUA__DATABASE__COSMOSDB__KEY""             = azurerm_cosmosdb_account.honua.primary_key
    ""HONUA__DATABASE__COSMOSDB__DATABASE""        = azurerm_cosmosdb_sql_database.honua.name");
        }

        tf.AppendLine(@"
  }

  identity {
    type = ""SystemAssigned""
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}");

        if (hasCosmosDb)
        {
            tf.AppendLine(@"
# Cosmos DB Account
resource ""azurerm_cosmosdb_account"" ""honua"" {
  name                = ""honua-cosmos-${var.environment}""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  offer_type          = ""Standard""
  kind                = ""GlobalDocumentDB""

  consistency_policy {
    consistency_level       = ""Session""
    max_interval_in_seconds = 5
    max_staleness_prefix    = 100
  }

  geo_location {
    location          = azurerm_resource_group.honua.location
    failover_priority = 0
  }

  capabilities {
    name = ""EnableServerless""
  }

  tags = {
    Environment = var.environment
    Application = ""honua""
  }
}

# Cosmos DB SQL Database
resource ""azurerm_cosmosdb_sql_database"" ""honua"" {
  name                = ""honua""
  resource_group_name = azurerm_resource_group.honua.name
  account_name        = azurerm_cosmosdb_account.honua.name
}

# Cosmos DB SQL Container for GIS data
resource ""azurerm_cosmosdb_sql_container"" ""gis_data"" {
  name                  = ""gis_data""
  resource_group_name   = azurerm_resource_group.honua.name
  account_name          = azurerm_cosmosdb_account.honua.name
  database_name         = azurerm_cosmosdb_sql_database.honua.name
  partition_key_path    = ""/id""
  partition_key_version = 1

  indexing_policy {
    indexing_mode = ""consistent""

    included_path {
      path = ""/*""
    }

    spatial_index {
      path = ""/geometry/?""
    }
  }
}

# Cosmos DB SQL Container for layers
resource ""azurerm_cosmosdb_sql_container"" ""layers"" {
  name                  = ""layers""
  resource_group_name   = azurerm_resource_group.honua.name
  account_name          = azurerm_cosmosdb_account.honua.name
  database_name         = azurerm_cosmosdb_sql_database.honua.name
  partition_key_path    = ""/layerId""
  partition_key_version = 1

  indexing_policy {
    indexing_mode = ""consistent""

    included_path {
      path = ""/*""
    }
  }
}");
        }

        tf.AppendLine(@"
# Outputs
output ""function_app_url"" {
  value       = ""https://${azurerm_linux_function_app.honua.default_hostname}""
  description = ""URL to access the Honua Functions App""
}

output ""function_app_name"" {
  value = azurerm_linux_function_app.honua.name
}

output ""storage_account_name"" {
  value = azurerm_storage_account.honua_tiles.name
}

output ""tiles_container_url"" {
  value = ""https://${azurerm_storage_account.honua_tiles.name}.blob.core.windows.net/${azurerm_storage_container.tiles.name}""
}");

        if (hasCosmosDb)
        {
            tf.AppendLine(@"
output ""cosmosdb_endpoint"" {
  value = azurerm_cosmosdb_account.honua.endpoint
}

output ""cosmosdb_database_name"" {
  value = azurerm_cosmosdb_sql_database.honua.name
}

output ""cosmosdb_primary_key"" {
  value     = azurerm_cosmosdb_account.honua.primary_key
  sensitive = true
}");
        }

        if (hasMonitoring)
        {
            tf.AppendLine(@"
output ""application_insights_key"" {
  value     = azurerm_application_insights.honua.instrumentation_key
  sensitive = true
}

output ""application_insights_connection_string"" {
  value     = azurerm_application_insights.honua.connection_string
  sensitive = true
}");
        }

        return tf.ToString();
    }

    // Helper methods for analysis

}
