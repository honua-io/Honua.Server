// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Handles input validation, normalization, and context building.
/// </summary>
public sealed class InputProcessingStage : IWorkflowStage<WorkflowContext, WorkflowContext>
{
    private readonly IAnsiConsole _console;
    private readonly IHonuaCliEnvironment _environment;
    private readonly IConsultantContextBuilder _contextBuilder;

    public InputProcessingStage(
        IAnsiConsole console,
        IHonuaCliEnvironment environment,
        IConsultantContextBuilder contextBuilder)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
    }

    public async Task<WorkflowContext> ExecuteAsync(WorkflowContext context, CancellationToken cancellationToken)
    {
        _environment.EnsureInitialized();

        var workspacePath = _environment.ResolveWorkspacePath(context.Request.WorkspacePath);
        var normalizedRequest = context.Request with { WorkspacePath = workspacePath };

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

        context.PlanningContext = planningContext;
        return new WorkflowContext
        {
            Request = normalizedRequest,
            PlanningContext = planningContext,
            Plan = context.Plan,
            ExecutionResult = context.ExecutionResult,
            SessionId = context.SessionId,
            IsMultiAgentMode = context.IsMultiAgentMode,
            MultiAgentResult = context.MultiAgentResult
        };
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
}
