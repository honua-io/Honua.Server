// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Cache for prepared statements in PostgreSQL.
/// Tracks prepared statements per connection to improve query performance.
/// </summary>
internal sealed class PostgresPreparedStatementCache
{
    private readonly ConcurrentDictionary<string, PreparedStatementEntry> _cache = new();
    private readonly ILogger<PostgresPreparedStatementCache>? _logger;
    private long _cacheHits;
    private long _cacheMisses;
    private long _prepareCalls;

    public PostgresPreparedStatementCache(ILogger<PostgresPreparedStatementCache>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Prepare a command if not already prepared.
    /// Returns true if the statement was newly prepared, false if already cached.
    /// </summary>
    public async Task<bool> PrepareIfNeededAsync(
        NpgsqlCommand command,
        string querySignature,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(querySignature))
        {
            throw new ArgumentException("Query signature cannot be null or empty", nameof(querySignature));
        }

        // Check if already prepared on this connection
        var connectionId = command.Connection?.ProcessID ?? 0;
        var cacheKey = $"{connectionId}:{querySignature}";

        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            Interlocked.Increment(ref _cacheHits);
            _logger?.LogTrace("Prepared statement cache hit: {QuerySignature}", querySignature);
            return false; // Already prepared
        }

        // Prepare the statement
        try
        {
            await command.PrepareAsync(cancellationToken).ConfigureAwait(false);
            Interlocked.Increment(ref _prepareCalls);

            // Cache the prepared statement metadata
            var newEntry = new PreparedStatementEntry
            {
                QuerySignature = querySignature,
                ConnectionId = connectionId,
                PreparedAt = DateTime.UtcNow
            };

            _cache.TryAdd(cacheKey, newEntry);
            Interlocked.Increment(ref _cacheMisses);

            _logger?.LogDebug(
                "Prepared statement: {QuerySignature}, Connection: {ConnectionId}",
                querySignature, connectionId);

            return true; // Newly prepared
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "Failed to prepare statement: {QuerySignature}. Query will execute without preparation.",
                querySignature);
            return false;
        }
    }

    /// <summary>
    /// Invalidate prepared statements for a specific connection.
    /// Called when a connection is closed or reset.
    /// </summary>
    public void InvalidateConnection(int connectionId)
    {
        var removed = 0;
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith($"{connectionId}:", StringComparison.Ordinal))
            {
                if (_cache.TryRemove(key, out _))
                {
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            _logger?.LogDebug(
                "Invalidated {Count} prepared statements for connection {ConnectionId}",
                removed, connectionId);
        }
    }

    /// <summary>
    /// Clear all cached prepared statement metadata.
    /// Does not unprepare statements on the server.
    /// </summary>
    public void Clear()
    {
        var count = _cache.Count;
        _cache.Clear();
        _logger?.LogInformation("Cleared {Count} prepared statement cache entries", count);
    }

    /// <summary>
    /// Get cache statistics.
    /// </summary>
    public PreparedStatementCacheStats GetStatistics()
    {
        return new PreparedStatementCacheStats
        {
            CacheHits = Interlocked.Read(ref _cacheHits),
            CacheMisses = Interlocked.Read(ref _cacheMisses),
            PrepareCalls = Interlocked.Read(ref _prepareCalls),
            CachedStatements = _cache.Count
        };
    }

    private sealed class PreparedStatementEntry
    {
        public string QuerySignature { get; init; } = "";
        public int ConnectionId { get; init; }
        public DateTime PreparedAt { get; init; }
    }
}

/// <summary>
/// Statistics for prepared statement cache.
/// </summary>
public sealed class PreparedStatementCacheStats
{
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public long PrepareCalls { get; init; }
    public int CachedStatements { get; init; }

    public double HitRate
    {
        get
        {
            var total = CacheHits + CacheMisses;
            return total > 0 ? (double)CacheHits / total : 0;
        }
    }
}

/// <summary>
/// Helper to generate query signatures for prepared statement caching.
/// </summary>
internal static class QuerySignatureGenerator
{
    /// <summary>
    /// Generate a signature for a query based on operation type and table/layer.
    /// Examples:
    /// - "GetById:public.buildings"
    /// - "Query:public.parcels"
    /// - "Count:public.sensors"
    /// </summary>
    public static string Generate(string operationType, string tableName)
    {
        return $"{operationType}:{tableName}";
    }

    /// <summary>
    /// Generate a signature for a layer-based query.
    /// </summary>
    public static string Generate(string operationType, string serviceId, string layerId)
    {
        return $"{operationType}:{serviceId}:{layerId}";
    }
}
