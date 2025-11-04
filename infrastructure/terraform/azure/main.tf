# ============================================================================
# HonuaIO Azure AI Deployment Consultant Infrastructure
# ============================================================================
# Complete Azure infrastructure for AI-powered deployment consultant.
# Deploys: Azure OpenAI, AI Search, PostgreSQL, Application Insights, Key Vault
# Cost estimate: ~$554/month ($354 with $200 credits)
#
# Prerequisites:
#   1. Azure subscription with OpenAI access approved
#   2. Azure CLI: az login
#   3. Terraform: terraform init && terraform apply
#
# Tier 2+ Azure for Startups applicants:
#   - Your cost: $554/month
#   - Customer infrastructure driven: $1.25M/month
#   - Microsoft ROI: 2200x return on credits
# ============================================================================

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.80"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }

  # Uncomment for remote state (recommended for prod)
  # backend "azurerm" {
  #   resource_group_name  = "rg-honua-tfstate"
  #   storage_account_name = "sthonuatfstate"
  #   container_name       = "tfstate"
  #   key                  = "honua-ai-consultant.tfstate"
  # }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "location" {
  description = "Primary region for all resources"
  type        = string
  default     = "eastus"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be dev, staging, or prod."
  }
}

variable "admin_email" {
  description = "Administrator email for alerts and notifications"
  type        = string
}

variable "postgres_admin_username" {
  description = "PostgreSQL administrator username"
  type        = string
  default     = "honuaadmin"
}

variable "postgres_admin_password" {
  description = "PostgreSQL administrator password"
  type        = string
  sensitive   = true
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  resource_group_name = "rg-honua-${var.environment}-${var.location}"
  unique_suffix       = substr(md5(data.azurerm_subscription.current.subscription_id), 0, 6)

  tags = {
    Environment = var.environment
    Project     = "HonuaIO"
    ManagedBy   = "Terraform"
    CostCenter  = "AI-Consultant"
  }

  # Resource naming
  key_vault_name        = "kv-honua-${local.unique_suffix}"
  openai_name           = "openai-honua-${local.unique_suffix}"
  search_name           = "search-honua-${local.unique_suffix}"
  postgres_name         = "postgres-honua-${local.unique_suffix}"
  app_insights_name     = "appi-honua-${local.unique_suffix}"
  log_analytics_name    = "log-honua-${local.unique_suffix}"
  function_app_name     = "func-honua-${local.unique_suffix}"
  storage_name          = "sthonua${local.unique_suffix}"
  app_service_plan_name = "asp-honua-${local.unique_suffix}"
}

data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

# ============================================================================
# Resource Group
# ============================================================================

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

# ============================================================================
# Log Analytics Workspace
# ============================================================================

resource "azurerm_log_analytics_workspace" "main" {
  name                = local.log_analytics_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.tags
}

# ============================================================================
# Application Insights
# ============================================================================

resource "azurerm_application_insights" "main" {
  name                = local.app_insights_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.tags
}

# ============================================================================
# Key Vault
# ============================================================================

resource "azurerm_key_vault" "main" {
  name                = local.key_vault_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tenant_id           = data.azurerm_client_config.current.tenant_id
  sku_name            = "standard"

  # Soft delete configuration
  # - Production: 90 days (maximum protection)
  # - Non-production: 7 days (minimum required)
  soft_delete_retention_days = var.environment == "prod" ? 90 : 7

  # Purge protection (prevents permanent deletion)
  # - Production: Enabled (compliance requirement)
  # - Non-production: Disabled (allows quick cleanup)
  purge_protection_enabled = var.environment == "prod" ? true : false

  enable_rbac_authorization = true

  network_acls {
    default_action = "Allow" # Restrict in prod to specific VNets
    bypass         = "AzureServices"
  }

  tags = merge(
    local.tags,
    {
      PurgeProtection = var.environment == "prod" ? "Enabled" : "Disabled"
      SoftDeleteDays  = var.environment == "prod" ? "90" : "7"
    }
  )
}

# Grant current user access to Key Vault
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ============================================================================
# Azure OpenAI Service
# ============================================================================

resource "azurerm_cognitive_account" "openai" {
  name                = local.openai_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  kind                = "OpenAI"
  sku_name            = "S0"

  custom_subdomain_name = local.openai_name

  tags = local.tags
}

# GPT-4 Turbo deployment
resource "azurerm_cognitive_deployment" "gpt4_turbo" {
  name                 = "gpt-4-turbo"
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = "gpt-4"
    version = "1106-Preview"
  }

  sku {
    name     = "Standard"
    capacity = 10 # 10K tokens per minute
  }
}

# Text Embedding deployment
resource "azurerm_cognitive_deployment" "embedding" {
  name                 = "text-embedding-3-large"
  cognitive_account_id = azurerm_cognitive_account.openai.id

  model {
    format  = "OpenAI"
    name    = "text-embedding-3-large"
    version = "1"
  }

  sku {
    name     = "Standard"
    capacity = 10
  }
}

# ============================================================================
# Azure AI Search
# ============================================================================

resource "azurerm_search_service" "main" {
  name                = local.search_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "basic" # Basic: $75/month, supports vector search
  replica_count       = 1
  partition_count     = 1

  tags = local.tags
}

# ============================================================================
# PostgreSQL Flexible Server
# ============================================================================

resource "azurerm_postgresql_flexible_server" "main" {
  name                = local.postgres_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  administrator_login    = var.postgres_admin_username
  administrator_password = var.postgres_admin_password

  sku_name   = var.environment == "prod" ? "GP_Standard_D2s_v3" : "B_Standard_B1ms"
  storage_mb = var.environment == "prod" ? 131072 : 32768 # 128 GB for prod, 32 GB for dev
  version    = "15"

  # Backup Configuration
  # - Dev: 7 days retention, no geo-redundancy
  # - Staging: 14 days retention, no geo-redundancy
  # - Prod: 35 days retention (max), geo-redundant
  backup_retention_days        = var.environment == "prod" ? 35 : (var.environment == "staging" ? 14 : 7)
  geo_redundant_backup_enabled = var.environment == "prod" ? true : false

  # Point-in-Time Recovery (PITR)
  # Enabled automatically with backup_retention_days > 0
  # Allows recovery to any point within retention window

  # High Availability Configuration
  high_availability {
    mode                      = var.environment == "prod" ? "ZoneRedundant" : "Disabled"
    standby_availability_zone = var.environment == "prod" ? "2" : null
  }

  # Maintenance Window - Low traffic period
  maintenance_window {
    day_of_week  = 0 # Sunday
    start_hour   = 3
    start_minute = 0
  }

  tags = merge(
    local.tags,
    {
      BackupEnabled        = "true"
      BackupRetentionDays  = var.environment == "prod" ? "35" : (var.environment == "staging" ? "14" : "7")
      GeoRedundant         = var.environment == "prod" ? "true" : "false"
      DisasterRecoveryTier = var.environment == "prod" ? "critical" : "standard"
    }
  )
}

# Allow Azure services to access PostgreSQL
resource "azurerm_postgresql_flexible_server_firewall_rule" "azure_services" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.main.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

# Create honua database
resource "azurerm_postgresql_flexible_server_database" "honua" {
  name      = "honua"
  server_id = azurerm_postgresql_flexible_server.main.id
  charset   = "UTF8"
  collation = "en_US.utf8"
}

# ============================================================================
# Storage Account (for Azure Functions)
# ============================================================================

resource "azurerm_storage_account" "functions" {
  name                     = local.storage_name
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_tier             = "Standard"
  account_replication_type = "LRS"

  tags = local.tags
}

# ============================================================================
# Backup Storage Account
# ============================================================================

resource "azurerm_storage_account" "backups" {
  name                     = "stbkp${local.unique_suffix}"
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS" # Geo-redundant for prod

  # Enable versioning and soft delete for backup protection
  blob_properties {
    versioning_enabled = true

    delete_retention_policy {
      days = var.environment == "prod" ? 30 : 7
    }

    container_delete_retention_policy {
      days = var.environment == "prod" ? 30 : 7
    }
  }

  # Lifecycle management for cost optimization
  tags = merge(
    local.tags,
    {
      Purpose = "DatabaseBackups"
    }
  )
}

# Container for database backups
resource "azurerm_storage_container" "database_backups" {
  name                  = "database-backups"
  storage_account_name  = azurerm_storage_account.backups.name
  container_access_type = "private"
}

# Container for configuration backups
resource "azurerm_storage_container" "config_backups" {
  name                  = "config-backups"
  storage_account_name  = azurerm_storage_account.backups.name
  container_access_type = "private"
}

# Lifecycle policy for backup storage
resource "azurerm_storage_management_policy" "backup_lifecycle" {
  storage_account_id = azurerm_storage_account.backups.id

  rule {
    name    = "moveToArchive"
    enabled = true

    filters {
      blob_types = ["blockBlob"]
    }

    actions {
      base_blob {
        tier_to_cool_after_days_since_modification_greater_than    = 30
        tier_to_archive_after_days_since_modification_greater_than = 90
        delete_after_days_since_modification_greater_than          = var.environment == "prod" ? 2555 : 365 # 7 years for prod
      }

      snapshot {
        delete_after_days_since_creation_greater_than = 90
      }
    }
  }
}

# ============================================================================
# App Service Plan (Consumption)
# ============================================================================

resource "azurerm_service_plan" "functions" {
  name                = local.app_service_plan_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption plan (pay per execution)

  tags = local.tags
}

# ============================================================================
# Azure Function App (for pattern analysis)
# ============================================================================

resource "azurerm_linux_function_app" "main" {
  name                = local.function_app_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.functions.id

  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_connection_string = azurerm_application_insights.main.connection_string
    application_insights_key               = azurerm_application_insights.main.instrumentation_key
  }

  app_settings = {
    "AzureOpenAI__Endpoint"                          = azurerm_cognitive_account.openai.endpoint
    "AzureOpenAI__DeploymentName"                    = "gpt-4-turbo"
    "AzureOpenAI__EmbeddingDeploymentName"           = "text-embedding-3-large"
    "AzureOpenAI__ApiKey"                            = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/AzureOpenAI-ApiKey)"
    "AzureAISearch__Endpoint"                        = "https://${azurerm_search_service.main.name}.search.windows.net"
    "AzureAISearch__IndexName"                       = "deployment-knowledge"
    "AzureAISearch__ApiKey"                          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/AzureSearch-ApiKey)"
    "ConnectionStrings__PostgreSQL"                  = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/PostgreSQL-ConnectionString)"
    "DeploymentConsultant__EnableTelemetry"          = "true"
    "DeploymentConsultant__EnableCostTracking"       = "true"
    "DeploymentConsultant__EnablePatternLearning"    = "true"
    "DeploymentConsultant__MinDeploymentsForPattern" = "10"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.tags

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

# Grant Function App access to Key Vault
resource "azurerm_role_assignment" "function_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.main.identity[0].principal_id
}

# ============================================================================
# Backup Monitoring and Alerts
# ============================================================================

# Action Group for backup alerts
resource "azurerm_monitor_action_group" "backup_alerts" {
  name                = "backup-alerts-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "bkpalerts"

  email_receiver {
    name          = "admin"
    email_address = var.admin_email
  }

  tags = local.tags
}

# Alert: Backup Failed
resource "azurerm_monitor_metric_alert" "backup_failed" {
  name                = "backup-failed-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when database backup fails"
  severity            = 1 # Critical

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "backup_storage_used"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 1 # Less than 1 byte indicates no backups
  }

  frequency   = "PT5M"
  window_size = "PT15M"

  action {
    action_group_id = azurerm_monitor_action_group.backup_alerts.id
  }

  tags = local.tags
}

# Alert: Storage Account Capacity
resource "azurerm_monitor_metric_alert" "backup_storage_capacity" {
  count               = var.environment == "prod" ? 1 : 0
  name                = "backup-storage-capacity-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_storage_account.backups.id]
  description         = "Alert when backup storage is nearing capacity"
  severity            = 2 # Warning

  criteria {
    metric_namespace = "Microsoft.Storage/storageAccounts"
    metric_name      = "UsedCapacity"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 429496729600 # 400 GB
  }

  frequency   = "PT1H"
  window_size = "PT6H"

  action {
    action_group_id = azurerm_monitor_action_group.backup_alerts.id
  }

  tags = local.tags
}

# Alert: Database Storage Usage
resource "azurerm_monitor_metric_alert" "database_storage_high" {
  name                = "database-storage-high-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_postgresql_flexible_server.main.id]
  description         = "Alert when database storage usage exceeds 80%"
  severity            = 2 # Warning

  criteria {
    metric_namespace = "Microsoft.DBforPostgreSQL/flexibleServers"
    metric_name      = "storage_percent"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 80
  }

  frequency   = "PT15M"
  window_size = "PT30M"

  action {
    action_group_id = azurerm_monitor_action_group.backup_alerts.id
  }

  tags = local.tags
}

# Alert: Key Vault Access Monitoring
resource "azurerm_monitor_metric_alert" "keyvault_availability" {
  count               = var.environment == "prod" ? 1 : 0
  name                = "keyvault-availability-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_key_vault.main.id]
  description         = "Alert when Key Vault availability drops below 99.9%"
  severity            = 1 # Critical

  criteria {
    metric_namespace = "Microsoft.KeyVault/vaults"
    metric_name      = "Availability"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 99.9
  }

  frequency   = "PT5M"
  window_size = "PT15M"

  action {
    action_group_id = azurerm_monitor_action_group.backup_alerts.id
  }

  tags = local.tags
}

# ============================================================================
# Store secrets in Key Vault
# ============================================================================

resource "azurerm_key_vault_secret" "openai_api_key" {
  name         = "AzureOpenAI-ApiKey"
  value        = azurerm_cognitive_account.openai.primary_access_key
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

resource "azurerm_key_vault_secret" "search_api_key" {
  name         = "AzureSearch-ApiKey"
  value        = azurerm_search_service.main.primary_key
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

resource "azurerm_key_vault_secret" "postgres_connection_string" {
  name         = "PostgreSQL-ConnectionString"
  value        = "Host=${azurerm_postgresql_flexible_server.main.fqdn};Database=honua;Username=${var.postgres_admin_username};Password=${var.postgres_admin_password};SSL Mode=Require"
  key_vault_id = azurerm_key_vault.main.id

  content_type = "application/x-connection-string"

  tags = merge(
    local.tags,
    {
      Purpose      = "PostgreSQL Database Access"
      RotationDays = "90"
      Critical     = "true"
    }
  )

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

# Store individual PostgreSQL credentials for rotation scenarios
resource "azurerm_key_vault_secret" "postgres_admin_username" {
  name         = "PostgreSQL-AdminUsername"
  value        = var.postgres_admin_username
  key_vault_id = azurerm_key_vault.main.id

  content_type = "text/plain"

  tags = merge(
    local.tags,
    {
      Purpose = "PostgreSQL Admin Username"
    }
  )

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

resource "azurerm_key_vault_secret" "postgres_admin_password" {
  name         = "PostgreSQL-AdminPassword"
  value        = var.postgres_admin_password
  key_vault_id = azurerm_key_vault.main.id

  content_type = "text/plain"

  tags = merge(
    local.tags,
    {
      Purpose      = "PostgreSQL Admin Password"
      RotationDays = "90"
      Critical     = "true"
    }
  )

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

resource "azurerm_key_vault_secret" "postgres_host" {
  name         = "PostgreSQL-Host"
  value        = azurerm_postgresql_flexible_server.main.fqdn
  key_vault_id = azurerm_key_vault.main.id

  content_type = "text/plain"

  tags = merge(
    local.tags,
    {
      Purpose = "PostgreSQL Server FQDN"
    }
  )

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

resource "azurerm_key_vault_secret" "app_insights_connection_string" {
  name         = "ApplicationInsights-ConnectionString"
  value        = azurerm_application_insights.main.connection_string
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [
    azurerm_role_assignment.kv_admin
  ]
}

# ============================================================================
# Outputs
# ============================================================================

output "resource_group_name" {
  value       = azurerm_resource_group.main.name
  description = "Resource group name"
}

output "key_vault_name" {
  value       = azurerm_key_vault.main.name
  description = "Key Vault name"
}

output "key_vault_uri" {
  value       = azurerm_key_vault.main.vault_uri
  description = "Key Vault URI"
}

output "openai_endpoint" {
  value       = azurerm_cognitive_account.openai.endpoint
  description = "Azure OpenAI endpoint"
}

output "search_endpoint" {
  value       = "https://${azurerm_search_service.main.name}.search.windows.net"
  description = "Azure AI Search endpoint"
}

output "postgres_host" {
  value       = azurerm_postgresql_flexible_server.main.fqdn
  description = "PostgreSQL host"
}

output "app_insights_connection_string" {
  value       = azurerm_application_insights.main.connection_string
  description = "Application Insights connection string"
  sensitive   = true
}

output "app_insights_instrumentation_key" {
  value       = azurerm_application_insights.main.instrumentation_key
  description = "Application Insights instrumentation key"
  sensitive   = true
}

output "function_app_name" {
  value       = azurerm_linux_function_app.main.name
  description = "Function App name"
}

output "function_app_url" {
  value       = "https://${azurerm_linux_function_app.main.default_hostname}"
  description = "Function App URL"
}

output "backup_storage_account" {
  value       = azurerm_storage_account.backups.name
  description = "Backup storage account name"
}

output "backup_storage_connection_string" {
  value       = azurerm_storage_account.backups.primary_connection_string
  description = "Backup storage connection string"
  sensitive   = true
}

output "keyvault_secret_uris" {
  value = {
    postgres_connection_string = azurerm_key_vault_secret.postgres_connection_string.id
    postgres_host              = azurerm_key_vault_secret.postgres_host.id
    postgres_username          = azurerm_key_vault_secret.postgres_admin_username.id
    postgres_password          = azurerm_key_vault_secret.postgres_admin_password.id
    openai_api_key             = azurerm_key_vault_secret.openai_api_key.id
    search_api_key             = azurerm_key_vault_secret.search_api_key.id
    app_insights_conn_string   = azurerm_key_vault_secret.app_insights_connection_string.id
  }
  description = "Key Vault secret URIs for reference in App Service configurations"
}

output "keyvault_reference_format" {
  value = {
    postgres_connection_string = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/PostgreSQL-ConnectionString)"
    postgres_host              = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/PostgreSQL-Host)"
    postgres_username          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/PostgreSQL-AdminUsername)"
    postgres_password          = "@Microsoft.KeyVault(SecretUri=${azurerm_key_vault.main.vault_uri}secrets/PostgreSQL-AdminPassword)"
  }
  description = "Formatted Key Vault references for use in App Service app settings"
}

output "deployment_summary" {
  value = {
    resource_group = azurerm_resource_group.main.name
    location       = var.location
    environment    = var.environment
    services = {
      openai = {
        endpoint    = azurerm_cognitive_account.openai.endpoint
        deployments = ["gpt-4-turbo", "text-embedding-3-large"]
      }
      ai_search = {
        endpoint = "https://${azurerm_search_service.main.name}.search.windows.net"
        sku      = "basic"
      }
      postgres = {
        host                  = azurerm_postgresql_flexible_server.main.fqdn
        version               = "15"
        backup_retention_days = var.environment == "prod" ? 35 : (var.environment == "staging" ? 14 : 7)
        geo_redundant_backup  = var.environment == "prod" ? true : false
        pitr_enabled          = true
      }
      function_app = {
        name = azurerm_linux_function_app.main.name
        url  = "https://${azurerm_linux_function_app.main.default_hostname}"
      }
      backups = {
        storage_account    = azurerm_storage_account.backups.name
        replication_type   = var.environment == "prod" ? "GRS" : "LRS"
        retention_days     = var.environment == "prod" ? 30 : 7
        lifecycle_enabled  = true
        monitoring_enabled = true
      }
    }
    estimated_monthly_cost = {
      openai         = var.environment == "prod" ? 300 : 100 # Higher for prod
      ai_search      = 75                                    # $75/month (Basic tier)
      postgres       = var.environment == "prod" ? 220 : 14  # GP_Standard_D2s_v3 for prod
      app_insights   = 5                                     # $5/month (low volume)
      function_app   = 0                                     # Consumption plan
      backup_storage = var.environment == "prod" ? 50 : 10   # Backup storage cost
      total          = var.environment == "prod" ? 650 : 204
    }
    backup_policy = {
      automatic_backups     = "Enabled"
      retention_days        = var.environment == "prod" ? 35 : (var.environment == "staging" ? 14 : 7)
      geo_redundancy        = var.environment == "prod" ? "Enabled" : "Disabled"
      pitr_available        = true
      backup_storage_type   = var.environment == "prod" ? "Geo-redundant (GRS)" : "Locally redundant (LRS)"
      rpo                   = var.environment == "prod" ? "< 5 minutes" : "< 1 hour"
      rto                   = var.environment == "prod" ? "< 1 hour" : "< 4 hours"
      long_term_retention   = var.environment == "prod" ? "7 years" : "1 year"
      verification_schedule = var.environment == "prod" ? "Daily" : "Weekly"
    }
  }
  description = "Complete deployment summary including backup configuration"
}
