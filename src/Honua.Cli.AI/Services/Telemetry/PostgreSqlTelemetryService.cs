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

namespace Honua.Cli.AI.Services.Telemetry;

/// <summary>
/// PostgreSQL-backed telemetry service for pattern usage tracking and learning loop.
/// Stores telemetry events in a relational database for analytics and reinforcement learning.
/// </summary>
public sealed class PostgreSqlTelemetryService : IPatternUsageTelemetry
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlTelemetryService> _logger;

    public PostgreSqlTelemetryService(string connectionString, ILogger<PostgreSqlTelemetryService> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes the database schema for telemetry storage.
    /// </summary>
    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Create telemetry events table
            var createEventsTable = @"
                CREATE TABLE IF NOT EXISTS honua_telemetry_events (
                    id BIGSERIAL PRIMARY KEY,
                    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    event_type VARCHAR(100) NOT NULL,
                    agent_name VARCHAR(200),
                    pattern_name VARCHAR(200),
                    success BOOLEAN NOT NULL,
                    duration_ms INTEGER,
                    context JSONB,
                    error_message TEXT,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_events_timestamp ON honua_telemetry_events(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_events_agent ON honua_telemetry_events(agent_name);
                CREATE INDEX IF NOT EXISTS idx_events_pattern ON honua_telemetry_events(pattern_name);
                CREATE INDEX IF NOT EXISTS idx_events_success ON honua_telemetry_events(success);
            ";

            // Create agent performance aggregates table
            var createAggregatesTable = @"
                CREATE TABLE IF NOT EXISTS honua_agent_performance (
                    id BIGSERIAL PRIMARY KEY,
                    agent_name VARCHAR(200) NOT NULL,
                    pattern_name VARCHAR(200) NOT NULL,
                    total_executions INTEGER NOT NULL DEFAULT 0,
                    successful_executions INTEGER NOT NULL DEFAULT 0,
                    failed_executions INTEGER NOT NULL DEFAULT 0,
                    avg_duration_ms DECIMAL(10,2),
                    last_execution_at TIMESTAMPTZ,
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    UNIQUE(agent_name, pattern_name)
                );

                CREATE INDEX IF NOT EXISTS idx_performance_agent ON honua_agent_performance(agent_name);
                CREATE INDEX IF NOT EXISTS idx_performance_success_rate ON honua_agent_performance((successful_executions::DECIMAL / NULLIF(total_executions, 0)) DESC);
            ";

            // Create LLM provider routing decisions table
            var createRoutingTable = @"
                CREATE TABLE IF NOT EXISTS honua_llm_routing_decisions (
                    id BIGSERIAL PRIMARY KEY,
                    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                    task_type VARCHAR(200) NOT NULL,
                    selected_provider VARCHAR(100) NOT NULL,
                    fallback_used BOOLEAN NOT NULL DEFAULT false,
                    success BOOLEAN NOT NULL,
                    latency_ms INTEGER,
                    token_count INTEGER,
                    cost_usd DECIMAL(10,6),
                    context JSONB,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
                );

                CREATE INDEX IF NOT EXISTS idx_routing_timestamp ON honua_llm_routing_decisions(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_routing_provider ON honua_llm_routing_decisions(selected_provider);
                CREATE INDEX IF NOT EXISTS idx_routing_task_type ON honua_llm_routing_decisions(task_type);
            ";

            await connection.ExecuteAsync(createEventsTable);
            await connection.ExecuteAsync(createAggregatesTable);
            await connection.ExecuteAsync(createRoutingTable);

            _logger.LogInformation("PostgreSQL telemetry database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PostgreSQL telemetry database");
            throw;
        }
    }

    /// <inheritdoc />
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, pattern_name, success, context)
                VALUES (@EventType, @PatternId, @WasAccepted, @Context::jsonb)
            ";

            var context = new Dictionary<string, object>
            {
                ["patternId"] = patternId,
                ["rank"] = rank,
                ["confidenceOverall"] = confidence.Overall,
                ["confidenceLevel"] = confidence.Level,
                ["vectorSimilarity"] = confidence.VectorSimilarity,
                ["successRate"] = confidence.SuccessRate,
                ["deploymentCount"] = confidence.DeploymentCount,
                ["dataVolumeGb"] = requirements.DataVolumeGb,
                ["concurrentUsers"] = requirements.ConcurrentUsers,
                ["cloudProvider"] = requirements.CloudProvider,
                ["region"] = requirements.Region
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "pattern_recommendation",
                    PatternId = patternId,
                    WasAccepted = wasAccepted,
                    Context = System.Text.Json.JsonSerializer.Serialize(context)
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked recommendation: {PatternId} rank={Rank} accepted={Accepted}",
                patternId, rank, wasAccepted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track pattern recommendation");
            // Don't throw - telemetry failures shouldn't break the application
        }
    }

    /// <inheritdoc />
    public async Task TrackDeploymentOutcomeAsync(
        string patternId,
        bool success,
        string? feedback = null,
        string? deploymentMetadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, pattern_name, success, context, error_message)
                VALUES (@EventType, @PatternId, @Success, @Context::jsonb, @Feedback)
            ";

            var context = new Dictionary<string, object>
            {
                ["patternId"] = patternId,
                ["deploymentMetadata"] = deploymentMetadata ?? string.Empty
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "deployment_outcome",
                    PatternId = patternId,
                    Success = success,
                    Context = System.Text.Json.JsonSerializer.Serialize(context),
                    Feedback = feedback
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked deployment outcome: {PatternId} success={Success}",
                patternId, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track deployment outcome");
        }
    }

    /// <inheritdoc />
    public async Task<double?> GetPatternAcceptanceRateAsync(
        string patternId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    COUNT(*) FILTER (WHERE success = true) as accepted,
                    COUNT(*) as total
                FROM honua_telemetry_events
                WHERE event_type = 'pattern_recommendation'
                  AND pattern_name = @PatternId
            ";

            var command = new CommandDefinition(
                query,
                new { PatternId = patternId },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleOrDefaultAsync<(int Accepted, int Total)>(command);

            if (result.Total == 0)
                return null;

            return (double)result.Accepted / result.Total;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern acceptance rate");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<PatternUsageStats> GetUsageStatsAsync(
        string patternId,
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                WITH recommendations AS (
                    SELECT
                        COUNT(*) as times_recommended,
                        COUNT(*) FILTER (WHERE success = true) as times_accepted
                    FROM honua_telemetry_events
                    WHERE event_type = 'pattern_recommendation'
                      AND pattern_name = @PatternId
                      AND timestamp >= NOW() - @Period::interval
                ),
                deployments AS (
                    SELECT
                        COUNT(*) as times_deployed,
                        COUNT(*) FILTER (WHERE success = true) as successful_deployments,
                        COUNT(*) FILTER (WHERE success = false) as failed_deployments
                    FROM honua_telemetry_events
                    WHERE event_type = 'deployment_outcome'
                      AND pattern_name = @PatternId
                      AND timestamp >= NOW() - @Period::interval
                )
                SELECT
                    COALESCE(r.times_recommended, 0) as TimesRecommended,
                    COALESCE(r.times_accepted, 0) as TimesAccepted,
                    COALESCE(d.times_deployed, 0) as TimesDeployed,
                    COALESCE(d.successful_deployments, 0) as SuccessfulDeployments,
                    COALESCE(d.failed_deployments, 0) as FailedDeployments
                FROM recommendations r
                CROSS JOIN deployments d
            ";

            var statsCommand = new CommandDefinition(
                query,
                new
                {
                    PatternId = patternId,
                    Period = $"{period.TotalMinutes} minutes"
                },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleOrDefaultAsync<dynamic>(statsCommand);

            if (result == null)
                return new PatternUsageStats { PatternId = patternId };

            return new PatternUsageStats
            {
                PatternId = patternId,
                TimesRecommended = result.timesrecommended ?? 0,
                TimesAccepted = result.timesaccepted ?? 0,
                TimesDeployed = result.timesdeployed ?? 0,
                SuccessfulDeployments = result.successfuldeployments ?? 0,
                FailedDeployments = result.faileddeployments ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern usage stats");
            return new PatternUsageStats { PatternId = patternId };
        }
    }

    /// <inheritdoc />
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, agent_name, pattern_name, success, duration_ms, context, error_message)
                VALUES (@EventType, @AgentName, @TaskType, @Success, @ExecutionTimeMs, @Context::jsonb, @Feedback)
            ";

            var context = new Dictionary<string, object>
            {
                ["taskType"] = taskType,
                ["confidenceScore"] = confidenceScore
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "agent_performance",
                    AgentName = agentName,
                    TaskType = taskType,
                    Success = success,
                    ExecutionTimeMs = executionTimeMs,
                    Context = System.Text.Json.JsonSerializer.Serialize(context),
                    Feedback = feedback
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked agent performance: {AgentName}/{TaskType} success={Success} time={TimeMs}ms",
                agentName, taskType, success, executionTimeMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track agent performance");
        }
    }

    /// <inheritdoc />
    public async Task<Agents.AgentPerformanceStats> GetAgentPerformanceAsync(
        string agentName,
        TimeSpan period,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    COUNT(*) as TotalInteractions,
                    COUNT(*) FILTER (WHERE success = true) as SuccessfulInteractions,
                    COUNT(*) FILTER (WHERE success = false) as FailedInteractions,
                    AVG((context->>'confidenceScore')::double precision) as AverageConfidence,
                    AVG(duration_ms) as AvgDurationMs
                FROM honua_telemetry_events
                WHERE event_type = 'agent_performance'
                  AND agent_name = @AgentName
                  AND timestamp >= NOW() - @Period::interval
            ";

            var command = new CommandDefinition(
                query,
                new
                {
                    AgentName = agentName,
                    Period = $"{period.TotalMinutes} minutes"
                },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleOrDefaultAsync<dynamic>(command);

            if (result == null)
                return new Agents.AgentPerformanceStats { AgentName = agentName };

            return new Agents.AgentPerformanceStats
            {
                AgentName = agentName,
                TotalInteractions = result.totalinteractions ?? 0,
                SuccessfulInteractions = result.successfulinteractions ?? 0,
                FailedInteractions = result.failedinteractions ?? 0,
                AverageConfidence = result.averageconfidence ?? 0.0,
                AverageExecutionTime = TimeSpan.FromMilliseconds(result.avgdurationms ?? 0.0)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent performance stats");
            return new Agents.AgentPerformanceStats { AgentName = agentName };
        }
    }

    /// <inheritdoc />
    public async Task<double> GetAgentTaskMatchScoreAsync(
        string agentName,
        string taskType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var query = @"
                SELECT
                    COUNT(*) as total,
                    COUNT(*) FILTER (WHERE success = true) as successful,
                    AVG((context->>'confidenceScore')::double precision) as avg_confidence,
                    AVG(duration_ms) as avg_duration
                FROM honua_telemetry_events
                WHERE event_type = 'agent_performance'
                  AND agent_name = @AgentName
                  AND pattern_name = @TaskType
                  AND timestamp >= NOW() - INTERVAL '30 days'
            ";

            var command = new CommandDefinition(
                query,
                new { AgentName = agentName, TaskType = taskType },
                cancellationToken: cancellationToken);

            var result = await connection.QuerySingleOrDefaultAsync<dynamic>(command);

            if (result == null || result.total == 0)
                return 0.5; // Default neutral score for no data

            int total = result.total;
            int successful = result.successful ?? 0;
            double avgConfidence = result.avg_confidence ?? 0.5;
            double avgDuration = result.avg_duration ?? 5000.0;

            // Calculate match score:
            // 50% success rate
            // 30% confidence score
            // 20% speed (inverse of duration, capped at 10s)
            double successRate = (double)successful / total;
            double speedScore = Math.Max(0, 1.0 - (avgDuration / 10000.0));

            double matchScore = (successRate * 0.5) + (avgConfidence * 0.3) + (speedScore * 0.2);

            return Math.Clamp(matchScore, 0.0, 1.0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent task match score");
            return 0.5;
        }
    }

    /// <inheritdoc />
    public async Task TrackArchitectureSwarmAsync(
        string request,
        List<string> optionsPresented,
        string? userSelection,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, success, context)
                VALUES (@EventType, @Success, @Context::jsonb)
            ";

            var context = new Dictionary<string, object>
            {
                ["request"] = request,
                ["optionsPresented"] = optionsPresented,
                ["userSelection"] = userSelection ?? string.Empty,
                ["optionsCount"] = optionsPresented.Count,
                ["metadata"] = metadata ?? new Dictionary<string, object>()
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "architecture_swarm",
                    Success = !string.IsNullOrEmpty(userSelection),
                    Context = System.Text.Json.JsonSerializer.Serialize(context)
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked architecture swarm: {OptionsCount} options, selected={Selected}",
                optionsPresented.Count, userSelection ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track architecture swarm");
        }
    }

    /// <inheritdoc />
    public async Task TrackReviewOutcomeAsync(
        string reviewType,
        string patternId,
        bool approved,
        int issuesFound,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, pattern_name, success, context)
                VALUES (@EventType, @PatternId, @Approved, @Context::jsonb)
            ";

            var context = new Dictionary<string, object>
            {
                ["reviewType"] = reviewType,
                ["patternId"] = patternId,
                ["issuesFound"] = issuesFound,
                ["metadata"] = metadata ?? new Dictionary<string, object>()
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "review_outcome",
                    PatternId = patternId,
                    Approved = approved,
                    Context = System.Text.Json.JsonSerializer.Serialize(context)
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked review outcome: {ReviewType} for {PatternId} approved={Approved} issues={Issues}",
                reviewType, patternId, approved, issuesFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track review outcome");
        }
    }

    /// <inheritdoc />
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
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, success, duration_ms, context)
                VALUES (@EventType, @Success, @DurationMs, @Context::jsonb)
            ";

            var context = new Dictionary<string, object>
            {
                ["strategy"] = strategy,
                ["phasesCreated"] = phasesCreated,
                ["tasksCreated"] = tasksCreated
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "decomposition",
                    Success = successful,
                    DurationMs = (int)duration.TotalMilliseconds,
                    Context = System.Text.Json.JsonSerializer.Serialize(context)
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked decomposition: {Strategy} phases={Phases} tasks={Tasks} successful={Success}",
                strategy, phasesCreated, tasksCreated, successful);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track decomposition");
        }
    }

    /// <inheritdoc />
    public async Task TrackValidationLoopAsync(
        string action,
        int iterationsNeeded,
        bool ultimatelySucceeded,
        List<string> failureReasons,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var insertQuery = @"
                INSERT INTO honua_telemetry_events
                (event_type, success, context)
                VALUES (@EventType, @Success, @Context::jsonb)
            ";

            var context = new Dictionary<string, object>
            {
                ["action"] = action,
                ["iterationsNeeded"] = iterationsNeeded,
                ["failureReasons"] = failureReasons
            };

            var command = new CommandDefinition(
                insertQuery,
                new
                {
                    EventType = "validation_loop",
                    Success = ultimatelySucceeded,
                    Context = System.Text.Json.JsonSerializer.Serialize(context)
                },
                cancellationToken: cancellationToken);

            await connection.ExecuteAsync(command);

            _logger.LogDebug("Tracked validation loop: {Action} iterations={Iterations} succeeded={Success}",
                action, iterationsNeeded, ultimatelySucceeded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track validation loop");
        }
    }

    /// <summary>
    /// Records pattern usage telemetry.
    /// </summary>
    public async Task RecordPatternUsageAsync(
        string agentName,
        string patternName,
        bool success,
        TimeSpan duration,
        Dictionary<string, object>? context = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            // Insert telemetry event
            var insertEvent = @"
                INSERT INTO honua_telemetry_events
                (event_type, agent_name, pattern_name, success, duration_ms, context)
                VALUES (@EventType, @AgentName, @PatternName, @Success, @DurationMs, @Context::jsonb)
            ";

            await connection.ExecuteAsync(insertEvent, new
            {
                EventType = "pattern_usage",
                AgentName = agentName,
                PatternName = patternName,
                Success = success,
                DurationMs = (int)duration.TotalMilliseconds,
                Context = context != null ? System.Text.Json.JsonSerializer.Serialize(context) : null
            });

            // Update aggregates
            var upsertAggregate = @"
                INSERT INTO honua_agent_performance
                (agent_name, pattern_name, total_executions, successful_executions, failed_executions, avg_duration_ms, last_execution_at)
                VALUES (@AgentName, @PatternName, 1, @SuccessCount, @FailCount, @DurationMs, NOW())
                ON CONFLICT (agent_name, pattern_name)
                DO UPDATE SET
                    total_executions = honua_agent_performance.total_executions + 1,
                    successful_executions = honua_agent_performance.successful_executions + @SuccessCount,
                    failed_executions = honua_agent_performance.failed_executions + @FailCount,
                    avg_duration_ms = (honua_agent_performance.avg_duration_ms * honua_agent_performance.total_executions + @DurationMs) / (honua_agent_performance.total_executions + 1),
                    last_execution_at = NOW(),
                    updated_at = NOW()
            ";

            await connection.ExecuteAsync(upsertAggregate, new
            {
                AgentName = agentName,
                PatternName = patternName,
                SuccessCount = success ? 1 : 0,
                FailCount = success ? 0 : 1,
                DurationMs = (int)duration.TotalMilliseconds
            });

            _logger.LogDebug("Recorded pattern usage: {Agent}/{Pattern} - {Success}", agentName, patternName, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record pattern usage to PostgreSQL");
            // Don't throw - telemetry failures shouldn't break the application
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, int>> GetPatternUsageCountsAsync(string agentName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT pattern_name, total_executions
                FROM honua_agent_performance
                WHERE agent_name = @AgentName
                ORDER BY total_executions DESC
            ";

            var results = await connection.QueryAsync<(string PatternName, int Count)>(query, new { AgentName = agentName });

            var counts = new Dictionary<string, int>();
            foreach (var (patternName, count) in results)
            {
                counts[patternName] = count;
            }

            return counts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern usage counts from PostgreSQL");
            return new Dictionary<string, int>();
        }
    }

    /// <inheritdoc />
    public async Task<double> GetPatternSuccessRateAsync(string agentName, string patternName)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT
                    CASE
                        WHEN total_executions = 0 THEN 0.0
                        ELSE successful_executions::DECIMAL / total_executions
                    END as success_rate
                FROM honua_agent_performance
                WHERE agent_name = @AgentName AND pattern_name = @PatternName
            ";

            var successRate = await connection.QuerySingleOrDefaultAsync<double?>(query, new { AgentName = agentName, PatternName = patternName });

            return successRate ?? 0.5; // Default to 0.5 if no data
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pattern success rate from PostgreSQL");
            return 0.5;
        }
    }

    /// <summary>
    /// Records LLM provider routing decision for reinforcement learning.
    /// </summary>
    public async Task RecordLlmRoutingDecisionAsync(
        string taskType,
        string selectedProvider,
        bool fallbackUsed,
        bool success,
        int latencyMs,
        int? tokenCount = null,
        decimal? costUsd = null,
        Dictionary<string, object>? context = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var insertDecision = @"
                INSERT INTO honua_llm_routing_decisions
                (task_type, selected_provider, fallback_used, success, latency_ms, token_count, cost_usd, context)
                VALUES (@TaskType, @Provider, @FallbackUsed, @Success, @LatencyMs, @TokenCount, @CostUsd, @Context::jsonb)
            ";

            await connection.ExecuteAsync(insertDecision, new
            {
                TaskType = taskType,
                Provider = selectedProvider,
                FallbackUsed = fallbackUsed,
                Success = success,
                LatencyMs = latencyMs,
                TokenCount = tokenCount,
                CostUsd = costUsd,
                Context = context != null ? System.Text.Json.JsonSerializer.Serialize(context) : null
            });

            _logger.LogDebug("Recorded LLM routing decision: {TaskType} -> {Provider} ({Success})", taskType, selectedProvider, success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record LLM routing decision");
        }
    }

    /// <summary>
    /// Gets provider performance statistics for reinforcement learning.
    /// </summary>
    public async Task<Dictionary<string, ProviderStats>> GetProviderStatsAsync(string taskType, TimeSpan timeWindow)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = @"
                SELECT
                    selected_provider as Provider,
                    COUNT(*) as TotalRequests,
                    SUM(CASE WHEN success THEN 1 ELSE 0 END) as SuccessfulRequests,
                    AVG(latency_ms) as AvgLatencyMs,
                    AVG(CASE WHEN token_count IS NOT NULL THEN token_count ELSE NULL END) as AvgTokens,
                    SUM(COALESCE(cost_usd, 0)) as TotalCostUsd
                FROM honua_llm_routing_decisions
                WHERE task_type = @TaskType
                  AND timestamp >= NOW() - @TimeWindow::interval
                GROUP BY selected_provider
            ";

            var results = await connection.QueryAsync<ProviderStats>(query, new
            {
                TaskType = taskType,
                TimeWindow = $"{timeWindow.TotalMinutes} minutes"
            });

            var stats = new Dictionary<string, ProviderStats>();
            foreach (var stat in results)
            {
                stats[stat.Provider] = stat;
            }

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get provider stats from PostgreSQL");
            return new Dictionary<string, ProviderStats>();
        }
    }

    /// <summary>
    /// Gets top performing patterns for a given time window.
    /// </summary>
    public async Task<List<PatternPerformance>> GetTopPerformingPatternsAsync(int limit = 10, TimeSpan? timeWindow = null)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync().ConfigureAwait(false);

            var query = timeWindow.HasValue
                ? @"
                    SELECT
                        agent_name as AgentName,
                        pattern_name as PatternName,
                        COUNT(*) as ExecutionCount,
                        AVG(CASE WHEN success THEN 1.0 ELSE 0.0 END) as SuccessRate,
                        AVG(duration_ms) as AvgDurationMs
                    FROM honua_telemetry_events
                    WHERE timestamp >= NOW() - @TimeWindow::interval
                    GROUP BY agent_name, pattern_name
                    ORDER BY SuccessRate DESC, ExecutionCount DESC
                    LIMIT @Limit
                "
                : @"
                    SELECT
                        agent_name as AgentName,
                        pattern_name as PatternName,
                        total_executions as ExecutionCount,
                        CASE
                            WHEN total_executions = 0 THEN 0.0
                            ELSE successful_executions::DECIMAL / total_executions
                        END as SuccessRate,
                        avg_duration_ms as AvgDurationMs
                    FROM honua_agent_performance
                    ORDER BY SuccessRate DESC, ExecutionCount DESC
                    LIMIT @Limit
                ";

            var results = await connection.QueryAsync<PatternPerformance>(query, new
            {
                Limit = limit,
                TimeWindow = timeWindow.HasValue ? $"{timeWindow.Value.TotalMinutes} minutes" : null
            });

            return results.AsList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get top performing patterns");
            return new List<PatternPerformance>();
        }
    }
}

/// <summary>
/// LLM provider performance statistics.
/// </summary>
public sealed class ProviderStats
{
    public string Provider { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double AvgLatencyMs { get; set; }
    public double? AvgTokens { get; set; }
    public decimal TotalCostUsd { get; set; }

    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0.0;
}

/// <summary>
/// Pattern performance metrics.
/// </summary>
public sealed class PatternPerformance
{
    public string AgentName { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public double SuccessRate { get; set; }
    public double AvgDurationMs { get; set; }
}
