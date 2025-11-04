// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets;
using Honua.Cli.AI.Services.Planning;
using Npgsql;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Execution.Executors;

/// <summary>
/// Executes VACUUM ANALYZE operations on PostgreSQL/PostGIS databases.
/// VACUUM reclaims storage and ANALYZE updates query planner statistics.
/// </summary>
public sealed class VacuumAnalyzeExecutor : IStepExecutor
{
    public StepType SupportedStepType => StepType.VacuumAnalyze;

    public async Task<StepValidationResult> ValidateAsync(
        PlanStep step,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var vacuumInfo = ParseVacuumOperation(step.Operation);

            if (vacuumInfo.Full && !context.ContinueOnError)
            {
                warnings.Add("VACUUM FULL requires an exclusive table lock and may take a long time");
                warnings.Add("Consider running VACUUM (without FULL) instead for production systems");
            }

            // Check table exists and get size
            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope { Level = AccessLevel.ReadOnly },
                    Duration = TimeSpan.FromMinutes(1),
                    Purpose = "Check table size for VACUUM estimation",
                    Operations = new List<string> { "SELECT" }
                },
                cancellationToken);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (vacuumInfo.TableName.HasValue())
            {
                var tableSize = await GetTableSizeAsync(conn, vacuumInfo.TableName, cancellationToken);

                if (tableSize > 10_000_000_000) // 10GB
                {
                    warnings.Add($"Table is very large ({FormatBytes(tableSize)}). VACUUM may take significant time.");
                }

                // Check for dead tuples
                var deadTuples = await GetDeadTuplesAsync(conn, vacuumInfo.TableName, cancellationToken);
                if (deadTuples == 0)
                {
                    warnings.Add($"Table '{vacuumInfo.TableName}' has no dead tuples. VACUUM may not be necessary.");
                }
            }
            else
            {
                warnings.Add("Running VACUUM on entire database. This may take a long time.");
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
            var vacuumInfo = ParseVacuumOperation(step.Operation);

            // VACUUM requires special connection handling (can't run in transaction)
            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope
                    {
                        Level = AccessLevel.DDL,
                        AllowedOperations = new List<string> { "VACUUM", "ANALYZE" }
                    },
                    Duration = TimeSpan.FromHours(1),
                    Purpose = vacuumInfo.TableName != null
                        ? $"VACUUM {vacuumInfo.TableName}"
                        : "VACUUM database",
                    Operations = new List<string> { "VACUUM", "ANALYZE" }
                },
                cancellationToken);

            // VACUUM cannot run inside a transaction block
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Capture statistics before VACUUM
            long deadTuplesBefore = 0;
            long tableSizeBefore = 0;

            if (vacuumInfo.TableName.HasValue())
            {
                deadTuplesBefore = await GetDeadTuplesAsync(conn, vacuumInfo.TableName, cancellationToken);
                tableSizeBefore = await GetTableSizeAsync(conn, vacuumInfo.TableName, cancellationToken);
            }

            // Build VACUUM command
            var sql = BuildVacuumSql(vacuumInfo);

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.StepStarted,
                Message = $"Executing: {sql}",
                PlanId = "current"
            });

            await using var cmd = new NpgsqlCommand(sql, conn)
            {
                CommandTimeout = 3600 // 1 hour
            };

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            sw.Stop();

            // Capture statistics after VACUUM
            long deadTuplesAfter = 0;
            long tableSizeAfter = 0;
            long freedSpace = 0;

            if (vacuumInfo.TableName.HasValue())
            {
                deadTuplesAfter = await GetDeadTuplesAsync(conn, vacuumInfo.TableName, cancellationToken);
                tableSizeAfter = await GetTableSizeAsync(conn, vacuumInfo.TableName, cancellationToken);
                freedSpace = tableSizeBefore - tableSizeAfter;
            }

            var summary = vacuumInfo.TableName.HasValue()
                ? $"VACUUM completed on {vacuumInfo.TableName}\n" +
                  $"Dead tuples: {deadTuplesBefore:N0} → {deadTuplesAfter:N0} (removed {deadTuplesBefore - deadTuplesAfter:N0})\n" +
                  (freedSpace > 0 ? $"Space freed: {FormatBytes(freedSpace)}\n" : "") +
                  $"Duration: {sw.Elapsed.TotalSeconds:F1}s"
                : $"VACUUM completed on database\n" +
                  $"Duration: {sw.Elapsed.TotalSeconds:F1}s";

            context.LogEvent(new ExecutionEvent
            {
                Timestamp = DateTime.UtcNow,
                Type = ExecutionEventType.StepCompleted,
                Message = $"VACUUM completed ({sw.Elapsed.TotalSeconds:F1}s)",
                PlanId = "current"
            });

            return new StepExecutionResult
            {
                Success = true,
                Output = summary,
                Duration = sw.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["TableName"] = vacuumInfo.TableName ?? "database",
                    ["Full"] = vacuumInfo.Full,
                    ["Analyze"] = vacuumInfo.Analyze,
                    ["DeadTuplesRemoved"] = deadTuplesBefore - deadTuplesAfter,
                    ["SpaceFreed"] = freedSpace
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
                Message = $"VACUUM failed: {ex.Message}",
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
            var vacuumInfo = ParseVacuumOperation(step.Operation);

            if (vacuumInfo.TableName.IsNullOrWhiteSpace())
            {
                // Database-wide VACUUM - harder to estimate
                return new TimeEstimate
                {
                    Estimated = TimeSpan.FromMinutes(10),
                    Min = TimeSpan.FromMinutes(2),
                    Max = TimeSpan.FromHours(1),
                    Confidence = 0.3
                };
            }

            var connectionString = await context.GetCredentialAsync(
                new CredentialRequirement
                {
                    SecretRef = "database",
                    Scope = new AccessScope { Level = AccessLevel.ReadOnly },
                    Duration = TimeSpan.FromMinutes(1),
                    Purpose = "Estimate VACUUM duration",
                    Operations = new List<string> { "SELECT" }
                },
                cancellationToken);

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            var tableSize = await GetTableSizeAsync(conn, vacuumInfo.TableName, cancellationToken);
            var deadTuples = await GetDeadTuplesAsync(conn, vacuumInfo.TableName, cancellationToken);

            // Estimate based on table size and dead tuple ratio
            // VACUUM: ~200MB/s on typical hardware
            // VACUUM FULL: ~50MB/s (much slower due to table rebuild)
            var throughput = vacuumInfo.Full ? 50_000_000.0 : 200_000_000.0;

            var estimatedSeconds = Math.Max(2, tableSize / throughput);

            // If few dead tuples, VACUUM will be faster
            if (deadTuples < 1000)
            {
                estimatedSeconds *= 0.5;
            }

            return new TimeEstimate
            {
                Estimated = TimeSpan.FromSeconds(estimatedSeconds),
                Min = TimeSpan.FromSeconds(estimatedSeconds * 0.3),
                Max = TimeSpan.FromSeconds(estimatedSeconds * 3.0),
                Confidence = 0.7
            };
        }
        catch
        {
            return new TimeEstimate
            {
                Estimated = TimeSpan.FromMinutes(5),
                Min = TimeSpan.FromSeconds(30),
                Max = TimeSpan.FromMinutes(30),
                Confidence = 0.4
            };
        }
    }

    private VacuumInfo ParseVacuumOperation(string operation)
    {
        // Expected format: "VACUUM [FULL] [ANALYZE] [table_name]"
        // or JSON: {"table": "...", "full": true, "analyze": true}

        if (operation.TrimStart().StartsWith("{"))
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(operation);
            return new VacuumInfo
            {
                TableName = json!.ContainsKey("table") ? json["table"].GetString() : null,
                Full = json.ContainsKey("full") && json["full"].GetBoolean(),
                Analyze = !json.ContainsKey("analyze") || json["analyze"].GetBoolean() // Default true
            };
        }

        // Parse SQL-like format
        var parts = operation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var info = new VacuumInfo { Full = false, Analyze = false };

        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i].ToUpperInvariant();
            if (part == "FULL") info.Full = true;
            else if (part == "ANALYZE") info.Analyze = true;
            else if (!part.StartsWith("VACUUM")) info.TableName = parts[i];
        }

        return info;
    }

    private string BuildVacuumSql(VacuumInfo info)
    {
        var parts = new List<string> { "VACUUM" };

        if (info.Full) parts.Add("FULL");
        if (info.Analyze) parts.Add("ANALYZE");

        var sql = string.Join(" ", parts);

        if (info.TableName.HasValue())
        {
            sql += $" {info.TableName}";
        }

        return sql;
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

    private async Task<long> GetDeadTuplesAsync(
        NpgsqlConnection conn,
        string tableName,
        CancellationToken cancellationToken)
    {
        var sql = @"
            SELECT n_dead_tup
            FROM pg_stat_user_tables
            WHERE relname = @table";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("table", tableName);

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

internal sealed class VacuumInfo
{
    public string? TableName { get; set; }
    public bool Full { get; set; }
    public bool Analyze { get; set; }
}
