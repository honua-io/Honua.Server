// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.Services;
using Honua.Server.Core.Performance;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Handles output formatting and result finalization for both multi-agent and plan-based workflows.
/// </summary>
public sealed class OutputFormattingStage : IWorkflowStage<WorkflowContext, ConsultantResult>
{
    private readonly IAnsiConsole _console;
    private readonly ISessionLogWriter _logWriter;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IAgentCoordinator? _agentCoordinator;

    public OutputFormattingStage(
        IAnsiConsole console,
        ISessionLogWriter logWriter,
        IHonuaCliEnvironment environment,
        IAgentCoordinator? agentCoordinator = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logWriter = logWriter ?? throw new ArgumentNullException(nameof(logWriter));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _agentCoordinator = agentCoordinator;
    }

    public async Task<ConsultantResult> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        if (context.IsMultiAgentMode && context.MultiAgentResult != null)
        {
            return await FinalizeMultiAgentResultAsync(context, cancellationToken);
        }

        return await FinalizePlanBasedResultAsync(context, cancellationToken);
    }

    private async Task<ConsultantResult> FinalizeMultiAgentResultAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var coordinatorResult = context.MultiAgentResult!;

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

    private async Task<ConsultantResult> FinalizePlanBasedResultAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var plan = context.Plan ?? ConsultantPlan.Empty;
        var planningContext = context.PlanningContext;
        var executionResult = context.ExecutionResult;

        if (planningContext == null)
        {
            return new ConsultantResult(false, "Planning context was not initialized", plan, false, false);
        }

        if (plan.Steps.Count == 0)
        {
            return new ConsultantResult(false, "No plan generated for the provided prompt.", plan, false, false);
        }

        // Determine approval and execution status
        var approved = executionResult != null;
        var executed = approved && executionResult!.Success;

        // Write log if not suppressed
        if (!request.SuppressLogging)
        {
            var logPath = await _logWriter
                .AppendAsync(WorkflowLogBuilder.BuildLogEntry(planningContext, plan, approved, executionResult), cancellationToken)
                .ConfigureAwait(false);
            AnnounceLogSaved(logPath);
        }

        if (request.Verbose)
        {
            _console.WriteLine("[dim]Verbose mode: additional execution details available in the session log.[/]");
        }

        // Handle different completion scenarios
        if (request.DryRun)
        {
            return new ConsultantResult(true, "Dry-run plan ready for review.", plan, false, false);
        }

        if (!approved)
        {
            return new ConsultantResult(true, "Plan generated but not approved by operator.", plan, false, false);
        }

        if (executionResult != null)
        {
            if (executionResult.Success)
            {
                return new ConsultantResult(true, executionResult.Message, plan, true, true);
            }
            else
            {
                return new ConsultantResult(false, executionResult.Message, plan, true, false);
            }
        }

        return new ConsultantResult(true, "Plan generated successfully.", plan, approved, executed);
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
        var builder = new System.Text.StringBuilder()
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
}
