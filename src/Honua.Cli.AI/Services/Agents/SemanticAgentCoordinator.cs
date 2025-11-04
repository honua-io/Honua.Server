// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.AI.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Coordinates multiple specialized agents using Semantic Kernel to fulfill user requests.
/// Implements intent analysis, agent routing, and multi-agent orchestration transparently.
/// </summary>
public sealed class SemanticAgentCoordinator : IAgentCoordinator
{
    private readonly ILlmProvider _llmProvider;
    private readonly Kernel _kernel;
    private readonly IntelligentAgentSelector _agentSelector;
    private readonly IPatternUsageTelemetry? _telemetry;
    private readonly IAgentHistoryStore? _historyStore;
    private readonly ILogger<SemanticAgentCoordinator> _logger;
    private readonly ILlmProviderRouter? _router;
    private readonly ILlmProviderFactory? _factory;
    private readonly LlmProviderOptions? _options;
    private readonly List<AgentInteraction> _sessionHistory = new();
    private readonly string _sessionId = Guid.NewGuid().ToString();

    public SemanticAgentCoordinator(
        ILlmProvider llmProvider,
        Kernel kernel,
        IntelligentAgentSelector agentSelector,
        ILogger<SemanticAgentCoordinator> logger,
        IPatternUsageTelemetry? telemetry = null,
        IAgentHistoryStore? historyStore = null,
        ILlmProviderRouter? router = null,
        ILlmProviderFactory? factory = null,
        Microsoft.Extensions.Options.IOptions<LlmProviderOptions>? options = null)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _agentSelector = agentSelector ?? throw new ArgumentNullException(nameof(agentSelector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetry = telemetry;
        _historyStore = historyStore;
        _router = router;
        _factory = factory;
        _options = options?.Value;
    }

    public async Task<AgentCoordinatorResult> ProcessRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var agentsInvolved = new List<string>();
        var steps = new List<AgentStepResult>();
        var warnings = new List<string>();

        try
        {
            // Phase 1: Intent Analysis
            var intent = await AnalyzeIntentAsync(request, context, cancellationToken);

            if (context.Verbosity >= VerbosityLevel.Debug)
            {
                Console.WriteLine($"[Debug] Detected intent: {intent.PrimaryIntent}");
                Console.WriteLine($"[Debug] Requires agents: {string.Join(", ", intent.RequiredAgents)}");
            }

            // Phase 2: Route to Appropriate Agent(s)
            var agentResults = new List<AgentStepResult>();

            if (intent.RequiresMultipleAgents)
            {
                // Multi-agent orchestration
                agentResults = await OrchestrateMultiAgentsAsync(intent, request, context, cancellationToken);
            }
            else
            {
                // Single agent execution with intelligent selection
                var availableAgents = intent.RequiredAgents.ToArray();
                string selectedAgent;
                AgentConfidence? confidence = null;

                if (availableAgents.Length > 1)
                {
                    // Use intelligent selector if multiple candidates
                    var selection = await _agentSelector.SelectBestAgentAsync(
                        request,
                        availableAgents,
                        cancellationToken);

                    selectedAgent = selection.AgentName;
                    confidence = selection.Confidence;

                    _logger.LogInformation(
                        "Intelligent routing selected {AgentName} with {Level} confidence ({Score:P0})",
                        selectedAgent,
                        confidence.Level,
                        confidence.Overall);
                }
                else
                {
                    // Only one agent option
                    selectedAgent = availableAgents.First();
                }

                var result = await ExecuteSingleAgentAsync(
                    selectedAgent,
                    intent,
                    request,
                    context,
                    confidence,
                    cancellationToken);
                agentResults.Add(result);
            }

            agentsInvolved.AddRange(agentResults.Select(r => r.AgentName).Distinct());
            steps.AddRange(agentResults);

            // Phase 3: Synthesize Response
            var response = SynthesizeResponse(agentResults, context.Verbosity);

            // Phase 4: Determine Next Steps
            var nextSteps = DetermineNextSteps(intent, agentResults);

            // Record interaction in memory
            var interaction = new AgentInteraction
            {
                Timestamp = startTime,
                UserRequest = request,
                AgentsUsed = agentsInvolved,
                Success = agentResults.All(r => r.Success),
                Response = response
            };
            _sessionHistory.Add(interaction);

            var overallSuccess = agentResults.All(r => r.Success);
            var totalDuration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Save to persistent history store
            await SaveSessionHistoryAsync(
                request,
                response,
                agentsInvolved.ToArray(),
                overallSuccess,
                intent.PrimaryIntent,
                totalDuration,
                cancellationToken);

            return new AgentCoordinatorResult
            {
                Success = overallSuccess,
                Response = response,
                AgentsInvolved = agentsInvolved,
                Steps = steps,
                Warnings = warnings,
                NextSteps = nextSteps
            };
        }
        catch (Exception ex)
        {
            return new AgentCoordinatorResult
            {
                Success = false,
                ErrorMessage = $"Agent coordination failed: {ex.Message}",
                AgentsInvolved = agentsInvolved,
                Steps = steps,
                Warnings = warnings
            };
        }
    }

    public Task<AgentInteractionHistory> GetHistoryAsync()
    {
        return Task.FromResult(new AgentInteractionHistory
        {
            SessionId = _sessionId,
            Interactions = _sessionHistory.ToList()
        });
    }

    /// <summary>
    /// Analyzes user intent to determine which agents are needed.
    /// </summary>
    private async Task<AgentStepResult> HandleDeploymentExecutionAgent(
        string agentPrompt,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Parse the prompt to determine deployment type and location
        // The prompt should contain information about what terraform directory to execute
        var deploymentType = "terraform";
        var terraformDir = "terraform";

        // Check if the prompt contains specific provider information
        if (agentPrompt.Contains("aws", StringComparison.OrdinalIgnoreCase))
        {
            deploymentType = "aws";
            terraformDir = "terraform-aws";
        }
        else if (agentPrompt.Contains("azure", StringComparison.OrdinalIgnoreCase))
        {
            deploymentType = "azure";
            terraformDir = "terraform-azure";
        }
        else if (agentPrompt.Contains("gcp", StringComparison.OrdinalIgnoreCase) ||
                 agentPrompt.Contains("google", StringComparison.OrdinalIgnoreCase))
        {
            deploymentType = "gcp";
            terraformDir = "terraform-gcp";
        }

        var executionAgent = new Specialized.DeploymentExecutionAgent(_kernel, _llmProvider);
        return await executionAgent.ExecuteDeploymentAsync(
            deploymentType,
            terraformDir,
            context,
            cancellationToken);
    }

    private async Task<IntentAnalysisResult> AnalyzeIntentAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Use smart routing for intent analysis if available (fast task → OpenAI preferred)
        var provider = await GetProviderForTaskAsync("intent-classification", "low", cancellationToken);

        var systemPrompt = @"You are an expert intent classifier for a geospatial infrastructure consultant system.

Your job is to analyze user requests and determine which specialized agents should handle them.

CRITICAL ROUTING RULES:
- If request contains ""generate"", ""create"", ""terraform"", ""docker-compose"", ""kubernetes"", ""manifest"", ""AWS"", ""Azure"", ""GCP"" → USE DeploymentConfiguration
- If request contains ""error"", ""broken"", ""fix"", ""debug"", ""issue"" → USE Troubleshooting
- Troubleshooting is ONLY for diagnosing existing problems, NOT for generating new infrastructure

Available Specialized Agents:
1. **ArchitectureConsulting** - Design recommendations, cost analysis, trade-off evaluation, architecture options
2. **DeploymentConfiguration** - Generate infrastructure code (Terraform, docker-compose, Kubernetes manifests), cloud deployment configuration (AWS, Azure, GCP), container orchestration setup
3. **DeploymentExecution** - Execute Terraform deployments, run terraform init/plan/apply, validate infrastructure
4. **PerformanceBenchmark** - Performance benchmarking, load testing, capacity planning, stress testing, benchmark strategy generation
5. **PerformanceOptimization** - Database indexes, query tuning, caching strategies, scaling recommendations
6. **SecurityHardening** - Authentication setup, authorization policies, CORS configuration, security scanning
7. **HonuaUpgrade** - Version upgrades, patch management, database migrations, blue/green deployments
8. **MigrationImport** - Data import (GeoPackage, Shapefile), ArcGIS migration, schema mapping
9. **Troubleshooting** - Error diagnosis, log analysis, health check failures, root cause analysis (NOT for generating infrastructure)
10. **CertificateManagement** - SSL/TLS certificate setup, Let's Encrypt ACME automation, certificate renewal, HTTPS configuration
11. **DnsConfiguration** - DNS record management, Route53/Cloudflare/Azure DNS/GCP DNS integration, DNS propagation verification
12. **BlueGreenDeployment** - Zero-downtime deployments, blue-green cutover, canary rollout, telemetry-based automatic rollback
13. **SpaDeployment** - Single Page Application (SPA) deployment support, CORS configuration, subdomain/API Gateway routing, React/Vue/Angular integration examples

Intent Categories:
- **architecture**: Design decisions, cost analysis, comparing deployment options, trade-offs
- **setup**: Initial setup, first-time configuration, workspace initialization
- **deployment**: Infrastructure deployment, cloud resources, container orchestration
- **data**: Data loading, format conversion, schema mapping
- **performance**: Slow queries, optimization needs, scaling issues
- **benchmark**: Load testing, performance benchmarking, capacity planning, stress testing
- **security**: Authentication, authorization, security hardening
- **upgrade**: Version upgrades, patch application
- **troubleshooting**: Errors, failures, debugging
- **metadata**: OGC metadata, service configuration
- **migration**: ArcGIS to Honua migration
- **spa**: Single Page Application deployment, CORS setup, frontend integration

Analyze the request and return JSON:
{
  ""primaryIntent"": ""<intent category>"",
  ""requiredAgents"": [""<agent1>"", ""<agent2>""],
  ""requiresMultipleAgents"": true/false,
  ""reasoning"": ""<why these agents>""
}

Examples:
- ""What's the best way to deploy Honua?"" → {primaryIntent: ""architecture"", requiredAgents: [""ArchitectureConsulting""], requiresMultipleAgents: false}
- ""I need to deploy for 10000 users, what will it cost?"" → {primaryIntent: ""architecture"", requiredAgents: [""ArchitectureConsulting""], requiresMultipleAgents: false}
- ""Should I use Kubernetes or serverless?"" → {primaryIntent: ""architecture"", requiredAgents: [""ArchitectureConsulting""], requiresMultipleAgents: false}
- ""Generate Terraform for AWS with EC2 and RDS"" → {primaryIntent: ""deployment"", requiredAgents: [""DeploymentConfiguration""], requiresMultipleAgents: false}
- ""Create docker-compose.yml with PostgreSQL"" → {primaryIntent: ""deployment"", requiredAgents: [""DeploymentConfiguration""], requiresMultipleAgents: false}
- ""Generate Kubernetes manifests for production"" → {primaryIntent: ""deployment"", requiredAgents: [""DeploymentConfiguration""], requiresMultipleAgents: false}
- ""Create a load testing plan for my deployment"" → {primaryIntent: ""benchmark"", requiredAgents: [""PerformanceBenchmark""], requiresMultipleAgents: false}
- ""How do I benchmark my tile server?"" → {primaryIntent: ""benchmark"", requiredAgents: [""PerformanceBenchmark""], requiresMultipleAgents: false}
- ""My parcels layer is slow"" → {primaryIntent: ""troubleshooting"", requiredAgents: [""Troubleshooting"", ""PerformanceOptimization""], requiresMultipleAgents: true}
- ""Deploy to AWS/Azure/GCP"" → {primaryIntent: ""deployment"", requiredAgents: [""ArchitectureConsulting"", ""DeploymentConfiguration"", ""DeploymentExecution""], requiresMultipleAgents: true}
- ""Set up production with OAuth"" → {primaryIntent: ""deployment"", requiredAgents: [""DeploymentConfiguration"", ""SecurityHardening""], requiresMultipleAgents: true}
- ""Migrate from ArcGIS"" → {primaryIntent: ""migration"", requiredAgents: [""MigrationImport""], requiresMultipleAgents: false}
- ""Upgrade to latest version"" → {primaryIntent: ""upgrade"", requiredAgents: [""HonuaUpgrade""], requiresMultipleAgents: false}
- ""Help me deploy my React app with Honua"" → {primaryIntent: ""spa"", requiredAgents: [""SpaDeployment""], requiresMultipleAgents: false}
- ""I need CORS setup for my Vue frontend"" → {primaryIntent: ""spa"", requiredAgents: [""SpaDeployment""], requiresMultipleAgents: false}
- ""How do I integrate Angular with Honua API?"" → {primaryIntent: ""spa"", requiredAgents: [""SpaDeployment""], requiresMultipleAgents: false}";

        var userPrompt = $@"Analyze this user request:
""{request}""

Context:
- Workspace: {context.WorkspacePath}
- Mode: {(context.DryRun ? "planning" : "execution")}

Return JSON intent analysis.";

        var llmRequest = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            Temperature = 0.1, // Low temperature for consistent intent classification
            MaxTokens = 500
        };

        var response = await provider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogWarning("LLM intent analysis failed: {Error}", response.Content);
            // Fallback: route to DeploymentConfiguration for infrastructure requests
            return new IntentAnalysisResult
            {
                PrimaryIntent = "deployment",
                RequiredAgents = new List<string> { "DeploymentConfiguration" },
                RequiresMultipleAgents = false,
                Reasoning = "Intent analysis failed, using deployment fallback for infrastructure requests"
            };
        }

        try
        {
            // Parse JSON response
            var jsonContent = ExtractJson(response.Content);
            _logger.LogDebug("Intent analysis JSON: {Json}", jsonContent);

            var result = JsonSerializer.Deserialize<IntentAnalysisResult>(jsonContent, CliJsonOptions.DevTooling);

            if (result != null)
            {
                _logger.LogInformation("Intent classified as: {Intent}, Agents: {Agents}",
                    result.PrimaryIntent, string.Join(", ", result.RequiredAgents));
            }

            return result ?? new IntentAnalysisResult
            {
                PrimaryIntent = "deployment",
                RequiredAgents = new List<string> { "DeploymentConfiguration" },
                RequiresMultipleAgents = false
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse intent JSON: {Content}", response.Content);
            // Fallback to DeploymentConfiguration for infrastructure requests
            return new IntentAnalysisResult
            {
                PrimaryIntent = "deployment",
                RequiredAgents = new List<string> { "DeploymentConfiguration" },
                RequiresMultipleAgents = false,
                Reasoning = "Could not parse intent, using deployment fallback"
            };
        }
    }

    /// <summary>
    /// Executes a single specialized agent.
    /// </summary>
    private async Task<AgentStepResult> ExecuteSingleAgentAsync(
        string agentName,
        IntentAnalysisResult intent,
        string originalRequest,
        AgentExecutionContext context,
        AgentConfidence? confidence,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var agentPrompt = BuildAgentPrompt(agentName, intent, originalRequest, context);

            // Instantiate and invoke the appropriate specialized agent
            AgentStepResult result = agentName switch
            {
                "ArchitectureConsulting" => await new Specialized.ArchitectureConsultingAgent(_kernel, _llmProvider)
                    .AnalyzeArchitectureAsync(agentPrompt, context, cancellationToken),

                "DeploymentConfiguration" => await new Specialized.DeploymentConfigurationAgent(_kernel, _llmProvider)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "DeploymentExecution" => await HandleDeploymentExecutionAgent(agentPrompt, context, cancellationToken),

                "PerformanceBenchmark" => await new Specialized.PerformanceBenchmarkAgent(_kernel, _llmProvider)
                    .GenerateBenchmarkPlanAsync(agentPrompt, context, cancellationToken),

                "PerformanceOptimization" => await new Specialized.PerformanceOptimizationAgent(_kernel)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "SecurityHardening" => await new Specialized.SecurityHardeningAgent(_kernel)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "HonuaUpgrade" => await new Specialized.HonuaUpgradeAgent(_kernel)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "MigrationImport" => await new Specialized.MigrationImportAgent(_kernel)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "DataIngestion" => await new Specialized.DataIngestionAgent(_kernel, _llmProvider, Microsoft.Extensions.Logging.Abstractions.NullLogger<Specialized.DataIngestionAgent>.Instance)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "BlueGreenDeployment" => await new Specialized.BlueGreenDeploymentAgent(_kernel, _llmProvider)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "CertificateManagement" => await new Specialized.CertificateManagementAgent(_kernel, _llmProvider)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "CloudPermissionGenerator" => new AgentStepResult
                {
                    AgentName = "CloudPermissionGenerator",
                    Action = "generate_permissions",
                    Success = false,
                    Message = "CloudPermissionGenerator requires deployment analysis - use through consultant workflow",
                    Duration = TimeSpan.Zero
                },

                "CostReview" => ConvertCostReviewResult(
                    await new Specialized.CostReviewAgent(_kernel, _llmProvider)
                        .ReviewAsync("terraform", agentPrompt, context, cancellationToken)),

                "DeploymentTopology" => new AgentStepResult
                {
                    AgentName = "DeploymentTopology",
                    Action = "analyze_topology",
                    Success = false,
                    Message = "DeploymentTopologyAnalyzer requires terraform plan or config - use through consultant workflow",
                    Duration = TimeSpan.Zero
                },

                "DnsConfiguration" => await new Specialized.DnsConfigurationAgent(_kernel, _llmProvider)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "GitOpsConfiguration" => await new Specialized.GitOpsConfigurationAgent(_kernel, _llmProvider)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "HonuaConsultant" => new AgentStepResult
                {
                    AgentName = "HonuaConsultant",
                    Action = "generate_configuration",
                    Success = false,
                    Message = "HonuaConsultant requires deployment analysis - use through consultant workflow",
                    Duration = TimeSpan.Zero
                },

                "SecurityReview" => ConvertSecurityReviewResult(
                    await new Specialized.SecurityReviewAgent(_kernel, _llmProvider)
                        .ReviewAsync("terraform", agentPrompt, context, cancellationToken)),

                "ObservabilityConfiguration" => await new Specialized.ObservabilityConfigurationAgent(_kernel, _llmProvider, Microsoft.Extensions.Logging.Abstractions.NullLogger<Specialized.ObservabilityConfigurationAgent>.Instance)
                    .GenerateObservabilityConfigAsync(
                        new Specialized.ObservabilityConfigRequest
                        {
                            DeploymentName = "honua",
                            Environment = "development",
                            Platform = "docker-compose",
                            Backend = "prometheus"
                        },
                        context,
                        cancellationToken),

                "Troubleshooting" => await new Specialized.TroubleshootingAgent(_kernel)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                "SpaDeployment" => await new Specialized.SpaDeploymentAgent(_kernel, _llmProvider, Microsoft.Extensions.Logging.Abstractions.NullLogger<Specialized.SpaDeploymentAgent>.Instance)
                    .ProcessAsync(agentPrompt, context, cancellationToken),

                _ => new AgentStepResult
                {
                    AgentName = agentName,
                    Action = "process_request",
                    Success = false,
                    Message = $"Unknown agent: {agentName}",
                    Duration = sw.Elapsed
                }
            };

            sw.Stop();

            // Track agent performance if telemetry is available
            if (_telemetry != null && confidence != null)
            {
                await TrackAgentPerformanceAsync(
                    agentName,
                    intent.PrimaryIntent,
                    result.Success,
                    confidence.Overall,
                    (int)sw.ElapsedMilliseconds,
                    result.Success ? null : result.Message,
                    cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();

            // Track failure if telemetry is available
            if (_telemetry != null && confidence != null)
            {
                await TrackAgentPerformanceAsync(
                    agentName,
                    intent.PrimaryIntent,
                    success: false,
                    confidence.Overall,
                    (int)sw.ElapsedMilliseconds,
                    ex.Message,
                    cancellationToken);
            }

            return new AgentStepResult
            {
                AgentName = agentName,
                Action = "process_request",
                Success = false,
                Message = $"Agent threw exception: {ex.Message}",
                Duration = sw.Elapsed
            };
        }
    }

    /// <summary>
    /// Tracks agent performance asynchronously (fire-and-forget).
    /// </summary>
    private async Task TrackAgentPerformanceAsync(
        string agentName,
        string taskType,
        bool success,
        double confidenceScore,
        int executionTimeMs,
        string? feedback,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telemetry!.TrackAgentPerformanceAsync(
                agentName,
                taskType,
                success,
                confidenceScore,
                executionTimeMs,
                feedback,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track agent performance for {AgentName}", agentName);
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    /// <summary>
    /// Orchestrates multiple agents in sequence or parallel as needed.
    /// </summary>
    private async Task<List<AgentStepResult>> OrchestrateMultiAgentsAsync(
        IntentAnalysisResult intent,
        string originalRequest,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var results = new List<AgentStepResult>();

        // Use intelligent agent selection to pick the best agent for this task
        var (selectedAgent, confidence) = await _agentSelector.SelectBestAgentAsync(
            originalRequest,
            intent.RequiredAgents.ToArray(),
            cancellationToken);

        _logger.LogInformation(
            "Multi-agent orchestration selected {AgentName} with {Level} confidence ({Score:P0})",
            selectedAgent,
            confidence.Level,
            confidence.Overall);

        // Determine execution strategy: sequential vs parallel
        var executionStrategy = DetermineExecutionStrategy(intent);

        if (executionStrategy == ExecutionStrategy.Sequential)
        {
            // Execute selected agent first, then others if needed
            var orderedAgents = new[] { selectedAgent }
                .Concat(intent.RequiredAgents.Where(a => a != selectedAgent))
                .ToList();

            foreach (var agentName in orderedAgents)
            {
                var agentConfidence = agentName == selectedAgent ? confidence : null;
                var result = await ExecuteSingleAgentAsync(
                    agentName,
                    intent,
                    originalRequest,
                    context,
                    agentConfidence,
                    cancellationToken);
                results.Add(result);

                // Stop if critical agent fails
                if (!result.Success && IsAgentCritical(agentName, intent))
                {
                    break;
                }
            }
        }
        else
        {
            // Execute agents in parallel (when they're independent)
            var tasks = intent.RequiredAgents.Select(agentName =>
            {
                var agentConfidence = agentName == selectedAgent ? confidence : null;
                return ExecuteSingleAgentAsync(
                    agentName,
                    intent,
                    originalRequest,
                    context,
                    agentConfidence,
                    cancellationToken);
            });

            var agentResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            results.AddRange(agentResults);
        }

        return results;
    }

    /// <summary>
    /// Synthesizes individual agent responses into a unified user-facing response.
    /// </summary>
    private string SynthesizeResponse(List<AgentStepResult> agentResults, VerbosityLevel verbosity)
    {
        if (!agentResults.Any())
        {
            return "No agents were able to process your request.";
        }

        var sb = new StringBuilder();

        // For normal verbosity, hide agent details and present unified response
        if (verbosity <= VerbosityLevel.Normal)
        {
            var successfulResults = agentResults.Where(r => r.Success).ToList();
            if (successfulResults.Any())
            {
                // Combine successful responses
                foreach (var result in successfulResults)
                {
                    sb.AppendLine(result.Message);
                }
            }
            else
            {
                sb.AppendLine("Unable to complete the request:");
                foreach (var result in agentResults.Where(r => !r.Success))
                {
                    sb.AppendLine($"  • {result.Message}");
                }
            }
        }
        else
        {
            // Debug/Verbose mode: Show agent details
            sb.AppendLine($"Coordinated {agentResults.Count} specialized agent(s):");
            sb.AppendLine();

            foreach (var result in agentResults)
            {
                var status = result.Success ? "✓" : "✗";
                sb.AppendLine($"{status} {result.AgentName} ({result.Duration.TotalMilliseconds:F0}ms)");

                if (verbosity >= VerbosityLevel.Debug)
                {
                    sb.AppendLine($"  Action: {result.Action}");
                }

                sb.AppendLine($"  {result.Message}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Determines suggested next steps based on the results.
    /// </summary>
    private List<string> DetermineNextSteps(IntentAnalysisResult intent, List<AgentStepResult> results)
    {
        var nextSteps = new List<string>();

        // Intent-specific next steps
        switch (intent.PrimaryIntent)
        {
            case "setup":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Verify the setup by running: honua status");
                    nextSteps.Add("Import your first dataset");
                }
                break;

            case "deployment":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Test the deployed endpoints");
                    nextSteps.Add("Set up monitoring and alerts");
                    nextSteps.Add("Configure backup and disaster recovery");
                }
                break;

            case "benchmark":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Review the generated benchmark plan");
                    nextSteps.Add("Execute baseline tests first to establish normal performance");
                    nextSteps.Add("Run load tests and analyze results for bottlenecks");
                }
                break;

            case "performance":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Run performance benchmarks to verify improvements");
                    nextSteps.Add("Monitor query performance over time");
                }
                break;

            case "migration":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Validate migrated data for accuracy");
                    nextSteps.Add("Update client applications to use new endpoints");
                    nextSteps.Add("Run parallel systems during validation period");
                }
                break;

            case "spa":
                if (results.All(r => r.Success))
                {
                    nextSteps.Add("Add CORS configuration to metadata.json");
                    nextSteps.Add("Test cross-origin requests from your SPA");
                    nextSteps.Add("Implement JWT Bearer authentication");
                    nextSteps.Add("Deploy with CloudFront/Azure Front Door for production");
                }
                break;
        }

        return nextSteps;
    }

    // Helper methods

    private string BuildAgentPrompt(
        string agentName,
        IntentAnalysisResult intent,
        string originalRequest,
        AgentExecutionContext context)
    {
        var builder = new StringBuilder();
        builder.Append(originalRequest?.Trim() ?? string.Empty);

        if (intent.Reasoning.HasValue())
        {
            builder.Append("\n");
            builder.Append($"(intent: {intent.PrimaryIntent}, reason: {intent.Reasoning})");
        }
        else if (intent.PrimaryIntent.HasValue())
        {
            builder.Append("\n");
            builder.Append($"(intent: {intent.PrimaryIntent})");
        }

        builder.Append("\n");
        builder.Append($"Mode: {(context.DryRun ? "plan" : "execute")}");
        builder.Append("\n");
        builder.Append($"Agent focus: {agentName}");

        return builder.ToString();
    }

    private string GetAgentSystemPrompt(string agentName)
    {
        return agentName switch
        {
            "ArchitectureConsulting" => @"You are the Architecture Consulting specialist. You help users make informed decisions about their GIS infrastructure:
- Analyze requirements (users, data volume, traffic patterns, budget)
- Present multiple architecture options (serverless, K8s, VMs, hybrid)
- Compare cost vs. performance trade-offs
- Recommend appropriate cloud provider and deployment strategy
- Explain complexity, scalability, and operational burden
Focus on understanding the user's needs and guiding them to the right design decision.",

            "DeploymentConfiguration" => @"You are the Deployment Configuration specialist. You handle:
- OGC metadata generation and updates
- Service configuration (WFS, WMS, OGC API Features)
- GitOps workflows for environment promotion
- Layer and collection management
Focus on declarative, version-controlled configuration changes.",

            "DeploymentExecution" => @"You are the Deployment Execution specialist. You handle:
- Running terraform init, plan, and apply commands
- Executing cloud deployments (AWS, Azure, GCP)
- Infrastructure provisioning and validation
- Deployment rollbacks and tear-downs
Focus on actual infrastructure deployment and execution.",

            "PerformanceBenchmark" => @"You are the Performance Benchmark specialist. You handle:
- Load testing strategy and plan generation
- Capacity planning and sizing recommendations
- Stress testing and breaking point identification
- Benchmark tool configuration (Apache Bench, wrk, Locust, k6)
- Performance baseline establishment
Focus on generating practical, executable benchmark plans with clear success criteria.",

            "PerformanceOptimization" => @"You are the Performance Optimization specialist. You handle:
- Spatial index creation (GiST, BRIN, SP-GiST)
- Query optimization and EXPLAIN plan analysis
- Caching strategies (Redis, CDN, response caching)
- Database tuning (connection pooling, work_mem, shared_buffers)
- Vector tile optimization
Focus on measurable performance improvements with clear metrics.",

            "SecurityHardening" => @"You are the Security Hardening specialist. You handle:
- Authentication configuration (OAuth2, OIDC, API keys)
- Authorization policies (RBAC, ABAC)
- CORS policy setup
- Security scanning and vulnerability assessment
- Secrets management (Vault, AWS Secrets Manager)
Focus on defense-in-depth and principle of least privilege.",

            "HonuaUpgrade" => @"You are the Honua Upgrade specialist. You handle:
- Version upgrade planning
- Database schema migrations
- Blue/green and canary deployment strategies
- Rollback procedures
- Breaking change management
Focus on zero-downtime upgrades with safe rollback plans.",

            "MigrationImport" => @"You are the Migration & Import specialist. You handle:
- ArcGIS Server/Portal migration to Honua
- Data import (GeoPackage, Shapefile, GeoJSON, PostGIS dumps)
- Schema mapping and transformation
- Coded domain conversion
- Attachment migration
Focus on data integrity and validation throughout the migration process.",

            "Troubleshooting" => @"You are the Troubleshooting & Diagnostics specialist. You handle:
- Error diagnosis and root cause analysis
- Log analysis and pattern detection
- Performance degradation investigation
- Health check failure diagnosis
- Configuration validation
Focus on systematic problem-solving with clear evidence and actionable solutions.",

            "SpaDeployment" => @"You are the Single Page Application (SPA) Deployment specialist. You handle:
- SPA framework detection (React, Vue, Angular, Svelte)
- CORS configuration for metadata.json
- Subdomain deployment architecture (app.example.com + api.example.com)
- API Gateway path routing (CloudFront, Azure Front Door, Cloud CDN)
- Framework-specific integration examples (axios, fetch, HttpClient)
- JWT Bearer authentication setup for SPAs
Focus on practical, production-ready SPA deployments with security best practices.",

            _ => "You are a general-purpose geospatial consultant."
        };
    }

    private ExecutionStrategy DetermineExecutionStrategy(IntentAnalysisResult intent)
    {
        // Some agent combinations need sequential execution
        var sequentialPairs = new[]
        {
            ("Troubleshooting", "PerformanceOptimization"),
            ("MigrationImport", "PerformanceOptimization"),
            ("DeploymentConfiguration", "SecurityHardening"),
            ("DeploymentConfiguration", "DeploymentExecution"),
            ("DeploymentExecution", "SecurityHardening")
        };

        var agents = intent.RequiredAgents.ToList();

        // Check if we have a known sequential pattern
        for (int i = 0; i < agents.Count - 1; i++)
        {
            var pair = (agents[i], agents[i + 1]);
            if (sequentialPairs.Contains(pair))
            {
                return ExecutionStrategy.Sequential;
            }
        }

        // Default to parallel execution for independent agents
        return ExecutionStrategy.Parallel;
    }

    private bool IsAgentCritical(string agentName, IntentAnalysisResult intent)
    {
        // First agent in a sequence is always critical
        return intent.RequiredAgents.IndexOf(agentName) == 0;
    }

    private string ExtractJson(string text)
    {
        // Remove markdown code blocks if present
        if (text.Contains("```json"))
        {
            var startIndex = text.IndexOf("```json") + 7;
            var endIndex = text.IndexOf("```", startIndex);
            if (endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        else if (text.Contains("```"))
        {
            var startIndex = text.IndexOf("```") + 3;
            var endIndex = text.IndexOf("```", startIndex);
            if (endIndex > startIndex)
            {
                return text.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        return text.Trim();
    }

    /// <summary>
    /// Saves agent session history to persistent storage.
    /// </summary>
    private async Task SaveSessionHistoryAsync(
        string userRequest,
        string agentResponse,
        string[] agentsUsed,
        bool success,
        string taskType,
        int totalDurationMs,
        CancellationToken cancellationToken)
    {
        if (_historyStore == null)
        {
            return; // History store not configured
        }

        try
        {
            // Save individual interaction
            await _historyStore.SaveInteractionAsync(
                _sessionId,
                string.Join(", ", agentsUsed),
                userRequest,
                agentResponse,
                success,
                confidenceScore: null,
                taskType,
                totalDurationMs,
                errorMessage: success ? null : agentResponse,
                metadata: null,
                cancellationToken);

            // Save session summary
            var outcome = success ? "success" : "failure";
            await _historyStore.SaveSessionSummaryAsync(
                _sessionId,
                userRequest,
                outcome,
                agentsUsed,
                _sessionHistory.Count,
                totalDurationMs,
                userSatisfaction: null,
                metadata: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save agent session history for {SessionId}", _sessionId);
            // Don't throw - history failures shouldn't break the workflow
        }
    }

    /// <summary>
    /// Gets the appropriate LLM provider for a task type.
    /// Uses smart routing if enabled and multiple providers available, otherwise uses default.
    /// </summary>
    private async Task<ILlmProvider> GetProviderForTaskAsync(string taskType, string criticality, CancellationToken cancellationToken)
    {
        // If smart routing is disabled or not available, use default provider
        if (_router == null || _factory == null || _options == null || !_options.EnableSmartRouting)
        {
            return _llmProvider;
        }

        // Check if multiple providers are available
        var availableProviders = _factory.GetAvailableProviders()
            .Where(p => p != "mock")
            .ToArray();

        if (availableProviders.Length <= 1)
        {
            // Only one provider, use default
            return _llmProvider;
        }

        // Use smart routing
        try
        {
            var taskContext = new LlmTaskContext
            {
                TaskType = taskType,
                Criticality = criticality,
                MaxLatencyMs = 30000,
                MaxCostUsd = 0.10m
            };

            // NOTE: This is a CLI tool context, not ASP.NET, so this async call is safe.
            var routed = await _router.RouteRequestAsync(
                new LlmRequest { SystemPrompt = "", UserPrompt = "" }, // Dummy request for routing
                taskContext,
                cancellationToken
            ).ConfigureAwait(false);

            // Get provider name from routed response metadata or default
            // For now, determine provider based on task context
            var selectedProvider = taskType.ToLowerInvariant() switch
            {
                "intent-classification" => "openai",
                "security-review" => "anthropic",
                "cost-review" => "openai",
                "architecture-swarm" => "anthropic",
                _ => criticality == "critical" ? "anthropic" : "openai"
            };

            var provider = _factory.GetProvider(selectedProvider);
            if (provider != null)
            {
                _logger.LogDebug("Routed {TaskType} to {Provider}", taskType, selectedProvider);
                return provider;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Smart routing failed, falling back to default provider");
        }

        return _llmProvider;
    }

    private static AgentStepResult ConvertCostReviewResult(Specialized.CostReviewResult result)
    {
        return new AgentStepResult
        {
            AgentName = "CostReview",
            Action = "review",
            Success = true,
            Message = $"Cost Review: {result.Issues.Count} issues found",
            Duration = TimeSpan.Zero
        };
    }

    private static AgentStepResult ConvertSecurityReviewResult(Specialized.SecurityReviewResult result)
    {
        return new AgentStepResult
        {
            AgentName = "SecurityReview",
            Action = "review",
            Success = true,
            Message = $"Security Review: {result.Issues.Count} issues found",
            Duration = TimeSpan.Zero
        };
    }
}

/// <summary>
/// Result of intent analysis.
/// </summary>
public sealed class IntentAnalysisResult
{
    public string PrimaryIntent { get; set; } = string.Empty;
    public List<string> RequiredAgents { get; set; } = new();
    public bool RequiresMultipleAgents { get; set; }
    public string? Reasoning { get; set; }
}

/// <summary>
/// Execution strategy for multi-agent workflows.
/// </summary>
internal enum ExecutionStrategy
{
    Sequential,
    Parallel
}
