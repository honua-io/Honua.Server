// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Cli.AI.Services.Agents;

/// <summary>
/// Stores agent interaction history in PostgreSQL for long-term learning and context.
/// Enables the consultant to learn from past interactions and improve over time.
/// </summary>
public interface IAgentHistoryStore
{
    /// <summary>
    /// Saves an agent interaction to the history store.
    /// </summary>
    Task SaveInteractionAsync(
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
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all interactions for a specific session.
    /// </summary>
    Task<List<AgentHistoryRecord>> GetSessionHistoryAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent interactions for an agent.
    /// </summary>
    Task<List<AgentHistoryRecord>> GetAgentInteractionsAsync(
        string agentName,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a session summary when the session completes.
    /// </summary>
    Task SaveSessionSummaryAsync(
        string sessionId,
        string initialRequest,
        string finalOutcome,
        string[] agentsUsed,
        int interactionCount,
        int totalDurationMs,
        int? userSatisfaction = null,
        JsonDocument? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a session summary by ID.
    /// </summary>
    Task<AgentSessionSummary?> GetSessionSummaryAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent session summaries.
    /// </summary>
    Task<List<AgentSessionSummary>> GetRecentSessionsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single agent interaction stored in history.
/// </summary>
public sealed class AgentHistoryRecord
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string UserRequest { get; set; } = string.Empty;
    public string? AgentResponse { get; set; }
    public bool Success { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? TaskType { get; set; }
    public int? ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents a session summary.
/// </summary>
public sealed class AgentSessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string InitialRequest { get; set; } = string.Empty;
    public string FinalOutcome { get; set; } = string.Empty;
    public string[] AgentsUsed { get; set; } = Array.Empty<string>();
    public int InteractionCount { get; set; }
    public int TotalDurationMs { get; set; }
    public int? UserSatisfaction { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? MetadataJson { get; set; }
}
