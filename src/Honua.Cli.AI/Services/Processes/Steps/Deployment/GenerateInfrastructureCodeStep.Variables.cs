// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Terraform variables and tfvars generation methods.
/// Handles the creation of variable definitions and value assignments for all cloud providers.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    private string GenerateVariablesTf(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => @"variable ""db_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing virtual network""
  type        = bool
  default     = false
}

variable ""existing_vnet_id"" {
  description = ""Resource ID of the existing virtual network""
  type        = string
  default     = """"
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing Azure Database for PostgreSQL""
  type        = bool
  default     = false
}

variable ""existing_sql_server_id"" {
  description = ""Resource ID of the existing Azure Database for PostgreSQL server""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Azure DNS zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone_id"" {
  description = ""Resource ID of the existing Azure DNS zone""
  type        = string
  default     = """"
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing VPC network""
  type        = bool
  default     = false
}

variable ""existing_network_self_link"" {
  description = ""Self link of the existing VPC network""
  type        = string
  default     = """"
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing Cloud SQL instance""
  type        = bool
  default     = false
}

variable ""existing_sql_instance"" {
  description = ""Name of the existing Cloud SQL instance""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Cloud DNS managed zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone"" {
  description = ""Name of the existing Cloud DNS managed zone""
  type        = string
  default     = """"
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing VPC""
  type        = bool
  default     = false
}

variable ""existing_vpc_id"" {
  description = ""Identifier of the existing VPC when reuse_existing_network is true""
  type        = string
  default     = """"
}

variable ""existing_subnet_ids"" {
  description = ""List of subnet IDs to reuse when reuse_existing_network is true""
  type        = list(string)
  default     = []
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing RDS instance""
  type        = bool
  default     = false
}

variable ""existing_database_identifier"" {
  description = ""Identifier of the existing database instance""
  type        = string
  default     = """"
}

variable ""existing_database_endpoint"" {
  description = ""Endpoint of the existing database instance""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Route53 hosted zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone_id"" {
  description = ""Route53 hosted zone ID to reuse""
  type        = string
  default     = """"
}

variable ""existing_dns_zone_name"" {
  description = ""Route53 hosted zone name to reuse""
  type        = string
  default     = """"
}
",
            "gcp" => @"variable ""project_id"" {
  description = ""GCP project ID""
  type        = string
}

variable ""db_root_password"" {
  description = ""Database root password""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}

variable ""dev_authorized_network"" {
  description = ""CIDR range for development access to Cloud SQL (only used in non-production)""
  type        = string
  default     = ""0.0.0.0/0""
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}
",
            "azure" => @"variable ""db_admin_login"" {
  description = ""Database administrator login username""
  type        = string
}

variable ""db_admin_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}

variable ""docker_registry_username"" {
  description = ""Docker registry username for pulling container images""
  type        = string
  sensitive   = true
}

variable ""docker_registry_password"" {
  description = ""Docker registry password for pulling container images""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}
",
            _ => ""
        };
    }

    private string GenerateTfVars(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => GenerateAwsTfVars(),
            "gcp" => GenerateGcpTfVars(),
            "azure" => GenerateAzureTfVars(),
            _ => ""
        };
    }

    private string GenerateAwsTfVars()
    {
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_password"] = dbPassword;
        _logger.LogInformation("Generated secure database password and stored in deployment state");

        var corsOrigins = DeriveCorsOrigins();
        var sb = new StringBuilder();

        sb.AppendLine("# Securely generated database password");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine($@"db_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var vpcId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(vpcId))
            {
                sb.AppendLine($@"existing_vpc_id = ""{vpcId}""");
            }

            var subnetIds = GetInfrastructureOutput("existing_subnet_ids");
            if (!string.IsNullOrWhiteSpace(subnetIds))
            {
                var formatted = string.Join(", ",
                    subnetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id => $"\"{id}\""));
                sb.AppendLine($@"existing_subnet_ids = [{formatted}]");
            }

            if (!string.IsNullOrWhiteSpace(existing.NetworkNotes))
            {
                sb.AppendLine($@"network_notes = ""{existing.NetworkNotes}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var databaseId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(databaseId))
            {
                sb.AppendLine($@"existing_database_identifier = ""{databaseId}""");
            }

            var endpoint = GetInfrastructureOutput("database_endpoint");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                sb.AppendLine($@"existing_database_endpoint = ""{endpoint}""");
            }

            if (!string.IsNullOrWhiteSpace(existing.DatabaseNotes))
            {
                sb.AppendLine($@"database_notes = ""{existing.DatabaseNotes}""");
            }
        }

        if (existing.ReuseDns)
        {
            var dnsZoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dnsZoneId))
            {
                sb.AppendLine($@"existing_dns_zone_id = ""{dnsZoneId}""");
            }

            var dnsZoneName = GetInfrastructureOutput("existing_dns_zone_name");
            if (!string.IsNullOrWhiteSpace(dnsZoneName))
            {
                sb.AppendLine($@"existing_dns_zone_name = ""{dnsZoneName}""");
            }

            if (!string.IsNullOrWhiteSpace(existing.DnsNotes))
            {
                sb.AppendLine($@"dns_notes = ""{existing.DnsNotes}""");
            }
        }

        return sb.ToString();
    }

    private string GenerateGcpTfVars()
    {
        var gcpProjectId = _state.GcpProjectId
            ?? Environment.GetEnvironmentVariable("GCP_PROJECT_ID")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");

        if (string.IsNullOrEmpty(gcpProjectId))
        {
            _logger.LogError("GCP project ID not configured. Set GcpProjectId in deployment state or GCP_PROJECT_ID/GOOGLE_CLOUD_PROJECT environment variable");
            throw new InvalidOperationException(
                "GCP project ID is required for GCP deployments. " +
                "Configure GcpProjectId in deployment state or set GCP_PROJECT_ID/GOOGLE_CLOUD_PROJECT environment variable.");
        }

        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_root_password"] = dbPassword;
        _logger.LogInformation("Using GCP project ID: {ProjectId}", gcpProjectId);

        var corsOrigins = DeriveCorsOrigins();
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var sb = new StringBuilder();
        sb.AppendLine("# GCP project ID from configuration");
        sb.AppendLine($@"project_id = ""{gcpProjectId}""");
        sb.AppendLine();
        sb.AppendLine("# Securely generated database password");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine($@"db_root_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var networkId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(networkId))
            {
                sb.AppendLine($@"existing_network_self_link = ""{networkId}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var dbId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dbId))
            {
                sb.AppendLine($@"existing_sql_instance = ""{dbId}""");
            }
        }

        if (existing.ReuseDns)
        {
            var zoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                sb.AppendLine($@"existing_dns_zone = ""{zoneId}""");
            }
        }

        return sb.ToString();
    }

    private string GenerateAzureTfVars()
    {
        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_admin_password"] = dbPassword;
        _logger.LogInformation("Generated secure database credentials");

        var corsOrigins = DeriveCorsOrigins();
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var sb = new StringBuilder();
        sb.AppendLine("# Database administrator credentials");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine(@"db_admin_login = ""honuaadmin""");
        sb.AppendLine($@"db_admin_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Docker registry credentials for pulling container images");
        sb.AppendLine("# Replace with your actual registry credentials");
        sb.AppendLine(@"docker_registry_username = ""SET_YOUR_DOCKER_USERNAME""");
        sb.AppendLine(@"docker_registry_password = ""SET_YOUR_DOCKER_PASSWORD""");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var vnetId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(vnetId))
            {
                sb.AppendLine($@"existing_vnet_id = ""{vnetId}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var serverId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(serverId))
            {
                sb.AppendLine($@"existing_sql_server_id = ""{serverId}""");
            }
        }

        if (existing.ReuseDns)
        {
            var zoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                sb.AppendLine($@"existing_dns_zone_id = ""{zoneId}""");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Derives CORS allowed origins from deployment configuration.
    /// Returns a JSON array of origins based on the tier and configured domains.
    /// </summary>
    private string DeriveCorsOrigins()
    {
        // For production, never allow wildcard - require explicit configuration
        if (_state.Tier.ToLower() == "production")
        {
            _logger.LogWarning("Production deployment requires explicit CORS origins. Update terraform.tfvars after generation with your allowed domains.");
            _logger.LogInformation("Example: cors_allowed_origins = [\"https://yourdomain.com\", \"https://app.yourdomain.com\"]");
            // Return empty array to force manual configuration
            return "[]";
        }

        // For development/staging, allow wildcard but log a warning
        _logger.LogInformation("Non-production deployment: allowing CORS wildcard. Configure specific domains for production.");
        return "[\"*\"]";
    }

    private string? GetInfrastructureOutput(string key)
    {
        if (_state.InfrastructureOutputs is null)
        {
            return null;
        }

        return _state.InfrastructureOutputs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string ToTfBool(bool value) => value ? "true" : "false";
}
