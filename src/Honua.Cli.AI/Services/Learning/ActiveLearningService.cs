// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.Learning;

/// <summary>
/// Active learning service that intelligently requests specific feedback when confidence is low
/// or when strategic information would most improve the learning loop.
/// </summary>
public sealed class ActiveLearningService
{
    private readonly string _connectionString;
    private readonly ILogger<ActiveLearningService> _logger;

    // Confidence thresholds for requesting feedback
    private const double LowConfidenceThreshold = 0.4;
    private const double UncertaintyThreshold = 0.5;  // Between 0.45-0.55 is uncertain
    private const int MinimumSamplesBeforeRequest = 3;

    public ActiveLearningService(string connectionString, ILogger<ActiveLearningService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Determines if feedback should be requested for a pattern recommendation.
    /// Uses active learning strategy to focus on most informative samples.
    /// </summary>
    public async Task<FeedbackRequest?> ShouldRequestFeedbackAsync(
        string patternId,
        double confidenceScore,
        DeploymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get pattern statistics
            var patternStats = await GetPatternStatisticsAsync(patternId, cancellationToken);

            // Strategy 1: Low confidence patterns
            if (confidenceScore < LowConfidenceThreshold)
            {
                return new FeedbackRequest
                {
                    PatternId = patternId,
                    RequestType = FeedbackRequestType.LowConfidence,
                    Priority = FeedbackPriority.High,
                    Questions = new List<string>
                    {
                        "This pattern has low confidence. Was this recommendation helpful?",
                        "What aspects of the configuration would you like to adjust?",
                        "Would you prefer different instance types or scaling parameters?"
                    },
                    Explanation = $"Pattern confidence is low ({confidenceScore:P0}), need more data to improve recommendations",
                    ShouldRequestSatisfactionRating = true
                };
            }

            // Strategy 2: Uncertainty region (need disambiguation)
            if (Math.Abs(confidenceScore - UncertaintyThreshold) < 0.05)
            {
                return new FeedbackRequest
                {
                    PatternId = patternId,
                    RequestType = FeedbackRequestType.Uncertainty,
                    Priority = FeedbackPriority.Medium,
                    Questions = new List<string>
                    {
                        "We're uncertain about this recommendation. How well does it match your needs?",
                        "What's most important: cost, performance, or scalability?"
                    },
                    Explanation = $"Pattern is in uncertainty region ({confidenceScore:P0}), feedback would help improve future recommendations",
                    ShouldRequestSatisfactionRating = true
                };
            }

            // Strategy 3: Cold start (new or rarely used pattern)
            if (patternStats.TotalInteractions < MinimumSamplesBeforeRequest)
            {
                return new FeedbackRequest
                {
                    PatternId = patternId,
                    RequestType = FeedbackRequestType.ColdStart,
                    Priority = FeedbackPriority.High,
                    Questions = new List<string>
                    {
                        "This is a newer pattern with limited usage data. Did it work well for you?",
                        "What would make this pattern better for your use case?"
                    },
                    Explanation = $"Pattern has only {patternStats.TotalInteractions} interactions, need more feedback to validate",
                    ShouldRequestSatisfactionRating = true
                };
            }

            // Strategy 4: High modification rate (pattern needs tuning)
            if (patternStats.ModificationRate > 0.6)
            {
                return new FeedbackRequest
                {
                    PatternId = patternId,
                    RequestType = FeedbackRequestType.HighModificationRate,
                    Priority = FeedbackPriority.Medium,
                    Questions = new List<string>
                    {
                        "Most users modify this pattern's configuration. What did you change?",
                        "Should we adjust the default configuration for this pattern?"
                    },
                    Explanation = $"Pattern has {patternStats.ModificationRate:P0} modification rate, feedback helps improve defaults",
                    ShouldRequestSatisfactionRating = false,
                    ShouldRequestConfigDiff = true
                };
            }

            // Strategy 5: Exploration vs exploitation (sample diverse requirements)
            var shouldExplore = await ShouldExploreRequirementsAsync(requirements, cancellationToken);
            if (shouldExplore)
            {
                return new FeedbackRequest
                {
                    PatternId = patternId,
                    RequestType = FeedbackRequestType.Exploration,
                    Priority = FeedbackPriority.Low,
                    Questions = new List<string>
                    {
                        "This is a unique requirements combination. How well did the pattern work?",
                        "Did the pattern handle your specific needs adequately?"
                    },
                    Explanation = "Exploring underrepresented requirement combinations to improve pattern coverage",
                    ShouldRequestSatisfactionRating = true
                };
            }

            // Strategy 6: Validation of high-confidence patterns (occasional checking)
            if (confidenceScore > 0.85 && patternStats.TotalInteractions > 20)
            {
                // Only request feedback 10% of the time for high-confidence patterns
                if (new Random().NextDouble() < 0.1)
                {
                    return new FeedbackRequest
                    {
                        PatternId = patternId,
                        RequestType = FeedbackRequestType.Validation,
                        Priority = FeedbackPriority.Low,
                        Questions = new List<string>
                        {
                            "Quick feedback: Did this pattern meet your expectations?"
                        },
                        Explanation = "Validating high-confidence pattern to ensure quality",
                        ShouldRequestSatisfactionRating = true
                    };
                }
            }

            // No feedback needed
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to determine feedback request strategy");
            return null;
        }
    }

    /// <summary>
    /// Gets pattern statistics for active learning decisions.
    /// </summary>
    private async Task<PatternStatistics> GetPatternStatisticsAsync(
        string patternId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var query = @"
            SELECT
                COUNT(*) as TotalInteractions,
                AVG(CASE WHEN was_accepted = true THEN 1.0 ELSE 0.0 END) as AcceptanceRate,
                AVG(CASE WHEN was_modified = true THEN 1.0 ELSE 0.0 END) as ModificationRate,
                AVG(confidence_score) as AvgConfidence,
                COUNT(*) FILTER (WHERE user_satisfaction_rating IS NOT NULL) as FeedbackCount
            FROM pattern_interaction_feedback
            WHERE pattern_id = @PatternId
              AND recommended_at >= NOW() - INTERVAL '90 days'
        ";

        var command = new CommandDefinition(
            query,
            new { PatternId = patternId },
            cancellationToken: cancellationToken);

        var result = await connection.QuerySingleOrDefaultAsync<PatternStatistics>(command);
        return result ?? new PatternStatistics();
    }

    /// <summary>
    /// Determines if requirements combination is underexplored (should request feedback).
    /// </summary>
    private async Task<bool> ShouldExploreRequirementsAsync(
        DeploymentRequirements requirements,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Find similar requirement combinations
        var query = @"
            WITH recent_deployments AS (
                SELECT
                    cloud_provider,
                    region,
                    data_volume_gb,
                    concurrent_users
                FROM deployment_history
                WHERE timestamp >= NOW() - INTERVAL '90 days'
            )
            SELECT COUNT(*) as similar_count
            FROM recent_deployments
            WHERE cloud_provider = @CloudProvider
              AND region = @Region
              AND ABS(data_volume_gb - @DataVolumeGb) < @DataVolumeGb * 0.3
              AND ABS(concurrent_users - @ConcurrentUsers) < @ConcurrentUsers * 0.3
        ";

        var command = new CommandDefinition(
            query,
            new
            {
                requirements.CloudProvider,
                requirements.Region,
                requirements.DataVolumeGb,
                requirements.ConcurrentUsers
            },
            cancellationToken: cancellationToken);

        var similarCount = await connection.QuerySingleAsync<int>(command);

        // If less than 5 similar deployments, this is an exploration opportunity
        return similarCount < 5;
    }

    /// <summary>
    /// Records that feedback was requested for analytics.
    /// </summary>
    public async Task RecordFeedbackRequestAsync(
        FeedbackRequest request,
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO active_learning_requests (
                    interaction_id,
                    pattern_id,
                    request_type,
                    priority,
                    questions,
                    requested_at
                )
                VALUES (
                    @InteractionId,
                    @PatternId,
                    @RequestType,
                    @Priority,
                    @Questions::jsonb,
                    NOW()
                )
            ";

            var questionsJson = System.Text.Json.JsonSerializer.Serialize(request.Questions);

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    InteractionId = interactionId,
                    request.PatternId,
                    RequestType = request.RequestType.ToString(),
                    Priority = request.Priority.ToString(),
                    Questions = questionsJson
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug(
                "Recorded active learning feedback request for pattern {PatternId}, type: {Type}",
                request.PatternId, request.RequestType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record feedback request");
        }
    }

    /// <summary>
    /// Gets active learning effectiveness metrics.
    /// </summary>
    public async Task<ActiveLearningMetrics> GetActiveLearningMetricsAsync(
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                WITH request_stats AS (
                    SELECT
                        request_type,
                        COUNT(*) as request_count,
                        -- Count how many got responses by joining with feedback
                        COUNT(pif.user_satisfaction_rating) as response_count
                    FROM active_learning_requests alr
                    LEFT JOIN pattern_interaction_feedback pif ON alr.interaction_id = pif.id
                    WHERE alr.requested_at >= NOW() - @TimeWindow::interval
                    GROUP BY request_type
                )
                SELECT
                    SUM(request_count) as TotalRequests,
                    SUM(response_count) as TotalResponses,
                    AVG(CASE WHEN request_count > 0 THEN response_count::DECIMAL / request_count ELSE 0 END) as AvgResponseRate
                FROM request_stats
            ";

            var command = new CommandDefinition(
                query,
                new { TimeWindow = $"{timeWindow.TotalMinutes} minutes" },
                cancellationToken: cancellationToken);

            var metrics = await connection.QuerySingleAsync<ActiveLearningMetrics>(command);
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active learning metrics");
            return new ActiveLearningMetrics();
        }
    }
}

/// <summary>
/// Feedback request recommendation from active learning.
/// </summary>
public sealed class FeedbackRequest
{
    public string PatternId { get; init; } = string.Empty;
    public FeedbackRequestType RequestType { get; init; }
    public FeedbackPriority Priority { get; init; }
    public List<string> Questions { get; init; } = new();
    public string Explanation { get; init; } = string.Empty;
    public bool ShouldRequestSatisfactionRating { get; init; }
    public bool ShouldRequestConfigDiff { get; init; }
}

public enum FeedbackRequestType
{
    LowConfidence,
    Uncertainty,
    ColdStart,
    HighModificationRate,
    Exploration,
    Validation
}

public enum FeedbackPriority
{
    Low,
    Medium,
    High
}

/// <summary>
/// Pattern statistics for active learning.
/// </summary>
public sealed class PatternStatistics
{
    public int TotalInteractions { get; init; }
    public double AcceptanceRate { get; init; }
    public double ModificationRate { get; init; }
    public double AvgConfidence { get; init; }
    public int FeedbackCount { get; init; }
}

/// <summary>
/// Active learning effectiveness metrics.
/// </summary>
public sealed class ActiveLearningMetrics
{
    public int TotalRequests { get; init; }
    public int TotalResponses { get; init; }
    public double AvgResponseRate { get; init; }
}
