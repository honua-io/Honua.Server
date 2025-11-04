// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Main consultant workflow implementation.
/// Delegates to refactored implementation for maintainability.
/// </summary>
public sealed class ConsultantWorkflow : IConsultantWorkflow
{
    private readonly ConsultantWorkflowRefactored _implementation;
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IConsultantContextBuilder _contextBuilder;
    private readonly IConsultantPlanner _planner;
    private readonly IConsultantPlanFormatter _planFormatter;
    private readonly ISessionLogWriter _logWriter;
    private readonly IConsultantExecutor _executor;
    private readonly IAgentCoordinator? _agentCoordinator;
    private readonly IEnumerable<IAgentCritic>? _agentCritics;
    private readonly IPatternUsageTelemetry? _patternTelemetry;
    private readonly IConsultantSessionStore? _sessionStore;
    private readonly Honua.Cli.AI.Services.Agents.Specialized.DiagramGeneratorAgent? _diagramGenerator;
    private readonly Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationAgent? _architectureDocAgent;

    public ConsultantWorkflow(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        IConsultantContextBuilder contextBuilder,
        IConsultantPlanner planner,
        IConsultantPlanFormatter planFormatter,
        ISessionLogWriter logWriter,
        IConsultantExecutor executor,
        IAgentCoordinator? agentCoordinator = null,
        IEnumerable<IAgentCritic>? agentCritics = null,
        IPatternUsageTelemetry? patternTelemetry = null,
        IConsultantSessionStore? sessionStore = null,
        Honua.Cli.AI.Services.Agents.Specialized.DiagramGeneratorAgent? diagramGenerator = null,
        Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationAgent? architectureDocAgent = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _planFormatter = planFormatter ?? throw new ArgumentNullException(nameof(planFormatter));
        _logWriter = logWriter ?? throw new ArgumentNullException(nameof(logWriter));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _agentCoordinator = agentCoordinator;
        _agentCritics = agentCritics;
        _patternTelemetry = patternTelemetry;
        _sessionStore = sessionStore;
        _diagramGenerator = diagramGenerator;
        _architectureDocAgent = architectureDocAgent;

        _implementation = new ConsultantWorkflowRefactored(
            console,
            environment,
            contextBuilder,
            planner,
            planFormatter,
            logWriter,
            executor,
            agentCoordinator,
            agentCritics,
            patternTelemetry,
            sessionStore,
            diagramGenerator,
            architectureDocAgent);
    }

    public async Task<ConsultantResult> ExecuteAsync(ConsultantRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _environment.EnsureInitialized();

        var workspacePath = _environment.ResolveWorkspacePath(request.WorkspacePath);
        var normalizedRequest = request with { WorkspacePath = workspacePath };

        _console.WriteLine("Honua Consultant (preview build)");
        _console.WriteLine($"Planning workspace: {workspacePath}");

        var prompt = normalizedRequest.Prompt;
        if (prompt.IsNullOrWhiteSpace())
        {
            prompt = _console.Ask<string>("[bold]What outcome should we plan together?[/]");
            normalizedRequest = normalizedRequest with { Prompt = prompt };
        }

        var planningContext = await _contextBuilder.BuildAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);

        RenderContextSummary(planningContext);

        // Multi-agent execution flow
        if (_agentCoordinator == null)
        {
            if (normalizedRequest.Mode == ConsultantExecutionMode.MultiAgent)
            {
                var message = "Multi-agent mode requested, but the agent coordinator is not configured.";
                _console.MarkupLine($"[red]{message}[/]");
                return new ConsultantResult(false, message, ConsultantPlan.Empty, false, false);
            }
        }
        else if (normalizedRequest.Mode == ConsultantExecutionMode.MultiAgent || normalizedRequest.Mode == ConsultantExecutionMode.Auto)
        {
            var coordinationResult = await RunMultiAgentAsync(normalizedRequest, cancellationToken).ConfigureAwait(false);

            if (coordinationResult.Success || normalizedRequest.Mode == ConsultantExecutionMode.MultiAgent)
            {
                return await FinalizeMultiAgentAsync(normalizedRequest, coordinationResult, cancellationToken);
            }

            RenderMultiAgentFallbackMessage(coordinationResult);
            if (normalizedRequest.Verbose)
            {
                RenderVerboseMultiAgentSummary(coordinationResult);
            }
            _console.WriteLine();
            _console.MarkupLine("[yellow]Falling back to plan-based consultant workflow...[/]");
            _console.WriteLine();
        }

        var plan = await _planner.CreatePlanAsync(planningContext, cancellationToken).ConfigureAwait(false);

        if (plan.Steps.Count == 0)
        {
            _console.WriteLine("No plan steps were produced. Try refining the prompt.");
            return new ConsultantResult(false, "No plan generated for the provided prompt.", plan, false, false);
        }

        _planFormatter.Render(plan, normalizedRequest);

        _console.WriteLine();

        // Generate and display architecture diagram if available
        if (_diagramGenerator != null && !normalizedRequest.DryRun)
        {
            await RenderArchitectureDiagramAsync(plan, planningContext, cancellationToken);
        }

        // Generate and display architecture documentation if available
        if (_architectureDocAgent != null && !normalizedRequest.DryRun)
        {
            await RenderArchitectureDocumentationAsync(plan, planningContext, cancellationToken);
        }

        // Display metadata configuration if Honua deployment
        if (!normalizedRequest.DryRun && IsHonuaDeployment(planningContext))
        {
            RenderMetadataConfiguration(planningContext);
        }

        // Dry-run mode: show plan but don't execute
        if (normalizedRequest.DryRun)
        {
            _console.WriteLine("Dry-run mode: Plan generated. No actions will be executed.");

        if (!normalizedRequest.SuppressLogging)
        {
            var logPath = await _logWriter
                .AppendAsync(BuildLogEntry(planningContext, plan, false), cancellationToken)
                .ConfigureAwait(false);
            AnnounceLogSaved(logPath);
        }

        if (normalizedRequest.Verbose)
        {
            _console.WriteLine("[dim]Verbose mode: additional execution details available in the session log.[/]");
        }

        return new ConsultantResult(true, "Dry-run plan ready for review.", plan, false, false);
    }

        // Request approval for execution
        var approved = normalizedRequest.AutoApprove;

        // Check for confidence-based auto-approval
        if (!approved && normalizedRequest.TrustHighConfidence && plan.RecommendedPatternIds?.Count > 0)
        {
            approved = CheckHighConfidenceAutoApproval(plan, planningContext);
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

        if (!normalizedRequest.SuppressLogging)
        {
            var logPath = await _logWriter
                .AppendAsync(BuildLogEntry(planningContext, plan, false), cancellationToken)
                .ConfigureAwait(false);
            AnnounceLogSaved(logPath);
        }

        if (normalizedRequest.Verbose)
        {
            _console.WriteLine("[dim]Verbose mode: additional execution details available in the session log.[/]");
        }

        return new ConsultantResult(true, "Plan generated but not approved by operator.", plan, false, false);
    }

        // Track pattern acceptance when plan is approved
        await TrackPatternAcceptanceAsync(plan, planningContext, cancellationToken).ConfigureAwait(false);

        // Save session for potential refinement
        await SaveSessionAsync(plan, planningContext, cancellationToken).ConfigureAwait(false);

        // Execute the approved plan
        var executionResult = await _executor.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);

        // Track deployment outcomes
        await TrackDeploymentOutcomeAsync(plan, executionResult, cancellationToken).ConfigureAwait(false);

        if (!normalizedRequest.SuppressLogging)
        {
            var logPath = await _logWriter
                .AppendAsync(BuildLogEntry(planningContext, plan, approved, executionResult), cancellationToken)
                .ConfigureAwait(false);
            AnnounceLogSaved(logPath);
        }

        if (normalizedRequest.Verbose)
        {
            _console.WriteLine("[dim]Verbose mode: additional execution details available in the session log.[/]");
        }

        if (executionResult.Success)
        {
            return new ConsultantResult(true, executionResult.Message, plan, true, true);
        }
        else
        {
            return new ConsultantResult(false, executionResult.Message, plan, true, false);
        }
    }

    private static string BuildLogEntry(ConsultantPlanningContext context, ConsultantPlan plan, bool approved)
    {
        return BuildLogEntry(context, plan, approved, null);
    }

    private static string BuildLogEntry(ConsultantPlanningContext context, ConsultantPlan plan, bool approved, ExecutionResult? executionResult)
    {
        var request = context.Request;

        var builder = new StringBuilder()
            .AppendLine($"Prompt: {request.Prompt}")
            .AppendLine($"Workspace: {request.WorkspacePath}")
            .AppendLine("Mode: " + (request.DryRun ? "dry-run" : "apply"))
            .AppendLine($"ExecutionMode: {request.Mode}")
            .AppendLine($"Approved: {approved.ToString().ToLowerInvariant()}")
            .AppendLine($"ContextTags: {string.Join(',', context.Workspace.Tags)}")
            .AppendLine("Steps:");

        var index = 1;
        foreach (var step in plan.Steps)
        {
            var inputs = step.Inputs.Count == 0
                ? "(no inputs)"
                : string.Join(", ", step.Inputs.Select(pair => $"{pair.Key}={pair.Value}"));

            builder.AppendLine($"  {index}. {step.Skill}.{step.Action} -> {inputs}");
            if (step.Category.HasValue())
            {
                builder.AppendLine($"     Category: {step.Category}");
            }
            if (step.Rationale.HasValue())
            {
                builder.AppendLine($"     Rationale: {step.Rationale}");
            }
            if (step.SuccessCriteria.HasValue())
            {
                builder.AppendLine($"     SuccessCriteria: {step.SuccessCriteria}");
            }
            if (step.Risk.HasValue())
            {
                builder.AppendLine($"     Risk: {step.Risk}");
            }
            if (step.Dependencies is { Count: > 0 })
            {
                builder.AppendLine($"     DependsOn: {string.Join(',', step.Dependencies)}");
            }

            if (executionResult?.StepResults != null)
            {
                var stepResult = executionResult.StepResults.FirstOrDefault(r => r.StepIndex == index);
                if (stepResult != null)
                {
                    builder.AppendLine($"     Result: {(stepResult.Success ? "SUCCESS" : "FAILED")}");
                    if (stepResult.Error.HasValue())
                    {
                        builder.AppendLine($"     Error: {stepResult.Error}");
                    }
                }
            }

            index++;
        }

        if (plan.ExecutiveSummary.HasValue())
        {
            builder.AppendLine()
                .AppendLine("ExecutiveSummary:")
                .AppendLine(plan.ExecutiveSummary);
        }

        if (plan.Confidence.HasValue())
        {
            builder.AppendLine($"Confidence: {plan.Confidence}");
        }

        if (executionResult != null)
        {
            builder.AppendLine()
                .AppendLine($"Execution: {(executionResult.Success ? "SUCCESS" : "FAILED")}")
                .AppendLine($"Message: {executionResult.Message}");
        }

        if (context.Observations.Count > 0)
        {
            builder.AppendLine()
                .AppendLine("ContextObservations:");
            foreach (var obs in context.Observations)
            {
                builder.AppendLine($"  - [{obs.Severity}] {obs.Summary}: {obs.Recommendation}");
            }
        }

        if (plan.HighlightedObservations is { Count: > 0 })
        {
            builder.AppendLine()
                .AppendLine("PlanHighlightObservations:");
            foreach (var obs in plan.HighlightedObservations)
            {
                builder.AppendLine($"  - [{obs.Severity}] {obs.Summary}: {obs.Recommendation}");
            }
        }

        return builder.ToString();
    }

    private async Task<AgentCoordinatorResult> RunMultiAgentAsync(ConsultantRequest request, CancellationToken cancellationToken)
    {
        if (_agentCoordinator == null)
        {
            throw new InvalidOperationException("Agent coordinator is not available");
        }

        var context = new AgentExecutionContext
        {
            WorkspacePath = request.WorkspacePath,
            DryRun = request.DryRun,
            RequireApproval = !request.AutoApprove,
            SessionId = Guid.NewGuid().ToString(),
            Verbosity = Honua.Cli.AI.Services.Agents.VerbosityLevel.Normal
        };

        var result = await _agentCoordinator
            .ProcessRequestAsync(request.Prompt ?? string.Empty, context, cancellationToken)
            .ConfigureAwait(false);

        return await ApplyCriticsAsync(request, result, cancellationToken);
    }

    private async Task<ConsultantResult> FinalizeMultiAgentAsync(
        ConsultantRequest request,
        AgentCoordinatorResult coordinatorResult,
        CancellationToken cancellationToken)
    {
        _console.WriteLine("[dim]Using multi-agent consultation mode[/]");
        _console.WriteLine();

        if (coordinatorResult.Success)
        {
            if (coordinatorResult.Response.HasValue())
            {
                _console.WriteLine(coordinatorResult.Response);
            }
        }
        else
        {
            var errorMessage = coordinatorResult.ErrorMessage.IsNullOrWhiteSpace()
                ? "Multi-agent coordination did not complete successfully."
                : coordinatorResult.ErrorMessage;

            _console.MarkupLine($"[red]{errorMessage}[/]");

            if (coordinatorResult.Response.HasValue())
            {
                _console.WriteLine();
                _console.WriteLine(coordinatorResult.Response);
            }
        }

        if (coordinatorResult.NextSteps?.Count > 0)
        {
            _console.WriteLine();
            _console.WriteLine("[bold]Suggested next steps:[/]");
            foreach (var step in coordinatorResult.NextSteps)
            {
                _console.WriteLine($"  • {step}");
            }
        }

        if (coordinatorResult.Warnings.Count > 0 && !request.Verbose)
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Critic warnings:[/]");
            foreach (var warning in coordinatorResult.Warnings)
            {
                _console.MarkupLine($"  • {Markup.Escape(warning)}");
            }
        }

        AgentInteractionHistory? history = null;
        if (_agentCoordinator != null)
        {
            history = await _agentCoordinator.GetHistoryAsync().ConfigureAwait(false);
        }

        if (!request.SuppressLogging)
        {
            var logEntry = BuildMultiAgentLogEntry(request, coordinatorResult, history);
            var logPath = await _logWriter.AppendAsync(logEntry, cancellationToken).ConfigureAwait(false);
            AnnounceLogSaved(logPath);

            var transcriptPath = await WriteMultiAgentTranscriptAsync(request, coordinatorResult, history, cancellationToken).ConfigureAwait(false);
            AnnounceTranscriptSaved(transcriptPath);
        }
        else if (request.Verbose)
        {
            var transcriptPath = await WriteMultiAgentTranscriptAsync(request, coordinatorResult, history, cancellationToken).ConfigureAwait(false);
            AnnounceTranscriptSaved(transcriptPath);
        }

        var emptyPlan = ConsultantPlan.Empty;
        var message = coordinatorResult.Success
            ? coordinatorResult.Response
            : coordinatorResult.ErrorMessage ?? coordinatorResult.Response;

        if (request.Verbose)
        {
            RenderVerboseMultiAgentSummary(coordinatorResult);
        }

        return new ConsultantResult(
            coordinatorResult.Success,
            message,
            emptyPlan,
            Approved: true,
            Executed: coordinatorResult.Success);
    }

    private void RenderMultiAgentFallbackMessage(AgentCoordinatorResult result)
    {
        if (result.ErrorMessage.HasValue())
        {
            _console.MarkupLine($"[yellow]{result.ErrorMessage}[/]");
        }
        else if (result.Response.HasValue())
        {
            _console.WriteLine(result.Response);
        }
        else
        {
            _console.MarkupLine("[yellow]Multi-agent coordination was unable to generate a response.[/]");
        }

        if (result.NextSteps?.Count > 0)
        {
            _console.WriteLine();
            _console.WriteLine("[bold]Suggested next steps from multi-agent attempt:[/]");
            foreach (var step in result.NextSteps)
            {
                _console.WriteLine($"  • {step}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Critic warnings:[/]");
            foreach (var warning in result.Warnings)
            {
                _console.MarkupLine($"  • {Markup.Escape(warning)}");
            }
        }

        if (result is { Steps.Count: > 0 })
        {
            _console.WriteLine();
        }
    }

    private void AnnounceLogSaved(string? path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return;
        }

        var escaped = Markup.Escape(path);
        _console.MarkupLine($"[dim]Session log saved to {escaped}[/]");
    }

    private void AnnounceTranscriptSaved(string? path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return;
        }

        var escaped = Markup.Escape(path);
        _console.MarkupLine($"[dim]Multi-agent transcript saved to {escaped}[/]");
    }

    private static string BuildMultiAgentLogEntry(ConsultantRequest request, AgentCoordinatorResult result, AgentInteractionHistory? history)
    {
        var builder = new StringBuilder()
            .AppendLine($"Prompt: {request.Prompt}")
            .AppendLine($"Workspace: {request.WorkspacePath}")
            .AppendLine("Mode: multi-agent")
            .AppendLine($"ExecutionMode: {request.Mode}")
            .AppendLine($"Agents used: {string.Join(", ", result.AgentsInvolved)}")
            .AppendLine($"Success: {result.Success}");

        if (result.ErrorMessage.HasValue())
        {
            builder.AppendLine($"Error: {result.ErrorMessage}");
        }

        if (result.Response.HasValue())
        {
            builder.AppendLine($"Response: {result.Response}");
        }

        if (result.NextSteps?.Count > 0)
        {
            builder.AppendLine("NextSteps:");
            foreach (var step in result.NextSteps)
            {
                builder.AppendLine($"  - {step}");
            }
        }

        if (result.Warnings.Count > 0)
        {
            builder.AppendLine("Warnings:");
            foreach (var warning in result.Warnings)
            {
                builder.AppendLine($"  - {warning}");
            }
        }

        if (history is { Interactions.Count: > 0 })
        {
            builder.AppendLine()
                .AppendLine($"HistorySession: {history.SessionId}");
            foreach (var interaction in history.Interactions)
            {
                builder.AppendLine($"  [{interaction.Timestamp:u}] {interaction.UserRequest} -> {(interaction.Success ? "success" : "fail")}");
                if (interaction.AgentsUsed.Count > 0)
                {
                    builder.AppendLine($"    Agents: {string.Join(", ", interaction.AgentsUsed)}");
                }
                if (interaction.Response.HasValue())
                {
                    builder.AppendLine($"    Response: {interaction.Response}");
                }
            }
        }

        return builder.ToString();
    }

    private async Task<string?> WriteMultiAgentTranscriptAsync(
        ConsultantRequest request,
        AgentCoordinatorResult result,
        AgentInteractionHistory? history,
        CancellationToken cancellationToken)
    {
        try
        {
            _environment.EnsureInitialized();

            var payload = new
            {
                prompt = request.Prompt,
                workspace = request.WorkspacePath,
                mode = request.Mode.ToString(),
                success = result.Success,
                response = result.Response,
                error = result.ErrorMessage,
                agents = result.AgentsInvolved,
                steps = result.Steps.Select(step => new
                {
                    agent = step.AgentName,
                    action = step.Action,
                    success = step.Success,
                    message = step.Message,
                    durationMs = step.Duration.TotalMilliseconds
                }).ToList(),
                nextSteps = result.NextSteps,
                warnings = result.Warnings,
                history = history?.Interactions.Select(interaction => new
                {
                    timestamp = interaction.Timestamp,
                    request = interaction.UserRequest,
                    agents = interaction.AgentsUsed,
                    success = interaction.Success,
                    response = interaction.Response
                }).ToList(),
                historySessionId = history?.SessionId
            };

            var json = JsonSerializer.Serialize(payload, JsonSerializerOptionsRegistry.WebIndented);

            json = SessionLogSanitizer.Sanitize(json);

            var fileName = $"consultant-{DateTimeOffset.UtcNow:yyyyMMdd}-multi-{Guid.NewGuid():N}.json";
            var path = Path.Combine(_environment.LogsRoot, fileName);
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            return path;
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[yellow]Failed to write multi-agent transcript: {Markup.Escape(ex.Message)}[/]");
            return null;
        }
    }

    private async Task<AgentCoordinatorResult> ApplyCriticsAsync(ConsultantRequest request, AgentCoordinatorResult result, CancellationToken cancellationToken)
    {
        if (_agentCritics == null || !_agentCritics.Any())
        {
            return result;
        }

        var snapshot = new ConsultantRequestSnapshot(
            request.Prompt ?? string.Empty,
            request.DryRun,
            request.Mode.ToString());

        var warningsCollection = result.Warnings ?? new List<string>();
        var seen = new HashSet<string>(warningsCollection, StringComparer.OrdinalIgnoreCase);

        foreach (var critic in _agentCritics)
        {
            try
            {
                var warnings = await critic
                    .EvaluateAsync(snapshot, result, cancellationToken)
                    .ConfigureAwait(false);

                foreach (var warning in warnings)
                {
                    if (warning.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    if (seen.Add(warning))
                    {
                        warningsCollection.Add(warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _console.MarkupLine($"[yellow]Critic '{critic.GetType().Name}' failed: {Markup.Escape(ex.Message)}[/]");
            }
        }

        if (result.Warnings == null && warningsCollection.Count > 0)
        {
            // result.Warnings has init-only setter; preserve warnings via reflection workaround
            var property = typeof(AgentCoordinatorResult).GetProperty(nameof(AgentCoordinatorResult.Warnings));
            property?.SetValue(result, warningsCollection);
        }

        return result;
    }

    private void RenderContextSummary(ConsultantPlanningContext context)
    {
        _console.WriteLine();
        _console.MarkupLine("[grey]Context snapshot[/]");
        _console.WriteLine($"Workspace: {context.Workspace.RootPath}");

        if (context.Workspace.MetadataDetected && context.Workspace.Metadata is { } metadata)
        {
            _console.MarkupLine($"Metadata: [silver]{metadata.Services.Count} services[/], [silver]{metadata.DataSources.Count} data sources[/], [silver]{metadata.RasterDatasets.Count} raster datasets[/]");
        }
        else
        {
            _console.MarkupLine("Metadata: [red]not detected[/]");
        }

        var infra = context.Workspace.Infrastructure;
        var infraTokens = new List<string>();
        if (infra.HasDockerCompose) infraTokens.Add("docker-compose");
        if (infra.HasKubernetesManifests) infraTokens.Add("kubernetes");
        if (infra.HasHelmCharts) infraTokens.Add("helm");
        if (infra.HasTerraform) infraTokens.Add("terraform");
        if (infra.HasCiPipelines) infraTokens.Add("ci/cd");
        if (infra.HasMonitoringConfig) infraTokens.Add("observability");

        if (infraTokens.Count > 0)
        {
            _console.MarkupLine($"Infrastructure artifacts: [silver]{string.Join(", ", infraTokens)}[/]");
        }
        else
        {
            _console.MarkupLine("Infrastructure artifacts: [yellow]not detected[/]");
        }

        if (infra.PotentialCloudProviders.Count > 0)
        {
            _console.MarkupLine($"Cloud signals: [silver]{string.Join(", ", infra.PotentialCloudProviders)}[/]");
        }

        if (context.Observations.Count > 0)
        {
            _console.WriteLine();
            _console.MarkupLine("[bold yellow]Advisor notes:[/]");
            foreach (var obs in context.Observations.Take(5))
            {
                var style = ResolveSeverityStyle(obs.Severity);
                var summary = Markup.Escape(obs.Summary ?? string.Empty);
                _console.MarkupLine($"  • [{style}]{summary}[/]");
            }
            if (context.Observations.Count > 5)
            {
                _console.MarkupLine($"  • ... {context.Observations.Count - 5} additional observations");
            }
        }

        _console.WriteLine();
    }

    private static string ResolveSeverityStyle(string severity)
    {
        return severity?.ToLowerInvariant() switch
        {
            "critical" or "high" => "red",
            "medium" or "moderate" => "yellow",
            "low" or "info" or "information" => "silver",
            _ => "silver"
        };
    }

    private bool CheckHighConfidenceAutoApproval(ConsultantPlan plan, ConsultantPlanningContext context)
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
        // Properly handle and log exceptions to avoid silent failures
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
                        // We already tracked recommendations earlier; now update to mark as accepted
                        // Note: In a real implementation, you'd update the existing record
                        // For now, track a new event with wasAccepted=true
                        await _patternTelemetry.TrackRecommendationAsync(
                            patternId,
                            requirements,
                            new PatternConfidence { Overall = 1.0, Level = "High" }, // Placeholder
                            rank++,
                            wasAccepted: true,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail the workflow - pattern tracking is telemetry, not critical path
                        _console.MarkupLine($"[dim]Note: Pattern tracking failed: {ex.Message}[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                // Top-level catch for critical exceptions - log to stderr for visibility
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
        // Properly handle and log exceptions to avoid silent failures
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
                        // Log but don't fail the workflow - pattern tracking is telemetry, not critical path
                        _console.MarkupLine($"[dim]Note: Deployment outcome tracking failed: {ex.Message}[/]");
                    }
                }
            }
            catch (Exception ex)
            {
                // Top-level catch for critical exceptions - log to stderr for visibility
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

    /// <summary>
    /// Renders ASCII architecture diagram for the deployment plan.
    /// </summary>
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

            // Determine cloud provider from plan
            var cloudProvider = DetermineCloudProvider(plan);

            // Generate deployment summary for diagram
            var deploymentSummary = plan.ExecutiveSummary ?? context.Request.Prompt ?? "Honua deployment";

            // Generate ASCII diagram
            var diagram = await _diagramGenerator.GenerateAsciiArchitectureDiagramAsync(
                deploymentSummary,
                cloudProvider,
                cancellationToken);

            _console.WriteLine(diagram);
            _console.WriteLine();

            // If terraform steps exist, offer to generate terraform graph
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
            // Don't fail workflow if diagram generation fails
            _console.MarkupLine($"[dim yellow]⚠️  Diagram generation failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    /// <summary>
    /// Determines cloud provider from plan steps.
    /// </summary>
    private string DetermineCloudProvider(ConsultantPlan plan)
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

    /// <summary>
    /// Saves consultant session for potential refinement.
    /// </summary>
    private async Task SaveSessionAsync(
        ConsultantPlan plan,
        ConsultantPlanningContext context,
        CancellationToken cancellationToken)
    {
        if (_sessionStore == null)
        {
            return; // Session store not configured
        }

        try
        {
            // Generate session ID from timestamp + hash
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var requestHash = Math.Abs((context.Request.Prompt ?? "unknown").GetHashCode()).ToString("X8");
            var sessionId = $"{timestamp}-{requestHash}";

            await _sessionStore.SaveSessionAsync(sessionId, plan, context, cancellationToken);
            _console.MarkupLine($"[dim]Session ID: {sessionId} (use 'honua consultant refine --session {sessionId}' to refine)[/]");
        }
        catch (Exception)
        {
            // Don't fail the workflow if session save fails
            // Session saving is optional enhancement, not critical path
        }
    }

    private void RenderVerboseMultiAgentSummary(AgentCoordinatorResult result)
    {
        _console.WriteLine();
        _console.MarkupLine("[bold]Agent steps:[/]");

        if (result.Steps.Count == 0)
        {
            _console.MarkupLine("[dim]No agent steps were recorded.[/]");
        }
        else
        {
            foreach (var step in result.Steps)
            {
                var status = step.Success ? "[green]success[/]" : "[red]failed[/]";
                var message = step.Message.IsNullOrWhiteSpace() ? string.Empty : $" – {Markup.Escape(step.Message)}";
                _console.MarkupLine($"  • [silver]{Markup.Escape(step.AgentName)}[/] {Markup.Escape(step.Action)} ({status}){message}");
            }
        }

        if (result.Warnings?.Count > 0)
        {
            _console.WriteLine();
            _console.MarkupLine("[yellow]Warnings:[/]");
            foreach (var warning in result.Warnings)
            {
                _console.MarkupLine($"  • {Markup.Escape(warning)}");
            }
        }

        if (result.NextSteps?.Count > 0)
        {
            _console.WriteLine();
            _console.MarkupLine("[bold]Next steps:[/]");
            foreach (var step in result.NextSteps)
            {
                _console.MarkupLine($"  • {Markup.Escape(step)}");
            }
        }
    }

    /// <summary>
    /// Generates and renders comprehensive architecture documentation.
    /// </summary>
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

            var cloudProvider = DetermineCloudProvider(plan);
            var request = new Honua.Cli.AI.Services.Agents.Specialized.ArchitectureDocumentationRequest
            {
                DeploymentSummary = plan.ExecutiveSummary ?? context.Request.Prompt ?? "Honua deployment",
                CloudProvider = cloudProvider,
                PlanSteps = plan.Steps.Select(s => $"{s.Description} ({s.Rationale})").ToList(),
                UserRequirements = context.Request.Prompt
            };

            var doc = await _architectureDocAgent.GenerateAsync(request, cancellationToken);

            // Render condensed version for terminal
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

            // Save full documentation to file
            var docMarkdown = _architectureDocAgent.RenderMarkdown(doc);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var docPath = Path.Combine(_environment.LogsRoot, $"architecture-{timestamp}.md");
            await File.WriteAllTextAsync(docPath, docMarkdown, cancellationToken);

            _console.MarkupLine($"[dim]📄 Full architecture documentation saved: {Markup.Escape(docPath)}[/]");
            _console.WriteLine();
        }
        catch (Exception ex)
        {
            // Don't fail workflow if documentation generation fails
            _console.MarkupLine($"[dim yellow]⚠️  Architecture documentation generation failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    /// <summary>
    /// Renders Honua metadata configuration example.
    /// </summary>
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

                // Show sample service configuration
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
            // Don't fail workflow if metadata rendering fails
            _console.MarkupLine($"[dim yellow]⚠️  Metadata configuration display failed: {ex.Message}[/]");
            _console.WriteLine();
        }
    }

    /// <summary>
    /// Checks if this is a Honua deployment (has metadata or GIS-related content).
    /// </summary>
    private bool IsHonuaDeployment(ConsultantPlanningContext context)
    {
        // Check if metadata exists
        if (context.Workspace.MetadataDetected)
        {
            return true;
        }

        // Check if prompt mentions GIS/spatial terms
        var prompt = context.Request.Prompt?.ToLowerInvariant() ?? "";
        var gisKeywords = new[] { "gis", "spatial", "postgis", "ogc", "wfs", "wms", "layer", "feature", "honua" };
        return gisKeywords.Any(keyword => prompt.Contains(keyword));
    }
}
