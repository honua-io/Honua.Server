// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Cli.Services.Consultant;

/// <summary>
/// Result of executing an consultant plan.
/// </summary>
/// <param name="Success">Whether all steps executed successfully.</param>
/// <param name="Message">Overall execution message.</param>
/// <param name="StepResults">Results from each executed step.</param>
public sealed record ExecutionResult(
    bool Success,
    string Message,
    IReadOnlyList<StepExecutionResult> StepResults);

/// <summary>
/// Result of executing a single plan step.
/// </summary>
/// <param name="StepIndex">Index of the step (1-based).</param>
/// <param name="Skill">Skill name that was invoked.</param>
/// <param name="Action">Action name that was invoked.</param>
/// <param name="Success">Whether the step succeeded.</param>
/// <param name="Output">Output from the plugin function (typically JSON guidance).</param>
/// <param name="Error">Error message if the step failed.</param>
public sealed record StepExecutionResult(
    int StepIndex,
    string Skill,
    string Action,
    bool Success,
    string? Output,
    string? Error);
