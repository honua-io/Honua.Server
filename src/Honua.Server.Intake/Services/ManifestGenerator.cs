// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Intake.Services;

/// <summary>
/// Interface for generating build manifests from customer requirements.
/// </summary>
public interface IManifestGenerator
{
    /// <summary>
    /// Generates a build manifest from customer requirements.
    /// </summary>
    /// <param name="requirements">The customer requirements extracted from the AI conversation.</param>
    /// <param name="buildName">Optional custom build name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated build manifest.</returns>
    Task<BuildManifest> GenerateAsync(BuildRequirements requirements, string? buildName = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Generates build manifests from AI-extracted customer requirements.
/// </summary>
public sealed class ManifestGenerator : IManifestGenerator
{
    private readonly ILogger<ManifestGenerator> _logger;

    // Protocol to module mapping
    private static readonly Dictionary<string, string> ProtocolModuleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ogc-api"] = "OgcApi",
        ["ogc-api-features"] = "OgcApi",
        ["ogc-api-tiles"] = "OgcApi",
        ["ogc-api-records"] = "OgcApi",
        ["esri-rest"] = "GeoservicesREST",
        ["esri"] = "GeoservicesREST",
        ["featureserver"] = "GeoservicesREST",
        ["mapserver"] = "GeoservicesREST",
        ["wfs"] = "Wfs",
        ["wms"] = "Wms",
        ["wmts"] = "Wmts",
        ["wcs"] = "Wcs",
        ["csw"] = "Csw",
        ["stac"] = "Stac",
        ["vector-tiles"] = "VectorTiles",
        ["mvt"] = "VectorTiles",
        ["carto"] = "Carto",
        ["zarr"] = "Zarr",
        ["print"] = "Print",
        ["geometry"] = "Geometry",
        ["odata"] = "OData"
    };

    // Database to connector mapping
    private static readonly Dictionary<string, string> DatabaseConnectorMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["postgresql"] = "PostgreSQL",
        ["postgres"] = "PostgreSQL",
        ["postgis"] = "PostgreSQL",
        ["sqlserver"] = "SqlServer",
        ["mssql"] = "SqlServer",
        ["sqlite"] = "SQLite",
        ["bigquery"] = "BigQuery",
        ["snowflake"] = "Snowflake",
        ["oracle"] = "Oracle",
        ["oracle-spatial"] = "Oracle",
        ["s3"] = "S3",
        ["azure-blob"] = "AzureBlob",
        ["gcs"] = "GoogleCloudStorage",
        ["geojson"] = "FileSystem",
        ["shapefile"] = "FileSystem",
        ["file"] = "FileSystem"
    };

    public ManifestGenerator(ILogger<ManifestGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<BuildManifest> GenerateAsync(
        BuildRequirements requirements,
        string? buildName = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating build manifest for tier {Tier}, architecture {Architecture}",
            requirements.Tier, requirements.Architecture);

        // Map protocols to modules
        var modules = MapProtocolsToModules(requirements.Protocols);

        // Map databases to connectors
        var databaseConnectors = MapDatabasesToConnectors(requirements.Databases);

        // Generate cloud targets
        var cloudTargets = GenerateCloudTargets(requirements);

        // Calculate resource requirements
        var resources = CalculateResourceRequirements(requirements);

        // Generate environment variables
        var environmentVariables = GenerateEnvironmentVariables(requirements);

        // Determine build name
        var name = buildName ?? $"honua-{requirements.Tier.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMdd}";

        var manifest = new BuildManifest
        {
            Version = "1.0",
            Name = name,
            Architecture = requirements.Architecture,
            Modules = modules,
            DatabaseConnectors = databaseConnectors,
            CloudTargets = cloudTargets,
            Resources = resources,
            EnvironmentVariables = environmentVariables,
            Tier = requirements.Tier,
            Tags = GenerateTags(requirements),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        _logger.LogInformation(
            "Generated manifest: Name={Name}, Modules={ModuleCount}, Connectors={ConnectorCount}, CloudTargets={CloudTargetCount}",
            manifest.Name, manifest.Modules.Count, manifest.DatabaseConnectors.Count, manifest.CloudTargets?.Count ?? 0);

        await Task.CompletedTask;
        return manifest;
    }

    private List<string> MapProtocolsToModules(List<string> protocols)
    {
        var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Always include core modules
        modules.Add("Core");
        modules.Add("Api");

        // Map requested protocols to modules
        foreach (var protocol in protocols)
        {
            if (ProtocolModuleMap.TryGetValue(protocol, out var module))
            {
                modules.Add(module);
                _logger.LogDebug("Mapped protocol {Protocol} to module {Module}", protocol, module);
            }
            else
            {
                _logger.LogWarning("Unknown protocol {Protocol}, skipping", protocol);
            }
        }

        return modules.ToList();
    }

    private List<string> MapDatabasesToConnectors(List<string> databases)
    {
        var connectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var database in databases)
        {
            if (DatabaseConnectorMap.TryGetValue(database, out var connector))
            {
                connectors.Add(connector);
                _logger.LogDebug("Mapped database {Database} to connector {Connector}", database, connector);
            }
            else
            {
                _logger.LogWarning("Unknown database {Database}, skipping", database);
            }
        }

        return connectors.ToList();
    }

    private List<CloudTarget> GenerateCloudTargets(BuildRequirements requirements)
    {
        var targets = new List<CloudTarget>();

        var provider = requirements.CloudProvider.ToLowerInvariant();

        if (provider == "on-premises" || provider == "multi-cloud")
        {
            // For on-premises or multi-cloud, we don't generate specific targets
            _logger.LogDebug("Skipping cloud target generation for provider {Provider}", provider);
            return targets;
        }

        var target = new CloudTarget
        {
            Provider = provider,
            Region = GetDefaultRegion(provider),
            InstanceType = GetRecommendedInstanceType(requirements),
            RegistryUrl = GetRegistryUrl(provider),
            Configuration = GenerateCloudConfiguration(requirements)
        };

        targets.Add(target);

        _logger.LogInformation("Generated cloud target for {Provider} in {Region} with instance type {InstanceType}",
            target.Provider, target.Region, target.InstanceType);

        return targets;
    }

    private string GetDefaultRegion(string provider)
    {
        return provider switch
        {
            "aws" => "us-east-1",
            "azure" => "eastus",
            "gcp" => "us-central1",
            _ => string.Empty
        };
    }

    private string GetRecommendedInstanceType(BuildRequirements requirements)
    {
        var loadClass = requirements.Load?.Classification?.ToLowerInvariant() ?? "light";
        var isArm64 = requirements.Architecture.Contains("arm64", StringComparison.OrdinalIgnoreCase);
        var provider = requirements.CloudProvider.ToLowerInvariant();

        return (provider, loadClass, isArm64) switch
        {
            // AWS
            ("aws", "light", true) => "t4g.medium",
            ("aws", "light", false) => "t3.medium",
            ("aws", "moderate", true) => "c7g.large",
            ("aws", "moderate", false) => "c6i.large",
            ("aws", "heavy", true) => "c7g.xlarge",
            ("aws", "heavy", false) => "c6i.xlarge",

            // Azure
            ("azure", "light", true) => "Standard_D2ps_v5",
            ("azure", "light", false) => "Standard_D2s_v5",
            ("azure", "moderate", true) => "Standard_D4ps_v5",
            ("azure", "moderate", false) => "Standard_D4s_v5",
            ("azure", "heavy", true) => "Standard_D8ps_v5",
            ("azure", "heavy", false) => "Standard_D8s_v5",

            // GCP
            ("gcp", "light", true) => "t2a-standard-2",
            ("gcp", "light", false) => "e2-standard-2",
            ("gcp", "moderate", true) => "t2a-standard-4",
            ("gcp", "moderate", false) => "e2-standard-4",
            ("gcp", "heavy", true) => "t2a-standard-8",
            ("gcp", "heavy", false) => "e2-standard-8",

            _ => "medium"
        };
    }

    private string GetRegistryUrl(string provider)
    {
        return provider switch
        {
            "aws" => "ecr.aws",
            "azure" => "azurecr.io",
            "gcp" => "gcr.io",
            _ => string.Empty
        };
    }

    private Dictionary<string, string> GenerateCloudConfiguration(BuildRequirements requirements)
    {
        var config = new Dictionary<string, string>();

        var provider = requirements.CloudProvider.ToLowerInvariant();

        switch (provider)
        {
            case "aws":
                config["DeploymentMethod"] = "ECS";
                config["NetworkMode"] = "awsvpc";
                config["LogDriver"] = "awslogs";
                break;

            case "azure":
                config["DeploymentMethod"] = "ACI";
                config["OsType"] = "Linux";
                config["RestartPolicy"] = "Always";
                break;

            case "gcp":
                config["DeploymentMethod"] = "CloudRun";
                config["ConcurrencyLimit"] = "80";
                config["CpuThrottling"] = "true";
                break;
        }

        return config;
    }

    private ResourceRequirements CalculateResourceRequirements(BuildRequirements requirements)
    {
        var loadClass = requirements.Load?.Classification?.ToLowerInvariant() ?? "light";

        var (minCpu, minMemory, recCpu, recMemory) = loadClass switch
        {
            "light" => (1.0, 2.0, 2.0, 4.0),
            "moderate" => (2.0, 4.0, 4.0, 8.0),
            "heavy" => (4.0, 8.0, 8.0, 16.0),
            _ => (1.0, 2.0, 2.0, 4.0)
        };

        var storageGb = requirements.Load?.DataVolumeGb ?? 50.0;

        // Add overhead for raster data or STAC
        if (requirements.Protocols.Any(p => p.Contains("stac", StringComparison.OrdinalIgnoreCase) ||
                                             p.Contains("zarr", StringComparison.OrdinalIgnoreCase)))
        {
            storageGb += 100.0;  // Extra storage for raster/STAC catalogs
        }

        return new ResourceRequirements
        {
            MinCpu = minCpu,
            MinMemoryGb = minMemory,
            RecommendedCpu = recCpu,
            RecommendedMemoryGb = recMemory,
            StorageGb = storageGb
        };
    }

    private Dictionary<string, string> GenerateEnvironmentVariables(BuildRequirements requirements)
    {
        var env = new Dictionary<string, string>
        {
            ["HONUA_TIER"] = requirements.Tier.ToUpperInvariant(),
            ["HONUA_ARCHITECTURE"] = requirements.Architecture,
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["HONUA_TELEMETRY_ENABLED"] = "true"
        };

        // Add tier-specific variables
        if (requirements.Tier.Equals("enterprise", StringComparison.OrdinalIgnoreCase) ||
            requirements.Tier.Equals("enterprise-asp", StringComparison.OrdinalIgnoreCase))
        {
            env["HONUA_AUDIT_LOGGING"] = "true";
            env["HONUA_ADVANCED_SECURITY"] = "true";
        }

        // Add multi-tenancy flag for Enterprise ASP
        if (requirements.Tier.Equals("enterprise-asp", StringComparison.OrdinalIgnoreCase) ||
            requirements.AdvancedFeatures?.Contains("multi-tenancy", StringComparer.OrdinalIgnoreCase) == true)
        {
            env["HONUA_MULTITENANCY_ENABLED"] = "true";
        }

        // Add SAML if requested
        if (requirements.AdvancedFeatures?.Contains("saml", StringComparer.OrdinalIgnoreCase) == true)
        {
            env["HONUA_SAML_ENABLED"] = "true";
        }

        return env;
    }

    private List<string> GenerateTags(BuildRequirements requirements)
    {
        var tags = new List<string>
        {
            $"tier:{requirements.Tier.ToLowerInvariant()}",
            $"arch:{requirements.Architecture}",
            $"provider:{requirements.CloudProvider.ToLowerInvariant()}"
        };

        if (requirements.Load?.Classification != null)
        {
            tags.Add($"load:{requirements.Load.Classification.ToLowerInvariant()}");
        }

        // Add protocol tags
        foreach (var protocol in requirements.Protocols.Take(3))  // Limit to first 3
        {
            tags.Add($"protocol:{protocol.ToLowerInvariant()}");
        }

        return tags;
    }
}
