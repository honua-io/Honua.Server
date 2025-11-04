// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Agents;
using Honua.Cli.AI.Services.Telemetry;
using Honua.Cli.AI.Services.VectorSearch;
using Honua.Cli.Services;
using Honua.Cli.Services.Consultant.Workflows;
using Spectre.Console;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Refactored consultant workflow that orchestrates focused workflow stages.
/// Uses pipeline pattern for clean separation of concerns.
/// </summary>
public sealed class ConsultantWorkflowRefactored : IConsultantWorkflow
{
    private readonly InputProcessingStage _inputProcessing;
    private readonly MultiAgentExecutionStage _multiAgentExecution;
    private readonly PlanBasedExecutionStage _planBasedExecution;
    private readonly OutputFormattingStage _outputFormatting;

    public ConsultantWorkflowRefactored(
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
        if (console == null) throw new ArgumentNullException(nameof(console));
        if (environment == null) throw new ArgumentNullException(nameof(environment));
        if (contextBuilder == null) throw new ArgumentNullException(nameof(contextBuilder));
        if (planner == null) throw new ArgumentNullException(nameof(planner));
        if (planFormatter == null) throw new ArgumentNullException(nameof(planFormatter));
        if (logWriter == null) throw new ArgumentNullException(nameof(logWriter));
        if (executor == null) throw new ArgumentNullException(nameof(executor));

        // Initialize workflow stages
        _inputProcessing = new InputProcessingStage(console, environment, contextBuilder);
        _multiAgentExecution = new MultiAgentExecutionStage(console, agentCoordinator, agentCritics);
        _planBasedExecution = new PlanBasedExecutionStage(
            console,
            planner,
            planFormatter,
            executor,
            environment,
            patternTelemetry,
            sessionStore,
            diagramGenerator,
            architectureDocAgent);
        _outputFormatting = new OutputFormattingStage(console, logWriter, environment, agentCoordinator);
    }

    public async Task<ConsultantResult> ExecuteAsync(ConsultantRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        // Create initial workflow context
        var context = new WorkflowContext
        {
            Request = request
        };

        // Stage 1: Input Processing & Context Building
        context = await _inputProcessing.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

        // Stage 2: Multi-Agent Execution (if enabled)
        context = await _multiAgentExecution.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

        // If multi-agent succeeded or is required, skip plan-based execution
        if (context.IsMultiAgentMode)
        {
            return await _outputFormatting.ExecuteAsync(context, cancellationToken);
        }

        // Stage 3: Plan-Based Execution
        context = await _planBasedExecution.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);

        // Stage 4: Output Formatting & Result Finalization
        return await _outputFormatting.ExecuteAsync(context, cancellationToken);
    }
}
