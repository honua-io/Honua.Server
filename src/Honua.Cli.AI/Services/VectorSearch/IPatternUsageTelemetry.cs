// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// Tracks usage and effectiveness of deployment pattern recommendations.
/// Enables learning from user behavior and pattern outcomes.
/// </summary>
public interface IPatternUsageTelemetry
{
    /// <summary>
    /// Records that a pattern was recommended to the user.
    /// </summary>
    /// <param name="patternId">The ID of the recommended pattern</param>
    /// <param name="requirements">The deployment requirements that triggered this recommendation</param>
    /// <param name="confidence">The confidence score of the recommendation</param>
    /// <param name="rank">The rank of this pattern in the search results (1 = top match)</param>
    /// <param name="wasAccepted">Whether the user accepted this recommendation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackRecommendationAsync(
        string patternId,
        DeploymentRequirements requirements,
        PatternConfidence confidence,
        int rank,
        bool wasAccepted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the outcome of a deployment that used a recommended pattern.
    /// </summary>
    /// <param name="patternId">The ID of the pattern that was used</param>
    /// <param name="success">Whether the deployment succeeded</param>
    /// <param name="feedback">Optional user feedback about the deployment</param>
    /// <param name="deploymentMetadata">Optional metadata about the deployment (JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackDeploymentOutcomeAsync(
        string patternId,
        bool success,
        string? feedback = null,
        string? deploymentMetadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the acceptance rate for a specific pattern (recommendations → actual usage).
    /// </summary>
    /// <param name="patternId">The pattern ID to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Acceptance rate (0.0 to 1.0) or null if no data</returns>
    Task<double?> GetPatternAcceptanceRateAsync(
        string patternId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets usage statistics for a pattern over a time period.
    /// </summary>
    Task<PatternUsageStats> GetUsageStatsAsync(
        string patternId,
        TimeSpan period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks agent performance for intelligent routing.
    /// </summary>
    Task TrackAgentPerformanceAsync(
        string agentName,
        string taskType,
        bool success,
        double confidenceScore,
        int executionTimeMs,
        string? feedback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets performance stats for an agent over a time period.
    /// </summary>
    Task<Agents.AgentPerformanceStats> GetAgentPerformanceAsync(
        string agentName,
        TimeSpan period,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets task match score for an agent based on task type and history.
    /// </summary>
    Task<double> GetAgentTaskMatchScoreAsync(
        string agentName,
        string taskType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks architecture swarm recommendations and user selections (for learning loop).
    /// </summary>
    Task TrackArchitectureSwarmAsync(
        string request,
        List<string> optionsPresented,
        string? userSelection,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks security/cost review outcomes to improve pattern recommendations.
    /// </summary>
    Task TrackReviewOutcomeAsync(
        string reviewType,
        string patternId,
        bool approved,
        int issuesFound,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks hierarchical decomposition effectiveness.
    /// </summary>
    Task TrackDecompositionAsync(
        string strategy,
        int phasesCreated,
        int tasksCreated,
        bool successful,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks validation loop outcomes for learning which patterns need more validation.
    /// </summary>
    Task TrackValidationLoopAsync(
        string action,
        int iterationsNeeded,
        bool ultimatelySucceeded,
        List<string> failureReasons,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Usage statistics for a deployment pattern.
/// </summary>
public sealed class PatternUsageStats
{
    public string PatternId { get; init; } = string.Empty;
    public int TimesRecommended { get; init; }
    public int TimesAccepted { get; init; }
    public int TimesDeployed { get; init; }
    public int SuccessfulDeployments { get; init; }
    public int FailedDeployments { get; init; }

    public double AcceptanceRate =>
        TimesRecommended > 0 ? (double)TimesAccepted / TimesRecommended : 0;

    public double SuccessRate =>
        TimesDeployed > 0 ? (double)SuccessfulDeployments / TimesDeployed : 0;

    public string Summary =>
        $"Recommended {TimesRecommended}x, accepted {TimesAccepted}x ({AcceptanceRate:P0}), " +
        $"deployed {TimesDeployed}x with {SuccessRate:P0} success rate";
}
