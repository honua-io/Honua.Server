// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Guards;

/// <summary>
/// Guards against hallucinations, unsafe outputs, or rogue agent behavior.
/// </summary>
public interface IOutputGuard
{
    /// <summary>
    /// Validates LLM/agent output for hallucinations, unsafe commands, or malicious behavior.
    /// </summary>
    /// <param name="agentOutput">The agent's generated output</param>
    /// <param name="agentName">Name of the agent that generated this output</param>
    /// <param name="originalInput">The user's original input for context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Guard result with safety assessment</returns>
    Task<OutputGuardResult> ValidateOutputAsync(
        string agentOutput,
        string agentName,
        string originalInput,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of output guard validation.
/// </summary>
public sealed class OutputGuardResult
{
    /// <summary>
    /// Whether the output is safe to return to user/execute.
    /// </summary>
    public required bool IsSafe { get; init; }

    /// <summary>
    /// Confidence score (0.0 to 1.0) that the output is safe and accurate.
    /// </summary>
    public required double ConfidenceScore { get; init; }

    /// <summary>
    /// Detected issues (hallucinations, dangerous commands, etc.).
    /// </summary>
    public required string[] DetectedIssues { get; init; }

    /// <summary>
    /// Hallucination risk score (0.0 = low risk, 1.0 = high risk).
    /// </summary>
    public required double HallucinationRisk { get; init; }

    /// <summary>
    /// Whether the output contains dangerous operations.
    /// </summary>
    public required bool ContainsDangerousOperations { get; init; }

    /// <summary>
    /// Corrected version of the output (if issues were fixed).
    /// </summary>
    public string? CorrectedOutput { get; init; }

    /// <summary>
    /// Explanation of why output was flagged (for logging/debugging).
    /// </summary>
    public string? Explanation { get; init; }
}
