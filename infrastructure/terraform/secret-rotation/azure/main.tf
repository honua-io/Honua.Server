# ============================================================================
# Azure Secret Rotation Infrastructure
# ============================================================================
# Terraform configuration for automated secret rotation in Azure
#
# Resources:
# - Azure Function for rotation logic
# - Timer trigger for automatic rotation
# - Action Group for notifications
# - Key Vault secrets with rotation enabled
# - Managed Identity for Function App
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
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "eastus"
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  default     = "prod"
}

variable "project_name" {
  description = "Project name for resource naming"
  type        = string
  default     = "honua"
}

variable "rotation_days" {
  description = "Number of days between automatic rotations"
  type        = number
  default     = 90
}

variable "notification_email" {
  description = "Email address for rotation notifications"
  type        = string
}

variable "notification_webhook_url" {
  description = "Webhook URL for notifications (Teams/Slack)"
  type        = string
  default     = ""
}

variable "postgres_host" {
  description = "PostgreSQL server hostname"
  type        = string
}

variable "postgres_port" {
  description = "PostgreSQL server port"
  type        = number
  default     = 5432
}

variable "postgres_database" {
  description = "PostgreSQL database name"
  type        = string
  default     = "honua"
}

variable "postgres_master_username" {
  description = "PostgreSQL master username"
  type        = string
  sensitive   = true
}

variable "postgres_master_password" {
  description = "PostgreSQL master password"
  type        = string
  sensitive   = true
}

variable "postgres_app_username" {
  description = "PostgreSQL application username"
  type        = string
  default     = "honua_app"
}

variable "api_endpoint" {
  description = "API endpoint for testing"
  type        = string
}

variable "tags" {
  description = "Additional tags for resources"
  type        = map(string)
  default     = {}
}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  resource_group_name = "rg-${var.project_name}-rotation-${var.environment}"
  unique_suffix       = substr(md5(data.azurerm_subscription.current.subscription_id), 0, 6)

  common_tags = merge(
    var.tags,
    {
      Environment = var.environment
      Project     = var.project_name
      ManagedBy   = "Terraform"
      Component   = "SecretRotation"
    }
  )

  key_vault_name      = "kv-${var.project_name}-${local.unique_suffix}"
  function_app_name   = "func-rotation-${local.unique_suffix}"
  storage_name        = "strotation${local.unique_suffix}"
  app_service_plan    = "asp-rotation-${var.environment}"
  app_insights_name   = "appi-rotation-${var.environment}"
  log_analytics_name  = "log-rotation-${var.environment}"
}

data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

# ============================================================================
# Resource Group
# ============================================================================

resource "azurerm_resource_group" "main" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.common_tags
}

# ============================================================================
# Log Analytics & Application Insights
# ============================================================================

resource "azurerm_log_analytics_workspace" "main" {
  name                = local.log_analytics_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.common_tags
}

resource "azurerm_application_insights" "main" {
  name                = local.app_insights_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  workspace_id        = azurerm_log_analytics_workspace.main.id
  application_type    = "web"
  tags                = local.common_tags
}

# ============================================================================
# Key Vault
# ============================================================================

resource "azurerm_key_vault" "main" {
  name                        = local.key_vault_name
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"
  soft_delete_retention_days  = 90
  purge_protection_enabled    = var.environment == "prod"
  enable_rbac_authorization   = true

  network_acls {
    default_action = "Allow" # Restrict to VNet in production
    bypass         = "AzureServices"
  }

  tags = local.common_tags
}

# Grant current user Key Vault Administrator access
resource "azurerm_role_assignment" "kv_admin_current_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# ============================================================================
# Storage Account for Function App
# ============================================================================

resource "azurerm_storage_account" "functions" {
  name                     = local.storage_name
  location                 = azurerm_resource_group.main.location
  resource_group_name      = azurerm_resource_group.main.name
  account_tier             = "Standard"
  account_replication_type = "LRS"
  min_tls_version          = "TLS1_2"

  tags = local.common_tags
}

# ============================================================================
# App Service Plan
# ============================================================================

resource "azurerm_service_plan" "functions" {
  name                = local.app_service_plan
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption plan

  tags = local.common_tags
}

# ============================================================================
# Function App
# ============================================================================

resource "azurerm_linux_function_app" "rotation" {
  name                = local.function_app_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.functions.id

  storage_account_name       = azurerm_storage_account.functions.name
  storage_account_access_key = azurerm_storage_account.functions.primary_access_key

  site_config {
    application_stack {
      node_version = "18"
    }

    application_insights_connection_string = azurerm_application_insights.main.connection_string
    application_insights_key               = azurerm_application_insights.main.instrumentation_key
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME"       = "node"
    "WEBSITE_NODE_DEFAULT_VERSION"   = "~18"
    "KEY_VAULT_URL"                  = azurerm_key_vault.main.vault_uri
    "POSTGRES_MASTER_SECRET_NAME"    = azurerm_key_vault_secret.postgres_master.name
    "DATABASE_SECRET_NAME"           = azurerm_key_vault_secret.postgres_app.name
    "API_ENDPOINT"                   = var.api_endpoint
    "NOTIFICATION_WEBHOOK_URL"       = var.notification_webhook_url
    "ENVIRONMENT"                    = var.environment
  }

  identity {
    type = "SystemAssigned"
  }

  tags = local.common_tags

  depends_on = [
    azurerm_role_assignment.kv_admin_current_user
  ]
}

# Grant Function App access to Key Vault
resource "azurerm_role_assignment" "function_kv_secrets_officer" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = azurerm_linux_function_app.rotation.identity[0].principal_id
}

# ============================================================================
# Key Vault Secrets
# ============================================================================

# PostgreSQL Master Credentials
resource "azurerm_key_vault_secret" "postgres_master" {
  name         = "postgres-master"
  value        = jsonencode({
    type     = "postgresql"
    host     = var.postgres_host
    port     = var.postgres_port
    database = var.postgres_database
    username = var.postgres_master_username
    password = var.postgres_master_password
  })
  key_vault_id = azurerm_key_vault.main.id

  tags = {
    type       = "postgresql"
    autoRotate = "false" # Master rotated manually
  }

  depends_on = [
    azurerm_role_assignment.kv_admin_current_user
  ]
}

# PostgreSQL Application Credentials (auto-rotated)
resource "azurerm_key_vault_secret" "postgres_app" {
  name         = "postgres-app"
  value        = jsonencode({
    type     = "postgresql"
    host     = var.postgres_host
    port     = var.postgres_port
    database = var.postgres_database
    username = var.postgres_app_username
    password = "ChangeMe123!" # Will be rotated
  })
  key_vault_id = azurerm_key_vault.main.id

  tags = {
    type       = "postgresql"
    autoRotate = "true"
  }

  depends_on = [
    azurerm_role_assignment.kv_admin_current_user
  ]

  lifecycle {
    ignore_changes = [value] # Managed by rotation function
  }
}

# JWT Signing Key (auto-rotated)
resource "azurerm_key_vault_secret" "jwt_signing_key" {
  name         = "jwt-signing-key"
  value        = jsonencode({
    type       = "jwt-signing-key"
    signingKey = base64encode(random_bytes.jwt_key.result)
  })
  key_vault_id = azurerm_key_vault.main.id

  tags = {
    type       = "jwt-signing-key"
    autoRotate = "true"
  }

  depends_on = [
    azurerm_role_assignment.kv_admin_current_user
  ]

  lifecycle {
    ignore_changes = [value] # Managed by rotation function
  }
}

resource "random_bytes" "jwt_key" {
  length = 32 # 256 bits
}

# ============================================================================
# Monitoring & Alerts
# ============================================================================

# Action Group for notifications
resource "azurerm_monitor_action_group" "rotation_alerts" {
  name                = "rotation-alerts-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "rotation"

  email_receiver {
    name          = "admin"
    email_address = var.notification_email
  }

  dynamic "webhook_receiver" {
    for_each = var.notification_webhook_url != "" ? [1] : []
    content {
      name        = "webhook"
      service_uri = var.notification_webhook_url
    }
  }

  tags = local.common_tags
}

# Alert: Function failures
resource "azurerm_monitor_metric_alert" "function_errors" {
  name                = "rotation-function-errors-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.rotation.id]
  description         = "Alert when rotation function fails"
  severity            = 1 # Critical

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "FunctionExecutionCount"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 0

    dimension {
      name     = "Status"
      operator = "Include"
      values   = ["Failed"]
    }
  }

  frequency   = "PT5M"
  window_size = "PT15M"

  action {
    action_group_id = azurerm_monitor_action_group.rotation_alerts.id
  }

  tags = local.common_tags
}

# Alert: Function duration
resource "azurerm_monitor_metric_alert" "function_duration" {
  name                = "rotation-function-duration-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_linux_function_app.rotation.id]
  description         = "Alert when rotation takes too long"
  severity            = 2 # Warning

  criteria {
    metric_namespace = "Microsoft.Web/sites"
    metric_name      = "FunctionExecutionUnits"
    aggregation      = "Average"
    operator         = "GreaterThan"
    threshold        = 240000 # 4 minutes
  }

  frequency   = "PT5M"
  window_size = "PT15M"

  action {
    action_group_id = azurerm_monitor_action_group.rotation_alerts.id
  }

  tags = local.common_tags
}

# ============================================================================
# Diagnostic Settings
# ============================================================================

resource "azurerm_monitor_diagnostic_setting" "function_diagnostics" {
  name                       = "function-diagnostics"
  target_resource_id         = azurerm_linux_function_app.rotation.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log {
    category = "FunctionAppLogs"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

resource "azurerm_monitor_diagnostic_setting" "keyvault_diagnostics" {
  name                       = "keyvault-diagnostics"
  target_resource_id         = azurerm_key_vault.main.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  enabled_log {
    category = "AuditEvent"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

# ============================================================================
# Outputs
# ============================================================================

output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "function_app_name" {
  description = "Name of the rotation function app"
  value       = azurerm_linux_function_app.rotation.name
}

output "function_app_url" {
  description = "URL of the rotation function app"
  value       = "https://${azurerm_linux_function_app.rotation.default_hostname}"
}

output "key_vault_name" {
  description = "Name of the Key Vault"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "URI of the Key Vault"
  value       = azurerm_key_vault.main.vault_uri
}

output "postgres_app_secret_name" {
  description = "Name of PostgreSQL app secret"
  value       = azurerm_key_vault_secret.postgres_app.name
}

output "jwt_secret_name" {
  description = "Name of JWT signing key secret"
  value       = azurerm_key_vault_secret.jwt_signing_key.name
}

output "app_insights_connection_string" {
  description = "Application Insights connection string"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

output "rotation_schedule" {
  description = "Secret rotation schedule"
  value       = "Every ${var.rotation_days} days (Timer Trigger: 0 0 0 */${var.rotation_days} * *)"
}
