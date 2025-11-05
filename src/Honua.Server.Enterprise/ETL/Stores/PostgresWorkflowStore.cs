// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Enterprise.ETL.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Enterprise.ETL.Stores;

/// <summary>
/// PostgreSQL-based implementation of workflow store
/// </summary>
public class PostgresWorkflowStore : IWorkflowStore
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresWorkflowStore> _logger;

    public PostgresWorkflowStore(string connectionString, ILogger<PostgresWorkflowStore> logger)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================================
    // Workflow Definition Operations
    // ============================================================================

    public async Task<WorkflowDefinition> CreateWorkflowAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geoetl_workflows (
                workflow_id, tenant_id, version, name, description, author, tags, category,
                metadata_custom, definition, parameters, is_published, is_deleted,
                created_at, updated_at, created_by, updated_by
            ) VALUES (
                @WorkflowId, @TenantId, @Version, @Name, @Description, @Author, @Tags, @Category,
                @MetadataCustom::jsonb, @Definition::jsonb, @Parameters::jsonb, @IsPublished, @IsDeleted,
                @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy
            )
            RETURNING *";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleAsync<WorkflowRow>(sql, new
            {
                WorkflowId = workflow.Id,
                TenantId = workflow.TenantId,
                Version = workflow.Version,
                Name = workflow.Metadata.Name,
                Description = workflow.Metadata.Description,
                Author = workflow.Metadata.Author,
                Tags = workflow.Metadata.Tags?.ToArray(),
                Category = workflow.Metadata.Category,
                MetadataCustom = workflow.Metadata.Custom != null ? JsonSerializer.Serialize(workflow.Metadata.Custom) : null,
                Definition = JsonSerializer.Serialize(new
                {
                    nodes = workflow.Nodes,
                    edges = workflow.Edges
                }),
                Parameters = workflow.Parameters.Count > 0 ? JsonSerializer.Serialize(workflow.Parameters) : null,
                IsPublished = workflow.IsPublished,
                IsDeleted = workflow.IsDeleted,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                CreatedBy = workflow.CreatedBy,
                UpdatedBy = workflow.UpdatedBy
            });

            _logger.LogInformation("Created workflow {WorkflowId} for tenant {TenantId}", workflow.Id, workflow.TenantId);
            return MapToWorkflowDefinition(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow {WorkflowId}", workflow.Id);
            throw;
        }
    }

    public async Task<WorkflowDefinition?> GetWorkflowAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT * FROM geoetl_workflows WHERE workflow_id = @WorkflowId AND NOT is_deleted";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleOrDefaultAsync<WorkflowRow>(sql, new { WorkflowId = workflowId });
            return row != null ? MapToWorkflowDefinition(row) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    public async Task<WorkflowDefinition> UpdateWorkflowAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            UPDATE geoetl_workflows SET
                version = @Version,
                name = @Name,
                description = @Description,
                author = @Author,
                tags = @Tags,
                category = @Category,
                metadata_custom = @MetadataCustom::jsonb,
                definition = @Definition::jsonb,
                parameters = @Parameters::jsonb,
                is_published = @IsPublished,
                updated_at = @UpdatedAt,
                updated_by = @UpdatedBy
            WHERE workflow_id = @WorkflowId
            RETURNING *";

        try
        {
            workflow.UpdatedAt = DateTimeOffset.UtcNow;

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var row = await connection.QuerySingleAsync<WorkflowRow>(sql, new
            {
                WorkflowId = workflow.Id,
                Version = workflow.Version,
                Name = workflow.Metadata.Name,
                Description = workflow.Metadata.Description,
                Author = workflow.Metadata.Author,
                Tags = workflow.Metadata.Tags?.ToArray(),
                Category = workflow.Metadata.Category,
                MetadataCustom = workflow.Metadata.Custom != null ? JsonSerializer.Serialize(workflow.Metadata.Custom) : null,
                Definition = JsonSerializer.Serialize(new
                {
                    nodes = workflow.Nodes,
                    edges = workflow.Edges
                }),
                Parameters = workflow.Parameters.Count > 0 ? JsonSerializer.Serialize(workflow.Parameters) : null,
                IsPublished = workflow.IsPublished,
                UpdatedAt = workflow.UpdatedAt,
                UpdatedBy = workflow.UpdatedBy
            });

            _logger.LogInformation("Updated workflow {WorkflowId}", workflow.Id);
            return MapToWorkflowDefinition(row);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow {WorkflowId}", workflow.Id);
            throw;
        }
    }

    public async Task DeleteWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE geoetl_workflows SET is_deleted = TRUE, updated_at = NOW() WHERE workflow_id = @WorkflowId";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new { WorkflowId = workflowId });
            _logger.LogInformation("Soft deleted workflow {WorkflowId}", workflowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    public async Task<List<WorkflowDefinition>> ListWorkflowsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflows
            WHERE tenant_id = @TenantId AND NOT is_deleted
            ORDER BY created_at DESC";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<WorkflowRow>(sql, new { TenantId = tenantId });
            return rows.Select(MapToWorkflowDefinition).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing workflows for tenant {TenantId}", tenantId);
            throw;
        }
    }

    // ============================================================================
    // Workflow Run Operations
    // ============================================================================

    public async Task<WorkflowRun> CreateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO geoetl_workflow_runs (
                run_id, workflow_id, tenant_id, status, created_at, started_at, completed_at,
                triggered_by, trigger_type, parameter_values, features_processed, bytes_read, bytes_written,
                peak_memory_mb, cpu_time_ms, compute_cost_usd, storage_cost_usd, output_locations,
                error_message, error_stack, input_datasets, output_datasets, state
            ) VALUES (
                @RunId, @WorkflowId, @TenantId, @Status, @CreatedAt, @StartedAt, @CompletedAt,
                @TriggeredBy, @TriggerType, @ParameterValues::jsonb, @FeaturesProcessed, @BytesRead, @BytesWritten,
                @PeakMemoryMb, @CpuTimeMs, @ComputeCostUsd, @StorageCostUsd, @OutputLocations::jsonb,
                @ErrorMessage, @ErrorStack, @InputDatasets::jsonb, @OutputDatasets::jsonb, @State::jsonb
            )";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await connection.ExecuteAsync(sql, new
            {
                RunId = run.Id,
                WorkflowId = run.WorkflowId,
                TenantId = run.TenantId,
                Status = run.Status.ToString().ToLowerInvariant(),
                CreatedAt = run.CreatedAt,
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                TriggeredBy = run.TriggeredBy,
                TriggerType = run.TriggerType.ToString().ToLowerInvariant(),
                ParameterValues = run.ParameterValues != null ? JsonSerializer.Serialize(run.ParameterValues) : null,
                FeaturesProcessed = run.FeaturesProcessed,
                BytesRead = run.BytesRead,
                BytesWritten = run.BytesWritten,
                PeakMemoryMb = run.PeakMemoryMB,
                CpuTimeMs = run.CpuTimeMs,
                ComputeCostUsd = run.ComputeCostUsd,
                StorageCostUsd = run.StorageCostUsd,
                OutputLocations = run.OutputLocations != null ? JsonSerializer.Serialize(run.OutputLocations) : null,
                ErrorMessage = run.ErrorMessage,
                ErrorStack = run.ErrorStack,
                InputDatasets = run.InputDatasets != null ? JsonSerializer.Serialize(run.InputDatasets) : null,
                OutputDatasets = run.OutputDatasets != null ? JsonSerializer.Serialize(run.OutputDatasets) : null,
                State = run.State != null ? JsonSerializer.Serialize(run.State) : null
            });

            _logger.LogInformation("Created workflow run {RunId} for workflow {WorkflowId}", run.Id, run.WorkflowId);
            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating workflow run {RunId}", run.Id);
            throw;
        }
    }

    public async Task<WorkflowRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_runs WHERE run_id = @RunId;
            SELECT * FROM geoetl_node_runs WHERE workflow_run_id = @RunId ORDER BY started_at;";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var multi = await connection.QueryMultipleAsync(sql, new { RunId = runId });

            var runRow = await multi.ReadSingleOrDefaultAsync<WorkflowRunRow>();
            if (runRow == null) return null;

            var nodeRows = (await multi.ReadAsync<NodeRunRow>()).ToList();

            return MapToWorkflowRun(runRow, nodeRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving workflow run {RunId}", runId);
            throw;
        }
    }

    public async Task<WorkflowRun> UpdateRunAsync(WorkflowRun run, CancellationToken cancellationToken = default)
    {
        const string sqlRun = @"
            UPDATE geoetl_workflow_runs SET
                status = @Status,
                started_at = @StartedAt,
                completed_at = @CompletedAt,
                features_processed = @FeaturesProcessed,
                bytes_read = @BytesRead,
                bytes_written = @BytesWritten,
                peak_memory_mb = @PeakMemoryMb,
                cpu_time_ms = @CpuTimeMs,
                compute_cost_usd = @ComputeCostUsd,
                storage_cost_usd = @StorageCostUsd,
                output_locations = @OutputLocations::jsonb,
                error_message = @ErrorMessage,
                error_stack = @ErrorStack,
                input_datasets = @InputDatasets::jsonb,
                output_datasets = @OutputDatasets::jsonb,
                state = @State::jsonb
            WHERE run_id = @RunId";

        const string sqlNodeUpsert = @"
            INSERT INTO geoetl_node_runs (
                node_run_id, workflow_run_id, node_id, node_type, status, started_at, completed_at,
                duration_ms, features_processed, error_message, geoprocessing_run_id, output, retry_count
            ) VALUES (
                @NodeRunId, @WorkflowRunId, @NodeId, @NodeType, @Status, @StartedAt, @CompletedAt,
                @DurationMs, @FeaturesProcessed, @ErrorMessage, @GeoprocessingRunId, @Output::jsonb, @RetryCount
            )
            ON CONFLICT (node_run_id) DO UPDATE SET
                status = EXCLUDED.status,
                started_at = EXCLUDED.started_at,
                completed_at = EXCLUDED.completed_at,
                duration_ms = EXCLUDED.duration_ms,
                features_processed = EXCLUDED.features_processed,
                error_message = EXCLUDED.error_message,
                output = EXCLUDED.output,
                retry_count = EXCLUDED.retry_count";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            // Update run
            await connection.ExecuteAsync(sqlRun, new
            {
                RunId = run.Id,
                Status = run.Status.ToString().ToLowerInvariant(),
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                FeaturesProcessed = run.FeaturesProcessed,
                BytesRead = run.BytesRead,
                BytesWritten = run.BytesWritten,
                PeakMemoryMb = run.PeakMemoryMB,
                CpuTimeMs = run.CpuTimeMs,
                ComputeCostUsd = run.ComputeCostUsd,
                StorageCostUsd = run.StorageCostUsd,
                OutputLocations = run.OutputLocations != null ? JsonSerializer.Serialize(run.OutputLocations) : null,
                ErrorMessage = run.ErrorMessage,
                ErrorStack = run.ErrorStack,
                InputDatasets = run.InputDatasets != null ? JsonSerializer.Serialize(run.InputDatasets) : null,
                OutputDatasets = run.OutputDatasets != null ? JsonSerializer.Serialize(run.OutputDatasets) : null,
                State = run.State != null ? JsonSerializer.Serialize(run.State) : null
            }, transaction);

            // Upsert node runs
            foreach (var nodeRun in run.NodeRuns)
            {
                await connection.ExecuteAsync(sqlNodeUpsert, new
                {
                    NodeRunId = nodeRun.Id,
                    WorkflowRunId = run.Id,
                    NodeId = nodeRun.NodeId,
                    NodeType = nodeRun.NodeType,
                    Status = nodeRun.Status.ToString().ToLowerInvariant(),
                    StartedAt = nodeRun.StartedAt,
                    CompletedAt = nodeRun.CompletedAt,
                    DurationMs = nodeRun.DurationMs,
                    FeaturesProcessed = nodeRun.FeaturesProcessed,
                    ErrorMessage = nodeRun.ErrorMessage,
                    GeoprocessingRunId = nodeRun.GeoprocessingRunId,
                    Output = nodeRun.Output != null ? JsonSerializer.Serialize(nodeRun.Output) : null,
                    RetryCount = nodeRun.RetryCount
                }, transaction);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Updated workflow run {RunId}", run.Id);
            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating workflow run {RunId}", run.Id);
            throw;
        }
    }

    public async Task<List<WorkflowRun>> ListRunsAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_runs
            WHERE workflow_id = @WorkflowId
            ORDER BY created_at DESC
            LIMIT 100";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<WorkflowRunRow>(sql, new { WorkflowId = workflowId });
            return rows.Select(row => MapToWorkflowRun(row, new List<NodeRunRow>())).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing runs for workflow {WorkflowId}", workflowId);
            throw;
        }
    }

    public async Task<List<WorkflowRun>> ListRunsByTenantAsync(
        Guid tenantId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT * FROM geoetl_workflow_runs
            WHERE tenant_id = @TenantId
            ORDER BY created_at DESC
            LIMIT @Limit";

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var rows = await connection.QueryAsync<WorkflowRunRow>(sql, new { TenantId = tenantId, Limit = limit });
            return rows.Select(row => MapToWorkflowRun(row, new List<NodeRunRow>())).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing runs for tenant {TenantId}", tenantId);
            throw;
        }
    }

    // ============================================================================
    // Mapping Helpers
    // ============================================================================

    private WorkflowDefinition MapToWorkflowDefinition(WorkflowRow row)
    {
        var definition = JsonSerializer.Deserialize<Dictionary<string, object>>(row.Definition);
        var nodes = definition?.TryGetValue("nodes", out var nodesObj) == true
            ? JsonSerializer.Deserialize<List<WorkflowNode>>(nodesObj.ToString()!) ?? new List<WorkflowNode>()
            : new List<WorkflowNode>();
        var edges = definition?.TryGetValue("edges", out var edgesObj) == true
            ? JsonSerializer.Deserialize<List<WorkflowEdge>>(edgesObj.ToString()!) ?? new List<WorkflowEdge>()
            : new List<WorkflowEdge>();

        return new WorkflowDefinition
        {
            Id = row.WorkflowId,
            TenantId = row.TenantId,
            Version = row.Version,
            Metadata = new WorkflowMetadata
            {
                Name = row.Name,
                Description = row.Description,
                Author = row.Author,
                Tags = row.Tags?.ToList() ?? new List<string>(),
                Category = row.Category,
                Custom = !string.IsNullOrEmpty(row.MetadataCustom)
                    ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.MetadataCustom)
                    : null
            },
            Parameters = !string.IsNullOrEmpty(row.Parameters)
                ? JsonSerializer.Deserialize<Dictionary<string, WorkflowParameter>>(row.Parameters) ?? new Dictionary<string, WorkflowParameter>()
                : new Dictionary<string, WorkflowParameter>(),
            Nodes = nodes,
            Edges = edges,
            IsPublished = row.IsPublished,
            IsDeleted = row.IsDeleted,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
            CreatedBy = row.CreatedBy,
            UpdatedBy = row.UpdatedBy
        };
    }

    private WorkflowRun MapToWorkflowRun(WorkflowRunRow row, List<NodeRunRow> nodeRows)
    {
        return new WorkflowRun
        {
            Id = row.RunId,
            WorkflowId = row.WorkflowId,
            TenantId = row.TenantId,
            Status = Enum.Parse<WorkflowRunStatus>(row.Status, true),
            StartedAt = row.StartedAt,
            CompletedAt = row.CompletedAt,
            TriggeredBy = row.TriggeredBy,
            TriggerType = Enum.Parse<WorkflowTriggerType>(row.TriggerType, true),
            ParameterValues = !string.IsNullOrEmpty(row.ParameterValues)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.ParameterValues)
                : null,
            FeaturesProcessed = row.FeaturesProcessed,
            BytesRead = row.BytesRead,
            BytesWritten = row.BytesWritten,
            PeakMemoryMB = row.PeakMemoryMb,
            CpuTimeMs = row.CpuTimeMs,
            ComputeCostUsd = row.ComputeCostUsd,
            StorageCostUsd = row.StorageCostUsd,
            OutputLocations = !string.IsNullOrEmpty(row.OutputLocations)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(row.OutputLocations)
                : null,
            ErrorMessage = row.ErrorMessage,
            ErrorStack = row.ErrorStack,
            InputDatasets = !string.IsNullOrEmpty(row.InputDatasets)
                ? JsonSerializer.Deserialize<List<string>>(row.InputDatasets)
                : null,
            OutputDatasets = !string.IsNullOrEmpty(row.OutputDatasets)
                ? JsonSerializer.Deserialize<List<string>>(row.OutputDatasets)
                : null,
            NodeRuns = nodeRows.Select(MapToNodeRun).ToList(),
            State = !string.IsNullOrEmpty(row.State)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.State)
                : null,
            CreatedAt = row.CreatedAt
        };
    }

    private NodeRun MapToNodeRun(NodeRunRow row)
    {
        return new NodeRun
        {
            Id = row.NodeRunId,
            WorkflowRunId = row.WorkflowRunId,
            NodeId = row.NodeId,
            NodeType = row.NodeType,
            Status = Enum.Parse<NodeRunStatus>(row.Status, true),
            StartedAt = row.StartedAt,
            CompletedAt = row.CompletedAt,
            DurationMs = row.DurationMs,
            FeaturesProcessed = row.FeaturesProcessed,
            ErrorMessage = row.ErrorMessage,
            GeoprocessingRunId = row.GeoprocessingRunId,
            Output = !string.IsNullOrEmpty(row.Output)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(row.Output)
                : null,
            RetryCount = row.RetryCount
        };
    }

    // ============================================================================
    // Database Row Classes
    // ============================================================================

    private class WorkflowRow
    {
        public Guid WorkflowId { get; set; }
        public Guid TenantId { get; set; }
        public int Version { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string[]? Tags { get; set; }
        public string? Category { get; set; }
        public string? MetadataCustom { get; set; }
        public string Definition { get; set; } = string.Empty;
        public string? Parameters { get; set; }
        public bool IsPublished { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Guid CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }

    private class WorkflowRunRow
    {
        public Guid RunId { get; set; }
        public Guid WorkflowId { get; set; }
        public Guid TenantId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public Guid? TriggeredBy { get; set; }
        public string TriggerType { get; set; } = string.Empty;
        public string? ParameterValues { get; set; }
        public long? FeaturesProcessed { get; set; }
        public long? BytesRead { get; set; }
        public long? BytesWritten { get; set; }
        public int? PeakMemoryMb { get; set; }
        public long? CpuTimeMs { get; set; }
        public decimal? ComputeCostUsd { get; set; }
        public decimal? StorageCostUsd { get; set; }
        public string? OutputLocations { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorStack { get; set; }
        public string? InputDatasets { get; set; }
        public string? OutputDatasets { get; set; }
        public string? State { get; set; }
    }

    private class NodeRunRow
    {
        public Guid NodeRunId { get; set; }
        public Guid WorkflowRunId { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public long? DurationMs { get; set; }
        public long? FeaturesProcessed { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid? GeoprocessingRunId { get; set; }
        public string? Output { get; set; }
        public int RetryCount { get; set; }
    }
}
