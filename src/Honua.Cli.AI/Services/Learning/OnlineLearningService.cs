// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.Learning;

/// <summary>
/// Online learning service that updates pattern confidence scores in real-time
/// based on user acceptance/rejection signals without waiting for full deployment outcomes.
/// </summary>
public sealed class OnlineLearningService
{
    private readonly string _connectionString;
    private readonly ILogger<OnlineLearningService> _logger;

    // Learning rate for online updates (how much to adjust based on single signal)
    private const double LearningRate = 0.1;
    private const double MinimumConfidence = 0.1;
    private const double MaximumConfidence = 0.95;

    public OnlineLearningService(string connectionString, ILogger<OnlineLearningService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Updates pattern confidence immediately based on user acceptance/rejection.
    /// Uses exponential moving average to quickly adapt to new signals.
    /// </summary>
    public async Task UpdatePatternConfidenceAsync(
        string patternId,
        bool wasAccepted,
        decimal currentConfidence,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Calculate new confidence using online learning
            // If accepted: nudge confidence up
            // If rejected: nudge confidence down
            var targetValue = wasAccepted ? 1.0 : 0.0;
            var currentValue = (double)currentConfidence;

            // Exponential moving average: new = old + learningRate * (target - old)
            var newConfidence = currentValue + LearningRate * (targetValue - currentValue);

            // Clamp to reasonable bounds
            newConfidence = Math.Clamp(newConfidence, MinimumConfidence, MaximumConfidence);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Record the online update
            var insertQuery = @"
                INSERT INTO pattern_online_learning_updates (
                    pattern_id,
                    previous_confidence,
                    new_confidence,
                    signal_type,
                    learning_rate,
                    update_timestamp
                )
                VALUES (
                    @PatternId,
                    @PreviousConfidence,
                    @NewConfidence,
                    @SignalType,
                    @LearningRate,
                    NOW()
                )
            ";

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    PatternId = patternId,
                    PreviousConfidence = currentConfidence,
                    NewConfidence = newConfidence,
                    SignalType = wasAccepted ? "acceptance" : "rejection",
                    LearningRate = LearningRate
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug(
                "Online learning update: {PatternId} confidence {Old:F3} -> {New:F3} (signal: {Signal})",
                patternId, currentConfidence, newConfidence, wasAccepted ? "accept" : "reject");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update pattern confidence online");
        }
    }

    /// <summary>
    /// Gets the latest online confidence score for a pattern.
    /// Falls back to historical batch calculation if no online updates exist.
    /// </summary>
    public async Task<double?> GetLatestConfidenceAsync(
        string patternId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT new_confidence
                FROM pattern_online_learning_updates
                WHERE pattern_id = @PatternId
                ORDER BY update_timestamp DESC
                LIMIT 1
            ";

            var command = new CommandDefinition(
                query,
                new { PatternId = patternId },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleOrDefaultAsync<double?>(command);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest confidence for pattern");
            return null;
        }
    }

    /// <summary>
    /// Reconciles online learning updates with batch learning from deployment outcomes.
    /// Called periodically to ensure online updates don't drift too far from reality.
    /// </summary>
    public async Task ReconcileWithBatchLearningAsync(
        TimeSpan reconciliationWindow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Get patterns with online updates but also completed deployments
            var query = @"
                WITH online_confidence AS (
                    SELECT DISTINCT ON (pattern_id)
                        pattern_id,
                        new_confidence as online_conf
                    FROM pattern_online_learning_updates
                    WHERE update_timestamp >= NOW() - @Window::interval
                    ORDER BY pattern_id, update_timestamp DESC
                ),
                batch_metrics AS (
                    SELECT
                        pif.pattern_id,
                        AVG(CASE WHEN pif.was_accepted = true THEN 1.0 ELSE 0.0 END) as acceptance_rate,
                        COUNT(*) as sample_count
                    FROM pattern_interaction_feedback pif
                    WHERE pif.recommended_at >= NOW() - @Window::interval
                      AND pif.was_accepted IS NOT NULL
                    GROUP BY pif.pattern_id
                    HAVING COUNT(*) >= 5  -- Need enough samples
                )
                SELECT
                    oc.pattern_id as PatternId,
                    oc.online_conf as OnlineConfidence,
                    bm.acceptance_rate as BatchAcceptanceRate,
                    bm.sample_count as SampleCount
                FROM online_confidence oc
                INNER JOIN batch_metrics bm ON oc.pattern_id = bm.pattern_id
                WHERE ABS(oc.online_conf - bm.acceptance_rate) > 0.2  -- Significant drift
            ";

            var command = new CommandDefinition(
                query,
                new { Window = $"{reconciliationWindow.TotalMinutes} minutes" },
                cancellationToken: cancellationToken);

            var drifts = await connection.QueryAsync<PatternConfidenceDrift>(command);

            foreach (var drift in drifts)
            {
                // Blend online and batch estimates (weighted by sample count)
                var blendWeight = Math.Min(0.7, drift.SampleCount / 20.0);  // More samples = trust batch more
                var reconciledConfidence =
                    drift.OnlineConfidence * (1 - blendWeight) +
                    drift.BatchAcceptanceRate * blendWeight;

                _logger.LogInformation(
                    "Reconciling pattern {PatternId}: online={Online:F3}, batch={Batch:F3} ({Samples} samples), reconciled={Reconciled:F3}",
                    drift.PatternId, drift.OnlineConfidence, drift.BatchAcceptanceRate,
                    drift.SampleCount, reconciledConfidence);

                // Record reconciliation
                var reconcileQuery = @"
                    INSERT INTO pattern_online_learning_updates (
                        pattern_id,
                        previous_confidence,
                        new_confidence,
                        signal_type,
                        learning_rate,
                        update_timestamp,
                        reconciliation_metadata
                    )
                    VALUES (
                        @PatternId,
                        @OnlineConfidence,
                        @ReconciledConfidence,
                        'reconciliation',
                        @BlendWeight,
                        NOW(),
                        @Metadata::jsonb
                    )
                ";

                var metadata = System.Text.Json.JsonSerializer.Serialize(new
                {
                    online_confidence = drift.OnlineConfidence,
                    batch_acceptance_rate = drift.BatchAcceptanceRate,
                    sample_count = drift.SampleCount,
                    blend_weight = blendWeight
                });

                await connection.ExecuteAsync(reconcileQuery, new
                {
                    drift.PatternId,
                    drift.OnlineConfidence,
                    ReconciledConfidence = reconciledConfidence,
                    BlendWeight = blendWeight,
                    Metadata = metadata
                });
            }

            _logger.LogInformation("Reconciled {Count} patterns with batch learning", drifts.AsList().Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile online and batch learning");
        }
    }

    /// <summary>
    /// Gets online learning statistics for monitoring.
    /// </summary>
    public async Task<OnlineLearningStats> GetOnlineLearningStatsAsync(
        TimeSpan timeWindow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    COUNT(DISTINCT pattern_id) as TotalPatternsUpdated,
                    COUNT(*) as TotalUpdates,
                    COUNT(*) FILTER (WHERE signal_type = 'acceptance') as AcceptanceUpdates,
                    COUNT(*) FILTER (WHERE signal_type = 'rejection') as RejectionUpdates,
                    COUNT(*) FILTER (WHERE signal_type = 'reconciliation') as ReconciliationUpdates,
                    AVG(new_confidence - previous_confidence) as AvgConfidenceChange,
                    MAX(update_timestamp) as LastUpdateTimestamp
                FROM pattern_online_learning_updates
                WHERE update_timestamp >= NOW() - @TimeWindow::interval
            ";

            var command = new CommandDefinition(
                query,
                new { TimeWindow = $"{timeWindow.TotalMinutes} minutes" },
                cancellationToken: cancellationToken);

            var stats = await connection.QuerySingleAsync<OnlineLearningStats>(command);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get online learning stats");
            return new OnlineLearningStats();
        }
    }
}

/// <summary>
/// Represents a drift between online and batch confidence scores.
/// </summary>
public sealed class PatternConfidenceDrift
{
    public string PatternId { get; init; } = string.Empty;
    public double OnlineConfidence { get; init; }
    public double BatchAcceptanceRate { get; init; }
    public int SampleCount { get; init; }
}

/// <summary>
/// Online learning statistics.
/// </summary>
public sealed class OnlineLearningStats
{
    public int TotalPatternsUpdated { get; init; }
    public int TotalUpdates { get; init; }
    public int AcceptanceUpdates { get; init; }
    public int RejectionUpdates { get; init; }
    public int ReconciliationUpdates { get; init; }
    public double AvgConfidenceChange { get; init; }
    public DateTime? LastUpdateTimestamp { get; init; }
}
