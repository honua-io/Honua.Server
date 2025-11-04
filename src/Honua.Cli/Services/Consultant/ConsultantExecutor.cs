// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Spectre.Console;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Executes consultant plans by invoking plugin functions through Semantic Kernel.
/// </summary>
public sealed class ConsultantExecutor : IConsultantExecutor
{
    private readonly Kernel _kernel;
    private readonly IAnsiConsole _console;

    public ConsultantExecutor(Kernel kernel, IAnsiConsole console)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    public async Task<ExecutionResult> ExecuteAsync(ConsultantPlan plan, CancellationToken cancellationToken)
    {
        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        var results = new List<StepExecutionResult>();
        var stepIndex = 1;

        _console.WriteLine();
        _console.Write(new Rule("[bold green]Executing Plan[/]").RuleStyle("green dim"));
        _console.WriteLine();

        foreach (var step in plan.Steps)
        {
            _console.MarkupLine($"[cyan]Step {stepIndex}:[/] {step.Skill}.{step.Action}");

            if (step.Category.HasValue())
            {
                _console.MarkupLine($"  [dim]Category:[/] {step.Category}");
            }

            if (step.Rationale.HasValue())
            {
                _console.MarkupLine($"  [dim]Rationale:[/] {Markup.Escape(step.Rationale)}");
            }

            if (step.Dependencies is { Count: > 0 })
            {
                _console.MarkupLine($"  [dim]Depends on:[/] {string.Join(", ", step.Dependencies)}");
            }

            var result = await ExecuteStepAsync(stepIndex, step, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (result.Success)
            {
                _console.MarkupLine($"  [green]✓[/] {step.Description ?? "Completed"}");

                if (step.SuccessCriteria.HasValue())
                {
                    _console.MarkupLine($"  [dim]Success criteria:[/] {Markup.Escape(step.SuccessCriteria)}");
                }

                if (step.Risk.HasValue())
                {
                    _console.MarkupLine($"  [dim]Risk awareness:[/] {Markup.Escape(step.Risk)}");
                }

                // Display output summary if available
                if (result.Output.HasValue())
                {
                    var preview = result.Output.Length > 100
                        ? result.Output.Substring(0, 100) + "..."
                        : result.Output;
                    _console.MarkupLine($"  [dim]{Markup.Escape(preview)}[/]");
                }
            }
            else
            {
                _console.MarkupLine($"  [red]✗[/] Failed: {Markup.Escape(result.Error ?? "Unknown error")}");

                // Stop execution on first failure
                var message = $"Execution stopped at step {stepIndex} due to failure.";
                _console.WriteLine();
                _console.MarkupLine($"[yellow]{message}[/]");

                return new ExecutionResult(false, message, results);
            }

            _console.WriteLine();
            stepIndex++;
        }

        var successMessage = $"Successfully executed all {results.Count} steps.";
        _console.Write(new Rule($"[bold green]{successMessage}[/]").RuleStyle("green"));

        return new ExecutionResult(true, successMessage, results);
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        int stepIndex,
        ConsultantPlanStep step,
        CancellationToken cancellationToken)
    {
        try
        {
            // Find the plugin
            var plugin = _kernel.Plugins.FirstOrDefault(p =>
                p.Name.Equals(step.Skill, StringComparison.OrdinalIgnoreCase));

            if (plugin is null)
            {
                return new StepExecutionResult(
                    stepIndex,
                    step.Skill,
                    step.Action,
                    false,
                    null,
                    $"Plugin '{step.Skill}' not found");
            }

            // Find the function
            var function = plugin.FirstOrDefault(f =>
                f.Name.Equals(step.Action, StringComparison.OrdinalIgnoreCase));

            if (function is null)
            {
                return new StepExecutionResult(
                    stepIndex,
                    step.Skill,
                    step.Action,
                    false,
                    null,
                    $"Function '{step.Action}' not found in plugin '{step.Skill}'");
            }

            // Build arguments from step inputs
            var arguments = new KernelArguments();
            foreach (var input in step.Inputs)
            {
                arguments[input.Key] = input.Value;
            }

            // Invoke the function
            var result = await _kernel.InvokeAsync(function, arguments, cancellationToken)
                .ConfigureAwait(false);

            var output = result.GetValue<string>() ?? string.Empty;

            return new StepExecutionResult(
                stepIndex,
                step.Skill,
                step.Action,
                true,
                output,
                null);
        }
        catch (Exception ex)
        {
            return new StepExecutionResult(
                stepIndex,
                step.Skill,
                step.Action,
                false,
                null,
                ex.Message);
        }
    }
}
