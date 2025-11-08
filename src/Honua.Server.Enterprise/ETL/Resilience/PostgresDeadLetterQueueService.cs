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
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.ETL.Resilience;

/// <summary>
/// PostgreSQL-based dead letter queue service
/// </summary>
public class PostgresDeadLetterQueueService : IDeadLetterQueueService
{
    private readonly string _connectionString;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<PostgresDeadLetterQueueService> _logger;

    public PostgresDeadLetterQueueService(
        string connectionString,
        IWorkflowEngine workflowEngine,
        ILogger<PostgresDeadLetterQueueService> logger)
    {
        _connectionString = connectionString;
        _workflowEngine = workflowEngine;
        _logger = logger;
    }

    public async Task<FailedWorkflow> AddAsync(
        WorkflowRun run,
        WorkflowError error,
        CancellationToken cancellationToken = default)
    {
        var failedWorkflow = new FailedWorkflow
        {
            WorkflowRunId = run.Id,
            WorkflowId = run.WorkflowId,
            TenantId = run.TenantId,
            FailedNodeId = error.NodeId,
            FailedNodeType = error.NodeType,
            FailedAt = DateTimeOffset.UtcNow,
            ErrorCategory = error.Category,
            ErrorMessage = error.Message,
            ErrorDetailsJson = JsonSerializer.Serialize(error),
            Status = FailedWorkflowStatus.Pending,
            Priority = DeterminePriority(error.Category)
        };

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO geoetl_failed_workflows (
                id, workflow_run_id, workflow_id, tenant_id, failed_node_id, failed_node_type,
                failed_at, error_category, error_message, error_details_json, status, priority
            ) VALUES (
                @Id, @WorkflowRunId, @WorkflowId, @TenantId, @FailedNodeId, @FailedNodeType,
                @FailedAt, @ErrorCategory, @ErrorMessage, @ErrorDetailsJson::jsonb, @Status, @Priority
            )";

        await conn.ExecuteAsync(sql, new
        {
            failedWorkflow.Id,
            failedWorkflow.WorkflowRunId,
            failedWorkflow.WorkflowId,
            failedWorkflow.TenantId,
            failedWorkflow.FailedNodeId,
            failedWorkflow.FailedNodeType,
            failedWorkflow.FailedAt,
            ErrorCategory = failedWorkflow.ErrorCategory.ToString(),
            failedWorkflow.ErrorMessage,
            failedWorkflow.ErrorDetailsJson,
            Status = failedWorkflow.Status.ToString(),
            Priority = failedWorkflow.Priority.ToString()
        });

        _logger.LogInformation(
            "Added failed workflow {Id} to dead letter queue for run {RunId}",
            failedWorkflow.Id,
            run.Id);

        return failedWorkflow;
    }

    public async Task<FailedWorkflow?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        const string sql = "SELECT * FROM geoetl_failed_workflows WHERE id = @Id";

        var row = await conn.QueryFirstOrDefaultAsync(sql, new { Id = id });
        return row != null ? MapFromDb(row) : null;
    }

    public async Task<PagedResult<FailedWorkflow>> ListAsync(
        FailedWorkflowFilter filter,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (filter.TenantId.HasValue)
        {
            whereClauses.Add("tenant_id = @TenantId");
            parameters.Add("TenantId", filter.TenantId.Value);
        }

        if (filter.WorkflowId.HasValue)
        {
            whereClauses.Add("workflow_id = @WorkflowId");
            parameters.Add("WorkflowId", filter.WorkflowId.Value);
        }

        if (filter.Status.HasValue)
        {
            whereClauses.Add("status = @Status");
            parameters.Add("Status", filter.Status.Value.ToString());
        }

        if (filter.ErrorCategory.HasValue)
        {
            whereClauses.Add("error_category = @ErrorCategory");
            parameters.Add("ErrorCategory", filter.ErrorCategory.Value.ToString());
        }

        if (filter.FromDate.HasValue)
        {
            whereClauses.Add("failed_at >= @FromDate");
            parameters.Add("FromDate", filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            whereClauses.Add("failed_at <= @ToDate");
            parameters.Add("ToDate", filter.ToDate.Value);
        }

        if (filter.AssignedTo.HasValue)
        {
            whereClauses.Add("assigned_to = @AssignedTo");
            parameters.Add("AssignedTo", filter.AssignedTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            whereClauses.Add("(error_message ILIKE @SearchText OR workflow_name ILIKE @SearchText)");
            parameters.Add("SearchText", $"%{filter.SearchText}%");
        }

        var whereClause = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        // Count total
        var countSql = $"SELECT COUNT(*) FROM geoetl_failed_workflows {whereClause}";
        var totalCount = await conn.ExecuteScalarAsync<int>(countSql, parameters);

        // Get page
        var orderBy = filter.SortBy switch
        {
            "priority" => "priority",
            "workflow" => "workflow_name",
            "category" => "error_category",
            _ => "failed_at"
        };

        var orderDirection = filter.SortDescending ? "DESC" : "ASC";

        var sql = $@"
            SELECT * FROM geoetl_failed_workflows
            {whereClause}
            ORDER BY {orderBy} {orderDirection}
            LIMIT @Take OFFSET @Skip";

        parameters.Add("Take", filter.Take);
        parameters.Add("Skip", filter.Skip);

        var rows = await conn.QueryAsync(sql, parameters);
        var items = rows.Select(MapFromDb).ToList();

        return new PagedResult<FailedWorkflow>
        {
            Items = items,
            TotalCount = totalCount,
            Skip = filter.Skip,
            Take = filter.Take
        };
    }

    public Task<WorkflowRun> RetryAsync(
        Guid failedWorkflowId,
        RetryOptions options,
        CancellationToken cancellationToken = default)
    {
        // This would need access to WorkflowEngine and workflow definition
        // For now, throw NotImplementedException - would be implemented in a full version
        throw new NotImplementedException("Retry functionality requires full workflow engine integration");
    }

    public Task<BulkRetryResult> BulkRetryAsync(
        List<Guid> failedWorkflowIds,
        RetryOptions options,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Bulk retry functionality requires full workflow engine integration");
    }

    public async Task AbandonAsync(
        Guid failedWorkflowId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        const string sql = @"
            UPDATE geoetl_failed_workflows
            SET status = @Status, resolution_notes = @Reason, resolved_at = @ResolvedAt
            WHERE id = @Id";

        await conn.ExecuteAsync(sql, new
        {
            Id = failedWorkflowId,
            Status = FailedWorkflowStatus.Abandoned.ToString(),
            Reason = reason,
            ResolvedAt = DateTimeOffset.UtcNow
        });

        _logger.LogInformation("Marked failed workflow {Id} as abandoned", failedWorkflowId);
    }

    public async Task AssignAsync(
        Guid failedWorkflowId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        const string sql = @"
            UPDATE geoetl_failed_workflows
            SET assigned_to = @UserId, status = @Status
            WHERE id = @Id";

        await conn.ExecuteAsync(sql, new
        {
            Id = failedWorkflowId,
            UserId = userId,
            Status = FailedWorkflowStatus.Investigating.ToString()
        });
    }

    public async Task<ErrorStatistics> GetStatisticsAsync(
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        from ??= DateTimeOffset.UtcNow.AddDays(-30);
        to ??= DateTimeOffset.UtcNow;

        var stats = new ErrorStatistics
        {
            FromDate = from.Value,
            ToDate = to.Value
        };

        // Total counts by status
        const string countsSql = @"
            SELECT
                COUNT(*) as TotalFailures,
                COUNT(*) FILTER (WHERE status = 'Pending') as PendingFailures,
                COUNT(*) FILTER (WHERE status = 'Resolved') as ResolvedFailures,
                COUNT(*) FILTER (WHERE status = 'Abandoned') as AbandonedFailures,
                AVG(retry_count) as AverageRetryCount
            FROM geoetl_failed_workflows
            WHERE failed_at BETWEEN @From AND @To";

        var counts = await conn.QueryFirstAsync(countsSql, new { From = from, To = to });
        stats.TotalFailures = counts.TotalFailures ?? 0;
        stats.PendingFailures = counts.PendingFailures ?? 0;
        stats.ResolvedFailures = counts.ResolvedFailures ?? 0;
        stats.AbandonedFailures = counts.AbandonedFailures ?? 0;
        stats.AverageRetryCount = counts.AverageRetryCount ?? 0.0;

        // By category
        const string categorySql = @"
            SELECT error_category, COUNT(*) as count
            FROM geoetl_failed_workflows
            WHERE failed_at BETWEEN @From AND @To
            GROUP BY error_category";

        var categories = await conn.QueryAsync(categorySql, new { From = from, To = to });
        foreach (var row in categories)
        {
            if (Enum.TryParse<ErrorCategory>(row.error_category, out ErrorCategory category))
            {
                stats.FailuresByCategory[category] = row.count;
            }
        }

        // By node type
        const string nodeTypeSql = @"
            SELECT failed_node_type, COUNT(*) as count
            FROM geoetl_failed_workflows
            WHERE failed_at BETWEEN @From AND @To AND failed_node_type IS NOT NULL
            GROUP BY failed_node_type
            ORDER BY count DESC
            LIMIT 10";

        var nodeTypes = await conn.QueryAsync(nodeTypeSql, new { From = from, To = to });
        foreach (var row in nodeTypes)
        {
            stats.FailuresByNodeType[row.failed_node_type] = row.count;
        }

        return stats;
    }

    public async Task<List<FailedWorkflow>> FindRelatedFailuresAsync(
        Guid failedWorkflowId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        // Find the original failure
        var original = await GetAsync(failedWorkflowId, cancellationToken);
        if (original == null) return new List<FailedWorkflow>();

        // Find similar failures (same error message pattern or node type)
        const string sql = @"
            SELECT * FROM geoetl_failed_workflows
            WHERE id != @Id
              AND (
                error_message = @ErrorMessage
                OR (failed_node_type = @NodeType AND error_category = @Category)
              )
            ORDER BY failed_at DESC
            LIMIT 20";

        var rows = await conn.QueryAsync(sql, new
        {
            Id = failedWorkflowId,
            original.ErrorMessage,
            NodeType = original.FailedNodeType,
            Category = original.ErrorCategory.ToString()
        });

        return rows.Select(MapFromDb).ToList();
    }

    public async Task<int> CleanupOldFailuresAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);

        var cutoffDate = DateTimeOffset.UtcNow.AddDays(-retentionDays);

        const string sql = @"
            DELETE FROM geoetl_failed_workflows
            WHERE (status = 'Resolved' OR status = 'Abandoned')
              AND resolved_at < @CutoffDate";

        var deletedCount = await conn.ExecuteAsync(sql, new { CutoffDate = cutoffDate });

        _logger.LogInformation(
            "Cleaned up {Count} old failed workflows older than {Days} days",
            deletedCount,
            retentionDays);

        return deletedCount;
    }

    private static FailedWorkflow MapFromDb(dynamic row)
    {
        return new FailedWorkflow
        {
            Id = row.id,
            WorkflowRunId = row.workflow_run_id,
            WorkflowId = row.workflow_id,
            WorkflowName = row.workflow_name,
            TenantId = row.tenant_id,
            FailedNodeId = row.failed_node_id,
            FailedNodeType = row.failed_node_type,
            FailedAt = row.failed_at,
            ErrorCategory = Enum.Parse<ErrorCategory>(row.error_category),
            ErrorMessage = row.error_message,
            ErrorDetailsJson = row.error_details_json,
            RetryCount = row.retry_count ?? 0,
            LastRetryAt = row.last_retry_at,
            Status = Enum.Parse<FailedWorkflowStatus>(row.status),
            AssignedTo = row.assigned_to,
            ResolutionNotes = row.resolution_notes,
            ResolvedAt = row.resolved_at,
            Priority = Enum.Parse<FailedWorkflowPriority>(row.priority)
        };
    }

    private static FailedWorkflowPriority DeterminePriority(ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.Configuration => FailedWorkflowPriority.High,
            ErrorCategory.Logic => FailedWorkflowPriority.High,
            ErrorCategory.Data => FailedWorkflowPriority.Medium,
            ErrorCategory.External => FailedWorkflowPriority.Low,
            ErrorCategory.Transient => FailedWorkflowPriority.Low,
            _ => FailedWorkflowPriority.Medium
        };
    }
}
