// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.Analytics;

/// <summary>
/// Provides learning analytics and dashboard metrics for the AI feedback loop.
/// Tracks pattern confidence trends, agent accuracy improvements, and feature importance.
/// </summary>
public sealed class LearningDashboardService
{
    private readonly string _connectionString;
    private readonly ILogger<LearningDashboardService> _logger;

    public LearningDashboardService(string connectionString, ILogger<LearningDashboardService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets overall learning statistics summary.
    /// </summary>
    public async Task<LearningStatsSummary> GetLearningStatsSummaryAsync(
        TimeSpan? timeWindow = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var window = timeWindow ?? TimeSpan.FromDays(30);
            var query = @"
                WITH pattern_stats AS (
                    SELECT
                        COUNT(DISTINCT pattern_id) as total_patterns,
                        COUNT(*) as total_interactions,
                        AVG(CASE WHEN was_accepted = true THEN 1.0 ELSE 0.0 END) as avg_acceptance_rate,
                        AVG(confidence_score) as avg_confidence,
                        AVG(time_to_decision_seconds) as avg_decision_time,
                        COUNT(*) FILTER (WHERE was_modified = true) as modified_count
                    FROM pattern_interaction_feedback
                    WHERE recommended_at >= NOW() - @TimeWindow::interval
                ),
                agent_stats AS (
                    SELECT
                        COUNT(DISTINCT agent_name) as total_agents,
                        AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as avg_success_rate
                    FROM agent_execution_metrics
                    WHERE started_at >= NOW() - @TimeWindow::interval
                ),
                deployment_stats AS (
                    SELECT
                        COUNT(*) as total_deployments,
                        AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as deployment_success_rate,
                        AVG(cost_accuracy_percent) as avg_cost_accuracy
                    FROM deployment_history
                    WHERE timestamp >= NOW() - @TimeWindow::interval
                )
                SELECT
                    COALESCE(p.total_patterns, 0) as TotalPatterns,
                    COALESCE(p.total_interactions, 0) as TotalInteractions,
                    COALESCE(p.avg_acceptance_rate, 0) as AverageAcceptanceRate,
                    COALESCE(p.avg_confidence, 0) as AverageConfidence,
                    COALESCE(p.avg_decision_time, 0) as AverageDecisionTimeSeconds,
                    COALESCE(p.modified_count, 0) as ModifiedCount,
                    COALESCE(a.total_agents, 0) as TotalAgents,
                    COALESCE(a.avg_success_rate, 0) as AverageAgentSuccessRate,
                    COALESCE(d.total_deployments, 0) as TotalDeployments,
                    COALESCE(d.deployment_success_rate, 0) as DeploymentSuccessRate,
                    COALESCE(d.avg_cost_accuracy, 0) as AverageCostAccuracy
                FROM pattern_stats p
                CROSS JOIN agent_stats a
                CROSS JOIN deployment_stats d
            ";

            var command = new CommandDefinition(
                query,
                new { TimeWindow = $"{window.TotalMinutes} minutes" },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleAsync<LearningStatsSummary>(command);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get learning stats summary");
            return new LearningStatsSummary();
        }
    }

    /// <summary>
    /// Gets pattern confidence trends over time.
    /// </summary>
    public async Task<List<PatternConfidenceTrend>> GetPatternConfidenceTrendsAsync(
        string? patternId = null,
        int weeksBack = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = patternId != null
                ? @"SELECT * FROM pattern_confidence_trends
                    WHERE pattern_id = @PatternId AND week >= NOW() - @WeeksBack::interval
                    ORDER BY week DESC"
                : @"SELECT * FROM pattern_confidence_trends
                    WHERE week >= NOW() - @WeeksBack::interval
                    ORDER BY pattern_id, week DESC";

            var command = new CommandDefinition(
                query,
                new { PatternId = patternId, WeeksBack = $"{weeksBack * 7} days" },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<PatternConfidenceTrend>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern confidence trends");
            return new List<PatternConfidenceTrend>();
        }
    }

    /// <summary>
    /// Gets pattern learning metrics with detailed statistics.
    /// </summary>
    public async Task<List<PatternLearningMetric>> GetPatternLearningMetricsAsync(
        int topN = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT * FROM pattern_learning_metrics
                ORDER BY total_recommendations DESC
                LIMIT @TopN
            ";

            var command = new CommandDefinition(
                query,
                new { TopN = topN },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<PatternLearningMetric>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern learning metrics");
            return new List<PatternLearningMetric>();
        }
    }

    /// <summary>
    /// Gets feature importance analysis showing which config fields are most often modified.
    /// </summary>
    public async Task<List<FeatureImportance>> GetFeatureImportanceAsync(
        string? patternId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = patternId != null
                ? @"SELECT * FROM feature_importance_analysis WHERE pattern_id = @PatternId"
                : @"SELECT * FROM feature_importance_analysis";

            var command = new CommandDefinition(
                query,
                new { PatternId = patternId },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<FeatureImportance>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get feature importance");
            return new List<FeatureImportance>();
        }
    }

    /// <summary>
    /// Gets agent performance improvements over time.
    /// </summary>
    public async Task<List<AgentPerformanceTrend>> GetAgentPerformanceTrendsAsync(
        string? agentName = null,
        int weeksBack = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    agent_name as AgentName,
                    DATE_TRUNC('week', started_at) as Week,
                    COUNT(*) as ExecutionCount,
                    AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as SuccessRate,
                    AVG(duration_ms) as AvgDurationMs
                FROM agent_execution_metrics
                WHERE started_at >= NOW() - @WeeksBack::interval
                  AND (@AgentName IS NULL OR agent_name = @AgentName)
                GROUP BY agent_name, DATE_TRUNC('week', started_at)
                ORDER BY agent_name, Week DESC
            ";

            var command = new CommandDefinition(
                query,
                new { AgentName = agentName, WeeksBack = $"{weeksBack * 7} days" },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<AgentPerformanceTrend>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent performance trends");
            return new List<AgentPerformanceTrend>();
        }
    }

    /// <summary>
    /// Gets user satisfaction trends over time.
    /// </summary>
    public async Task<List<SatisfactionTrend>> GetSatisfactionTrendsAsync(
        int weeksBack = 12,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    DATE_TRUNC('week', recommended_at) as Week,
                    COUNT(*) FILTER (WHERE user_satisfaction_rating IS NOT NULL) as ResponseCount,
                    AVG(user_satisfaction_rating) as AvgRating,
                    COUNT(*) FILTER (WHERE user_satisfaction_rating >= 4) as PositiveCount,
                    COUNT(*) FILTER (WHERE user_satisfaction_rating <= 2) as NegativeCount
                FROM pattern_interaction_feedback
                WHERE recommended_at >= NOW() - @WeeksBack::interval
                GROUP BY DATE_TRUNC('week', recommended_at)
                ORDER BY Week DESC
            ";

            var command = new CommandDefinition(
                query,
                new { WeeksBack = $"{weeksBack * 7} days" },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<SatisfactionTrend>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get satisfaction trends");
            return new List<SatisfactionTrend>();
        }
    }

    /// <summary>
    /// Gets insights about patterns that need improvement (low acceptance, high modification rate).
    /// </summary>
    public async Task<List<PatternInsight>> GetPatternInsightsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                WITH pattern_metrics AS (
                    SELECT
                        pattern_id,
                        COUNT(*) as total_recommendations,
                        AVG(CASE WHEN was_accepted = true THEN 1.0 ELSE 0.0 END) as acceptance_rate,
                        AVG(CASE WHEN was_modified = true THEN 1.0 ELSE 0.0 END) as modification_rate,
                        AVG(confidence_score) as avg_confidence,
                        AVG(time_to_decision_seconds) as avg_decision_time,
                        AVG(follow_up_questions_count) as avg_questions,
                        AVG(user_satisfaction_rating) as avg_satisfaction
                    FROM pattern_interaction_feedback
                    WHERE recommended_at >= NOW() - INTERVAL '90 days'
                    GROUP BY pattern_id
                    HAVING COUNT(*) >= 3  -- Need at least 3 data points
                )
                SELECT
                    pattern_id as PatternId,
                    total_recommendations as TotalRecommendations,
                    acceptance_rate as AcceptanceRate,
                    modification_rate as ModificationRate,
                    avg_confidence as AvgConfidence,
                    avg_decision_time as AvgDecisionTime,
                    avg_questions as AvgQuestions,
                    avg_satisfaction as AvgSatisfaction,
                    CASE
                        WHEN acceptance_rate < 0.3 THEN 'Low acceptance - pattern may not match user needs'
                        WHEN modification_rate > 0.7 THEN 'High modification rate - recommended config needs improvement'
                        WHEN avg_decision_time > 300 THEN 'Long decision time - pattern explanation may be unclear'
                        WHEN avg_questions > 3 THEN 'Many follow-up questions - documentation needs improvement'
                        WHEN avg_satisfaction < 3.0 THEN 'Low satisfaction - pattern needs review'
                        WHEN acceptance_rate > 0.8 AND modification_rate < 0.2 THEN 'Performing well - good pattern match'
                        ELSE 'Normal performance'
                    END as Insight
                FROM pattern_metrics
                ORDER BY
                    CASE
                        WHEN acceptance_rate < 0.3 THEN 1
                        WHEN modification_rate > 0.7 THEN 2
                        WHEN avg_decision_time > 300 THEN 3
                        WHEN avg_questions > 3 THEN 4
                        WHEN avg_satisfaction < 3.0 THEN 5
                        ELSE 6
                    END,
                    total_recommendations DESC
            ";

            var command = new CommandDefinition(query, cancellationToken: cancellationToken);
            var results = await connection.QueryAsync<PatternInsight>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern insights");
            return new List<PatternInsight>();
        }
    }
}

/// <summary>
/// Overall learning statistics summary.
/// </summary>
public sealed class LearningStatsSummary
{
    public int TotalPatterns { get; init; }
    public int TotalInteractions { get; init; }
    public double AverageAcceptanceRate { get; init; }
    public double AverageConfidence { get; init; }
    public double AverageDecisionTimeSeconds { get; init; }
    public int ModifiedCount { get; init; }
    public int TotalAgents { get; init; }
    public double AverageAgentSuccessRate { get; init; }
    public int TotalDeployments { get; init; }
    public double DeploymentSuccessRate { get; init; }
    public double AverageCostAccuracy { get; init; }
}

/// <summary>
/// Pattern confidence trend over time.
/// </summary>
public sealed class PatternConfidenceTrend
{
    public string PatternId { get; init; } = string.Empty;
    public DateTime Week { get; init; }
    public int RecommendationCount { get; init; }
    public double AvgConfidence { get; init; }
    public double AcceptanceRate { get; init; }
    public double AvgDecisionTime { get; init; }
}

/// <summary>
/// Pattern learning metrics.
/// </summary>
public sealed class PatternLearningMetric
{
    public string PatternId { get; init; } = string.Empty;
    public int TotalRecommendations { get; init; }
    public int AcceptedCount { get; init; }
    public int ModifiedCount { get; init; }
    public double AvgConfidence { get; init; }
    public double AvgDecisionTimeSeconds { get; init; }
    public double AvgQuestions { get; init; }
    public double? AvgSatisfaction { get; init; }
    public double AcceptanceRate { get; init; }
    public double ModificationRate { get; init; }
}

/// <summary>
/// Feature importance (which config fields are most frequently modified).
/// </summary>
public sealed class FeatureImportance
{
    public string PatternId { get; init; } = string.Empty;
    public string ChangedField { get; init; } = string.Empty;
    public int ChangeCount { get; init; }
    public double ChangePercentage { get; init; }
}

/// <summary>
/// Agent performance trend over time.
/// </summary>
public sealed class AgentPerformanceTrend
{
    public string AgentName { get; init; } = string.Empty;
    public DateTime Week { get; init; }
    public int ExecutionCount { get; init; }
    public double SuccessRate { get; init; }
    public double AvgDurationMs { get; init; }
}

/// <summary>
/// User satisfaction trend over time.
/// </summary>
public sealed class SatisfactionTrend
{
    public DateTime Week { get; init; }
    public int ResponseCount { get; init; }
    public double AvgRating { get; init; }
    public int PositiveCount { get; init; }
    public int NegativeCount { get; init; }
}

/// <summary>
/// Pattern insights with actionable recommendations.
/// </summary>
public sealed class PatternInsight
{
    public string PatternId { get; init; } = string.Empty;
    public int TotalRecommendations { get; init; }
    public double AcceptanceRate { get; init; }
    public double ModificationRate { get; init; }
    public double AvgConfidence { get; init; }
    public double AvgDecisionTime { get; init; }
    public double AvgQuestions { get; init; }
    public double? AvgSatisfaction { get; init; }
    public string Insight { get; init; } = string.Empty;
}
