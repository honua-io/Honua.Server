// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for generating Honua-specific configuration files (metadata.yaml, appsettings.json).
/// </summary>
public sealed class HonuaConfigurationService
{
    private readonly ILogger<HonuaConfigurationService> _logger;

    public HonuaConfigurationService(ILogger<HonuaConfigurationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Generates complete Honua configuration including metadata.yaml and appsettings.json.
    /// </summary>
    public async Task GenerateAsync(DeploymentAnalysis analysis, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        var warnings = new List<string>();

        // Generate metadata.yaml
        var metadata = GenerateMetadataConfiguration(analysis, out var metadataWarnings);
        warnings.AddRange(metadataWarnings);

        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var metadataYaml = serializer.Serialize(metadata);

        var metadataPath = Path.Combine(context.WorkspacePath, "metadata.yaml");
        await File.WriteAllTextAsync(metadataPath, metadataYaml, cancellationToken);

        _logger.LogInformation("Generated metadata.yaml");

        // Generate auxiliary configuration files for reverse proxies
        if (analysis.RequiredServices?.Any(s => s.Contains("nginx", StringComparison.OrdinalIgnoreCase)) ?? false)
        {
            await GenerateNginxConfigAsync(context, cancellationToken);
        }
        if (analysis.RequiredServices?.Any(s => s.Contains("prometheus", StringComparison.OrdinalIgnoreCase)) ?? false)
        {
            await GeneratePrometheusConfigAsync(context, cancellationToken);
        }

        // Generate appsettings.json
        var appSettings = GenerateAppSettingsConfiguration(analysis);
        var appSettingsJson = JsonSerializer.Serialize(appSettings, CliJsonOptions.Indented);

        var appSettingsPath = Path.Combine(context.WorkspacePath, "appsettings.json");
        await File.WriteAllTextAsync(appSettingsPath, appSettingsJson, cancellationToken);

        _logger.LogInformation("Generated appsettings.json");

        // Add authentication warning
        if (analysis.TargetEnvironment == "production")
        {
            warnings.Add("⚠️  QuickStart authentication configured for ease of setup. For production use, configure OIDC or Local authentication with proper identity management.");
        }

        // Log security warnings
        if (warnings.Count > 0)
        {
            _logger.LogWarning("SECURITY WARNINGS:");
            foreach (var warning in warnings)
            {
                _logger.LogWarning("  - {Warning}", warning);
            }
        }
    }

    private object GenerateMetadataConfiguration(DeploymentAnalysis analysis, out List<string> warnings)
    {
        warnings = new List<string>();

        // Determine database connection string based on deployment type
        var dbType = analysis.InfrastructureNeeds?.DatabaseType?.ToLowerInvariant() ?? "sqlite";
        string connectionString;
        string provider;

        if (dbType.Contains("postgis") || dbType.Contains("postgres"))
        {
            provider = "postgis";
            connectionString = "Host=postgis;Port=5432;Database=honua;Username=honua;Password=honua_password";
            warnings.Add("🔴 CRITICAL: Hardcoded database password. Use environment variables: ${HONUA_DB_PASSWORD}");
        }
        else if (dbType.Contains("mysql"))
        {
            provider = "mysql";
            connectionString = "Server=mysql;Port=3306;Database=honua;User=honua;Password=honua_password";
            warnings.Add("🔴 CRITICAL: Hardcoded database password. Use environment variables: ${HONUA_DB_PASSWORD}");
        }
        else if (dbType.Contains("sqlserver") || dbType.Contains("sql") && dbType.Contains("server"))
        {
            provider = "sqlserver";
            connectionString = "Server=sqlserver,1433;Database=honua;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True";
            warnings.Add("🔴 CRITICAL: Hardcoded database password. Use environment variables: ${HONUA_DB_PASSWORD}");
            warnings.Add("⚠️  TrustServerCertificate=True disables certificate validation. Enable in production.");
        }
        else
        {
            provider = "sqlite";
            connectionString = "Data Source=data/honua.db";
        }

        var metadata = new
        {
            catalog = new
            {
                id = "honua-catalog",
                title = "Honua GIS Catalog",
                description = $"Geospatial data services powered by Honua - {analysis.TargetEnvironment} environment",
                version = DateTime.UtcNow.ToString("yyyy.MM"),
                keywords = new[] { "gis", "ogc", "features", "tiles", analysis.TargetEnvironment }
            },
            folders = new[]
            {
                new
                {
                    id = "default",
                    title = "Default Services",
                    order = 10
                }
            },
            dataSources = new[]
            {
                new
                {
                    id = "primary",
                    provider = provider,
                    connectionString = connectionString
                }
            },
            services = new[]
            {
                new
                {
                    id = "sample-features",
                    title = "Sample Feature Service",
                    folderId = "default",
                    serviceType = "feature",
                    dataSourceId = "primary",
                    enabled = true,
                    description = "Sample OGC API Features service - configure layers via Honua CLI",
                    keywords = new[] { "sample", "features", "ogc" },
                    ogc = new
                    {
                        collectionsEnabled = true,
                        itemLimit = 1000,
                        defaultCrs = "EPSG:4326",
                        additionalCrs = new[] { "EPSG:3857" }
                    }
                }
            }
        };

        if (analysis.TargetEnvironment == "production")
        {
            warnings.Add("ℹ️  Production environment detected. Configure authentication via appsettings.json");
            warnings.Add("ℹ️  Enable HTTPS and configure TLS certificates");
            warnings.Add("ℹ️  Configure backup strategy for database and metadata");
        }

        return metadata;
    }

    private object GenerateAppSettingsConfiguration(DeploymentAnalysis analysis)
    {
        var logLevel = analysis.TargetEnvironment == "production" ? "Information" : "Debug";

        // For production, use QuickStart auth as a reasonable default (user should configure proper auth)
        // For development, also use QuickStart for ease of testing
        var authMode = "QuickStart";

        return new
        {
            logging = new
            {
                logLevel = new Dictionary<string, string>
                {
                    ["Default"] = logLevel,
                    ["Microsoft.AspNetCore"] = "Warning",
                    ["Honua"] = logLevel,
                    ["Honua.Server.Core"] = logLevel
                }
            },
            allowedHosts = "*",
            honua = new
            {
                metadata = new
                {
                    provider = "yaml",
                    path = "metadata.yaml"
                },
                authentication = new
                {
                    mode = authMode,
                    allowQuickStart = true
                },
                odata = new
                {
                    enabled = true,
                    allowWrites = analysis.TargetEnvironment != "production",
                    defaultPageSize = 100,
                    maxPageSize = 1000
                },
                services = new
                {
                    wfs = new { enabled = true },
                    wms = new { enabled = true },
                    wmts = new { enabled = true },
                    csw = new { enabled = true },
                    wcs = new { enabled = true },
                    stac = new { enabled = true }
                }
            }
        };
    }

    private async Task GenerateNginxConfigAsync(AgentExecutionContext context, CancellationToken cancellationToken)
    {
        var nginxConfig = @"events {
    worker_connections 1024;
}

http {
    upstream honua_backend {
        server honua:8080;
    }

    server {
        listen 80;
        server_name _;

        location / {
            proxy_pass http://honua_backend;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}";

        var nginxPath = Path.Combine(context.WorkspacePath, "nginx.conf");
        await File.WriteAllTextAsync(nginxPath, nginxConfig, cancellationToken);
        _logger.LogInformation("Generated nginx.conf");
    }

    private async Task GeneratePrometheusConfigAsync(AgentExecutionContext context, CancellationToken cancellationToken)
    {
        var prometheusConfig = @"global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'honua'
    static_configs:
      - targets: ['honua:8080']";

        var prometheusPath = Path.Combine(context.WorkspacePath, "prometheus.yml");
        await File.WriteAllTextAsync(prometheusPath, prometheusConfig, cancellationToken);
        _logger.LogInformation("Generated prometheus.yml");
    }
}
