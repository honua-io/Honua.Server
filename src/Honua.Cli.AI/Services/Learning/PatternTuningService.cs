// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.Learning;

/// <summary>
/// Automated pattern tuning service that analyzes user modifications to recommended configurations
/// and suggests improvements to pattern default values.
/// </summary>
public sealed class PatternTuningService
{
    private readonly string _connectionString;
    private readonly ILogger<PatternTuningService> _logger;

    // Thresholds for suggesting tuning
    private const int MinimumSamplesForTuning = 10;
    private const double MinimumModificationRate = 0.30;  // 30% of users modify this field
    private const double MinimumConsensusRate = 0.70;     // 70% modify to similar value

    public PatternTuningService(string connectionString, ILogger<PatternTuningService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes modification patterns and generates tuning recommendations.
    /// </summary>
    public async Task<List<PatternTuningRecommendation>> GenerateTuningRecommendationsAsync(
        string? patternId = null,
        TimeSpan? analysisWindow = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var window = analysisWindow ?? TimeSpan.FromDays(90);
            var recommendations = new List<PatternTuningRecommendation>();

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Get patterns with significant modification rates
            var patternsQuery = @"
                SELECT
                    pattern_id,
                    COUNT(*) as total_interactions,
                    COUNT(*) FILTER (WHERE was_modified = true) as modified_count,
                    AVG(CASE WHEN was_modified = true THEN 1.0 ELSE 0.0 END) as modification_rate
                FROM pattern_interaction_feedback
                WHERE recommended_at >= NOW() - @Window::interval
                  AND was_accepted = true
                  AND (@PatternId IS NULL OR pattern_id = @PatternId)
                GROUP BY pattern_id
                HAVING COUNT(*) >= @MinSamples
                   AND AVG(CASE WHEN was_modified = true THEN 1.0 ELSE 0.0 END) >= @MinModRate
                ORDER BY modification_rate DESC
            ";

            var patternsCommand = new CommandDefinition(
                patternsQuery,
                new
                {
                    Window = $"{window.TotalMinutes} minutes",
                    PatternId = patternId,
                    MinSamples = MinimumSamplesForTuning,
                    MinModRate = MinimumModificationRate
                },
                cancellationToken: cancellationToken);

            var patterns = await connection.QueryAsync<dynamic>(patternsCommand);

            foreach (var pattern in patterns)
            {
                string pid = pattern.pattern_id;
                int totalInteractions = pattern.total_interactions;
                int modifiedCount = pattern.modified_count;
                double modificationRate = pattern.modification_rate;

                // Analyze specific field modifications for this pattern
                var fieldRecommendations = await AnalyzeFieldModificationsAsync(
                    pid,
                    totalInteractions,
                    window,
                    cancellationToken);

                if (fieldRecommendations.Any())
                {
                    recommendations.Add(new PatternTuningRecommendation
                    {
                        PatternId = pid,
                        TotalSamples = totalInteractions,
                        ModifiedCount = modifiedCount,
                        ModificationRate = modificationRate,
                        FieldRecommendations = fieldRecommendations,
                        Priority = CalculatePriority(modificationRate, totalInteractions),
                        GeneratedAt = DateTime.UtcNow
                    });
                }
            }

            _logger.LogInformation(
                "Generated {Count} pattern tuning recommendations from {Patterns} patterns",
                recommendations.Sum(r => r.FieldRecommendations.Count),
                recommendations.Count);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate tuning recommendations");
            return new List<PatternTuningRecommendation>();
        }
    }

    /// <summary>
    /// Analyzes specific field modifications for a pattern.
    /// </summary>
    private async Task<List<FieldTuningRecommendation>> AnalyzeFieldModificationsAsync(
        string patternId,
        int totalInteractions,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        var recommendations = new List<FieldTuningRecommendation>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Get all modifications for this pattern
        var query = @"
            SELECT
                config_modifications,
                recommended_config,
                actual_config
            FROM pattern_interaction_feedback
            WHERE pattern_id = @PatternId
              AND was_modified = true
              AND config_modifications IS NOT NULL
              AND recommended_at >= NOW() - @Window::interval
        ";

        var command = new CommandDefinition(
            query,
            new
            {
                PatternId = patternId,
                Window = $"{window.TotalMinutes} minutes"
            },
            cancellationToken: cancellationToken);

        var modifications = await connection.QueryAsync<dynamic>(command);

        // Aggregate modifications by field
        var fieldModifications = new Dictionary<string, List<object>>();

        foreach (var mod in modifications)
        {
            string? configModsJson = mod.config_modifications;
            if (string.IsNullOrEmpty(configModsJson)) continue;

            try
            {
                var modDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configModsJson);
                if (modDict == null) continue;

                foreach (var (field, value) in modDict)
                {
                    if (!fieldModifications.ContainsKey(field))
                        fieldModifications[field] = new List<object>();

                    // Extract the value (handling different JSON types)
                    object? actualValue = value.ValueKind switch
                    {
                        JsonValueKind.Number => value.GetDouble(),
                        JsonValueKind.String => value.GetString(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => value.ToString()
                    };

                    if (actualValue != null)
                        fieldModifications[field].Add(actualValue);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse config modifications for pattern {PatternId}", patternId);
            }
        }

        // Analyze each field for consensus
        foreach (var (field, values) in fieldModifications)
        {
            var modificationCount = values.Count;
            var modificationRate = (double)modificationCount / totalInteractions;

            if (modificationRate < MinimumModificationRate)
                continue;

            // Check for value consensus
            var valueCounts = values
                .GroupBy(v => v.ToString())
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            var mostCommonValue = valueCounts.First();
            var consensusRate = (double)mostCommonValue.Count / modificationCount;

            if (consensusRate >= MinimumConsensusRate)
            {
                recommendations.Add(new FieldTuningRecommendation
                {
                    FieldName = field,
                    CurrentDefaultValue = "(varies)",  // Would need to query recommended_config
                    SuggestedValue = mostCommonValue.Value ?? "null",
                    ModificationCount = modificationCount,
                    ModificationRate = modificationRate,
                    ConsensusRate = consensusRate,
                    Confidence = CalculateFieldConfidence(modificationRate, consensusRate, modificationCount),
                    Explanation = $"{modificationCount} users ({modificationRate:P0}) modified this field, " +
                        $"{consensusRate:P0} changed it to '{mostCommonValue.Value}'"
                });
            }
        }

        return recommendations.OrderByDescending(r => r.Confidence).ToList();
    }

    /// <summary>
    /// Applies tuning recommendations to update pattern defaults.
    /// Requires human approval before applying.
    /// </summary>
    public async Task<PatternTuningResult> ApplyTuningRecommendationAsync(
        PatternTuningRecommendation recommendation,
        List<string> approvedFields,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var appliedFields = new List<string>();
            var failedFields = new List<string>();

            foreach (var field in approvedFields)
            {
                var fieldRec = recommendation.FieldRecommendations
                    .FirstOrDefault(f => f.FieldName == field);

                if (fieldRec == null)
                {
                    failedFields.Add(field);
                    continue;
                }

                // Record the tuning application
                var insertQuery = @"
                    INSERT INTO pattern_tuning_history (
                        pattern_id,
                        field_name,
                        old_value,
                        new_value,
                        modification_count,
                        modification_rate,
                        consensus_rate,
                        confidence,
                        approved_by,
                        applied_at
                    )
                    VALUES (
                        @PatternId,
                        @FieldName,
                        @OldValue,
                        @NewValue,
                        @ModificationCount,
                        @ModificationRate,
                        @ConsensusRate,
                        @Confidence,
                        @ApprovedBy,
                        NOW()
                    )
                ";

                var command = new CommandDefinition(
                    insertQuery,
                    new
                    {
                        recommendation.PatternId,
                        fieldRec.FieldName,
                        OldValue = fieldRec.CurrentDefaultValue,
                        NewValue = fieldRec.SuggestedValue,
                        fieldRec.ModificationCount,
                        fieldRec.ModificationRate,
                        fieldRec.ConsensusRate,
                        fieldRec.Confidence,
                        ApprovedBy = approvedBy
                    },
                    cancellationToken: cancellationToken);

                await connection.ExecuteAsync(command);
                appliedFields.Add(field);

                _logger.LogInformation(
                    "Applied tuning for pattern {PatternId}, field {Field}: {OldValue} -> {NewValue}",
                    recommendation.PatternId, field, fieldRec.CurrentDefaultValue, fieldRec.SuggestedValue);
            }

            return new PatternTuningResult
            {
                PatternId = recommendation.PatternId,
                AppliedFields = appliedFields,
                FailedFields = failedFields,
                Success = failedFields.Count == 0,
                Message = $"Applied {appliedFields.Count} field tunings, {failedFields.Count} failed"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply tuning recommendation");
            return new PatternTuningResult
            {
                PatternId = recommendation.PatternId,
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    private TuningPriority CalculatePriority(double modificationRate, int sampleCount)
    {
        if (modificationRate >= 0.7 && sampleCount >= 20)
            return TuningPriority.Critical;
        if (modificationRate >= 0.5 && sampleCount >= 15)
            return TuningPriority.High;
        if (modificationRate >= 0.4 && sampleCount >= 10)
            return TuningPriority.Medium;
        return TuningPriority.Low;
    }

    private double CalculateFieldConfidence(double modificationRate, double consensusRate, int sampleCount)
    {
        // Confidence = weighted combination of modification rate, consensus, and sample size
        var modificationScore = modificationRate;  // 0.0 - 1.0
        var consensusScore = consensusRate;        // 0.0 - 1.0
        var sampleScore = Math.Min(1.0, sampleCount / 30.0);  // 0.0 - 1.0 (capped at 30 samples)

        return (modificationScore * 0.3) + (consensusScore * 0.5) + (sampleScore * 0.2);
    }
}

/// <summary>
/// Pattern tuning recommendation.
/// </summary>
public sealed class PatternTuningRecommendation
{
    public string PatternId { get; init; } = string.Empty;
    public int TotalSamples { get; init; }
    public int ModifiedCount { get; init; }
    public double ModificationRate { get; init; }
    public List<FieldTuningRecommendation> FieldRecommendations { get; init; } = new();
    public TuningPriority Priority { get; init; }
    public DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Field-level tuning recommendation.
/// </summary>
public sealed class FieldTuningRecommendation
{
    public string FieldName { get; init; } = string.Empty;
    public string CurrentDefaultValue { get; init; } = string.Empty;
    public string SuggestedValue { get; init; } = string.Empty;
    public int ModificationCount { get; init; }
    public double ModificationRate { get; init; }
    public double ConsensusRate { get; init; }
    public double Confidence { get; init; }
    public string Explanation { get; init; } = string.Empty;
}

/// <summary>
/// Result of applying tuning recommendations.
/// </summary>
public sealed class PatternTuningResult
{
    public string PatternId { get; init; } = string.Empty;
    public List<string> AppliedFields { get; init; } = new();
    public List<string> FailedFields { get; init; } = new();
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public enum TuningPriority
{
    Low,
    Medium,
    High,
    Critical
}
