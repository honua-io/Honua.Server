// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant.Workflows;

/// <summary>
/// Builds log entries for consultant workflow execution.
/// </summary>
public static class WorkflowLogBuilder
{
    public static string BuildLogEntry(ConsultantPlanningContext context, ConsultantPlan plan, bool approved)
    {
        return BuildLogEntry(context, plan, approved, null);
    }

    public static string BuildLogEntry(ConsultantPlanningContext context, ConsultantPlan plan, bool approved, ExecutionResult? executionResult)
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
}
