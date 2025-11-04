// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Cli.AI.Services.VectorSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services;

/// <summary>
/// Service for approving pattern recommendations and indexing them in Azure AI Search.
/// Bridges PostgreSQL (approval workflow) and Azure AI Search (knowledge base).
/// </summary>
public sealed class PatternApprovalService
{
    private readonly IConfiguration _configuration;
    private readonly IDeploymentPatternKnowledgeStore _knowledgeStore;
    private readonly ILogger<PatternApprovalService> _logger;

    public PatternApprovalService(
        IConfiguration configuration,
        IDeploymentPatternKnowledgeStore knowledgeStore,
        ILogger<PatternApprovalService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _knowledgeStore = knowledgeStore ?? throw new ArgumentNullException(nameof(knowledgeStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all pending pattern recommendations awaiting human review.
    /// </summary>
    public async Task<List<PatternRecommendation>> GetPendingRecommendationsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenConnectionAsync(cancellationToken);

        var sql = """
            SELECT
                id,
                pattern_name,
                cloud_provider,
                region,
                configuration_json,
                applicability_json,
                evidence_json,
                analyzed_at,
                status
            FROM pattern_recommendations
            WHERE status = 'pending_review'
            ORDER BY analyzed_at DESC;
            """;

        var rows = await connection.QueryAsync<PatternRecommendation>(sql);

        _logger.LogInformation("Retrieved {Count} pending pattern recommendations", rows.AsList().Count);

        return rows.AsList();
    }

    /// <summary>
    /// Approves a pattern and indexes it in Azure AI Search.
    /// </summary>
    public async Task ApprovePatternAsync(
        Guid recommendationId,
        string approvedBy,
        string? reviewNotes = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenConnectionAsync(cancellationToken);

        // Get the recommendation
        var recommendation = await GetRecommendationByIdAsync(connection, recommendationId);
        if (recommendation == null)
        {
            throw new InvalidOperationException($"Pattern recommendation {recommendationId} not found");
        }

        if (recommendation.Status != "pending_review")
        {
            throw new InvalidOperationException(
                $"Pattern {recommendationId} is not pending review (status: {recommendation.Status})");
        }

        // Update status in PostgreSQL
        await connection.ExecuteAsync(
            """
            UPDATE pattern_recommendations
            SET status = 'approved',
                reviewed_by = @ReviewedBy,
                reviewed_at = NOW(),
                review_notes = @ReviewNotes
            WHERE id = @Id;
            """,
            new { Id = recommendationId, ReviewedBy = approvedBy, ReviewNotes = reviewNotes });

        _logger.LogInformation("Pattern {PatternName} approved by {ApprovedBy}",
            recommendation.PatternName, approvedBy);

        // Convert to DeploymentPattern and index in Azure AI Search
        var pattern = ConvertToDeploymentPattern(recommendation, approvedBy);

        try
        {
            await _knowledgeStore.IndexApprovedPatternAsync(pattern, cancellationToken);
            _logger.LogInformation("Pattern {PatternName} indexed in Azure AI Search", pattern.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index pattern {PatternName}", pattern.Name);

            // Rollback approval
            await connection.ExecuteAsync(
                "UPDATE pattern_recommendations SET status = 'pending_review' WHERE id = @Id",
                new { Id = recommendationId });

            throw;
        }
    }

    /// <summary>
    /// Rejects a pattern (not indexed).
    /// </summary>
    public async Task RejectPatternAsync(
        Guid recommendationId,
        string rejectedBy,
        string rejectionReason,
        CancellationToken cancellationToken = default)
    {
        using var connection = await OpenConnectionAsync(cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(
            """
            UPDATE pattern_recommendations
            SET status = 'rejected',
                reviewed_by = @ReviewedBy,
                reviewed_at = NOW(),
                review_notes = @RejectionReason
            WHERE id = @Id AND status = 'pending_review';
            """,
            new { Id = recommendationId, ReviewedBy = rejectedBy, RejectionReason = rejectionReason });

        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Pattern {recommendationId} not found or not pending review");
        }

        _logger.LogInformation("Pattern rejected by {RejectedBy}: {Reason}", rejectedBy, rejectionReason);
    }

    private async Task<PatternRecommendation?> GetRecommendationByIdAsync(IDbConnection connection, Guid id)
    {
        return await connection.QueryFirstOrDefaultAsync<PatternRecommendation>(
            """
            SELECT id, pattern_name, cloud_provider, region,
                   configuration_json, applicability_json, evidence_json, status
            FROM pattern_recommendations
            WHERE id = @Id;
            """,
            new { Id = id });
    }

    private static DeploymentPattern ConvertToDeploymentPattern(
        PatternRecommendation recommendation,
        string approvedBy)
    {
        var applicability = JsonSerializer.Deserialize<PatternApplicability>(recommendation.ApplicabilityJson)
            ?? throw new InvalidOperationException("Invalid applicability JSON");

        var evidence = JsonSerializer.Deserialize<PatternEvidence>(recommendation.EvidenceJson)
            ?? throw new InvalidOperationException("Invalid evidence JSON");

        var configuration = JsonSerializer.Deserialize<object>(recommendation.ConfigurationJson)
            ?? throw new InvalidOperationException("Invalid configuration JSON");

        return new DeploymentPattern
        {
            Id = recommendation.Id.ToString(),
            Name = recommendation.PatternName,
            CloudProvider = recommendation.CloudProvider,
            DataVolumeMin = applicability.DataVolumeMin,
            DataVolumeMax = applicability.DataVolumeMax,
            ConcurrentUsersMin = applicability.ConcurrentUsersMin,
            ConcurrentUsersMax = applicability.ConcurrentUsersMax,
            SuccessRate = evidence.SuccessRate,
            DeploymentCount = evidence.DeploymentCount,
            Configuration = configuration,
            HumanApproved = true,
            ApprovedBy = approvedBy,
            ApprovedDate = DateTime.UtcNow
        };
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

// DTOs

public sealed class PatternRecommendation
{
    public Guid Id { get; set; }
    public string PatternName { get; set; } = string.Empty;
    public string CloudProvider { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public string ApplicabilityJson { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class PatternApplicability
{
    public int DataVolumeMin { get; set; }
    public int DataVolumeMax { get; set; }
    public int ConcurrentUsersMin { get; set; }
    public int ConcurrentUsersMax { get; set; }
    public decimal BudgetMin { get; set; }
    public decimal BudgetMax { get; set; }
}

public sealed class PatternEvidence
{
    public double SuccessRate { get; set; }
    public int DeploymentCount { get; set; }
    public double AvgCostAccuracy { get; set; }
    public double AvgPerformance { get; set; }
    public double AvgCustomerSatisfaction { get; set; }
}
