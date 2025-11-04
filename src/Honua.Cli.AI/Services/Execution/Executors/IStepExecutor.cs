// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Planning;

namespace Honua.Cli.AI.Services.Execution.Executors;

/// <summary>
/// Interface for step executors that perform actual operations.
/// Each executor handles a specific type of step (CreateIndex, VacuumAnalyze, etc.).
/// </summary>
public interface IStepExecutor
{
    /// <summary>
    /// The type of step this executor handles.
    /// </summary>
    StepType SupportedStepType { get; }

    /// <summary>
    /// Validates that the step can be executed safely.
    /// </summary>
    Task<StepValidationResult> ValidateAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the step.
    /// </summary>
    Task<StepExecutionResult> ExecuteAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates how long this step will take.
    /// </summary>
    Task<TimeEstimate> EstimateDurationAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of step validation.
/// </summary>
public sealed class StepValidationResult
{
    public required bool IsValid { get; init; }
    public required List<string> Errors { get; init; } = new();
    public required List<string> Warnings { get; init; } = new();
    public bool CanProceed => IsValid && Errors.Count == 0;
}

/// <summary>
/// Result of step execution.
/// </summary>
public sealed class StepExecutionResult
{
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Output { get; init; }
    public TimeSpan Duration { get; init; }
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Time estimate for a step.
/// </summary>
public sealed class TimeEstimate
{
    public required TimeSpan Estimated { get; init; }
    public required TimeSpan Min { get; init; }
    public required TimeSpan Max { get; init; }
    public required double Confidence { get; init; } // 0.0 to 1.0
}
