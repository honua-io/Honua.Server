// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// PostgreSQL-based implementation of agent history storage.
/// Stores agent interactions and session summaries for long-term learning.
/// </summary>
public sealed class PostgresAgentHistoryStore : IAgentHistoryStore
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PostgresAgentHistoryStore> _logger;

    public PostgresAgentHistoryStore(
        IConfiguration configuration,
        ILogger<PostgresAgentHistoryStore> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveInteractionAsync(
        string sessionId,
        string agentName,
        string userRequest,
        string? agentResponse,
        bool success,
        double? confidenceScore = null,
        string? taskType = null,
        int? executionTimeMs = null,
        string? errorMessage = null,
        JsonDocument? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO agent_interactions
                    (session_id, agent_name, user_request, agent_response, success,
                     confidence_score, task_type, execution_time_ms, error_message, metadata)
                VALUES
                    (@SessionId, @AgentName, @UserRequest, @AgentResponse, @Success,
                     @ConfidenceScore, @TaskType, @ExecutionTimeMs, @ErrorMessage, @Metadata::jsonb)",
                new
                {
                    SessionId = sessionId,
                    AgentName = agentName,
                    UserRequest = userRequest,
                    AgentResponse = agentResponse,
                    Success = success,
                    ConfidenceScore = confidenceScore,
                    TaskType = taskType,
                    ExecutionTimeMs = executionTimeMs,
                    ErrorMessage = errorMessage,
                    Metadata = metadata?.RootElement.GetRawText()
                });

            _logger.LogInformation(
                "Saved agent interaction: {AgentName} in session {SessionId}, Success: {Success}",
                agentName,
                sessionId,
                success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save agent interaction");
            // Don't throw - history failures shouldn't break the workflow
        }
    }

    public async Task<List<AgentHistoryRecord>> GetSessionHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var interactions = await connection.QueryAsync<AgentHistoryRecord>(@"
                SELECT
                    id as Id,
                    session_id as SessionId,
                    agent_name as AgentName,
                    user_request as UserRequest,
                    agent_response as AgentResponse,
                    success as Success,
                    confidence_score as ConfidenceScore,
                    task_type as TaskType,
                    execution_time_ms as ExecutionTimeMs,
                    error_message as ErrorMessage,
                    metadata::text as MetadataJson,
                    created_at as CreatedAt
                FROM agent_interactions
                WHERE session_id = @SessionId
                ORDER BY created_at ASC",
                new { SessionId = sessionId });

            return interactions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session history for {SessionId}", sessionId);
            return new List<AgentHistoryRecord>();
        }
    }

    public async Task<List<AgentHistoryRecord>> GetAgentInteractionsAsync(
        string agentName,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var interactions = await connection.QueryAsync<AgentHistoryRecord>(@"
                SELECT
                    id as Id,
                    session_id as SessionId,
                    agent_name as AgentName,
                    user_request as UserRequest,
                    agent_response as AgentResponse,
                    success as Success,
                    confidence_score as ConfidenceScore,
                    task_type as TaskType,
                    execution_time_ms as ExecutionTimeMs,
                    error_message as ErrorMessage,
                    metadata::text as MetadataJson,
                    created_at as CreatedAt
                FROM agent_interactions
                WHERE agent_name = @AgentName
                ORDER BY created_at DESC
                LIMIT @Limit",
                new { AgentName = agentName, Limit = limit });

            return interactions.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve agent interactions for {AgentName}", agentName);
            return new List<AgentHistoryRecord>();
        }
    }

    public async Task SaveSessionSummaryAsync(
        string sessionId,
        string initialRequest,
        string finalOutcome,
        string[] agentsUsed,
        int interactionCount,
        int totalDurationMs,
        int? userSatisfaction = null,
        JsonDocument? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            await connection.ExecuteAsync(@"
                INSERT INTO agent_session_summaries
                    (session_id, initial_request, final_outcome, agents_used,
                     interaction_count, total_duration_ms, user_satisfaction, metadata, completed_at)
                VALUES
                    (@SessionId, @InitialRequest, @FinalOutcome, @AgentsUsed,
                     @InteractionCount, @TotalDurationMs, @UserSatisfaction, @Metadata::jsonb, NOW())
                ON CONFLICT (session_id) DO UPDATE SET
                    final_outcome = EXCLUDED.final_outcome,
                    agents_used = EXCLUDED.agents_used,
                    interaction_count = EXCLUDED.interaction_count,
                    total_duration_ms = EXCLUDED.total_duration_ms,
                    user_satisfaction = EXCLUDED.user_satisfaction,
                    metadata = EXCLUDED.metadata,
                    completed_at = NOW()",
                new
                {
                    SessionId = sessionId,
                    InitialRequest = initialRequest,
                    FinalOutcome = finalOutcome,
                    AgentsUsed = agentsUsed,
                    InteractionCount = interactionCount,
                    TotalDurationMs = totalDurationMs,
                    UserSatisfaction = userSatisfaction,
                    Metadata = metadata?.RootElement.GetRawText()
                });

            _logger.LogInformation(
                "Saved session summary: {SessionId}, Outcome: {Outcome}, Agents: {Agents}",
                sessionId,
                finalOutcome,
                string.Join(", ", agentsUsed));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session summary");
            // Don't throw - history failures shouldn't break the workflow
        }
    }

    public async Task<AgentSessionSummary?> GetSessionSummaryAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var summary = await connection.QueryFirstOrDefaultAsync<AgentSessionSummary>(@"
                SELECT
                    session_id as SessionId,
                    initial_request as InitialRequest,
                    final_outcome as FinalOutcome,
                    agents_used as AgentsUsed,
                    interaction_count as InteractionCount,
                    total_duration_ms as TotalDurationMs,
                    user_satisfaction as UserSatisfaction,
                    created_at as CreatedAt,
                    completed_at as CompletedAt,
                    metadata::text as MetadataJson
                FROM agent_session_summaries
                WHERE session_id = @SessionId",
                new { SessionId = sessionId });

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve session summary for {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<List<AgentSessionSummary>> GetRecentSessionsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var summaries = await connection.QueryAsync<AgentSessionSummary>(@"
                SELECT
                    session_id as SessionId,
                    initial_request as InitialRequest,
                    final_outcome as FinalOutcome,
                    agents_used as AgentsUsed,
                    interaction_count as InteractionCount,
                    total_duration_ms as TotalDurationMs,
                    user_satisfaction as UserSatisfaction,
                    created_at as CreatedAt,
                    completed_at as CompletedAt,
                    metadata::text as MetadataJson
                FROM agent_session_summaries
                ORDER BY created_at DESC
                LIMIT @Limit",
                new { Limit = limit });

            return summaries.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent sessions");
            return new List<AgentSessionSummary>();
        }
    }

    public async Task<List<AgentStats>> GetTopAgentsBySuccessRateAsync(int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);

            var query = @"
                SELECT
                    agent_name as AgentName,
                    COUNT(*) as TotalInteractions,
                    AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as SuccessRate,
                    AVG(execution_time_ms) as AverageExecutionTimeMs
                FROM agent_interactions
                GROUP BY agent_name
                HAVING COUNT(*) > 0
                ORDER BY AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) DESC, COUNT(*) DESC
                LIMIT @Limit";

            var stats = await connection.QueryAsync<AgentStats>(query, new { Limit = limit });
            return stats.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top agents by success rate");
            return new List<AgentStats>();
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
