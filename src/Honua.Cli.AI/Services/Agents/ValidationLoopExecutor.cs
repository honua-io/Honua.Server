// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Executes deployment tasks with validation loops and automatic retry/remediation.
/// Implements the Loop pattern for iterative refinement.
/// </summary>
public sealed class ValidationLoopExecutor
{
    private readonly ILogger<ValidationLoopExecutor> _logger;
    private const int MaxRetries = 3;

    public ValidationLoopExecutor(ILogger<ValidationLoopExecutor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an agent with validation loop - retries with remediation on failure.
    /// </summary>
    public async Task<LoopExecutionResult> ExecuteWithValidationAsync(
        Func<CancellationToken, Task<AgentStepResult>> executeAction,
        Func<AgentStepResult, CancellationToken, Task<ValidationResult>> validateAction,
        Func<ValidationResult, CancellationToken, Task<RemediationResult>> remediateAction,
        string actionName,
        CancellationToken cancellationToken)
    {
        var iterations = new List<LoopIteration>();
        var sw = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            var iteration = new LoopIteration
            {
                Attempt = attempt,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Loop iteration {Attempt}/{Max} for {Action}", attempt, MaxRetries, actionName);

                // Step 1: Execute the action
                iteration.ExecutionResult = await executeAction(cancellationToken);

                if (!iteration.ExecutionResult.Success)
                {
                    _logger.LogWarning("Execution failed on attempt {Attempt}: {Message}",
                        attempt, iteration.ExecutionResult.Message);
                    iteration.Phase = LoopPhase.ExecutionFailed;
                    iterations.Add(iteration);

                    // If execution itself failed, try remediation
                    if (attempt < MaxRetries)
                    {
                        var executionValidation = new ValidationResult
                        {
                            Passed = false,
                            Failures = new List<ValidationFailure>
                            {
                                new()
                                {
                                    Category = "Execution",
                                    Message = iteration.ExecutionResult.Message,
                                    Severity = "high"
                                }
                            }
                        };
                        iteration.RemediationResult = await remediateAction(executionValidation, cancellationToken);
                        continue;
                    }
                    break;
                }

                // Step 2: Validate the result
                iteration.ValidationResult = await validateAction(iteration.ExecutionResult, cancellationToken);

                if (iteration.ValidationResult.Passed)
                {
                    _logger.LogInformation("Validation passed on attempt {Attempt} for {Action}", attempt, actionName);
                    iteration.Phase = LoopPhase.Success;
                    iterations.Add(iteration);
                    sw.Stop();

                    return new LoopExecutionResult
                    {
                        Success = true,
                        TotalIterations = attempt,
                        FinalResult = iteration.ExecutionResult,
                        Iterations = iterations,
                        TotalDuration = sw.Elapsed,
                        Message = $"Action completed successfully after {attempt} iteration(s)"
                    };
                }

                _logger.LogWarning("Validation failed on attempt {Attempt}: {FailureCount} issues found",
                    attempt, iteration.ValidationResult.Failures.Count);

                iteration.Phase = LoopPhase.ValidationFailed;

                // Step 3: Attempt remediation if not the last attempt
                if (attempt < MaxRetries)
                {
                    _logger.LogInformation("Attempting remediation for {Action}", actionName);
                    iteration.RemediationResult = await remediateAction(iteration.ValidationResult, cancellationToken);

                    if (!iteration.RemediationResult.CanRetry)
                    {
                        _logger.LogError("Remediation determined retry is not possible");
                        iteration.Phase = LoopPhase.RemediationFailed;
                        iterations.Add(iteration);
                        break;
                    }

                    _logger.LogInformation("Remediation applied: {Action}", iteration.RemediationResult.ActionTaken);
                }

                iterations.Add(iteration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during loop iteration {Attempt} for {Action}", attempt, actionName);
                iteration.Phase = LoopPhase.Exception;
                iteration.Exception = ex;
                iterations.Add(iteration);

                if (attempt == MaxRetries)
                {
                    break;
                }
            }
        }

        sw.Stop();

        // All retries exhausted
        var lastIteration = iterations[^1];
        var failureMessage = lastIteration.Phase switch
        {
            LoopPhase.ExecutionFailed => $"Execution failed: {lastIteration.ExecutionResult?.Message}",
            LoopPhase.ValidationFailed => $"Validation failed after {MaxRetries} attempts: {lastIteration.ValidationResult?.Failures.Count} issues remaining",
            LoopPhase.RemediationFailed => "Remediation determined retry is not possible",
            LoopPhase.Exception => $"Exception occurred: {lastIteration.Exception?.Message}",
            _ => "Unknown failure"
        };

        return new LoopExecutionResult
        {
            Success = false,
            TotalIterations = iterations.Count,
            FinalResult = lastIteration.ExecutionResult,
            Iterations = iterations,
            TotalDuration = sw.Elapsed,
            Message = $"Action failed after {iterations.Count} iteration(s): {failureMessage}"
        };
    }

    /// <summary>
    /// Executes with simple retry (no validation, just retry on failure).
    /// </summary>
    public async Task<LoopExecutionResult> ExecuteWithRetryAsync(
        Func<CancellationToken, Task<AgentStepResult>> executeAction,
        string actionName,
        int maxAttempts = MaxRetries,
        CancellationToken cancellationToken = default)
    {
        var iterations = new List<LoopIteration>();
        var sw = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var iteration = new LoopIteration
            {
                Attempt = attempt,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Retry attempt {Attempt}/{Max} for {Action}", attempt, maxAttempts, actionName);

                iteration.ExecutionResult = await executeAction(cancellationToken);

                if (iteration.ExecutionResult.Success)
                {
                    iteration.Phase = LoopPhase.Success;
                    iterations.Add(iteration);
                    sw.Stop();

                    return new LoopExecutionResult
                    {
                        Success = true,
                        TotalIterations = attempt,
                        FinalResult = iteration.ExecutionResult,
                        Iterations = iterations,
                        TotalDuration = sw.Elapsed,
                        Message = $"Action succeeded on attempt {attempt}"
                    };
                }

                _logger.LogWarning("Attempt {Attempt} failed: {Message}", attempt, iteration.ExecutionResult.Message);
                iteration.Phase = LoopPhase.ExecutionFailed;
                iterations.Add(iteration);

                // Exponential backoff between retries
                if (attempt < maxAttempts)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogInformation("Waiting {Delay}s before retry", delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during attempt {Attempt} for {Action}", attempt, actionName);
                iteration.Phase = LoopPhase.Exception;
                iteration.Exception = ex;
                iterations.Add(iteration);

                if (attempt == maxAttempts)
                {
                    break;
                }

                // Backoff on exception too
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
                }
            }
        }

        sw.Stop();

        return new LoopExecutionResult
        {
            Success = false,
            TotalIterations = iterations.Count,
            FinalResult = iterations[^1].ExecutionResult,
            Iterations = iterations,
            TotalDuration = sw.Elapsed,
            Message = $"Action failed after {iterations.Count} attempt(s)"
        };
    }
}

/// <summary>
/// Result of loop execution with all iterations.
/// </summary>
public sealed class LoopExecutionResult
{
    public bool Success { get; init; }
    public int TotalIterations { get; init; }
    public AgentStepResult? FinalResult { get; init; }
    public List<LoopIteration> Iterations { get; init; } = new();
    public TimeSpan TotalDuration { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Single iteration of the validation loop.
/// </summary>
public sealed class LoopIteration
{
    public int Attempt { get; init; }
    public DateTime StartedAt { get; init; }
    public LoopPhase Phase { get; set; }
    public AgentStepResult? ExecutionResult { get; set; }
    public ValidationResult? ValidationResult { get; set; }
    public RemediationResult? RemediationResult { get; set; }
    public Exception? Exception { get; set; }
}

/// <summary>
/// Phase of the loop iteration.
/// </summary>
public enum LoopPhase
{
    Success,
    ExecutionFailed,
    ValidationFailed,
    RemediationFailed,
    Exception
}

/// <summary>
/// Result of validation check.
/// </summary>
public sealed class ValidationResult
{
    public bool Passed { get; init; }
    public List<ValidationFailure> Failures { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Individual validation failure.
/// </summary>
public sealed class ValidationFailure
{
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = "medium";
    public string? Suggestion { get; init; }
}

/// <summary>
/// Result of remediation attempt.
/// </summary>
public sealed class RemediationResult
{
    public bool CanRetry { get; init; }
    public string ActionTaken { get; init; } = string.Empty;
    public string Reasoning { get; init; } = string.Empty;
    public List<string> ChangesApplied { get; init; } = new();
}
