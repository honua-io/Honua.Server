// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.Planning;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Analyzes deployment plans and HonuaIO configuration to build a complete deployment topology.
/// </summary>
public sealed class DeploymentTopologyAnalyzer
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<DeploymentTopologyAnalyzer> _logger;

    public DeploymentTopologyAnalyzer(
        ILlmProvider llmProvider,
        ILogger<DeploymentTopologyAnalyzer> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes an execution plan and extracts the deployment topology.
    /// </summary>
    public Task<DeploymentTopology> AnalyzeFromPlanAsync(
        ExecutionPlan plan,
        string cloudProvider,
        string region,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing deployment plan to extract topology");

        var topology = new DeploymentTopology
        {
            CloudProvider = cloudProvider,
            Region = region,
            Environment = DetermineEnvironment(plan)
        };

        // Analyze database requirements
        topology = topology with { Database = AnalyzeDatabaseConfig(plan) };

        // Analyze compute requirements
        topology = topology with { Compute = AnalyzeComputeConfig(plan) };

        // Analyze storage requirements
        topology = topology with { Storage = AnalyzeStorageConfig(plan) };

        // Analyze networking requirements
        topology = topology with { Networking = AnalyzeNetworkingConfig(plan) };

        // Analyze monitoring requirements
        topology = topology with { Monitoring = AnalyzeMonitoringConfig(plan) };

        // Extract enabled features
        topology = topology with { Features = ExtractFeatures(plan) };

        return Task.FromResult(topology);
    }

    /// <summary>
    /// Analyzes HonuaIO configuration file to extract deployment topology.
    /// </summary>
    public async Task<DeploymentTopology> AnalyzeFromConfigAsync(
        string configContent,
        string cloudProvider,
        string region,
        string environment,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing HonuaIO configuration to extract topology");

        var prompt = BuildConfigAnalysisPrompt(configContent, cloudProvider, region, environment);

        var systemPrompt = @"You are an expert at analyzing HonuaIO geospatial server configurations.
Extract the deployment topology from the configuration, identifying all infrastructure requirements.
Consider: database provider, storage backends (S3/Azure Blob/GCS), caching, attachment storage,
monitoring, and enabled services (WFS, WMS, WMTS, OData, etc.).";

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        // Parse the response to build topology
        // For now, return a basic topology - in production, parse the LLM response
        return new DeploymentTopology
        {
            CloudProvider = cloudProvider,
            Region = region,
            Environment = environment,
            Database = new DatabaseConfig
            {
                Engine = "postgres",
                Version = "15",
                InstanceSize = environment == "prod" ? "db.r6g.xlarge" : "db.t4g.micro",
                StorageGB = environment == "prod" ? 100 : 20,
                HighAvailability = environment == "prod"
            },
            Compute = new ComputeConfig
            {
                Type = "container",
                InstanceSize = environment == "prod" ? "c6i.2xlarge" : "t3.medium",
                InstanceCount = environment == "prod" ? 3 : 1,
                AutoScaling = environment == "prod"
            },
            Storage = new StorageConfig
            {
                Type = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "s3",
                    "azure" => "blob",
                    "gcp" => "gcs",
                    _ => "filesystem"
                },
                AttachmentStorageGB = 100,
                RasterCacheGB = 500,
                Replication = environment == "prod" ? "cross-region" : "single-region"
            },
            Networking = new NetworkingConfig
            {
                LoadBalancer = true,
                PublicAccess = true,
                VpnRequired = false
            },
            Monitoring = new MonitoringConfig
            {
                Provider = cloudProvider.ToLowerInvariant() switch
                {
                    "aws" => "cloudwatch",
                    "azure" => "application-insights",
                    "gcp" => "cloud-monitoring",
                    _ => "prometheus"
                },
                EnableMetrics = true,
                EnableLogs = true,
                EnableTracing = environment == "prod"
            },
            Features = new List<string>
            {
                "OGC WFS 2.0",
                "OGC WMS 1.3",
                "OGC WMTS 1.0",
                "OData v4",
                "Vector Tiles",
                "Raster Tiles",
                "STAC Catalog"
            }
        };
    }

    private string DetermineEnvironment(ExecutionPlan plan)
    {
        // Try to infer environment from plan metadata or steps
        if (plan.Id.Contains("prod", StringComparison.OrdinalIgnoreCase))
            return "prod";
        if (plan.Id.Contains("staging", StringComparison.OrdinalIgnoreCase))
            return "staging";
        return "dev";
    }

    private DatabaseConfig? AnalyzeDatabaseConfig(ExecutionPlan plan)
    {
        // Look for database-related steps
        var hasDatabase = plan.Steps.Any(s =>
            s.Type.ToString().Contains("Database", StringComparison.OrdinalIgnoreCase) ||
            s.Type.ToString().Contains("Postgres", StringComparison.OrdinalIgnoreCase) ||
            (s.Description?.Contains("database", StringComparison.OrdinalIgnoreCase) ?? false));

        if (!hasDatabase)
            return null;

        var environment = DetermineEnvironment(plan);

        return new DatabaseConfig
        {
            Engine = "postgres",
            Version = "15",
            InstanceSize = environment == "prod" ? "db.r6g.xlarge" : "db.t4g.micro",
            StorageGB = environment == "prod" ? 100 : 20,
            HighAvailability = environment == "prod"
        };
    }

    private ComputeConfig? AnalyzeComputeConfig(ExecutionPlan plan)
    {
        var environment = DetermineEnvironment(plan);

        // Check for serverless indicators in plan
        var allText = (plan.Description ?? "") + " " + string.Join(" ", plan.Steps.Select(s => s.Description ?? ""));
        var isServerless =
            allText.Contains("Lambda", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("Functions", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("Cloud Functions", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("serverless", StringComparison.OrdinalIgnoreCase) ||
            allText.Contains("FaaS", StringComparison.OrdinalIgnoreCase);

        if (isServerless)
        {
            return new ComputeConfig
            {
                Type = "serverless",
                InstanceSize = "N/A", // Serverless auto-sizes
                InstanceCount = 0, // Auto-scaled
                AutoScaling = true
            };
        }

        return new ComputeConfig
        {
            Type = "container",
            InstanceSize = environment == "prod" ? "c6i.2xlarge" : "t3.medium",
            InstanceCount = environment == "prod" ? 3 : 1,
            AutoScaling = environment == "prod"
        };
    }

    private StorageConfig? AnalyzeStorageConfig(ExecutionPlan plan)
    {
        var hasStorage = plan.Steps.Any(s =>
            s.Type.ToString().Contains("Storage", StringComparison.OrdinalIgnoreCase) ||
            s.Type.ToString().Contains("S3", StringComparison.OrdinalIgnoreCase) ||
            s.Type.ToString().Contains("Blob", StringComparison.OrdinalIgnoreCase) ||
            (s.Description?.Contains("storage", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.Description?.Contains("s3", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.Description?.Contains("bucket", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.Description?.Contains("blob", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (s.Description?.Contains("gcs", StringComparison.OrdinalIgnoreCase) ?? false));

        if (!hasStorage)
            return null;

        var environment = DetermineEnvironment(plan);

        return new StorageConfig
        {
            Type = "s3", // Default to S3, can be inferred from plan
            AttachmentStorageGB = 100,
            RasterCacheGB = 500,
            Replication = environment == "prod" ? "cross-region" : "single-region"
        };
    }

    private NetworkingConfig AnalyzeNetworkingConfig(ExecutionPlan plan)
    {
        return new NetworkingConfig
        {
            LoadBalancer = true,
            PublicAccess = true,
            VpnRequired = false
        };
    }

    private MonitoringConfig AnalyzeMonitoringConfig(ExecutionPlan plan)
    {
        var environment = DetermineEnvironment(plan);

        return new MonitoringConfig
        {
            Provider = "cloudwatch",
            EnableMetrics = true,
            EnableLogs = true,
            EnableTracing = environment == "prod"
        };
    }

    private List<string> ExtractFeatures(ExecutionPlan plan)
    {
        var features = new List<string>();

        // Extract from plan description and steps
        var allText = (plan.Description ?? "") + " " + string.Join(" ", plan.Steps.Select(s => s.Description ?? ""));

        if (allText.Contains("WFS", StringComparison.OrdinalIgnoreCase))
            features.Add("OGC WFS 2.0");
        if (allText.Contains("WMS", StringComparison.OrdinalIgnoreCase))
            features.Add("OGC WMS 1.3");
        if (allText.Contains("WMTS", StringComparison.OrdinalIgnoreCase))
            features.Add("OGC WMTS 1.0");
        if (allText.Contains("OData", StringComparison.OrdinalIgnoreCase))
            features.Add("OData v4");
        if (allText.Contains("Vector Tiles", StringComparison.OrdinalIgnoreCase))
            features.Add("Vector Tiles");
        if (allText.Contains("Raster", StringComparison.OrdinalIgnoreCase))
            features.Add("Raster Tiles");
        if (allText.Contains("STAC", StringComparison.OrdinalIgnoreCase))
            features.Add("STAC Catalog");

        return features;
    }

    private string BuildConfigAnalysisPrompt(
        string configContent,
        string cloudProvider,
        string region,
        string environment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze this HonuaIO configuration and extract the deployment topology:");
        sb.AppendLine();
        sb.AppendLine($"Target Cloud: {cloudProvider}");
        sb.AppendLine($"Region: {region}");
        sb.AppendLine($"Environment: {environment}");
        sb.AppendLine();
        sb.AppendLine("Configuration:");
        sb.AppendLine("```json");
        sb.AppendLine(configContent);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Identify:");
        sb.AppendLine("1. Database requirements (engine, size, HA)");
        sb.AppendLine("2. Compute requirements (VMs, containers, scaling)");
        sb.AppendLine("3. Storage backends (S3/Blob/GCS for attachments and raster cache)");
        sb.AppendLine("4. Networking (load balancer, public access)");
        sb.AppendLine("5. Monitoring and logging requirements");
        sb.AppendLine("6. Enabled OGC services and features");

        return sb.ToString();
    }
}
