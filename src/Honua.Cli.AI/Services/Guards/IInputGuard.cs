// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Guards;

/// <summary>
/// Guards against malicious or unsafe user input before it reaches LLM or agents.
/// </summary>
public interface IInputGuard
{
    /// <summary>
    /// Validates user input for safety concerns (prompt injection, jailbreaks, malicious content).
    /// </summary>
    /// <param name="userInput">The user's raw input</param>
    /// <param name="context">Optional context about where this input is used</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Guard result with safety assessment</returns>
    Task<InputGuardResult> ValidateInputAsync(
        string userInput,
        string? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of input guard validation.
/// </summary>
public sealed class InputGuardResult
{
    /// <summary>
    /// Whether the input is safe to process.
    /// </summary>
    public required bool IsSafe { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) that the input is safe.
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Detected threats (if any).
    /// </summary>
    public required string[] DetectedThreats { get; init; }

    /// <summary>
    /// Sanitized version of the input (if threats were neutralized).
    /// </summary>
    public string? SanitizedInput { get; init; }

    /// <summary>
    /// Explanation of why input was flagged (for logging/debugging).
    /// </summary>
    public string? Explanation { get; init; }
}
