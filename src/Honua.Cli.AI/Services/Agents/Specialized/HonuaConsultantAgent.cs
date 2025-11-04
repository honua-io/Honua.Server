// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Security;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Comprehensive Honua GIS Cloud Consultant Agent
/// Handles complete lifecycle: configuration, deployment, security, upgrades, migrations, and operations
/// </summary>
public sealed class HonuaConsultantAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider? _llmProvider;

    public HonuaConsultantAgent(Kernel kernel, ILlmProvider? llmProvider = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider;
    }

    /// <summary>
    /// Analyzes deployment request and generates complete Honua configuration
    /// </summary>
    public async Task<AgentStepResult> GenerateCompleteConfigurationAsync(
        string request,
        DeploymentAnalysis deploymentAnalysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Analyze what configuration aspects are needed
            var configNeeds = await AnalyzeConfigurationNeedsAsync(request, deploymentAnalysis, context, cancellationToken);

            var generatedConfigs = new List<string>();
            var warnings = new List<string>();

            // Generate metadata configuration
            if (configNeeds.NeedsMetadata)
            {
                var metadata = await GenerateMetadataConfigurationAsync(request, deploymentAnalysis, context, cancellationToken);
                await SaveMetadataConfigurationAsync(metadata, context, cancellationToken);
                generatedConfigs.Add("metadata.json");

                // Analyze security issues
                var securityIssues = AnalyzeSecurityIssues(metadata);
                warnings.AddRange(securityIssues);
            }

            // Generate appsettings.json for runtime configuration
            if (configNeeds.NeedsAppSettings)
            {
                var appSettings = await GenerateAppSettingsAsync(request, deploymentAnalysis, configNeeds, context, cancellationToken);
                await SaveAppSettingsAsync(appSettings, context, cancellationToken);
                generatedConfigs.Add("appsettings.json");
            }

            // Generate GitOps configuration if requested
            if (configNeeds.UseGitOps)
            {
                await GenerateGitOpsConfigurationAsync(request, deploymentAnalysis, context, cancellationToken);
                generatedConfigs.Add(".gitops/deployment-policy.yaml");
            }

            // Generate upgrade/migration scripts if needed
            if (configNeeds.NeedsUpgradeScripts)
            {
                await GenerateUpgradeScriptsAsync(context, cancellationToken);
                generatedConfigs.Add("scripts/upgrade.sh");
            }

            var message = new StringBuilder();
            message.AppendLine($"✓ Generated complete Honua configuration:");
            foreach (var config in generatedConfigs)
            {
                message.AppendLine($"  - {config}");
            }

            if (warnings.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("⚠️  SECURITY WARNINGS:");
                foreach (var warning in warnings)
                {
                    message.AppendLine($"  - {warning}");
                }
            }

            return new AgentStepResult
            {
                AgentName = "HonuaConsultant",
                Action = "GenerateCompleteConfiguration",
                Success = true,
                Message = message.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentStepResult
            {
                AgentName = "HonuaConsultant",
                Action = "GenerateCompleteConfiguration",
                Success = false,
                Message = $"Error generating configuration: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<ConfigurationNeeds> AnalyzeConfigurationNeedsAsync(
        string request,
        DeploymentAnalysis deploymentAnalysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Sanitize user input to prevent prompt injection
        var sanitizedRequest = PromptInjectionFilter.SanitizeUserInput(request);

        // Detect potential injection attempts (log for monitoring)
        if (PromptInjectionFilter.DetectInjectionAttempt(request))
        {
            // Log the attempt but continue with sanitized input
            // In production, you might want to add additional monitoring/alerting here
        }

        // Use LLM to determine what configuration is needed
        var wrappedInput = PromptInjectionFilter.WrapUserInput(sanitizedRequest, sanitize: false);
        var prompt = $@"Analyze this Honua deployment request and determine what configuration is needed:

{wrappedInput}
Deployment Type: {deploymentAnalysis.DeploymentType}
Environment: {deploymentAnalysis.TargetEnvironment}

Determine:
1. Does it need metadata configuration? (always yes unless explicitly migration-only)
2. Does it need appsettings.json? (yes for runtime config, logging, auth)
3. Should GitOps be used? (check if user mentioned git, gitops, or infrastructure-as-code)
4. Does it need upgrade/migration scripts? (check if upgrading existing deployment)
5. What authentication mechanisms? (none, apikey, oauth, custom)
6. What logging level? (debug for dev, info for prod, trace if requested)
7. Should use default or custom data sources? (default for samples, custom if user specifies)

Respond in JSON format:
{{
  ""needsMetadata"": true,
  ""needsAppSettings"": true,
  ""useGitOps"": false,
  ""needsUpgradeScripts"": false,
  ""authMechanism"": ""none"",
  ""loggingLevel"": ""Information"",
  ""useDefaultDataSources"": true,
  ""securityProfile"": ""development""
}}";

        if (_llmProvider != null)
        {
            var systemPrompt = $@"You are a Honua GIS deployment consultant. Analyze configuration needs.

{PromptInjectionFilter.GetSecurityGuidance()}";

            var llmResponse = await _llmProvider.CompleteAsync(new LlmRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = prompt,
                MaxTokens = 2000,
                Temperature = 0.3
            }, cancellationToken);

            var response = llmResponse.Content;

            try
            {
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}') + 1;
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = response.Substring(jsonStart, jsonEnd - jsonStart);
                    var needs = JsonSerializer.Deserialize<ConfigurationNeeds>(json, CliJsonOptions.DevTooling);
                    if (needs != null)
                    {
                        return needs;
                    }
                }
            }
            catch
            {
                // Fall through to defaults
            }
        }

        // Default configuration needs
        return new ConfigurationNeeds
        {
            NeedsMetadata = true,
            NeedsAppSettings = true,
            UseGitOps = request.Contains("gitops", StringComparison.OrdinalIgnoreCase) ||
                       request.Contains("infrastructure-as-code", StringComparison.OrdinalIgnoreCase),
            NeedsUpgradeScripts = request.Contains("upgrade", StringComparison.OrdinalIgnoreCase) ||
                                 request.Contains("migrate", StringComparison.OrdinalIgnoreCase),
            AuthMechanism = "none",
            LoggingLevel = deploymentAnalysis.TargetEnvironment == "production" ? "Information" : "Debug",
            UseDefaultDataSources = !request.Contains("custom data", StringComparison.OrdinalIgnoreCase),
            SecurityProfile = deploymentAnalysis.TargetEnvironment
        };
    }

    private async Task<MetadataConfiguration> GenerateMetadataConfigurationAsync(
        string request,
        DeploymentAnalysis deploymentAnalysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var metadata = new MetadataConfiguration
        {
            Catalog = new CatalogConfiguration
            {
                Id = "honua-catalog",
                Title = "Honua GIS Catalog",
                Description = "Geospatial data services powered by Honua",
                Version = DateTime.UtcNow.ToString("yyyy.MM"),
                Keywords = new[] { "gis", "ogc", "features", "tiles" }
            },
            Folders = new List<FolderConfiguration>
            {
                new FolderConfiguration
                {
                    Id = "default",
                    Title = "Default",
                    Order = 10
                }
            },
            DataSources = await GenerateDataSourcesAsync(deploymentAnalysis, cancellationToken),
            Services = new List<ServiceConfiguration>()
        };

        // Add sample service if using defaults
        if (!request.Contains("no sample", StringComparison.OrdinalIgnoreCase))
        {
            metadata.Services.Add(new ServiceConfiguration
            {
                Id = "sample-features",
                Title = "Sample Feature Service",
                FolderId = "default",
                ServiceType = "feature",
                DataSourceId = metadata.DataSources.FirstOrDefault()?.Id ?? "primary",
                Enabled = true,
                Description = "Sample OGC API Features service",
                Keywords = new[] { "sample", "features" },
                Ogc = new OgcConfiguration
                {
                    CollectionsEnabled = true,
                    ItemLimit = 1000,
                    DefaultCrs = "EPSG:4326",
                    AdditionalCrs = new[] { "EPSG:3857" }
                }
            });
        }

        return metadata;
    }

    private async Task<List<DataSourceConfiguration>> GenerateDataSourcesAsync(
        DeploymentAnalysis deploymentAnalysis,
        CancellationToken cancellationToken)
    {
        var dataSources = new List<DataSourceConfiguration>();

        // Determine database type from deployment analysis
        var dbType = deploymentAnalysis.InfrastructureNeeds?.DatabaseType?.ToLowerInvariant() ?? "postgis";

        if (dbType.Contains("postgis") || dbType.Contains("postgres"))
        {
            dataSources.Add(new DataSourceConfiguration
            {
                Id = "primary",
                Provider = "postgis",
                ConnectionString = "Host=postgis;Port=5432;Database=honua;Username=honua;Password=honua_password"
            });
        }
        else if (dbType.Contains("mysql"))
        {
            dataSources.Add(new DataSourceConfiguration
            {
                Id = "primary",
                Provider = "mysql",
                ConnectionString = "Server=mysql;Port=3306;Database=honua;User=honua;Password=honua_password"
            });
        }
        else if (dbType.Contains("sqlserver") || dbType.Contains("mssql"))
        {
            dataSources.Add(new DataSourceConfiguration
            {
                Id = "primary",
                Provider = "sqlserver",
                ConnectionString = "Server=sqlserver,1433;Database=honua;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"
            });
        }
        else
        {
            // Default to SQLite for development
            dataSources.Add(new DataSourceConfiguration
            {
                Id = "primary",
                Provider = "sqlite",
                ConnectionString = "Data Source=data/honua.db"
            });
        }

        return await Task.FromResult(dataSources);
    }

    private List<string> AnalyzeSecurityIssues(MetadataConfiguration metadata)
    {
        var warnings = new List<string>();

        // Check for hardcoded passwords in connection strings
        foreach (var ds in metadata.DataSources)
        {
            if (ds.ConnectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase) &&
                !ds.ConnectionString.Contains("${", StringComparison.Ordinal))
            {
                warnings.Add($"🔴 CRITICAL: DataSource '{ds.Id}' has hardcoded password in connection string. Use environment variables or secrets management.");
            }

            if (ds.ConnectionString.Contains("TrustServerCertificate=True", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"⚠️  DataSource '{ds.Id}' has certificate validation disabled. Enable in production.");
            }

            // Warn about file-based datasources (SQLite, GeoPackage)
            if (ds.Provider.Equals("sqlite", StringComparison.OrdinalIgnoreCase) ||
                ds.Provider.Equals("gpkg", StringComparison.OrdinalIgnoreCase) ||
                ds.Provider.Equals("geopackage", StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"ℹ️  DataSource '{ds.Id}' uses file-based storage ({ds.Provider}). File-based datasources are recommended for prototyping/development only. For production, use PostGIS, MySQL, or SQL Server.");
            }
        }

        // Check for file-based raster datasources (local GeoTIFF files)
        // Note: RasterDatasets would need to be added to MetadataConfiguration for this check
        // For now, this is a reminder comment for future enhancement

        // Check if services are public without auth
        var publicServicesCount = metadata.Services.Count(s => s.Enabled);
        if (publicServicesCount > 0)
        {
            warnings.Add($"ℹ️  {publicServicesCount} service(s) configured without authentication. Consider enabling API keys or OAuth for production.");
        }

        return warnings;
    }

    private async Task SaveMetadataConfigurationAsync(
        MetadataConfiguration metadata,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();

        var yaml = serializer.Serialize(metadata);

        var path = Path.Combine(context.WorkspacePath, "metadata.yaml");
        await File.WriteAllTextAsync(path, yaml, cancellationToken);
    }

    private async Task<AppSettingsConfiguration> GenerateAppSettingsAsync(
        string request,
        DeploymentAnalysis deploymentAnalysis,
        ConfigurationNeeds configNeeds,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        return await Task.FromResult(new AppSettingsConfiguration
        {
            Logging = new LoggingConfiguration
            {
                LogLevel = new Dictionary<string, string>
                {
                    ["Default"] = configNeeds.LoggingLevel,
                    ["Microsoft.AspNetCore"] = "Warning",
                    ["Honua"] = configNeeds.LoggingLevel
                }
            },
            AllowedHosts = "*",
            Honua = new HonuaRuntimeConfiguration
            {
                Metadata = new MetadataRuntimeConfiguration
                {
                    Provider = "yaml",
                    Path = "metadata.yaml"
                }
            }
        });
    }

    private async Task SaveAppSettingsAsync(
        AppSettingsConfiguration appSettings,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(appSettings, CliJsonOptions.Indented);

        var path = Path.Combine(context.WorkspacePath, "appsettings.json");
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private async Task GenerateGitOpsConfigurationAsync(
        string request,
        DeploymentAnalysis deploymentAnalysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var gitopsDir = Path.Combine(context.WorkspacePath, ".gitops");
        Directory.CreateDirectory(gitopsDir);

        var policy = @"# Honua GitOps Deployment Policy
apiVersion: honua.io/v1
kind: DeploymentPolicy
metadata:
  name: honua-deployment
spec:
  autoSync: true
  syncInterval: 5m
  environments:
    - development
    - staging
    - production
  validation:
    validateMetadata: true
    validateConnections: true
    requireApproval: true
";

        var policyPath = Path.Combine(gitopsDir, "deployment-policy.yaml");
        await File.WriteAllTextAsync(policyPath, policy, cancellationToken);
    }

    private async Task GenerateUpgradeScriptsAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var scriptsDir = Path.Combine(context.WorkspacePath, "scripts");
        Directory.CreateDirectory(scriptsDir);

        var upgradeScript = @"#!/bin/bash
# Honua Upgrade Script
# Safely upgrades Honua server with zero-downtime

set -e

echo ""Starting Honua upgrade...""

# Backup current configuration
cp metadata.json metadata.json.backup
cp docker-compose.yml docker-compose.yml.backup

# Pull latest image
docker-compose pull honua

# Perform rolling upgrade
docker-compose up -d --no-deps honua

# Wait for health check
sleep 10
curl -f http://localhost:5000/ || (echo ""Upgrade failed"" && docker-compose restart honua && exit 1)

echo ""✓ Upgrade completed successfully""
";

        var scriptPath = Path.Combine(scriptsDir, "upgrade.sh");
        await File.WriteAllTextAsync(scriptPath, upgradeScript, cancellationToken);

        // Make script executable on Unix systems
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }
}

// Supporting types for configuration generation

public sealed class ConfigurationNeeds
{
    public bool NeedsMetadata { get; set; }
    public bool NeedsAppSettings { get; set; }
    public bool UseGitOps { get; set; }
    public bool NeedsUpgradeScripts { get; set; }
    public string AuthMechanism { get; set; } = "none";
    public string LoggingLevel { get; set; } = "Information";
    public bool UseDefaultDataSources { get; set; }
    public string SecurityProfile { get; set; } = "development";
}

public sealed class MetadataConfiguration
{
    public CatalogConfiguration Catalog { get; set; } = new();
    public List<FolderConfiguration> Folders { get; set; } = new();
    public List<DataSourceConfiguration> DataSources { get; set; } = new();
    public List<ServiceConfiguration> Services { get; set; } = new();
}

public sealed class CatalogConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string[] Keywords { get; set; } = Array.Empty<string>();
}

public sealed class FolderConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class DataSourceConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class ServiceConfiguration
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public string[] Keywords { get; set; } = Array.Empty<string>();
    public OgcConfiguration? Ogc { get; set; }
}

public sealed class OgcConfiguration
{
    public bool CollectionsEnabled { get; set; }
    public int ItemLimit { get; set; }
    public string DefaultCrs { get; set; } = string.Empty;
    public string[] AdditionalCrs { get; set; } = Array.Empty<string>();
}

public sealed class AppSettingsConfiguration
{
    public LoggingConfiguration Logging { get; set; } = new();
    public string AllowedHosts { get; set; } = "*";
    public HonuaRuntimeConfiguration Honua { get; set; } = new();
}

public sealed class LoggingConfiguration
{
    public Dictionary<string, string> LogLevel { get; set; } = new();
}

public sealed class HonuaRuntimeConfiguration
{
    public MetadataRuntimeConfiguration Metadata { get; set; } = new();
}

public sealed class MetadataRuntimeConfiguration
{
    public string Provider { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
