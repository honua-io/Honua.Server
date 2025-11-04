// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Redis-backed implementation of IProcessStateStore.
/// Stores process state in Redis with automatic expiration.
/// </summary>
public class RedisProcessStateStore : IProcessStateStore, IHealthCheck, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly RedisOptions _options;
    private readonly ILogger<RedisProcessStateStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public RedisProcessStateStore(
        IConnectionMultiplexer redis,
        IOptions<RedisOptions> options,
        ILogger<RedisProcessStateStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _database = _redis.GetDatabase();
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 64
        };

        _logger.LogInformation("RedisProcessStateStore initialized with prefix: {KeyPrefix}", _options.KeyPrefix);
    }

    /// <inheritdoc/>
    public async Task<ProcessInfo?> GetProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        try
        {
            var key = GetRedisKey(processId);
            var json = await _database.StringGetAsync(key);

            if (json.IsNullOrEmpty)
            {
                _logger.LogDebug("Process {ProcessId} not found in Redis", processId);
                return null;
            }

            var processInfo = JsonSerializer.Deserialize<ProcessInfo>(json!, _jsonOptions);
            _logger.LogDebug("Retrieved process {ProcessId} from Redis", processId);
            return processInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving process {ProcessId} from Redis", processId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task SaveProcessAsync(ProcessInfo processInfo, CancellationToken cancellationToken = default)
    {
        if (processInfo == null)
        {
            throw new ArgumentNullException(nameof(processInfo));
        }

        if (string.IsNullOrWhiteSpace(processInfo.ProcessId))
        {
            throw new ArgumentException("ProcessInfo.ProcessId cannot be null or empty", nameof(processInfo));
        }

        try
        {
            var key = GetRedisKey(processInfo.ProcessId);
            var json = JsonSerializer.Serialize(processInfo, _jsonOptions);
            var expiry = TimeSpan.FromSeconds(_options.TtlSeconds);

            await _database.StringSetAsync(key, json, expiry);

            // Also add to active processes set if status is Running or Pending
            if (processInfo.Status == "Running" || processInfo.Status == "Pending")
            {
                await _database.SetAddAsync(GetActiveSetKey(), processInfo.ProcessId);
            }
            else
            {
                // Remove from active set if completed/failed
                await _database.SetRemoveAsync(GetActiveSetKey(), processInfo.ProcessId);
            }

            _logger.LogDebug("Saved process {ProcessId} to Redis with TTL {TtlSeconds}s",
                processInfo.ProcessId, _options.TtlSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving process {ProcessId} to Redis", processInfo.ProcessId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateProcessStatusAsync(
        string processId,
        string status,
        int? completionPercentage = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
        }

        try
        {
            // Get existing process
            var processInfo = await GetProcessAsync(processId, cancellationToken);
            if (processInfo == null)
            {
                _logger.LogWarning("Cannot update status for non-existent process {ProcessId}", processId);
                throw new InvalidOperationException($"Process {processId} not found");
            }

            // Update fields
            processInfo.Status = status;
            if (completionPercentage.HasValue)
            {
                processInfo.CompletionPercentage = completionPercentage.Value;
            }
            if (errorMessage != null)
            {
                processInfo.ErrorMessage = errorMessage;
            }

            // Set end time if completed or failed
            if (status == "Completed" || status == "Failed")
            {
                processInfo.EndTime = DateTime.UtcNow;
            }

            // Save updated process
            await SaveProcessAsync(processInfo, cancellationToken);

            _logger.LogDebug("Updated process {ProcessId} status to {Status}", processId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating process {ProcessId} status", processId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ProcessInfo>> GetActiveProcessesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var activeSetKey = GetActiveSetKey();
            var processIds = await _database.SetMembersAsync(activeSetKey);

            var processes = new List<ProcessInfo>();
            foreach (var processId in processIds)
            {
                var process = await GetProcessAsync(processId.ToString(), cancellationToken);
                if (process != null)
                {
                    processes.Add(process);
                }
                else
                {
                    // Clean up stale entry in active set
                    await _database.SetRemoveAsync(activeSetKey, processId);
                }
            }

            _logger.LogDebug("Retrieved {Count} active processes from Redis", processes.Count);
            return processes.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active processes from Redis");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        try
        {
            var key = GetRedisKey(processId);
            var deleted = await _database.KeyDeleteAsync(key);

            // Also remove from active set
            await _database.SetRemoveAsync(GetActiveSetKey(), processId);

            if (deleted)
            {
                _logger.LogDebug("Deleted process {ProcessId} from Redis", processId);
            }
            else
            {
                _logger.LogDebug("Process {ProcessId} not found for deletion", processId);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting process {ProcessId} from Redis", processId);
            throw;
        }
    }

    /// <summary>
    /// Gets the full Redis key for a process ID.
    /// </summary>
    private string GetRedisKey(string processId)
    {
        return $"{_options.KeyPrefix}{processId}";
    }

    /// <summary>
    /// Gets the Redis key for the active processes set.
    /// </summary>
    private string GetActiveSetKey()
    {
        return $"{_options.KeyPrefix}active";
    }

    /// <inheritdoc/>
    public async Task<bool> CancelProcessAsync(string processId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process ID cannot be null or empty", nameof(processId));
        }

        try
        {
            // Get existing process
            var processInfo = await GetProcessAsync(processId, cancellationToken);
            if (processInfo == null)
            {
                _logger.LogWarning("Cannot cancel non-existent process {ProcessId}", processId);
                return false;
            }

            // Only cancel if process is still running or pending
            if (processInfo.Status != "Running" && processInfo.Status != "Pending")
            {
                _logger.LogWarning("Cannot cancel process {ProcessId} with status {Status}", processId, processInfo.Status);
                return false;
            }

            // Update status to Cancelled
            await UpdateProcessStatusAsync(processId, "Cancelled", cancellationToken: cancellationToken);

            _logger.LogInformation("Process {ProcessId} marked as cancelled", processId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling process {ProcessId}", processId);
            throw;
        }
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Try a simple PING command
            var latency = await db.PingAsync();

            if (latency.TotalMilliseconds > 1000)
            {
                return HealthCheckResult.Degraded(
                    $"Redis connection is slow ({latency.TotalMilliseconds:F0}ms)");
            }

            return HealthCheckResult.Healthy(
                $"Redis is healthy (ping: {latency.TotalMilliseconds:F0}ms)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Redis connection failed",
                ex);
        }
    }

    public void Dispose()
    {
        // Redis connection is managed by DI, don't dispose it here
    }
}
