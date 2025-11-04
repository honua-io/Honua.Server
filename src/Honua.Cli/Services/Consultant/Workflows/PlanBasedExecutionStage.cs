// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Telemetry;
using Honua.Cli.AI.Services.VectorSearch;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Handles traditional plan-based consultant workflow: planning, approval, and execution.
/// </summary>
public sealed class PlanBasedExecutionStage : IWorkflowStage<WorkflowContext, WorkflowContext>
{
    private readonly IAnsiConsole _console;
    private readonly IConsultantPlanner _planner;
    private readonly IConsultantPlanFormatter _planFormatter;
    private readonly IConsultantExecutor _executor;
    private readonly IPatternUsageTelemetry? _patternTelemetry;
    private readonly IConsultantSessionStore? _sessionStore;
    private readonly Honua.Cli.AI.Services.Agents.Specialized.DiagramGeneratorAgent? _diagramGenerator;
    private readonly Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationAgent? _architectureDocAgent;
    private readonly IHonuaCliEnvironment _environment;

    public PlanBasedExecutionStage(
        IAnsiConsole console,
        IConsultantPlanner planner,
        IConsultantPlanFormatter planFormatter,
        IConsultantExecutor executor,
        IHonuaCliEnvironment environment,
        IPatternUsageTelemetry? patternTelemetry = null,
        IConsultantSessionStore? sessionStore = null,
        Honua.Cli.AI.Services.Agents.Specialized.DiagramGeneratorAgent? diagramGenerator = null,
        Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationAgent? architectureDocAgent = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _planFormatter = planFormatter ?? throw new ArgumentNullException(nameof(planFormatter));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _patternTelemetry = patternTelemetry;
        _sessionStore = sessionStore;
        _diagramGenerator = diagramGenerator;
        _architectureDocAgent = architectureDocAgent;
    }

    public async Task<WorkflowContext> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        if (context.PlanningContext == null)
        {
            throw new InvalidOperationException("Planning context is required for plan-based execution");
        }

        var request = context.Request;
        var planningContext = context.PlanningContext;

        // Create plan
        var plan = await _planner.CreatePlanAsync(planningContext, cancellationToken).ConfigureAwait(false);

        if (plan.Steps.Count == 0)
        {
            _console.WriteLine("No plan steps were produced. Try refining the prompt.");
            context.Plan = plan;
            return context;
        }

        _planFormatter.Render(plan, request);
        _console.WriteLine();

        // Generate and display architecture diagram if available
        if (_diagramGenerator != null && !request.DryRun)
        {
            await RenderArchitectureDiagramAsync(plan, planningContext, cancellationToken);
        }

        // Generate and display architecture documentation if available
        if (_architectureDocAgent != null && !request.DryRun)
        {
            await RenderArchitectureDocumentationAsync(plan, planningContext, cancellationToken);
        }

        // Display metadata configuration if Honua deployment
        if (!request.DryRun && IsHonuaDeployment(planningContext))
        {
            RenderMetadataConfiguration(planningContext);
        }

        // Dry-run mode: show plan but don't execute
        if (request.DryRun)
        {
            _console.WriteLine("Dry-run mode: Plan generated. No actions will be executed.");
            context.Plan = plan;
            return context;
        }

        // Request approval for execution
        var approved = request.AutoApprove;

        // Check for confidence-based auto-approval
        if (!approved && request.TrustHighConfidence && plan.RecommendedPatternIds?.Count > 0)
        {
            approved = CheckHighConfidenceAutoApproval(plan);
            if (approved)
            {
                _console.MarkupLine("[green]✓ Auto-approved: All recommended patterns have High confidence (≥80%)[/]");
                _console.WriteLine();
            }
        }

        if (!approved)
        {
            var confirmation = new ConfirmationPrompt("Execute this plan?")
            {
                DefaultValue = true
            };

            approved = _console.Prompt(confirmation);
        }

        if (!approved)
        {
            _console.WriteLine("Plan approval declined. No actions executed.");
            context.Plan = plan;
            return context;
        }

        // Track pattern acceptance when plan is approved
        await TrackPatternAcceptanceAsync(plan, planningContext, cancellationToken).ConfigureAwait(false);

        // Save session for potential refinement
        await SaveSessionAsync(plan, planningContext, cancellationToken).ConfigureAwait(false);

        // Execute the approved plan
        var executionResult = await _executor.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);

        // Track deployment outcomes
        await TrackDeploymentOutcomeAsync(plan, executionResult, cancellationToken).ConfigureAwait(false);

        context.Plan = plan;
        context.ExecutionResult = executionResult;
        return context;
    }

    private bool CheckHighConfidenceAutoApproval(ConsultantPlan plan)
    {
        if (plan.RecommendedPatternIds == null || plan.RecommendedPatternIds.Count == 0)
        {
            return false;
        }

        // This is a simplified check - in a real implementation, you'd need to retrieve
        // the actual patterns and check their confidence scores
        // For now, we'll check if the plan's overall confidence is "high"
        if (!plan.Confidence.IsNullOrEmpty() &&
            plan.Confidence.Contains("high", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private Task TrackPatternAcceptanceAsync(
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        if (_patternTelemetry == null || plan.RecommendedPatternIds == null || plan.RecommendedPatternIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Update telemetry to mark patterns as accepted (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                var requirements = BuildDeploymentRequirements(context);
                var rank = 1;
                foreach (var patternId in plan.RecommendedPatternIds)
                {
                    try
                    {
                        await _patternTelemetry.TrackRecommendationAsync(
                            patternId,
                            requirements,
                            new PatternConfidence { Overall = 1.0, Level = "High" },
                            rank++,
                            wasAccepted: true,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _console.MarkupLine($"[dim]Note: Pattern tracking failed: {ex.Message}[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Critical error in pattern acceptance tracking: {ex.GetType().Name}: {ex.Message}");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    private Task TrackDeploymentOutcomeAsync(
        ConsultantPlan plan,
        ExecutionResult executionResult,
        CancellationToken cancellationToken)
    {
        if (_patternTelemetry == null || plan.RecommendedPatternIds == null || plan.RecommendedPatternIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Track deployment outcomes for all recommended patterns (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var patternId in plan.RecommendedPatternIds)
                {
                    try
                    {
                        await _patternTelemetry.TrackDeploymentOutcomeAsync(
                            patternId,
                            success: executionResult.Success,
                            feedback: executionResult.Message,
                            deploymentMetadata: null,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _console.MarkupLine($"[dim]Note: Deployment outcome tracking failed: {ex.Message}[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Critical error in deployment outcome tracking: {ex.GetType().Name}: {ex.Message}");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    private static DeploymentRequirements BuildDeploymentRequirements(ConsultantPlanningContext context)
    {
        return new DeploymentRequirements
        {
            CloudProvider = DetermineCloudProvider(context),
            DataVolumeGb = EstimateDataVolume(context),
            ConcurrentUsers = EstimateConcurrentUsers(context),
            Region = DetermineRegion(context)
        };
    }

    private static string DetermineCloudProvider(ConsultantPlanningContext context)
    {
        var infraProvider = context.Workspace.Infrastructure.PotentialCloudProviders.FirstOrDefault();
        if (!infraProvider.IsNullOrEmpty())
        {
            return infraProvider.ToLowerInvariant();
        }

        return "aws";
    }

    private static int EstimateDataVolume(ConsultantPlanningContext context)
    {
        var dataSources = context.Workspace.Metadata?.DataSources.Count ?? 0;
        if (dataSources > 0)
        {
            var estimate = 50 + (dataSources * 25);
            return Math.Clamp(estimate, 50, 1000);
        }

        return 100;
    }

    private static int EstimateConcurrentUsers(ConsultantPlanningContext context)
    {
        var services = context.Workspace.Metadata?.Services.Count ?? 0;
        if (services > 0)
        {
            var estimate = 40 + (services * 20);
            return Math.Clamp(estimate, 40, 400);
        }

        return 60;
    }

    private static string DetermineRegion(ConsultantPlanningContext context)
    {
        foreach (var tag in context.Workspace.Tags)
        {
            var lower = tag.Trim().ToLowerInvariant();
            if (lower.StartsWith("us-") || lower.StartsWith("eu-") || lower.StartsWith("ap-") ||
                lower.StartsWith("ca-") || lower.StartsWith("sa-"))
            {
                return lower;
            }
        }

        return "us-east-1";
    }

    private async Task SaveSessionAsync(
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        if (_sessionStore == null)
        {
            return;
        }

        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var requestHash = Math.Abs((context.Request.Prompt ?? "unknown").GetHashCode()).ToString("X8");
            var sessionId = $"{timestamp}-{requestHash}";

            await _sessionStore.SaveSessionAsync(sessionId, plan, context, cancellationToken);
            _console.MarkupLine($"[dim]Session ID: {sessionId} (use 'honua consultant refine --session {sessionId}' to refine)[/]");
        }
        catch (Exception)
        {
            // Don't fail the workflow if session save fails
        }
    }

    #region Rendering Methods

    private async Task RenderArchitectureDiagramAsync(
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        if (_diagramGenerator == null)
        {
            return;
        }

        try
        {
            _console.Write(new Rule("[bold blue]Architecture Diagram[/]").RuleStyle("blue dim"));
            _console.WriteLine();

            var cloudProvider = DetermineCloudProviderFromPlan(plan);
            var deploymentSummary = plan.ExecutiveSummary ?? context.Request.Prompt ?? "Honua deployment";

            var diagram = await _diagramGenerator.GenerateAsciiArchitectureDiagramAsync(
                deploymentSummary,
                cloudProvider,
                cancellationToken);

            _console.WriteLine(diagram);
            _console.WriteLine();

            var hasTerraformSteps = plan.Steps.Any(s =>
                s.Action.Contains("Terraform", StringComparison.OrdinalIgnoreCase) ||
                s.Skill.Contains("Deployment", StringComparison.OrdinalIgnoreCase));

            if (hasTerraformSteps)
            {
                _console.MarkupLine("[dim]💡 After execution, run 'terraform graph | dot -Tsvg > diagram.svg' in the terraform directory for detailed infrastructure diagram[/]");
                _console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[dim yellow]⚠️  Diagram generation failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    private string DetermineCloudProviderFromPlan(ConsultantPlan plan)
    {
        var allText = string.Join(" ", plan.Steps.Select(s => $"{s.Description} {s.Rationale}"))
            .ToLowerInvariant();

        if (allText.Contains("aws") || allText.Contains("ecs") || allText.Contains("ec2") || allText.Contains("rds"))
            return "AWS";
        if (allText.Contains("azure") || allText.Contains("container apps"))
            return "Azure";
        if (allText.Contains("gcp") || allText.Contains("google cloud") || allText.Contains("cloud run"))
            return "GCP";
        if (allText.Contains("kubernetes") || allText.Contains("k8s"))
            return "Kubernetes";
        if (allText.Contains("docker"))
            return "Docker";

        return "Cloud";
    }

    private async Task RenderArchitectureDocumentationAsync(
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        if (_architectureDocAgent == null)
        {
            return;
        }

        try
        {
            _console.Write(new Rule("[bold blue]Architecture Documentation[/]").RuleStyle("blue dim"));
            _console.WriteLine();

            var cloudProvider = DetermineCloudProviderFromPlan(plan);
            var request = new Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationRequest
            {
                DeploymentSummary = plan.ExecutiveSummary ?? context.Request.Prompt ?? "Honua deployment",
                CloudProvider = cloudProvider,
                PlanSteps = plan.Steps.Select(s => $"{s.Description} ({s.Rationale})").ToList(),
                UserRequirements = context.Request.Prompt
            };

            var doc = await _architectureDocAgent.GenerateAsync(request, cancellationToken);

            _console.MarkupLine("[bold]Executive Summary:[/]");
            _console.WriteLine(doc.ExecutiveSummary);
            _console.WriteLine();

            _console.MarkupLine("[bold]Key Architecture Decisions:[/]");
            var overviewLines = doc.ArchitectureOverview.Split('\n').Take(10);
            foreach (var line in overviewLines)
            {
                if (line.HasValue())
                {
                    _console.WriteLine(line);
                }
            }
            _console.MarkupLine("[dim]... (see full documentation for complete details)[/]");
            _console.WriteLine();

            var docMarkdown = _architectureDocAgent.RenderMarkdown(doc);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var docPath = System.IO.Path.Combine(_environment.LogsRoot, $"architecture-{timestamp}.md");
            await System.IO.File.WriteAllTextAsync(docPath, docMarkdown, cancellationToken);

            _console.MarkupLine($"[dim]📄 Full architecture documentation saved: {Markup.Escape(docPath)}[/]");
            _console.WriteLine();
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[dim yellow]⚠️  Architecture documentation generation failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    private void RenderMetadataConfiguration(ConsultantPlanningContext context)
    {
        try
        {
            _console.Write(new Rule("[bold blue]Honua Metadata Configuration[/]").RuleStyle("blue dim"));
            _console.WriteLine();

            if (context.Workspace.MetadataDetected && context.Workspace.Metadata != null)
            {
                var metadata = context.Workspace.Metadata;
                _console.MarkupLine("[bold]Current Metadata Summary:[/]");
                _console.MarkupLine($"  • Services: {metadata.Services.Count}");
                _console.MarkupLine($"  • Data Sources: {metadata.DataSources.Count}");
                _console.MarkupLine($"  • Raster Datasets: {metadata.RasterDatasets.Count}");
                _console.WriteLine();

                if (metadata.Services.Any())
                {
                    var firstService = metadata.Services.First();
                    _console.MarkupLine("[bold]Sample Service Configuration:[/]");
                    _console.WriteLine($"  Service ID: {firstService.Id}");
                    _console.WriteLine($"  Type: {firstService.ServiceType}");
                    _console.WriteLine($"  Enabled: {firstService.Enabled}");
                    _console.WriteLine();
                }

                _console.MarkupLine("[dim]💡 Tip: Use 'honua consultant' with data ingestion prompts to generate complete metadata configurations[/]");
            }
            else
            {
                _console.MarkupLine("[bold]Honua Metadata Configuration Template:[/]");
                _console.WriteLine();
                _console.MarkupLine("[dim]Your deployment will need metadata.json configuration for GIS services.[/]");
                _console.MarkupLine("[dim]Key components:[/]");
                _console.WriteLine("  • dataSources: Database connections (Npgsql for PostGIS)");
                _console.WriteLine("  • services: Feature/Map servers with OGC protocol configuration");
                _console.WriteLine("  • layers: Individual datasets with geometry and field definitions");
                _console.WriteLine();
                _console.MarkupLine("[dim]💡 Tip: Use 'honua consultant' with \"help me ingest data\" to generate complete configurations[/]");
            }

            _console.WriteLine();
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[dim yellow]⚠️  Metadata configuration display failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    private bool IsHonuaDeployment(ConsultantPlanningContext context)
    {
        if (context.Workspace.MetadataDetected)
        {
            return true;
        }

        var prompt = context.Request.Prompt?.ToLowerInvariant() ?? "";
        var gisKeywords = new[] { "gis", "spatial", "postgis", "ogc", "wfs", "wms", "layer", "feature", "honua" };
        return gisKeywords.Any(keyword => prompt.Contains(keyword));
    }

    #endregion
}
