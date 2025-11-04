// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Service responsible for analyzing deployment requirements from user requests.
/// </summary>
public sealed class DeploymentAnalysisService
{
    private readonly ILlmProvider? _llmProvider;
    private readonly ILogger<DeploymentAnalysisService> _logger;

    public DeploymentAnalysisService(ILlmProvider? llmProvider, ILogger<DeploymentAnalysisService> logger)
    {
        _llmProvider = llmProvider;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes deployment requirements from user request using LLM inference or fallback heuristics.
    /// </summary>
    public async Task<DeploymentAnalysis> AnalyzeAsync(string request, AgentExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("AnalyzeDeploymentRequirementsAsync called with request: {Request}", request);
        _logger.LogDebug("LLM provider is available: {IsAvailable}", _llmProvider != null);

        if (_llmProvider != null)
        {
            // Use LLM to intelligently analyze the deployment request
            var analysisPrompt = $$"""
                Analyze this deployment request and extract structured information:

                Request: "{{request}}"

                Respond with JSON containing:
                {
                  "deploymentType": "DockerCompose|Kubernetes|TerraformAWS|TerraformAzure",
                  "targetEnvironment": "development|staging|production",
                  "requiredServices": ["service1", "service2", ...],
                  "infrastructureNeeds": {
                    "needsDatabase": true|false,
                    "databaseType": "postgis|postgresql|mysql|sqlserver|mongodb|null",
                    "needsCache": true|false,
                    "cacheType": "redis|memcached|null",
                    "needsLoadBalancer": true|false,
                    "needsMessageQueue": true|false,
                    "needsObservability": true|false,
                    "observabilityStack": "prometheus-grafana|aspire-dashboard|victoriametrics|null"
                  }
                }

                Services to consider: honua-server (always), postgis, mysql, sqlserver, redis, nginx, rabbitmq, kafka, prometheus, grafana, aspire-dashboard, victoriametrics
                Database types: postgis (PostgreSQL with PostGIS), mysql (MySQL), sqlserver (Microsoft SQL Server)
                Cache types: redis, memcached
                Observability stacks: prometheus-grafana (full stack), aspire-dashboard (dev/testing), victoriametrics (lightweight)
                Deployment types: DockerCompose for "docker compose", Kubernetes for "k8s/kubernetes", TerraformAWS for "aws/terraform", TerraformAzure for "azure/terraform"

                Return ONLY valid JSON, no markdown formatting.
                """;

            var llmRequest = new LlmRequest
            {
                UserPrompt = analysisPrompt,
                Temperature = 0.1,  // Low temperature for consistent structured output
                MaxTokens = 500
            };

            var llmResponse = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

            if (llmResponse.Success)
            {
                try
                {
                    var cleanJson = llmResponse.Content.Trim();
                    _logger.LogDebug("LLM Response: {Response}", cleanJson.Substring(0, Math.Min(500, cleanJson.Length)));
                    // Remove markdown code blocks if present
                    if (cleanJson.StartsWith("```json"))
                        cleanJson = cleanJson.Substring(7);
                    if (cleanJson.StartsWith("```"))
                        cleanJson = cleanJson.Substring(3);
                    if (cleanJson.EndsWith("```"))
                        cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
                    cleanJson = cleanJson.Trim();

                    var options = CliJsonOptions.DevTooling;
                    var llmAnalysis = JsonSerializer.Deserialize<LlmDeploymentAnalysis>(cleanJson, options);

                    if (llmAnalysis != null && llmAnalysis.InfrastructureNeeds != null)
                    {
                        _logger.LogDebug("Deserialized RequiredServices: {RequiredServices}", string.Join(", ", llmAnalysis.RequiredServices ?? new List<string>()));

                        // Use TryParse instead of Parse for safer enum parsing
                        if (!Enum.TryParse<DeploymentType>(llmAnalysis.DeploymentType, true, out var deploymentType))
                        {
                            deploymentType = DeploymentType.DockerCompose; // Default fallback
                        }

                        var requiredServices = new List<string>(llmAnalysis.RequiredServices ?? new List<string>());

                        // Ensure database type is in RequiredServices if needed
                        if (llmAnalysis.InfrastructureNeeds.NeedsDatabase && !llmAnalysis.InfrastructureNeeds.DatabaseType.IsNullOrEmpty())
                        {
                            var dbType = llmAnalysis.InfrastructureNeeds.DatabaseType.ToLowerInvariant();
                            if (!requiredServices.Contains(dbType, StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add(dbType);
                                _logger.LogDebug("Added database type '{DatabaseType}' to RequiredServices", dbType);
                            }
                        }

                        // Ensure cache type is in RequiredServices if needed
                        if (llmAnalysis.InfrastructureNeeds.NeedsCache && !llmAnalysis.InfrastructureNeeds.CacheType.IsNullOrEmpty())
                        {
                            var cacheType = llmAnalysis.InfrastructureNeeds.CacheType.ToLowerInvariant();
                            if (!requiredServices.Contains(cacheType, StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add(cacheType);
                                _logger.LogDebug("Added cache type '{CacheType}' to RequiredServices", cacheType);
                            }
                        }

                        // Ensure load balancer service is in RequiredServices if needed
                        if (llmAnalysis.InfrastructureNeeds.NeedsLoadBalancer)
                        {
                            // Check if nginx or traefik was mentioned in the original request
                            var lbService = request.ToLowerInvariant().Contains("traefik") ? "traefik" :
                                          request.ToLowerInvariant().Contains("caddy") ? "caddy" :
                                          "nginx"; // default to nginx

                            if (!requiredServices.Contains(lbService, StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add(lbService);
                                _logger.LogDebug("Added load balancer '{LoadBalancer}' to RequiredServices", lbService);
                            }
                        }

                        // Detect storage needs from request (blob storage, CDN, Front Door)
                        if (request.ToLowerInvariant().Contains("blob") ||
                            request.ToLowerInvariant().Contains("storage") ||
                            request.ToLowerInvariant().Contains("cdn") ||
                            request.ToLowerInvariant().Contains("front door"))
                        {
                            if (!requiredServices.Contains("blob storage", StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add("blob storage");
                                _logger.LogDebug("Added 'blob storage' to RequiredServices from request");
                            }
                        }

                        // Detect monitoring needs from request (Application Insights, monitoring, observability)
                        if (request.ToLowerInvariant().Contains("application insights") ||
                            request.ToLowerInvariant().Contains("app insights") ||
                            request.ToLowerInvariant().Contains("insights") ||
                            request.ToLowerInvariant().Contains("monitoring") ||
                            request.ToLowerInvariant().Contains("observability"))
                        {
                            if (!requiredServices.Contains("application insights", StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add("application insights");
                                _logger.LogDebug("Added 'application insights' to RequiredServices from request");
                            }
                        }

                        // Detect managed identity needs from request
                        if (request.ToLowerInvariant().Contains("managed identity") ||
                            request.ToLowerInvariant().Contains("identity") ||
                            request.ToLowerInvariant().Contains("rbac"))
                        {
                            if (!requiredServices.Contains("managed identity", StringComparer.OrdinalIgnoreCase))
                            {
                                requiredServices.Add("managed identity");
                                _logger.LogDebug("Added 'managed identity' to RequiredServices from request");
                            }
                        }

                        var analysis = new DeploymentAnalysis
                        {
                            DeploymentType = deploymentType,
                            TargetEnvironment = llmAnalysis.TargetEnvironment ?? "development",
                            RequiredServices = requiredServices,
                            InfrastructureNeeds = new InfrastructureRequirements
                            {
                                NeedsDatabase = llmAnalysis.InfrastructureNeeds.NeedsDatabase,
                                DatabaseType = llmAnalysis.InfrastructureNeeds.DatabaseType,
                                NeedsCache = llmAnalysis.InfrastructureNeeds.NeedsCache,
                                CacheType = llmAnalysis.InfrastructureNeeds.CacheType,
                                NeedsLoadBalancer = llmAnalysis.InfrastructureNeeds.NeedsLoadBalancer,
                                NeedsObservability = llmAnalysis.InfrastructureNeeds.NeedsObservability,
                                ObservabilityStack = llmAnalysis.InfrastructureNeeds.ObservabilityStack
                            },
                            Port = ExtractPort(request)
                        };

                        _logger.LogDebug("Final RequiredServices in analysis: {RequiredServices}", string.Join(", ", analysis.RequiredServices));
                        return analysis;
                    }
                }
                catch (JsonException)
                {
                    // Fall through to hard-coded analysis
                }
            }
        }

        // Fallback to hard-coded analysis if LLM not available or failed
        return new DeploymentAnalysis
        {
            DeploymentType = DetermineDeploymentType(request),
            TargetEnvironment = DetermineEnvironment(request),
            RequiredServices = ExtractServices(request),
            InfrastructureNeeds = AnalyzeInfrastructure(request),
            Port = ExtractPort(request)
        };
    }

    private DeploymentType DetermineDeploymentType(string request)
    {
        var lower = request.ToLowerInvariant();

        // Explicit docker-compose request
        if (lower.Contains("docker") && lower.Contains("compose"))
            return DeploymentType.DockerCompose;

        // Kubernetes deployments
        if (lower.Contains("kubernetes") || lower.Contains("k8s") || lower.Contains("helm"))
            return DeploymentType.Kubernetes;

        // AWS cloud services (ECS, Fargate, etc.) - use Terraform
        if (lower.Contains("aws") || lower.Contains("ecs") || lower.Contains("fargate") ||
            lower.Contains("rds") || lower.Contains("elasticache") || lower.Contains("cloudwatch") ||
            (lower.Contains("terraform") && lower.Contains("aws")))
            return DeploymentType.TerraformAWS;

        // Azure cloud services - use Terraform
        if (lower.Contains("azure") || lower.Contains("container apps") || lower.Contains("aca") ||
            lower.Contains("cosmos") || lower.Contains("app service") || lower.Contains("key vault") ||
            (lower.Contains("terraform") && lower.Contains("azure")))
            return DeploymentType.TerraformAzure;

        // GCP cloud services - use Terraform
        if (lower.Contains("gcp") || lower.Contains("google cloud") || lower.Contains("cloud run") ||
            lower.Contains("cloud sql") || lower.Contains("cloud storage") || lower.Contains("secret manager") ||
            (lower.Contains("terraform") && (lower.Contains("gcp") || lower.Contains("google"))))
            return DeploymentType.TerraformGCP;

        // Default to Docker Compose for local/simple deployments
        return DeploymentType.DockerCompose;
    }

    private string DetermineEnvironment(string request)
    {
        var lower = request.ToLowerInvariant();

        if (lower.Contains("production") || lower.Contains("prod"))
            return "production";

        if (lower.Contains("staging") || lower.Contains("stage"))
            return "staging";

        if (lower.Contains("development") || lower.Contains("dev"))
            return "development";

        return "development";
    }

    private List<string> ExtractServices(string request)
    {
        var services = new List<string>();
        var lower = request.ToLowerInvariant();

        // Always include Honua server
        services.Add("honua-server");

        if (lower.Contains("postgis") || lower.Contains("postgres"))
            services.Add("postgis");

        if (lower.Contains("mysql"))
            services.Add("mysql");

        if (lower.Contains("sql server") || lower.Contains("sqlserver"))
            services.Add("sqlserver");

        if (lower.Contains("redis"))
            services.Add("redis");

        if (lower.Contains("nginx"))
            services.Add("nginx");

        if (lower.Contains("traefik"))
            services.Add("traefik");

        if (lower.Contains("caddy"))
            services.Add("caddy");

        // Observability services
        if (lower.Contains("prometheus"))
            services.Add("prometheus");

        if (lower.Contains("grafana"))
            services.Add("grafana");

        if (lower.Contains("aspire") && lower.Contains("dashboard"))
            services.Add("aspire-dashboard");

        if (lower.Contains("victoria"))
            services.Add("victoriametrics");

        // Azure-specific services
        if (lower.Contains("blob") || lower.Contains("storage") || lower.Contains("cdn") || lower.Contains("front door"))
            services.Add("blob storage");

        if (lower.Contains("application insights") || lower.Contains("app insights") ||
            lower.Contains("insights") || lower.Contains("monitoring") || lower.Contains("observability"))
            services.Add("application insights");

        if (lower.Contains("managed identity") || lower.Contains("identity") || lower.Contains("rbac"))
            services.Add("managed identity");

        return services;
    }

    private int ExtractPort(string request)
    {
        // Look for patterns like "port 18100", "use port 5000", "on port 8080"
        var portMatch = Regex.Match(
            request,
            @"(?:use\s+)?port\s+(\d+)",
            RegexOptions.IgnoreCase);

        if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
        {
            _logger.LogDebug("Extracted port {Port} from request", port);
            return port;
        }

        // Default to 5000 if no port specified
        _logger.LogDebug("No port specified in request, using default 5000");
        return 5000;
    }

    private InfrastructureRequirements AnalyzeInfrastructure(string request)
    {
        var lower = request.ToLowerInvariant();
        return new InfrastructureRequirements
        {
            NeedsDatabase = lower.Contains("database") || lower.Contains("postgis"),
            NeedsCache = lower.Contains("redis") || lower.Contains("cache"),
            NeedsLoadBalancer = lower.Contains("nginx") || lower.Contains("load balance"),
            NeedsObservability = lower.Contains("metrics") || lower.Contains("observability") ||
                                lower.Contains("prometheus") || lower.Contains("grafana") ||
                                lower.Contains("aspire") || lower.Contains("monitoring"),
            ObservabilityStack = DetermineObservabilityStack(request)
        };
    }

    private string? DetermineObservabilityStack(string request)
    {
        var lower = request.ToLowerInvariant();

        if (lower.Contains("aspire") && lower.Contains("dashboard"))
            return "aspire-dashboard";

        if (lower.Contains("victoria"))
            return "victoriametrics";

        if (lower.Contains("prometheus") && lower.Contains("grafana"))
            return "prometheus-grafana";

        if (lower.Contains("prometheus") || lower.Contains("grafana"))
            return "prometheus-grafana";  // Default to full stack if either mentioned

        return null;
    }
}

// LLM response type for JSON deserialization
internal sealed class LlmDeploymentAnalysis
{
    public string DeploymentType { get; set; } = "DockerCompose";
    public string TargetEnvironment { get; set; } = "development";
    public List<string> RequiredServices { get; set; } = new();
    public LlmInfrastructureNeeds InfrastructureNeeds { get; set; } = new();
}

internal sealed class LlmInfrastructureNeeds
{
    public bool NeedsDatabase { get; set; }
    public string? DatabaseType { get; set; }
    public bool NeedsCache { get; set; }
    public string? CacheType { get; set; }
    public bool NeedsLoadBalancer { get; set; }
    public bool NeedsMessageQueue { get; set; }
    public bool NeedsObservability { get; set; }
    public string? ObservabilityStack { get; set; }
}
