# ============================================================================
# Terraform State Backend Infrastructure - Azure
# ============================================================================
# This module creates the necessary Azure infrastructure for Terraform remote
# state storage with state locking:
#   - Storage Account for state storage (encrypted, versioned)
#   - Blob container for state files
#   - State locking via blob leases (native Azure feature)
#
# WARNING: This must be deployed BEFORE using remote state
# Run: terraform init && terraform apply
# Then update your backend configuration to use these resources
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

  # Initially, use local state to create the backend infrastructure
  # After creation, you can migrate to remote state
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = var.environment != "prod"
      recover_soft_deleted_key_vaults = true
    }
    resource_group {
      prevent_deletion_if_contains_resources = var.environment == "prod"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "location" {
  description = "Azure region for state backend resources"
  type        = string
  default     = "eastus"
}

variable "environment" {
  description = "Environment name (used for naming and tagging)"
  type        = string
  default     = "shared"
}

variable "resource_group_name" {
  description = "Name of the resource group for state backend (if not specified, will be auto-generated)"
  type        = string
  default     = ""
}

variable "storage_account_name" {
  description = "Name of the storage account for Terraform state (3-24 lowercase alphanumeric, if not specified will be auto-generated)"
  type        = string
  default     = ""
}

variable "container_name" {
  description = "Name of the blob container for state files"
  type        = string
  default     = "tfstate"
}

variable "enable_versioning" {
  description = "Enable blob versioning for state history"
  type        = bool
  default     = true
}

variable "enable_soft_delete" {
  description = "Enable soft delete for state files (recommended for production)"
  type        = bool
  default     = true
}

variable "soft_delete_retention_days" {
  description = "Number of days to retain soft-deleted state files"
  type        = number
  default     = 30
}

variable "enable_geo_replication" {
  description = "Enable geo-redundant storage (GRS) for disaster recovery"
  type        = bool
  default     = false
}

variable "allowed_ip_ranges" {
  description = "IP ranges allowed to access the storage account (empty list = allow all)"
  type        = list(string)
  default     = []
}

variable "enable_private_endpoint" {
  description = "Enable private endpoint for storage account (recommended for production)"
  type        = bool
  default     = false
}

variable "virtual_network_id" {
  description = "Virtual network ID for private endpoint (required if enable_private_endpoint is true)"
  type        = string
  default     = null
}

variable "subnet_id" {
  description = "Subnet ID for private endpoint (required if enable_private_endpoint is true)"
  type        = string
  default     = null
}

# ============================================================================
# Data Sources
# ============================================================================

data "azurerm_client_config" "current" {}
data "azurerm_subscription" "current" {}

# ============================================================================
# Local Variables
# ============================================================================

locals {
  # Generate unique suffix for naming
  unique_suffix = substr(md5(data.azurerm_subscription.current.subscription_id), 0, 6)

  # Generate resource names if not provided
  resource_group_name = coalesce(
    var.resource_group_name,
    "rg-honua-tfstate-${var.environment}"
  )

  storage_account_name = coalesce(
    var.storage_account_name,
    "sthonuatfstate${local.unique_suffix}"
  )

  # Storage account replication type
  storage_replication_type = var.enable_geo_replication ? "GRS" : "LRS"

  tags = {
    Environment = var.environment
    Project     = "HonuaIO"
    ManagedBy   = "Terraform"
    Component   = "StateBackend"
    Purpose     = "TerraformStateStorage"
  }
}

# ============================================================================
# Resource Group
# ============================================================================

resource "azurerm_resource_group" "tfstate" {
  name     = local.resource_group_name
  location = var.location
  tags     = local.tags
}

# ============================================================================
# Storage Account for Terraform State
# ============================================================================

resource "azurerm_storage_account" "tfstate" {
  name                     = local.storage_account_name
  resource_group_name      = azurerm_resource_group.tfstate.name
  location                 = azurerm_resource_group.tfstate.location
  account_tier             = "Standard"
  account_replication_type = local.storage_replication_type
  account_kind             = "StorageV2"

  # Enable HTTPS only
  enable_https_traffic_only = true
  min_tls_version           = "TLS1_2"

  # Enable infrastructure encryption for additional security
  infrastructure_encryption_enabled = true

  # Enable blob versioning for state history
  blob_properties {
    versioning_enabled = var.enable_versioning

    # Enable soft delete for state files
    dynamic "delete_retention_policy" {
      for_each = var.enable_soft_delete ? [1] : []
      content {
        days = var.soft_delete_retention_days
      }
    }

    # Enable container soft delete
    dynamic "container_delete_retention_policy" {
      for_each = var.enable_soft_delete ? [1] : []
      content {
        days = var.soft_delete_retention_days
      }
    }

    # Enable change feed for audit trail
    change_feed_enabled = true
  }

  # Network rules
  network_rules {
    default_action = length(var.allowed_ip_ranges) > 0 ? "Deny" : "Allow"
    ip_rules       = var.allowed_ip_ranges
    bypass         = ["AzureServices"]
  }

  tags = merge(
    local.tags,
    {
      Name = local.storage_account_name
    }
  )
}

# ============================================================================
# Blob Container for State Files
# ============================================================================

resource "azurerm_storage_container" "tfstate" {
  name                  = var.container_name
  storage_account_name  = azurerm_storage_account.tfstate.name
  container_access_type = "private"
}

# ============================================================================
# Private Endpoint (Optional, for Enhanced Security)
# ============================================================================

resource "azurerm_private_endpoint" "tfstate" {
  count               = var.enable_private_endpoint ? 1 : 0
  name                = "pe-${local.storage_account_name}"
  location            = azurerm_resource_group.tfstate.location
  resource_group_name = azurerm_resource_group.tfstate.name
  subnet_id           = var.subnet_id

  private_service_connection {
    name                           = "psc-${local.storage_account_name}"
    private_connection_resource_id = azurerm_storage_account.tfstate.id
    subresource_names              = ["blob"]
    is_manual_connection           = false
  }

  tags = local.tags
}

# ============================================================================
# Storage Account Logging and Monitoring
# ============================================================================

# Create a separate storage account for logs
resource "azurerm_storage_account" "tfstate_logs" {
  name                     = "${substr(local.storage_account_name, 0, 18)}logs"
  resource_group_name      = azurerm_resource_group.tfstate.name
  location                 = azurerm_resource_group.tfstate.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
  account_kind             = "StorageV2"

  enable_https_traffic_only = true
  min_tls_version           = "TLS1_2"

  tags = merge(
    local.tags,
    {
      Name = "${local.storage_account_name}-logs"
    }
  )
}

# ============================================================================
# Management Lock (Optional, for Production)
# ============================================================================

resource "azurerm_management_lock" "tfstate" {
  count      = var.environment == "prod" ? 1 : 0
  name       = "lock-${local.storage_account_name}"
  scope      = azurerm_storage_account.tfstate.id
  lock_level = "CanNotDelete"
  notes      = "Prevents accidental deletion of Terraform state storage"
}

# ============================================================================
# Azure Key Vault for Access Keys (Optional)
# ============================================================================

resource "random_id" "keyvault_suffix" {
  byte_length = 3
}

resource "azurerm_key_vault" "tfstate" {
  name                       = "kv-tfstate-${random_id.keyvault_suffix.hex}"
  location                   = azurerm_resource_group.tfstate.location
  resource_group_name        = azurerm_resource_group.tfstate.name
  tenant_id                  = data.azurerm_client_config.current.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = var.environment == "prod"
  enable_rbac_authorization  = true

  network_acls {
    default_action = length(var.allowed_ip_ranges) > 0 ? "Deny" : "Allow"
    bypass         = "AzureServices"
    ip_rules       = var.allowed_ip_ranges
  }

  tags = local.tags
}

# Grant current user access to Key Vault
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.tfstate.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Store storage account access key in Key Vault
resource "azurerm_key_vault_secret" "storage_account_key" {
  name         = "terraform-state-storage-key"
  value        = azurerm_storage_account.tfstate.primary_access_key
  key_vault_id = azurerm_key_vault.tfstate.id

  depends_on = [azurerm_role_assignment.kv_admin]

  tags = local.tags
}

# ============================================================================
# Outputs
# ============================================================================

output "resource_group_name" {
  description = "Name of the resource group"
  value       = azurerm_resource_group.tfstate.name
}

output "storage_account_name" {
  description = "Name of the storage account for Terraform state"
  value       = azurerm_storage_account.tfstate.name
}

output "storage_account_id" {
  description = "ID of the storage account"
  value       = azurerm_storage_account.tfstate.id
}

output "container_name" {
  description = "Name of the blob container for state files"
  value       = azurerm_storage_container.tfstate.name
}

output "storage_account_primary_access_key" {
  description = "Primary access key for the storage account"
  value       = azurerm_storage_account.tfstate.primary_access_key
  sensitive   = true
}

output "storage_account_primary_connection_string" {
  description = "Primary connection string for the storage account"
  value       = azurerm_storage_account.tfstate.primary_connection_string
  sensitive   = true
}

output "key_vault_name" {
  description = "Name of the Key Vault storing access keys"
  value       = azurerm_key_vault.tfstate.name
}

output "key_vault_id" {
  description = "ID of the Key Vault"
  value       = azurerm_key_vault.tfstate.id
}

output "backend_config" {
  description = "Backend configuration to use in other Terraform projects"
  value = {
    resource_group_name  = azurerm_resource_group.tfstate.name
    storage_account_name = azurerm_storage_account.tfstate.name
    container_name       = azurerm_storage_container.tfstate.name
    key                  = "terraform.tfstate" # Update this for each project
  }
}

output "backend_config_hcl" {
  description = "Ready-to-use backend configuration in HCL format"
  value = <<-EOT
    terraform {
      backend "azurerm" {
        resource_group_name  = "${azurerm_resource_group.tfstate.name}"
        storage_account_name = "${azurerm_storage_account.tfstate.name}"
        container_name       = "${azurerm_storage_container.tfstate.name}"
        key                  = "path/to/terraform.tfstate"  # Update this for each project
      }
    }
  EOT
}

output "backend_config_with_access_key" {
  description = "Backend configuration with access key (for CI/CD pipelines)"
  value = {
    resource_group_name  = azurerm_resource_group.tfstate.name
    storage_account_name = azurerm_storage_account.tfstate.name
    container_name       = azurerm_storage_container.tfstate.name
    access_key           = azurerm_storage_account.tfstate.primary_access_key
  }
  sensitive = true
}

output "state_locking_info" {
  description = "Information about state locking in Azure"
  value = <<-EOT
    Azure Terraform Backend uses blob leases for state locking.

    How it works:
    - When 'terraform apply' runs, it acquires a lease on the state blob
    - The lease is held for the duration of the operation
    - If another process tries to acquire the lease, it will fail
    - This prevents concurrent modifications to the state

    No additional resources (like DynamoDB in AWS) are needed!
    State locking is built into Azure Blob Storage.
  EOT
}
