// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Snowflake.Data.Client;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.Snowflake;

/// <summary>
/// Manages Snowflake connections and connection pooling.
/// Handles connection string normalization and encryption/decryption.
/// </summary>
internal sealed class SnowflakeConnectionManager : DisposableBase
{
    private readonly ConcurrentDictionary<string, Lazy<string>> _normalizedConnectionStrings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task<string>> _decryptionCache = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _decryptionLock = new(1, 1);
    private readonly IConnectionStringEncryptionService? _encryptionService;

    public SnowflakeConnectionManager(IConnectionStringEncryptionService? encryptionService = null)
    {
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Creates a Snowflake connection with connection pooling enabled.
    /// Connection strings are normalized with pooling defaults and cached for reuse.
    /// </summary>
    /// <param name="dataSource">Data source definition containing the connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A new Snowflake connection with pooling configured</returns>
    /// <exception cref="InvalidOperationException">Thrown if connection string is missing</exception>
    public async Task<SnowflakeDbConnection> CreateConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var connectionString = await DecryptConnectionStringAsync(dataSource.ConnectionString, cancellationToken).ConfigureAwait(false);

        // Normalize connection string with pooling defaults (cache the result)
        var normalizedCs = _normalizedConnectionStrings.GetOrAdd(
            connectionString,
            cs => new Lazy<string>(() => NormalizeConnectionString(cs), LazyThreadSafetyMode.ExecutionAndPublication)
        ).Value;

        return new SnowflakeDbConnection
        {
            ConnectionString = normalizedCs
        };
    }

    private async Task<string> DecryptConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_encryptionService == null)
        {
            return connectionString;
        }

        // Check cache first (double-check locking pattern for async)
        if (_decryptionCache.TryGetValue(connectionString, out var cachedTask))
        {
            return await cachedTask;
        }

        // Acquire lock to prevent duplicate decryption work
        await _decryptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check: another thread may have added the value while we were waiting
            if (_decryptionCache.TryGetValue(connectionString, out var cachedTask2))
            {
                return await cachedTask2;
            }

            // Start the decryption task and cache it
            var decryptionTask = _encryptionService.DecryptAsync(connectionString, cancellationToken);
            _decryptionCache[connectionString] = decryptionTask;

            return await decryptionTask;
        }
        finally
        {
            _decryptionLock.Release();
        }
    }

    /// <summary>
    /// Normalizes a Snowflake connection string with optimal pooling settings.
    /// Sets connection pooling parameters for high-performance server workloads.
    /// </summary>
    /// <param name="connectionString">Original connection string</param>
    /// <returns>Normalized connection string with pooling enabled</returns>
    private static string NormalizeConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        // SECURITY: Validate connection string for SQL injection and malformed input
        ConnectionStringValidator.Validate(connectionString, SnowflakeDataStoreProvider.ProviderKey);

        var builder = new SnowflakeDbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        // Configure connection pooling for optimal performance
        // Snowflake supports connection pooling similar to other ADO.NET providers

        // Set minimum pool size to keep warm connections ready (reduces cold-start latency)
        if (!builder.ContainsKey("minPoolSize") || builder["minPoolSize"]?.ToString().IsNullOrEmpty() == true)
        {
            builder["minPoolSize"] = "2";
        }

        // Set maximum pool size to limit concurrent connections
        // Snowflake typically handles 50-100 connections well per warehouse
        if (!builder.ContainsKey("maxPoolSize") || builder["maxPoolSize"]?.ToString().IsNullOrEmpty() == true)
        {
            builder["maxPoolSize"] = "50";
        }

        // Set connection timeout for acquiring connections from pool
        if (!builder.ContainsKey("connectionTimeout") || builder["connectionTimeout"]?.ToString().IsNullOrEmpty() == true)
        {
            builder["connectionTimeout"] = "60"; // 60 seconds
        }

        // Set application name for tracking in Snowflake query history
        if (!builder.ContainsKey("application") || builder["application"]?.ToString().IsNullOrEmpty() == true)
        {
            builder["application"] = "HonuaIO";
        }

        return builder.ConnectionString;
    }

    protected override void DisposeCore()
    {
        _normalizedConnectionStrings.Clear();
        _decryptionCache.Clear();
        _decryptionLock.Dispose();
    }
}
