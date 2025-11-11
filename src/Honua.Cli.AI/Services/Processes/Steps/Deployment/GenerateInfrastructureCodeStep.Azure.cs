// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Cli.AI.Services.Guardrails;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Azure-specific Terraform infrastructure code generation.
/// Contains methods for generating Azure resources including App Service, PostgreSQL, Storage, etc.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    private string GenerateAzureTerraform(ResourceEnvelope envelope, string? securityFeedback = null)
    {
        var planSku = envelope.MinVCpu >= 4 ? "EP3" : "EP1";
        var preWarmed = envelope.MinProvisionedConcurrency ?? Math.Max(1, envelope.MinInstances);
        var sanitizedName = SanitizeName(_state.DeploymentName);
        var storageAccountName = SanitizeStorageAccountName(_state.DeploymentName);

        // If security feedback provided, log it for debugging
        if (!string.IsNullOrEmpty(securityFeedback))
        {
            _logger.LogInformation("Regenerating Azure Terraform with security feedback:\n{Feedback}", securityFeedback);
        }

        var feedbackComment = string.IsNullOrEmpty(securityFeedback)
            ? ""
            : $@"
# SECURITY FEEDBACK FROM PREVIOUS ATTEMPT:
# {securityFeedback.Replace("\n", "\n# ")}
";

        return $@"{feedbackComment}
terraform {{
  required_providers {{
    azurerm = {{
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }}
  }}
}}

provider ""azurerm"" {{
  features {{}}
}}

# Guardrail envelope {envelope.Id} ({envelope.WorkloadProfile})
locals {{
  honua_guardrail_envelope   = ""{envelope.Id}""
  honua_guardrail_min_vcpu    = {envelope.MinVCpu}
  honua_guardrail_min_memory  = {envelope.MinMemoryGb}
  honua_guardrail_min_workers = {envelope.MinProvisionedConcurrency ?? Math.Max(1, envelope.MinInstances)}
  honua_pre_warmed_instances  = {preWarmed}
  sanitized_name              = ""{sanitizedName}""
  storage_account_name        = ""{storageAccountName}""
}}

check ""honua_guardrail_concurrency"" {{
  assert {{
    condition     = local.honua_pre_warmed_instances >= local.honua_guardrail_min_workers
    error_message = ""Provisioned concurrency must be >= ${{local.honua_guardrail_min_workers}} to satisfy envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

resource ""azurerm_resource_group"" ""honua"" {{
  name     = ""${{local.sanitized_name}}-rg""
  location = ""{_state.Region}""
}}

# Storage Account for Functions and rasters
resource ""azurerm_storage_account"" ""honua_functions"" {{
  name                     = ""${{local.storage_account_name}}fn""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  tags = {{
    purpose = ""functions""
  }}
}}

resource ""azurerm_storage_account"" ""honua_rasters"" {{
  name                     = ""${{local.storage_account_name}}data""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  blob_properties {{
    cors_rule {{
      allowed_headers    = [""*""]
      allowed_methods    = [""GET"", ""HEAD""]
      allowed_origins    = var.cors_allowed_origins
      exposed_headers    = [""ETag""]
      max_age_in_seconds = 3600
    }}
  }}

  tags = {{
    purpose = ""rasters""
  }}
}}

# PostgreSQL Database with credentials
resource ""azurerm_postgresql_server"" ""honua_db"" {{
  name                = ""${{local.sanitized_name}}-db""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku_name            = ""{GetAzureSkuName(_state.Tier)}""
  version             = ""11""

  administrator_login          = var.db_admin_login
  administrator_login_password = var.db_admin_password

  ssl_enforcement_enabled          = true
  ssl_minimal_tls_version_enforced = ""TLS1_2""

  backup_retention_days        = {(_state.Tier.ToLower() == "production" ? "7" : "1")}
  geo_redundant_backup_enabled = {(_state.Tier.ToLower() == "production" ? "true" : "false")}
  auto_grow_enabled            = true

  tags = {{
    Name = ""${{local.sanitized_name}}-db""
  }}
}}

resource ""azurerm_postgresql_database"" ""honua"" {{
  name                = ""honua""
  resource_group_name = azurerm_resource_group.honua.name
  server_name         = azurerm_postgresql_server.honua_db.name
  charset             = ""UTF8""
  collation           = ""English_United States.1252""
}}

resource ""azurerm_postgresql_firewall_rule"" ""allow_azure_services"" {{
  name                = ""allow-azure-services""
  resource_group_name = azurerm_resource_group.honua.name
  server_name         = azurerm_postgresql_server.honua_db.name
  start_ip_address    = ""0.0.0.0""
  end_ip_address      = ""0.0.0.0""
}}

# App Service Plan
resource ""azurerm_service_plan"" ""honua_plan"" {{
  name                = ""${{local.sanitized_name}}-plan""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  os_type             = ""Linux""
  sku_name            = ""{planSku}""

  tags = {{
    Name = ""${{local.sanitized_name}}-plan""
  }}
}}

# Linux Web App for Honua API
resource ""azurerm_linux_web_app"" ""honua"" {{
  name                = ""${{local.sanitized_name}}-api""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  service_plan_id     = azurerm_service_plan.honua_plan.id

  site_config {{
    always_on = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

    application_stack {{
      docker_registry_url      = ""https://index.docker.io""
      docker_registry_username = var.docker_registry_username
      docker_registry_password = var.docker_registry_password
      docker_image_name        = ""honua/api""
      docker_image_tag         = var.app_version != ""latest"" ? var.app_version : ""v1.0.0""
    }}

    health_check_path = ""/health""

    cors {{
      allowed_origins = var.cors_allowed_origins
    }}
  }}

  app_settings = {{
    ""DATABASE_HOST""     = azurerm_postgresql_server.honua_db.fqdn
    ""DATABASE_NAME""     = azurerm_postgresql_database.honua.name
    ""DATABASE_USER""     = ""${{var.db_admin_login}}@${{azurerm_postgresql_server.honua_db.name}}""
    ""DATABASE_PASSWORD"" = var.db_admin_password
    ""STORAGE_ACCOUNT""   = azurerm_storage_account.honua_rasters.name
    ""STORAGE_KEY""       = azurerm_storage_account.honua_rasters.primary_access_key
    ""AZURE_REGION""      = ""{_state.Region}""
  }}

  https_only = true

  tags = {{
    Name = ""${{local.sanitized_name}}-api""
  }}
}}

output ""honua_guardrail_envelope"" {{
  value = local.honua_guardrail_envelope
}}

output ""honua_guardrail_policy"" {{
  value = {{
    envelope_id   = local.honua_guardrail_envelope
    min_vcpu      = local.honua_guardrail_min_vcpu
    min_memory_gb = local.honua_guardrail_min_memory
    min_workers   = local.honua_guardrail_min_workers
  }}
}}

output ""app_url"" {{
  description = ""URL to access the Honua API""
  value       = ""https://${{azurerm_linux_web_app.honua.default_hostname}}""
}}

output ""database_fqdn"" {{
  description = ""PostgreSQL database FQDN""
  value       = azurerm_postgresql_server.honua_db.fqdn
  sensitive   = true
}}

output ""storage_account_name"" {{
  description = ""Storage account name for raster storage""
  value       = azurerm_storage_account.honua_rasters.name
}}
";
    }

    private string GetAzureSkuName(string tier) => tier.ToLower() switch
    {
        "development" => "B_Gen5_1",
        "staging" => "GP_Gen5_2",
        "production" => "MO_Gen5_4",
        _ => "B_Gen5_1"
    };
}
