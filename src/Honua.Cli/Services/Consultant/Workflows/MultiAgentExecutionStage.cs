// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Spectre.Console;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Handles multi-agent coordination and execution.
/// </summary>
public sealed class MultiAgentExecutionStage : IWorkflowStage<WorkflowContext, WorkflowContext>
{
    private readonly IAnsiConsole _console;
    private readonly IAgentCoordinator? _agentCoordinator;
    private readonly IReadOnlyList<IAgentCritic> _agentCritics;

    public MultiAgentExecutionStage(
        IAnsiConsole console,
        IAgentCoordinator? agentCoordinator = null,
        IEnumerable<IAgentCritic>? agentCritics = null)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _agentCoordinator = agentCoordinator;
        _agentCritics = (agentCritics ?? Array.Empty<IAgentCritic>()).ToList();
    }

    public async Task<WorkflowContext> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;

        // Check if multi-agent mode is requested
        if (_agentCoordinator == null)
        {
            if (request.Mode == ConsultantExecutionMode.MultiAgent)
            {
                var message = "Multi-agent mode requested, but the agent coordinator is not configured.";
                _console.MarkupLine($"[red]{message}[/]");
                context.IsMultiAgentMode = false;
                return context;
            }
            context.IsMultiAgentMode = false;
            return context;
        }

        if (request.Mode != ConsultantExecutionMode.MultiAgent && request.Mode != ConsultantExecutionMode.Auto)
        {
            context.IsMultiAgentMode = false;
            return context;
        }

        // Execute multi-agent coordination
        var coordinationResult = await RunMultiAgentAsync(request, cancellationToken).ConfigureAwait(false);

        if (coordinationResult.Success || request.Mode == ConsultantExecutionMode.MultiAgent)
        {
            context.IsMultiAgentMode = true;
            context.MultiAgentResult = coordinationResult;
            return context;
        }

        // Multi-agent failed, fall back to plan-based mode
        RenderMultiAgentFallbackMessage(coordinationResult);
        if (request.Verbose)
        {
            RenderVerboseMultiAgentSummary(coordinationResult);
        }
        _console.WriteLine();
        _console.MarkupLine("[yellow]Falling back to plan-based consultant workflow...[/]");
        _console.WriteLine();

        context.IsMultiAgentMode = false;
        context.MultiAgentResult = coordinationResult;
        return context;
    }

    private async Task<AgentCoordinatorResult> RunMultiAgentAsync(ConsultantRequest request, CancellationToken cancellationToken)
    {
        if (_agentCoordinator == null)
        {
            throw new InvalidOperationException("Agent coordinator is not available");
        }

        var executionContext = new AgentExecutionContext
        {
            WorkspacePath = request.WorkspacePath,
            DryRun = request.DryRun,
            RequireApproval = !request.AutoApprove,
            SessionId = Guid.NewGuid().ToString(),
            Verbosity = VerbosityLevel.Normal
        };

        var result = await _agentCoordinator
            .ProcessRequestAsync(request.Prompt ?? string.Empty, executionContext, cancellationToken)
            .ConfigureAwait(false);

        return await ApplyCriticsAsync(request, result, cancellationToken);
    }

    private async Task<AgentCoordinatorResult> ApplyCriticsAsync(ConsultantRequest request, AgentCoordinatorResult result, CancellationToken cancellationToken)
    {
        if (_agentCritics.Count == 0)
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
                    if (string.IsNullOrWhiteSpace(warning))
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

    private void RenderMultiAgentFallbackMessage(AgentCoordinatorResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            _console.MarkupLine($"[yellow]{result.ErrorMessage}[/]");
        }
        else if (!string.IsNullOrWhiteSpace(result.Response))
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
                var message = string.IsNullOrWhiteSpace(step.Message) ? string.Empty : $" – {Markup.Escape(step.Message)}";
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
