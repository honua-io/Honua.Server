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

namespace Honua.Server.Enterprise.ETL.Scheduling;

/// <summary>
/// PostgreSQL-based implementation of workflow schedule store
/// </summary>
public class PostgresWorkflowScheduleStore : IWorkflowScheduleStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresWorkflowScheduleStore> _logger;

    public PostgresWorkflowScheduleStore(
        string connectionString,
        ILogger<PostgresWorkflowScheduleStore> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================================
    // Schedule CRUD Operations
    // ============================================================================

    public async Task<WorkflowSchedule> CreateScheduleAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geoetl_workflow_schedules (
                schedule_id, workflow_id, tenant_id, name, description, cron_expression,
                timezone, enabled, status, next_run_at, last_run_at, last_run_status,
                last_run_id, parameter_values, max_concurrent_executions, retry_attempts,
                retry_delay_minutes, expires_at, notification_config, tags,
                created_at, updated_at, created_by, updated_by
            ) VALUES (
                @ScheduleId, @WorkflowId, @TenantId, @Name, @Description, @CronExpression,
                @Timezone, @Enabled, @Status, @NextRunAt, @LastRunAt, @LastRunStatus,
                @LastRunId, @ParameterValues::jsonb, @MaxConcurrentExecutions, @RetryAttempts,
                @RetryDelayMinutes, @ExpiresAt, @NotificationConfig::jsonb, @Tags,
                @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy
            )
            RETURNING *";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Calculate initial next run time
            schedule.NextRunAt = schedule.CalculateNextRun();

            var row = await connection.QuerySingleAsync<ScheduleRow>(sql, new
            {
                ScheduleId = schedule.Id,
                WorkflowId = schedule.WorkflowId,
                TenantId = schedule.TenantId,
                Name = schedule.Name,
                Description = schedule.Description,
                CronExpression = schedule.CronExpression,
                Timezone = schedule.Timezone,
                Enabled = schedule.Enabled,
                Status = schedule.Status.ToString().ToLowerInvariant(),
                NextRunAt = schedule.NextRunAt,
                LastRunAt = schedule.LastRunAt,
                LastRunStatus = schedule.LastRunStatus,
                LastRunId = schedule.LastRunId,
                ParameterValues = schedule.ParameterValues != null ? JsonSerializer.Serialize(schedule.ParameterValues) : null,
                MaxConcurrentExecutions = schedule.MaxConcurrentExecutions,
                RetryAttempts = schedule.RetryAttempts,
                RetryDelayMinutes = schedule.RetryDelayMinutes,
                ExpiresAt = schedule.ExpiresAt,
                NotificationConfig = schedule.NotificationConfig != null ? JsonSerializer.Serialize(schedule.NotificationConfig) : null,
                Tags = schedule.Tags?.ToArray(),
                CreatedAt = schedule.CreatedAt,
                UpdatedAt = schedule.UpdatedAt,
                CreatedBy = schedule.CreatedBy,
                UpdatedBy = schedule.UpdatedBy
            });

            _logger.LogInformation("Created workflow schedule {ScheduleId} for workflow {WorkflowId}", schedule.Id, schedule.WorkflowId);
            return MapToSchedule(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    public async Task<WorkflowSchedule?> GetScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM geoetl_workflow_schedules WHERE schedule_id = @ScheduleId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleOrDefaultAsync<ScheduleRow>(sql, new { ScheduleId = scheduleId });
            return row != null ? MapToSchedule(row) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<WorkflowSchedule> UpdateScheduleAsync(
        WorkflowSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_workflow_schedules SET
                name = @Name,
                description = @Description,
                cron_expression = @CronExpression,
                timezone = @Timezone,
                enabled = @Enabled,
                status = @Status,
                next_run_at = @NextRunAt,
                parameter_values = @ParameterValues::jsonb,
                max_concurrent_executions = @MaxConcurrentExecutions,
                retry_attempts = @RetryAttempts,
                retry_delay_minutes = @RetryDelayMinutes,
                expires_at = @ExpiresAt,
                notification_config = @NotificationConfig::jsonb,
                tags = @Tags,
                updated_at = @UpdatedAt,
                updated_by = @UpdatedBy
            WHERE schedule_id = @ScheduleId
            RETURNING *";

        try
        {
            schedule.UpdatedAt = DateTimeOffset.UtcNow;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleAsync<ScheduleRow>(sql, new
            {
                ScheduleId = schedule.Id,
                Name = schedule.Name,
                Description = schedule.Description,
                CronExpression = schedule.CronExpression,
                Timezone = schedule.Timezone,
                Enabled = schedule.Enabled,
                Status = schedule.Status.ToString().ToLowerInvariant(),
                NextRunAt = schedule.NextRunAt,
                ParameterValues = schedule.ParameterValues != null ? JsonSerializer.Serialize(schedule.ParameterValues) : null,
                MaxConcurrentExecutions = schedule.MaxConcurrentExecutions,
                RetryAttempts = schedule.RetryAttempts,
                RetryDelayMinutes = schedule.RetryDelayMinutes,
                ExpiresAt = schedule.ExpiresAt,
                NotificationConfig = schedule.NotificationConfig != null ? JsonSerializer.Serialize(schedule.NotificationConfig) : null,
                Tags = schedule.Tags?.ToArray(),
                UpdatedAt = schedule.UpdatedAt,
                UpdatedBy = schedule.UpdatedBy
            });

            _logger.LogInformation("Updated workflow schedule {ScheduleId}", schedule.Id);
            return MapToSchedule(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow schedule {ScheduleId}", schedule.Id);
            throw;
        }
    }

    public async Task DeleteScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM geoetl_workflow_schedules WHERE schedule_id = @ScheduleId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new { ScheduleId = scheduleId });
            _logger.LogInformation("Deleted workflow schedule {ScheduleId}", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<List<WorkflowSchedule>> ListSchedulesAsync(
        Guid tenantId,
        int limit = 100,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_schedules
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @Limit OFFSET @Offset";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<ScheduleRow>(sql, new { TenantId = tenantId, Limit = limit, Offset = offset });
            return rows.Select(MapToSchedule).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workflow schedules for tenant {TenantId}", tenantId);
            throw;
        }
    }

    public async Task<List<WorkflowSchedule>> ListSchedulesByWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_schedules
            WHERE workflow_id = @WorkflowId
            ORDER BY created_at DESC";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<ScheduleRow>(sql, new { WorkflowId = workflowId });
            return rows.Select(MapToSchedule).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workflow schedules for workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    public async Task<List<WorkflowSchedule>> GetDueSchedulesAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_schedules
            WHERE enabled = TRUE
                AND status = 'active'
                AND next_run_at IS NOT NULL
                AND next_run_at <= @AsOf
                AND (expires_at IS NULL OR expires_at > @AsOf)
            ORDER BY next_run_at ASC";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<ScheduleRow>(sql, new { AsOf = asOf });
            return rows.Select(MapToSchedule).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting due schedules as of {AsOf}", asOf);
            throw;
        }
    }

    public async Task UpdateNextRunTimeAsync(
        Guid scheduleId,
        DateTimeOffset? nextRunAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_workflow_schedules
            SET next_run_at = @NextRunAt, updated_at = NOW()
            WHERE schedule_id = @ScheduleId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new { ScheduleId = scheduleId, NextRunAt = nextRunAt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating next run time for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task UpdateScheduleStatusAsync(
        Guid scheduleId,
        ScheduleStatus status,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_workflow_schedules
            SET status = @Status, updated_at = NOW()
            WHERE schedule_id = @ScheduleId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new { ScheduleId = scheduleId, Status = status.ToString().ToLowerInvariant() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task UpdateLastRunAsync(
        Guid scheduleId,
        Guid runId,
        string status,
        DateTimeOffset runAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_workflow_schedules
            SET last_run_at = @RunAt,
                last_run_status = @Status,
                last_run_id = @RunId,
                updated_at = NOW()
            WHERE schedule_id = @ScheduleId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new
            {
                ScheduleId = scheduleId,
                RunId = runId,
                Status = status,
                RunAt = runAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last run for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    // ============================================================================
    // Schedule Execution Operations
    // ============================================================================

    public async Task<ScheduleExecution> CreateExecutionAsync(
        ScheduleExecution execution,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geoetl_schedule_executions (
                execution_id, schedule_id, workflow_run_id, scheduled_at, executed_at,
                completed_at, status, error_message, retry_count, skipped, skip_reason
            ) VALUES (
                @ExecutionId, @ScheduleId, @WorkflowRunId, @ScheduledAt, @ExecutedAt,
                @CompletedAt, @Status, @ErrorMessage, @RetryCount, @Skipped, @SkipReason
            )
            RETURNING *";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleAsync<ExecutionRow>(sql, new
            {
                ExecutionId = execution.Id,
                ScheduleId = execution.ScheduleId,
                WorkflowRunId = execution.WorkflowRunId,
                ScheduledAt = execution.ScheduledAt,
                ExecutedAt = execution.ExecutedAt,
                CompletedAt = execution.CompletedAt,
                Status = execution.Status.ToString().ToLowerInvariant(),
                ErrorMessage = execution.ErrorMessage,
                RetryCount = execution.RetryCount,
                Skipped = execution.Skipped,
                SkipReason = execution.SkipReason
            });

            return MapToExecution(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule execution {ExecutionId}", execution.Id);
            throw;
        }
    }

    public async Task<ScheduleExecution> UpdateExecutionAsync(
        ScheduleExecution execution,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_schedule_executions SET
                workflow_run_id = @WorkflowRunId,
                executed_at = @ExecutedAt,
                completed_at = @CompletedAt,
                status = @Status,
                error_message = @ErrorMessage,
                retry_count = @RetryCount,
                skipped = @Skipped,
                skip_reason = @SkipReason
            WHERE execution_id = @ExecutionId
            RETURNING *";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleAsync<ExecutionRow>(sql, new
            {
                ExecutionId = execution.Id,
                WorkflowRunId = execution.WorkflowRunId,
                ExecutedAt = execution.ExecutedAt,
                CompletedAt = execution.CompletedAt,
                Status = execution.Status.ToString().ToLowerInvariant(),
                ErrorMessage = execution.ErrorMessage,
                RetryCount = execution.RetryCount,
                Skipped = execution.Skipped,
                SkipReason = execution.SkipReason
            });

            return MapToExecution(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule execution {ExecutionId}", execution.Id);
            throw;
        }
    }

    public async Task<List<ScheduleExecution>> GetExecutionHistoryAsync(
        Guid scheduleId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_schedule_executions
            WHERE schedule_id = @ScheduleId
            ORDER BY scheduled_at DESC
            LIMIT @Limit";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<ExecutionRow>(sql, new { ScheduleId = scheduleId, Limit = limit });
            return rows.Select(MapToExecution).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting execution history for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    public async Task<int> GetRunningExecutionCountAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM geoetl_schedule_executions
            WHERE schedule_id = @ScheduleId
                AND status = 'running'";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            return await connection.ExecuteScalarAsync<int>(sql, new { ScheduleId = scheduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting running execution count for schedule {ScheduleId}", scheduleId);
            throw;
        }
    }

    // ============================================================================
    // Distributed Locking (PostgreSQL Advisory Locks)
    // ============================================================================

    public async Task<bool> AcquireScheduleLockAsync(
        Guid scheduleId,
        TimeSpan lockDuration,
        CancellationToken cancellationToken = default)
    {
        // Convert GUID to bigint for advisory lock
        var lockKey = GetLockKey(scheduleId);

        const string sql = "SELECT pg_try_advisory_lock(@LockKey)";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var acquired = await connection.ExecuteScalarAsync<bool>(sql, new { LockKey = lockKey });

            if (acquired)
            {
                _logger.LogDebug("Acquired lock for schedule {ScheduleId}", scheduleId);
            }

            return acquired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock for schedule {ScheduleId}", scheduleId);
            return false;
        }
    }

    public async Task ReleaseScheduleLockAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        var lockKey = GetLockKey(scheduleId);

        const string sql = "SELECT pg_advisory_unlock(@LockKey)";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new { LockKey = lockKey });
            _logger.LogDebug("Released lock for schedule {ScheduleId}", scheduleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock for schedule {ScheduleId}", scheduleId);
        }
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static long GetLockKey(Guid scheduleId)
    {
        // Convert first 8 bytes of GUID to long for advisory lock
        var bytes = scheduleId.ToByteArray();
        return BitConverter.ToInt64(bytes, 0);
    }

    private static WorkflowSchedule MapToSchedule(ScheduleRow row)
    {
        return new WorkflowSchedule
        {
            Id = row.schedule_id,
            WorkflowId = row.workflow_id,
            TenantId = row.tenant_id,
            Name = row.name,
            Description = row.description,
            CronExpression = row.cron_expression,
            Timezone = row.timezone,
            Enabled = row.enabled,
            Status = Enum.Parse<ScheduleStatus>(row.status, ignoreCase: true),
            NextRunAt = row.next_run_at,
            LastRunAt = row.last_run_at,
            LastRunStatus = row.last_run_status,
            LastRunId = row.last_run_id,
            ParameterValues = !string.IsNullOrEmpty(row.parameter_values)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.parameter_values)
                : null,
            MaxConcurrentExecutions = row.max_concurrent_executions,
            RetryAttempts = row.retry_attempts,
            RetryDelayMinutes = row.retry_delay_minutes,
            ExpiresAt = row.expires_at,
            NotificationConfig = !string.IsNullOrEmpty(row.notification_config)
                ? JsonSerializer.Deserialize<ScheduleNotificationConfig>(row.notification_config)
                : null,
            Tags = row.tags?.ToList() ?? new List<string>(),
            CreatedAt = row.created_at,
            UpdatedAt = row.updated_at,
            CreatedBy = row.created_by,
            UpdatedBy = row.updated_by
        };
    }

    private static ScheduleExecution MapToExecution(ExecutionRow row)
    {
        return new ScheduleExecution
        {
            Id = row.execution_id,
            ScheduleId = row.schedule_id,
            WorkflowRunId = row.workflow_run_id,
            ScheduledAt = row.scheduled_at,
            ExecutedAt = row.executed_at,
            CompletedAt = row.completed_at,
            Status = Enum.Parse<ScheduleExecutionStatus>(row.status, ignoreCase: true),
            ErrorMessage = row.error_message,
            RetryCount = row.retry_count,
            Skipped = row.skipped,
            SkipReason = row.skip_reason
        };
    }

    // Database row classes for Dapper mapping
    private class ScheduleRow
    {
        public Guid schedule_id { get; set; }
        public Guid workflow_id { get; set; }
        public Guid tenant_id { get; set; }
        public string name { get; set; } = string.Empty;
        public string? description { get; set; }
        public string cron_expression { get; set; } = string.Empty;
        public string timezone { get; set; } = "UTC";
        public bool enabled { get; set; }
        public string status { get; set; } = "active";
        public DateTimeOffset? next_run_at { get; set; }
        public DateTimeOffset? last_run_at { get; set; }
        public string? last_run_status { get; set; }
        public Guid? last_run_id { get; set; }
        public string? parameter_values { get; set; }
        public int max_concurrent_executions { get; set; }
        public int retry_attempts { get; set; }
        public int retry_delay_minutes { get; set; }
        public DateTimeOffset? expires_at { get; set; }
        public string? notification_config { get; set; }
        public string[]? tags { get; set; }
        public DateTimeOffset created_at { get; set; }
        public DateTimeOffset updated_at { get; set; }
        public Guid created_by { get; set; }
        public Guid? updated_by { get; set; }
    }

    private class ExecutionRow
    {
        public Guid execution_id { get; set; }
        public Guid schedule_id { get; set; }
        public Guid? workflow_run_id { get; set; }
        public DateTimeOffset scheduled_at { get; set; }
        public DateTimeOffset? executed_at { get; set; }
        public DateTimeOffset? completed_at { get; set; }
        public string status { get; set; } = "pending";
        public string? error_message { get; set; }
        public int retry_count { get; set; }
        public bool skipped { get; set; }
        public string? skip_reason { get; set; }
    }
}
