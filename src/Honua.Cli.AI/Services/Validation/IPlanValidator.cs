// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Planning;

namespace Honua.Cli.AI.Services.Validation;

/// <summary>
/// Validates execution plans before execution to ensure safety and correctness.
/// </summary>
public interface IPlanValidator
{
    /// <summary>
    /// Validates a plan and returns validation results.
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        ExecutionPlan plan,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of plan validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Whether the plan is valid and can be executed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Blocking errors that prevent execution.
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Non-blocking warnings that should be reviewed.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    /// <summary>
    /// Informational messages.
    /// </summary>
    public List<string> Info { get; init; } = new();

    /// <summary>
    /// Validation checks that were performed.
    /// </summary>
    public List<ValidationCheck> Checks { get; init; } = new();
}

public sealed class ValidationCheck
{
    public required string Name { get; init; }
    public required ValidationCheckResult Result { get; init; }
    public string? Message { get; init; }
}

public enum ValidationCheckResult
{
    Passed,
    Warning,
    Failed
}
