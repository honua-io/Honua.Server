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
/// Transfer learning service that handles cold start problems by learning from similar patterns.
/// When a new pattern has no historical data, it borrows confidence from similar patterns.
/// </summary>
public sealed class TransferLearningService
{
    private readonly string _connectionString;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<TransferLearningService> _logger;

    public TransferLearningService(
        string connectionString,
        IEmbeddingProvider embeddingProvider,
        ILogger<TransferLearningService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Bootstraps confidence for a new pattern by finding similar patterns and transferring their metrics.
    /// </summary>
    public async Task<TransferLearningResult> BootstrapNewPatternAsync(
        string newPatternId,
        DeploymentRequirements requirements,
        string patternConfigJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Find similar patterns based on requirements
            var similarPatterns = await FindSimilarPatternsAsync(
                requirements,
                patternConfigJson,
                topK: 5,
                cancellationToken);

            if (!similarPatterns.Any())
            {
                _logger.LogWarning(
                    "No similar patterns found for cold start of {PatternId}. Using default confidence.",
                    newPatternId);

                return new TransferLearningResult
                {
                    PatternId = newPatternId,
                    BootstrappedConfidence = 0.5,
                    BootstrapMethod = "default",
                    SimilarPatterns = new List<SimilarPattern>()
                };
            }

            // Calculate weighted confidence from similar patterns
            var totalSimilarity = similarPatterns.Sum(p => p.SimilarityScore);
            var weightedConfidence = similarPatterns.Sum(p =>
                (p.SimilarityScore / totalSimilarity) * p.AcceptanceRate);

            // Apply conservative discount for cold start (reduce by 20%)
            var bootstrappedConfidence = weightedConfidence * 0.8;

            _logger.LogInformation(
                "Bootstrapped pattern {PatternId} with confidence {Confidence:F3} from {Count} similar patterns",
                newPatternId, bootstrappedConfidence, similarPatterns.Count);

            // Record the transfer learning
            await RecordTransferLearningAsync(
                newPatternId,
                bootstrappedConfidence,
                similarPatterns,
                cancellationToken);

            return new TransferLearningResult
            {
                PatternId = newPatternId,
                BootstrappedConfidence = bootstrappedConfidence,
                BootstrapMethod = "transfer_learning",
                SimilarPatterns = similarPatterns,
                ConfidenceExplanation = $"Bootstrapped from {similarPatterns.Count} similar patterns " +
                    $"(avg similarity: {similarPatterns.Average(p => p.SimilarityScore):F2})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bootstrap pattern via transfer learning");
            return new TransferLearningResult
            {
                PatternId = newPatternId,
                BootstrappedConfidence = 0.5,
                BootstrapMethod = "error_fallback",
                SimilarPatterns = new List<SimilarPattern>()
            };
        }
    }

    /// <summary>
    /// Finds similar patterns based on deployment requirements and configuration.
    /// </summary>
    private async Task<List<SimilarPattern>> FindSimilarPatternsAsync(
        DeploymentRequirements requirements,
        string patternConfigJson,
        int topK,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Find patterns with similar requirements
        var query = @"
            WITH pattern_metrics AS (
                SELECT
                    pif.pattern_id,
                    COUNT(*) as total_interactions,
                    AVG(CASE WHEN pif.was_accepted = true THEN 1.0 ELSE 0.0 END) as acceptance_rate,
                    AVG(pif.confidence_score) as avg_confidence,
                    AVG(pif.time_to_decision_seconds) as avg_decision_time
                FROM pattern_interaction_feedback pif
                WHERE pif.recommended_at >= NOW() - INTERVAL '90 days'
                  AND pif.was_accepted IS NOT NULL
                GROUP BY pif.pattern_id
                HAVING COUNT(*) >= 3  -- Minimum interactions for reliability
            ),
            deployment_patterns AS (
                SELECT DISTINCT
                    dh.configuration->>'patternId' as pattern_id,
                    dh.cloud_provider,
                    dh.region,
                    dh.data_volume_gb,
                    dh.concurrent_users,
                    dh.instance_type
                FROM deployment_history dh
                WHERE dh.success = true
                  AND dh.timestamp >= NOW() - INTERVAL '90 days'
            )
            SELECT
                pm.pattern_id as PatternId,
                pm.acceptance_rate as AcceptanceRate,
                pm.avg_confidence as AvgConfidence,
                pm.avg_decision_time as AvgDecisionTime,
                pm.total_interactions as TotalInteractions,
                dp.cloud_provider as CloudProvider,
                dp.data_volume_gb as DataVolumeGb,
                dp.concurrent_users as ConcurrentUsers,
                -- Calculate similarity score based on requirements
                (
                    CASE WHEN dp.cloud_provider = @CloudProvider THEN 0.3 ELSE 0.0 END +
                    CASE WHEN dp.region = @Region THEN 0.1 ELSE 0.0 END +
                    (1.0 - ABS(dp.data_volume_gb - @DataVolumeGb)::DECIMAL / GREATEST(dp.data_volume_gb, @DataVolumeGb, 1)) * 0.3 +
                    (1.0 - ABS(dp.concurrent_users - @ConcurrentUsers)::DECIMAL / GREATEST(dp.concurrent_users, @ConcurrentUsers, 1)) * 0.3
                ) as SimilarityScore
            FROM pattern_metrics pm
            INNER JOIN deployment_patterns dp ON pm.pattern_id = dp.pattern_id
            WHERE dp.cloud_provider = @CloudProvider  -- Must match cloud provider
            ORDER BY SimilarityScore DESC
            LIMIT @TopK
        ";

        var command = new CommandDefinition(
            query,
            new
            {
                requirements.CloudProvider,
                requirements.Region,
                requirements.DataVolumeGb,
                requirements.ConcurrentUsers,
                TopK = topK
            },
            cancellationToken: cancellationToken);

        var results = await connection.QueryAsync<SimilarPattern>(command);
        return results.AsList();
    }

    /// <summary>
    /// Records transfer learning bootstrap for audit trail.
    /// </summary>
    private async Task RecordTransferLearningAsync(
        string newPatternId,
        double bootstrappedConfidence,
        List<SimilarPattern> similarPatterns,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var insertQuery = @"
            INSERT INTO transfer_learning_bootstraps (
                pattern_id,
                bootstrapped_confidence,
                source_patterns,
                bootstrap_timestamp
            )
            VALUES (
                @PatternId,
                @Confidence,
                @SourcePatterns::jsonb,
                NOW()
            )
        ";

        var sourcePatternsJson = System.Text.Json.JsonSerializer.Serialize(
            similarPatterns.Select(p => new
            {
                pattern_id = p.PatternId,
                similarity_score = p.SimilarityScore,
                acceptance_rate = p.AcceptanceRate,
                total_interactions = p.TotalInteractions
            }));

        var command = new CommandDefinition(
            insertQuery,
            new
            {
                PatternId = newPatternId,
                Confidence = bootstrappedConfidence,
                SourcePatterns = sourcePatternsJson
            },
            cancellationToken: cancellationToken);

        await connection.ExecuteAsync(command);
    }

    /// <summary>
    /// Identifies patterns that need transfer learning (no or insufficient data).
    /// </summary>
    public async Task<List<ColdStartPattern>> IdentifyColdStartPatternsAsync(
        int minimumInteractions = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                WITH pattern_interaction_counts AS (
                    SELECT
                        pattern_id,
                        COUNT(*) as interaction_count,
                        MAX(recommended_at) as last_recommended
                    FROM pattern_interaction_feedback
                    WHERE recommended_at >= NOW() - INTERVAL '90 days'
                    GROUP BY pattern_id
                ),
                all_patterns AS (
                    SELECT DISTINCT pattern_id
                    FROM pattern_recommendations
                    WHERE status = 'approved'
                )
                SELECT
                    ap.pattern_id as PatternId,
                    COALESCE(pic.interaction_count, 0) as InteractionCount,
                    pic.last_recommended as LastRecommended,
                    CASE
                        WHEN pic.interaction_count IS NULL THEN 'no_data'
                        WHEN pic.interaction_count < @MinInteractions THEN 'insufficient_data'
                        ELSE 'sufficient_data'
                    END as Status
                FROM all_patterns ap
                LEFT JOIN pattern_interaction_counts pic ON ap.pattern_id = pic.pattern_id
                WHERE COALESCE(pic.interaction_count, 0) < @MinInteractions
                ORDER BY COALESCE(pic.interaction_count, 0) ASC, pic.last_recommended DESC
            ";

            var command = new CommandDefinition(
                query,
                new { MinInteractions = minimumInteractions },
                cancellationToken: cancellationToken);

            var results = await connection.QueryAsync<ColdStartPattern>(command);
            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to identify cold start patterns");
            return new List<ColdStartPattern>();
        }
    }
}

/// <summary>
/// Result of transfer learning bootstrap.
/// </summary>
public sealed class TransferLearningResult
{
    public string PatternId { get; init; } = string.Empty;
    public double BootstrappedConfidence { get; init; }
    public string BootstrapMethod { get; init; } = string.Empty;
    public List<SimilarPattern> SimilarPatterns { get; init; } = new();
    public string ConfidenceExplanation { get; init; } = string.Empty;
}

/// <summary>
/// Similar pattern for transfer learning.
/// </summary>
public sealed class SimilarPattern
{
    public string PatternId { get; init; } = string.Empty;
    public double SimilarityScore { get; init; }
    public double AcceptanceRate { get; init; }
    public double AvgConfidence { get; init; }
    public double AvgDecisionTime { get; init; }
    public int TotalInteractions { get; init; }
    public string CloudProvider { get; init; } = string.Empty;
    public int DataVolumeGb { get; init; }
    public int ConcurrentUsers { get; init; }
}

/// <summary>
/// Pattern identified as needing transfer learning.
/// </summary>
public sealed class ColdStartPattern
{
    public string PatternId { get; init; } = string.Empty;
    public int InteractionCount { get; init; }
    public DateTime? LastRecommended { get; init; }
    public string Status { get; init; } = string.Empty;
}
