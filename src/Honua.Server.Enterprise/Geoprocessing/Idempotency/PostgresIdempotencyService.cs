// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using Honua.Server.Enterprise.Data;

namespace Honua.Server.Enterprise.Geoprocessing.Idempotency;

/// <summary>
/// PostgreSQL implementation of idempotency service for geoprocessing jobs.
/// Uses geoprocessing_idempotency table to track completed jobs and prevent duplicate execution.
/// </summary>
public sealed class PostgresIdempotencyService : IIdempotencyService
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresIdempotencyService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public PostgresIdempotencyService(
        string connectionString,
        ILogger<PostgresIdempotencyService> logger)
    {
        DapperBootstrapper.EnsureConfigured();
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public string ComputeIdempotencyKey(ProcessRun job)
    {
        if (job == null)
            throw new ArgumentNullException(nameof(job));

        // Serialize inputs to stable JSON (sorted keys)
        var inputsJson = JsonSerializer.Serialize(job.Inputs, JsonOptions);

        // Compute SHA256 hash of: JobId + InputsJson + ProcessId
        var data = $"{job.JobId}:{inputsJson}:{job.ProcessId}";
        var bytes = Encoding.UTF8.GetBytes(data);
        var hash = SHA256.HashData(bytes);

        // Return hex string
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public async Task<IdempotencyCacheEntry?> GetCachedResultAsync(
        string idempotencyKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
                SELECT
                    idempotency_key,
                    job_id,
                    result_hash,
                    result_payload,
                    completed_at,
                    expires_at
                FROM geoprocessing_idempotency
                WHERE idempotency_key = @IdempotencyKey
                  AND expires_at > NOW()";

            var row = await connection.QuerySingleOrDefaultAsync<IdempotencyRow>(
                sql,
                new { IdempotencyKey = idempotencyKey });

            if (row == null)
            {
                _logger.LogDebug(
                    "Idempotency cache miss for key: {IdempotencyKey}",
                    idempotencyKey);
                return null;
            }

            // Deserialize cached result
            var result = JsonSerializer.Deserialize<ProcessResult>(row.ResultPayload, JsonOptions);
            if (result == null)
            {
                _logger.LogWarning(
                    "Failed to deserialize cached result for key: {IdempotencyKey}",
                    idempotencyKey);
                return null;
            }

            _logger.LogInformation(
                "Idempotency cache hit for key: {IdempotencyKey}, job: {JobId}",
                idempotencyKey,
                row.JobId);

            return new IdempotencyCacheEntry
            {
                IdempotencyKey = row.IdempotencyKey,
                JobId = row.JobId,
                Result = result,
                CompletedAt = row.CompletedAt,
                ExpiresAt = row.ExpiresAt,
                ResultHash = row.ResultHash
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking idempotency cache for key: {IdempotencyKey}",
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task StoreCachedResultAsync(
        string idempotencyKey,
        ProcessRun job,
        ProcessResult result,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be null or empty", nameof(idempotencyKey));
        if (job == null)
            throw new ArgumentNullException(nameof(job));
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        var effectiveTtl = ttl ?? TimeSpan.FromDays(7);

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            // Serialize result to JSON
            var resultJson = JsonSerializer.Serialize(result, JsonOptions);
            var resultHash = ComputeResultHash(resultJson);

            var sql = @"
                INSERT INTO geoprocessing_idempotency (
                    idempotency_key,
                    job_id,
                    result_hash,
                    result_payload,
                    completed_at,
                    expires_at,
                    tenant_id,
                    process_id,
                    duration_ms,
                    features_processed
                )
                VALUES (
                    @IdempotencyKey,
                    @JobId,
                    @ResultHash,
                    @ResultPayload::jsonb,
                    @CompletedAt,
                    @ExpiresAt,
                    @TenantId,
                    @ProcessId,
                    @DurationMs,
                    @FeaturesProcessed
                )
                ON CONFLICT (idempotency_key) DO UPDATE SET
                    result_hash = EXCLUDED.result_hash,
                    result_payload = EXCLUDED.result_payload,
                    completed_at = EXCLUDED.completed_at,
                    expires_at = EXCLUDED.expires_at,
                    duration_ms = EXCLUDED.duration_ms,
                    features_processed = EXCLUDED.features_processed";

            await connection.ExecuteAsync(
                sql,
                new
                {
                    IdempotencyKey = idempotencyKey,
                    JobId = job.JobId,
                    ResultHash = resultHash,
                    ResultPayload = resultJson,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow + effectiveTtl,
                    TenantId = job.TenantId,
                    ProcessId = job.ProcessId,
                    DurationMs = result.DurationMs,
                    FeaturesProcessed = result.FeaturesProcessed
                });

            _logger.LogInformation(
                "Stored idempotency cache entry for job {JobId}, key: {IdempotencyKey}, TTL: {TTL}",
                job.JobId,
                idempotencyKey,
                effectiveTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error storing idempotency cache for job {JobId}, key: {IdempotencyKey}",
                job.JobId,
                idempotencyKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> CleanupExpiredEntriesAsync(CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
                DELETE FROM geoprocessing_idempotency
                WHERE expires_at <= NOW()";

            var deletedCount = await connection.ExecuteAsync(sql);

            if (deletedCount > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} expired idempotency entries",
                    deletedCount);
            }
            else
            {
                _logger.LogDebug("No expired idempotency entries to clean up");
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired idempotency entries");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IdempotencyStatistics> GetStatisticsAsync(
        Guid? tenantId = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var tenantFilter = tenantId.HasValue
                ? "WHERE tenant_id = @TenantId"
                : "";

            var sql = $@"
                SELECT
                    COUNT(*) as TotalEntries,
                    COUNT(*) FILTER (WHERE expires_at <= NOW() + INTERVAL '24 hours') as ExpiringIn24Hours,
                    COUNT(*) FILTER (WHERE expires_at <= NOW()) as ExpiredEntries,
                    COALESCE(SUM(pg_column_size(result_payload)) / 1024.0 / 1024.0, 0) as TotalSizeMB,
                    MIN(completed_at) as OldestEntry,
                    MAX(completed_at) as NewestEntry
                FROM geoprocessing_idempotency
                {tenantFilter}";

            var stats = await connection.QuerySingleAsync<IdempotencyStatistics>(
                sql,
                new { TenantId = tenantId });

            _logger.LogDebug(
                "Idempotency cache statistics - Total: {Total}, Expiring: {Expiring}, Expired: {Expired}, Size: {Size:F2} MB",
                stats.TotalEntries,
                stats.ExpiringIn24Hours,
                stats.ExpiredEntries,
                stats.TotalSizeMB);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving idempotency statistics");
            throw;
        }
    }

    /// <summary>
    /// Computes SHA256 hash of result JSON for integrity verification
    /// </summary>
    private static string ComputeResultHash(string resultJson)
    {
        var bytes = Encoding.UTF8.GetBytes(resultJson);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Database row structure for idempotency entries
    /// </summary>
    private class IdempotencyRow
    {
        public string IdempotencyKey { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string ResultHash { get; set; } = string.Empty;
        public string ResultPayload { get; set; } = string.Empty;
        public DateTimeOffset CompletedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
