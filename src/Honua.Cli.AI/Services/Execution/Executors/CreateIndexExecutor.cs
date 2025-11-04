// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets;
using Honua.Cli.AI.Services.Planning;
using Npgsql;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution.Executors;

/// <summary>
/// Executes CREATE INDEX operations on PostgreSQL/PostGIS databases.
/// This is one of the most common optimization operations.
/// </summary>
public sealed class CreateIndexExecutor : IStepExecutor
{
    public StepType SupportedStepType => StepType.CreateIndex;

    public async Task<StepValidationResult> ValidateAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Parse the operation to extract index details
            var indexInfo = ParseCreateIndexOperation(step.Operation);

            if (indexInfo.TableName.IsNullOrWhiteSpace())
            {
                errors.Add("Table name is required");
            }

            if (indexInfo.ColumnName.IsNullOrWhiteSpace())
            {
                errors.Add("Column name is required");
            }

            if (indexInfo.IndexType.IsNullOrWhiteSpace())
            {
                warnings.Add("Index type not specified, will use default (B-tree)");
            }

            // Check if index already exists
            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope { Level = AccessLevel.ReadOnly },
                    Duration = TimeSpan.FromMinutes(1),
                    Purpose = "Check if index exists",
                    Operations = new List<string> { "SELECT" }
                },
                cancellationToken);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var indexExists = await CheckIndexExistsAsync(
                conn,
                indexInfo.TableName,
                indexInfo.IndexName ?? GenerateIndexName(indexInfo),
                cancellationToken);

            if (indexExists)
            {
                warnings.Add($"Index '{indexInfo.IndexName}' already exists on {indexInfo.TableName}.{indexInfo.ColumnName}");
            }

            // Estimate index size
            var tableSize = await GetTableSizeAsync(conn, indexInfo.TableName, cancellationToken);
            if (tableSize > 1_000_000_000) // 1GB
            {
                warnings.Add($"Table is large ({FormatBytes(tableSize)}). Index creation may take several minutes.");
            }

            return new StepValidationResult
            {
                IsValid = true,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            errors.Add($"Validation error: {ex.Message}");
            return new StepValidationResult
            {
                IsValid = false,
                Errors = errors,
                Warnings = warnings
            };
        }
    }

    public async Task<StepExecutionResult> ExecuteAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var indexInfo = ParseCreateIndexOperation(step.Operation);

            // Get DDL credential (allows CREATE INDEX)
            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope
                    {
                        Level = AccessLevel.DDL,
                        AllowedOperations = new List<string> { "CREATE INDEX" }
                    },
                    Duration = TimeSpan.FromMinutes(30),
                    Purpose = $"Create index on {indexInfo.TableName}.{indexInfo.ColumnName}",
                    Operations = new List<string> { "CREATE INDEX" }
                },
                cancellationToken);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Build CREATE INDEX statement
            var indexName = indexInfo.IndexName ?? GenerateIndexName(indexInfo);
            var sql = BuildCreateIndexSql(indexName, indexInfo);

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.StepStarted,
                Message = $"Executing: {sql}",
                PlanId = "current"
            });

            // Execute with progress reporting for long-running operations
            await using var cmd = new NpgsqlCommand(sql, conn)
            {
                CommandTimeout = 1800 // 30 minutes
            };

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            sw.Stop();

            // Get index size
            var indexSize = await GetIndexSizeAsync(conn, indexName, cancellationToken);

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.StepCompleted,
                Message = $"Index created successfully ({FormatBytes(indexSize)}, {sw.Elapsed.TotalSeconds:F1}s)",
                PlanId = "current"
            });

            return new StepExecutionResult
            {
                Success = true,
                Output = $"Index '{indexName}' created on {indexInfo.TableName}.{indexInfo.ColumnName}\n" +
                         $"Size: {FormatBytes(indexSize)}\n" +
                         $"Duration: {sw.Elapsed.TotalSeconds:F1}s",
                Duration = sw.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["IndexName"] = indexName,
                    ["IndexSize"] = indexSize,
                    ["TableName"] = indexInfo.TableName,
                    ["ColumnName"] = indexInfo.ColumnName,
                    ["IndexType"] = indexInfo.IndexType ?? "btree"
                }
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.StepFailed,
                Message = $"Failed to create index: {ex.Message}",
                PlanId = "current"
            });

            return new StepExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = sw.Elapsed
            };
        }
    }

    public async Task<TimeEstimate> EstimateDurationAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var indexInfo = ParseCreateIndexOperation(step.Operation);

            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope { Level = AccessLevel.ReadOnly },
                    Duration = TimeSpan.FromMinutes(1),
                    Purpose = "Estimate index creation time",
                    Operations = new List<string> { "SELECT" }
                },
                cancellationToken);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Get table statistics
            var tableSize = await GetTableSizeAsync(conn, indexInfo.TableName, cancellationToken);
            var rowCount = await GetTableRowCountAsync(conn, indexInfo.TableName, cancellationToken);

            // Rough estimates based on empirical data:
            // - GIST index: ~500MB/minute on typical hardware
            // - B-tree index: ~1GB/minute
            var throughputBytesPerSecond = indexInfo.IndexType?.ToLowerInvariant() == "gist"
                ? 500_000_000 / 60.0  // 500MB/min
                : 1_000_000_000 / 60.0; // 1GB/min

            var estimatedSeconds = Math.Max(5, tableSize / throughputBytesPerSecond);

            return new TimeEstimate
            {
                Estimated = TimeSpan.FromSeconds(estimatedSeconds),
                Min = TimeSpan.FromSeconds(estimatedSeconds * 0.5),
                Max = TimeSpan.FromSeconds(estimatedSeconds * 2.0),
                Confidence = tableSize < 1_000_000_000 ? 0.8 : 0.6 // Lower confidence for large tables
            };
        }
        catch
        {
            // Fallback estimate
            return new TimeEstimate
            {
                Estimated = TimeSpan.FromSeconds(30),
                Min = TimeSpan.FromSeconds(10),
                Max = TimeSpan.FromMinutes(5),
                Confidence = 0.3
            };
        }
    }

    private CreateIndexInfo ParseCreateIndexOperation(string operation)
    {
        // Expected format: "CREATE INDEX ON table_name (column_name) USING index_type"
        // or JSON: {"table": "...", "column": "...", "type": "..."}

        if (operation.TrimStart().StartsWith("{"))
        {
            // JSON format
            var json = JsonSerializer.Deserialize<Dictionary<string, string>>(operation);
            return new CreateIndexInfo
            {
                TableName = json!["table"],
                ColumnName = json["column"],
                IndexType = json.ContainsKey("type") ? json["type"] : null,
                IndexName = json.ContainsKey("name") ? json["name"] : null
            };
        }

        // Parse SQL-like format
        var match = Regex.Match(operation,
            @"CREATE\s+INDEX\s+(?:(\w+)\s+)?ON\s+(\w+)\s*\(([^)]+)\)(?:\s+USING\s+(\w+))?",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Invalid CREATE INDEX operation: {operation}");
        }

        return new CreateIndexInfo
        {
            IndexName = match.Groups[1].Success ? match.Groups[1].Value : null,
            TableName = match.Groups[2].Value,
            ColumnName = match.Groups[3].Value.Trim(),
            IndexType = match.Groups[4].Success ? match.Groups[4].Value : null
        };
    }

    private string GenerateIndexName(CreateIndexInfo info)
    {
        var suffix = info.IndexType?.ToLowerInvariant() switch
        {
            "gist" => "gist",
            "gin" => "gin",
            "brin" => "brin",
            "hash" => "hash",
            _ => "idx"
        };

        return $"{info.TableName}_{info.ColumnName}_{suffix}";
    }

    private string BuildCreateIndexSql(string indexName, CreateIndexInfo info)
    {
        var indexType = info.IndexType?.ToUpperInvariant() ?? "BTREE";

        return $"CREATE INDEX CONCURRENTLY IF NOT EXISTS {indexName} " +
               $"ON {info.TableName} USING {indexType} ({info.ColumnName})";
    }

    private async Task<bool> CheckIndexExistsAsync(
        NpgsqlConnection conn,
        string tableName,
        string indexName,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE tablename = @table AND indexname = @index";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", tableName);
        cmd.Parameters.AddWithValue("index", indexName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null && Convert.ToInt64(result) > 0;
    }

    private async Task<long> GetTableSizeAsync(
        NpgsqlConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT pg_total_relation_size(@table::regclass)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", tableName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private async Task<long> GetTableRowCountAsync(
        NpgsqlConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = $"SELECT COUNT(*) FROM {tableName}";

        await using var cmd = new NpgsqlCommand(sql, conn);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private async Task<long> GetIndexSizeAsync(
        NpgsqlConnection conn,
        string indexName,
        CancellationToken cancellationToken)
    {
        var sql = "SELECT pg_relation_size(@index::regclass)";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("index", indexName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

internal sealed class CreateIndexInfo
{
    public string? IndexName { get; init; }
    public required string TableName { get; init; }
    public required string ColumnName { get; init; }
    public string? IndexType { get; init; }
}

