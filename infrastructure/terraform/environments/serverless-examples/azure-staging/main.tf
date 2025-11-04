# Example: Azure Container Apps Staging Environment
# Staging configuration with moderate resources and Front Door

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
  }

  # Configure backend for state storage
  # backend "azurerm" {
  #   resource_group_name  = "terraform-state-rg"
  #   storage_account_name = "honuatfstate"
  #   container_name       = "tfstate"
  #   key                  = "serverless/azure-staging.tfstate"
  # }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
    resource_group {
      prevent_deletion_if_contains_resources = true
    }
  }
}

# ==================== Container Apps Deployment ====================
module "honua_serverless" {
  source = "../../../modules/container-apps"

  environment     = "staging"
  location        = var.location
  container_image = var.container_image

  # Create resources
  create_resource_group = true
  create_vnet          = true
  create_database      = true
  create_key_vault     = true
  create_log_analytics = true
  create_front_door    = true

  # Staging scaling
  container_cpu    = 1
  container_memory = "2Gi"
  min_replicas     = 0  # Scale to zero
  max_replicas     = 20

  # Moderate database
  db_sku_name              = "GP_Standard_D2s_v3"
  db_storage_mb            = 65536  # 64 GB
  db_high_availability_mode = "Disabled"  # Save cost in staging
  db_geo_redundant_backup  = false
  db_backup_retention_days = 7

  # Azure Front Door Standard
  front_door_sku = "Standard_AzureFrontDoor"
  custom_domains = var.custom_domains

  # Network configuration
  vnet_address_space = "10.0.0.0/16"

  # Storage access
  enable_storage_access = true

  # Log retention
  log_retention_days = 14

  # CORS
  cors_origins = var.cors_origins

  tags = {
    Environment = "Staging"
    Team        = "Platform"
    CostCenter  = "Engineering"
  }
}

# ==================== Outputs ====================
output "container_app_url" {
  description = "Container App URL"
  value       = module.honua_serverless.container_app_url
}

output "front_door_url" {
  description = "Azure Front Door URL (use this for traffic)"
  value       = module.honua_serverless.front_door_endpoint_url
}

output "database_fqdn" {
  description = "Database FQDN"
  value       = module.honua_serverless.database_fqdn
  sensitive   = true
}

output "key_vault_uri" {
  description = "Key Vault URI"
  value       = module.honua_serverless.key_vault_uri
}

output "estimated_monthly_cost" {
  description = "Estimated monthly cost"
  value = <<-EOT
    Container Apps:           $30-60/month
    PostgreSQL (GP_D2s_v3):   $120/month
    Azure Front Door Standard: $35/month
    Virtual Network:          $0/month
    Key Vault:                $1/month
    Log Analytics:            $10/month
    ----------------------------------------
    Total Estimate:           $196-226/month

    Note: Container Apps and Front Door costs scale with traffic.
  EOT
}

output "staging_workflow" {
  description = "Staging environment workflow"
  value = <<-EOT
    Staging Environment Workflow:

    1. Deploy from CI/CD:
       - Build container image
       - Push to Azure Container Registry
       - Update container_image variable
       - Run terraform apply

    2. Run Integration Tests:
       - Health check: ${module.honua_serverless.container_app_url}/health
       - API tests against Front Door URL
       - Load testing
       - Security scanning

    3. Database Migrations:
       az postgres flexible-server execute \
         --name <db-name> \
         --database-name honua \
         --file-path migrations.sql

    4. Monitor:
       - Container App logs in Log Analytics
       - Front Door analytics
       - Application Insights

    5. Promote to Production:
       - Tag successful build
       - Update production terraform with tested image
       - Deploy to production environment
  EOT
}

output "monitoring_links" {
  description = "Azure Portal monitoring links"
  value       = module.honua_serverless.monitoring_urls
}
