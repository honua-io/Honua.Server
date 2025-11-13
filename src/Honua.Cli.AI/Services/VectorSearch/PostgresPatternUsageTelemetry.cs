// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// PostgreSQL-based implementation of pattern usage telemetry.
/// Tracks recommendations, acceptances, and deployment outcomes.
/// </summary>
public sealed class PostgresPatternUsageTelemetry : IPatternUsageTelemetry
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PostgresPatternUsageTelemetry> _logger;

    public PostgresPatternUsageTelemetry(
        IConfiguration configuration,
        ILogger<PostgresPatternUsageTelemetry> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task TrackRecommendationAsync(
        string patternId,
        DeploymentRequirements requirements,
        PatternConfidence confidence,
        int rank,
        bool wasAccepted,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO pattern_recommendation_tracking
                    (pattern_id, requirements_json, confidence_score, confidence_level,
                     rank, was_accepted, recommended_at)
                VALUES
                    (@PatternId, @Requirements, @ConfidenceScore, @ConfidenceLevel,
                     @Rank, @WasAccepted, NOW())",
                new
                {
                    PatternId = patternId,
                    Requirements = JsonSerializer.Serialize(requirements),
                    ConfidenceScore = confidence.Overall,
                    ConfidenceLevel = confidence.Level,
                    Rank = rank,
                    WasAccepted = wasAccepted
                });

            _logger.LogInformation(
                "Tracked recommendation: Pattern {PatternId}, Rank {Rank}, Accepted: {Accepted}, Confidence: {Confidence}",
                patternId,
                rank,
                wasAccepted,
                confidence.Level);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track pattern recommendation");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task TrackDeploymentOutcomeAsync(
        string patternId,
        bool success,
        string? feedback = null,
        string? deploymentMetadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO pattern_deployment_outcomes
                    (pattern_id, success, feedback, deployment_metadata, deployed_at)
                VALUES
                    (@PatternId, @Success, @Feedback, @Metadata::jsonb, NOW())",
                new
                {
                    PatternId = patternId,
                    Success = success,
                    Feedback = feedback,
                    Metadata = deploymentMetadata
                });

            _logger.LogInformation(
                "Tracked deployment outcome: Pattern {PatternId}, Success: {Success}",
                patternId,
                success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track deployment outcome");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task<double?> GetPatternAcceptanceRateAsync(
        string patternId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var result = await connection.QueryFirstOrDefaultAsync<(int Total, int Accepted)?>(
                @"SELECT
                    COUNT(*)::int as Total,
                    SUM(CASE WHEN was_accepted THEN 1 ELSE 0 END)::int as Accepted
                  FROM pattern_recommendation_tracking
                  WHERE pattern_id = @PatternId",
                new { PatternId = patternId });

            if (result == null || result.Value.Total == 0)
                return null;

            return (double)result.Value.Accepted / result.Value.Total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern acceptance rate");
            return null;
        }
    }

    public async Task<PatternUsageStats> GetUsageStatsAsync(
        string patternId,
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var cutoffDate = DateTime.UtcNow - period;

            // Get recommendation stats
            var recommendationStats = await connection.QueryFirstOrDefaultAsync<(int Total, int Accepted)>(
                @"SELECT
                    COUNT(*)::int as Total,
                    SUM(CASE WHEN was_accepted THEN 1 ELSE 0 END)::int as Accepted
                  FROM pattern_recommendation_tracking
                  WHERE pattern_id = @PatternId
                    AND recommended_at >= @CutoffDate",
                new { PatternId = patternId, CutoffDate = cutoffDate });

            // Get deployment outcome stats
            var deploymentStats = await connection.QueryFirstOrDefaultAsync<(int Total, int Successful)>(
                @"SELECT
                    COUNT(*)::int as Total,
                    SUM(CASE WHEN success THEN 1 ELSE 0 END)::int as Successful
                  FROM pattern_deployment_outcomes
                  WHERE pattern_id = @PatternId
                    AND deployed_at >= @CutoffDate",
                new { PatternId = patternId, CutoffDate = cutoffDate });

            return new PatternUsageStats
            {
                PatternId = patternId,
                TimesRecommended = recommendationStats.Total,
                TimesAccepted = recommendationStats.Accepted,
                TimesDeployed = deploymentStats.Total,
                SuccessfulDeployments = deploymentStats.Successful,
                FailedDeployments = deploymentStats.Total - deploymentStats.Successful
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern usage stats");

            return new PatternUsageStats { PatternId = patternId };
        }
    }

    public async Task TrackAgentPerformanceAsync(
        string agentName,
        string taskType,
        bool success,
        double confidenceScore,
        int executionTimeMs,
        string? feedback = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO agent_performance_tracking
                    (agent_name, task_type, success, confidence_score, execution_time_ms, feedback, tracked_at)
                VALUES
                    (@AgentName, @TaskType, @Success, @ConfidenceScore, @ExecutionTimeMs, @Feedback, NOW())",
                new
                {
                    AgentName = agentName,
                    TaskType = taskType,
                    Success = success,
                    ConfidenceScore = confidenceScore,
                    ExecutionTimeMs = executionTimeMs,
                    Feedback = feedback
                });

            _logger.LogInformation(
                "Tracked agent performance: {AgentName} on {TaskType}, Success: {Success}, Confidence: {Confidence:P0}",
                agentName,
                taskType,
                success,
                confidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track agent performance");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task<Agents.AgentPerformanceStats> GetAgentPerformanceAsync(
        string agentName,
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var cutoffDate = DateTime.UtcNow - period;

            var stats = await connection.QueryFirstOrDefaultAsync<(int Total, int Successful, double AvgConfidence, long AvgTimeMs)>(@"
                SELECT
                    COUNT(*)::int as Total,
                    SUM(CASE WHEN success THEN 1 ELSE 0 END)::int as Successful,
                    AVG(confidence_score) as AvgConfidence,
                    AVG(execution_time_ms)::bigint as AvgTimeMs
                FROM agent_performance_tracking
                WHERE agent_name = @AgentName
                  AND tracked_at >= @CutoffDate",
                new { AgentName = agentName, CutoffDate = cutoffDate });

            return new Agents.AgentPerformanceStats
            {
                AgentName = agentName,
                TotalInteractions = stats.Total,
                SuccessfulInteractions = stats.Successful,
                FailedInteractions = stats.Total - stats.Successful,
                AverageConfidence = stats.AvgConfidence,
                AverageExecutionTime = TimeSpan.FromMilliseconds(stats.AvgTimeMs)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent performance stats");
            return new Agents.AgentPerformanceStats { AgentName = agentName };
        }
    }

    public async Task<double> GetAgentTaskMatchScoreAsync(
        string agentName,
        string taskType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            // Task match score based on success rate for this specific task type
            var stats = await connection.QueryFirstOrDefaultAsync<(int Total, int Successful)>(@"
                SELECT
                    COUNT(*)::int as Total,
                    SUM(CASE WHEN success THEN 1 ELSE 0 END)::int as Successful
                FROM agent_performance_tracking
                WHERE agent_name = @AgentName
                  AND task_type = @TaskType
                  AND tracked_at >= NOW() - INTERVAL '90 days'",
                new { AgentName = agentName, TaskType = taskType });

            if (stats.Total == 0)
            {
                // No historical data - return neutral score
                return 0.5;
            }

            // Return success rate for this task type as the match score
            return (double)stats.Successful / stats.Total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent task match score");
            return 0.5; // Neutral score on error
        }
    }

    public async Task TrackArchitectureSwarmAsync(
        string request,
        System.Collections.Generic.List<string> optionsPresented,
        string? userSelection,
        System.Collections.Generic.Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO architecture_swarm_tracking
                    (request, options_presented, user_selection, metadata, tracked_at)
                VALUES
                    (@Request, @OptionsPresented::jsonb, @UserSelection, @Metadata::jsonb, NOW())",
                new
                {
                    Request = request,
                    OptionsPresented = JsonSerializer.Serialize(optionsPresented),
                    UserSelection = userSelection,
                    Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
                });

            _logger.LogInformation(
                "Tracked architecture swarm: {OptionCount} options presented, User selected: {Selection}",
                optionsPresented.Count,
                userSelection ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track architecture swarm");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task TrackReviewOutcomeAsync(
        string reviewType,
        string patternId,
        bool approved,
        int issuesFound,
        System.Collections.Generic.Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO review_outcome_tracking
                    (review_type, pattern_id, approved, issues_found, metadata, tracked_at)
                VALUES
                    (@ReviewType, @PatternId, @Approved, @IssuesFound, @Metadata::jsonb, NOW())",
                new
                {
                    ReviewType = reviewType,
                    PatternId = patternId,
                    Approved = approved,
                    IssuesFound = issuesFound,
                    Metadata = metadata != null ? JsonSerializer.Serialize(metadata) : null
                });

            _logger.LogInformation(
                "Tracked {ReviewType} review: Pattern {PatternId}, Approved: {Approved}, Issues: {Issues}",
                reviewType,
                patternId,
                approved,
                issuesFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track review outcome");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task TrackDecompositionAsync(
        string strategy,
        int phasesCreated,
        int tasksCreated,
        bool successful,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO decomposition_tracking
                    (strategy, phases_created, tasks_created, successful, duration_ms, tracked_at)
                VALUES
                    (@Strategy, @PhasesCreated, @TasksCreated, @Successful, @DurationMs, NOW())",
                new
                {
                    Strategy = strategy,
                    PhasesCreated = phasesCreated,
                    TasksCreated = tasksCreated,
                    Successful = successful,
                    DurationMs = (int)duration.TotalMilliseconds
                });

            _logger.LogInformation(
                "Tracked decomposition: {Strategy}, {Phases} phases, {Tasks} tasks, Duration: {Duration}ms",
                strategy,
                phasesCreated,
                tasksCreated,
                duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track decomposition");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task TrackValidationLoopAsync(
        string action,
        int iterationsNeeded,
        bool ultimatelySucceeded,
        System.Collections.Generic.List<string> failureReasons,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO validation_loop_tracking
                    (action, iterations_needed, ultimately_succeeded, failure_reasons, tracked_at)
                VALUES
                    (@Action, @IterationsNeeded, @UltimatelySucceeded, @FailureReasons::jsonb, NOW())",
                new
                {
                    Action = action,
                    IterationsNeeded = iterationsNeeded,
                    UltimatelySucceeded = ultimatelySucceeded,
                    FailureReasons = JsonSerializer.Serialize(failureReasons)
                });

            _logger.LogInformation(
                "Tracked validation loop: {Action}, {Iterations} iterations, Success: {Success}",
                action,
                iterationsNeeded,
                ultimatelySucceeded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track validation loop");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task TrackPatternInteractionAsync(
        PatternInteractionFeedback feedback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO pattern_interaction_feedback
                    (id, pattern_id, deployment_id, recommended_at, recommendation_rank,
                     confidence_score, recommended_config_json, decision_timestamp,
                     time_to_decision_seconds, was_accepted, was_modified,
                     actual_config_json, config_modifications_json, follow_up_questions_count,
                     user_hesitation_indicators_json, user_satisfaction_rating, user_feedback_text)
                VALUES
                    (@Id, @PatternId, @DeploymentId, @RecommendedAt, @RecommendationRank,
                     @ConfidenceScore, @RecommendedConfigJson::jsonb, @DecisionTimestamp,
                     @TimeToDecisionSeconds, @WasAccepted, @WasModified,
                     @ActualConfigJson::jsonb, @ConfigModificationsJson::jsonb, @FollowUpQuestionsCount,
                     @UserHesitationIndicatorsJson::jsonb, @UserSatisfactionRating, @UserFeedbackText)",
                new
                {
                    feedback.Id,
                    feedback.PatternId,
                    feedback.DeploymentId,
                    feedback.RecommendedAt,
                    feedback.RecommendationRank,
                    feedback.ConfidenceScore,
                    feedback.RecommendedConfigJson,
                    feedback.DecisionTimestamp,
                    feedback.TimeToDecisionSeconds,
                    feedback.WasAccepted,
                    feedback.WasModified,
                    feedback.ActualConfigJson,
                    feedback.ConfigModificationsJson,
                    feedback.FollowUpQuestionsCount,
                    feedback.UserHesitationIndicatorsJson,
                    feedback.UserSatisfactionRating,
                    feedback.UserFeedbackText
                });

            _logger.LogInformation(
                "Tracked pattern interaction: {PatternId}, Accepted: {Accepted}",
                feedback.PatternId,
                feedback.WasAccepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track pattern interaction");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task UpdatePatternDecisionAsync(
        Guid interactionId,
        bool wasAccepted,
        DateTime decisionTimestamp,
        string? actualConfigJson = null,
        string? configModificationsJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                UPDATE pattern_interaction_feedback
                SET decision_timestamp = @DecisionTimestamp,
                    time_to_decision_seconds = EXTRACT(EPOCH FROM (@DecisionTimestamp - recommended_at))::int,
                    was_accepted = @WasAccepted,
                    actual_config_json = @ActualConfigJson::jsonb,
                    config_modifications_json = @ConfigModificationsJson::jsonb,
                    was_modified = (@ConfigModificationsJson IS NOT NULL)
                WHERE id = @InteractionId",
                new
                {
                    InteractionId = interactionId,
                    WasAccepted = wasAccepted,
                    DecisionTimestamp = decisionTimestamp,
                    ActualConfigJson = actualConfigJson,
                    ConfigModificationsJson = configModificationsJson
                });

            _logger.LogInformation(
                "Updated pattern decision: {InteractionId}, Accepted: {Accepted}",
                interactionId,
                wasAccepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pattern decision");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task RecordUserSatisfactionAsync(
        Guid interactionId,
        int rating,
        string? feedbackText = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                UPDATE pattern_interaction_feedback
                SET user_satisfaction_rating = @Rating,
                    user_feedback_text = @FeedbackText
                WHERE id = @InteractionId",
                new
                {
                    InteractionId = interactionId,
                    Rating = rating,
                    FeedbackText = feedbackText
                });

            _logger.LogInformation(
                "Recorded user satisfaction: {InteractionId}, Rating: {Rating}",
                interactionId,
                rating);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record user satisfaction");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    public async Task IncrementFollowUpQuestionsAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                UPDATE pattern_interaction_feedback
                SET follow_up_questions_count = follow_up_questions_count + 1
                WHERE id = @InteractionId",
                new { InteractionId = interactionId });

            _logger.LogDebug(
                "Incremented follow-up questions count for interaction {InteractionId}",
                interactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to increment follow-up questions");
            // Don't throw - telemetry failures shouldn't break the workflow
        }
    }

    private async Task<IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("ConnectionStrings:PostgreSQL not configured");

        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
