// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Caching;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using Polly;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Manages PostgreSQL connections, data sources, and connection pooling.
/// Handles connection string encryption/decryption and normalization.
/// </summary>
internal sealed class PostgresConnectionManager : DisposableBase
{
    private readonly ConcurrentDictionary<string, Lazy<NpgsqlDataSource>> _dataSources = new(StringComparer.Ordinal);
    private readonly IMemoryCache _decryptionCache;
    private readonly SemaphoreSlim _decryptionLock = new(1, 1);
    private readonly PostgresConnectionPoolMetrics _metrics;
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ResiliencePipeline _retryPipeline;

    public PostgresConnectionManager(
        PostgresConnectionPoolMetrics metrics,
        IMemoryCache memoryCache,
        IConnectionStringEncryptionService? encryptionService = null)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _decryptionCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _encryptionService = encryptionService;
        _retryPipeline = DatabaseRetryPolicy.CreatePostgresRetryPipeline();
    }

    public ResiliencePipeline RetryPipeline => _retryPipeline;

    public async Task<NpgsqlConnection> CreateConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var stopwatch = _metrics.CreateWaitTimeStopwatch();
        NpgsqlConnection connection;

        try
        {
            var dataSourceInstance = await GetOrCreateDataSourceAsync(dataSource, cancellationToken).ConfigureAwait(false);
            connection = dataSourceInstance.CreateConnection();
            _metrics.RecordPoolWaitTime(dataSource.ConnectionString, stopwatch.Elapsed);
        }
        catch (NpgsqlException npgsqlEx)
        {
            _metrics.RecordConnectionFailure(dataSource.ConnectionString, npgsqlEx);

            // Wrap Npgsql exceptions in domain-specific exceptions for better error handling
            if (npgsqlEx.IsTransient)
            {
                throw new DataStoreConnectionException(dataSource.Id, "Database connection failed transiently", npgsqlEx);
            }

            throw new DataStoreConnectionException(dataSource.Id, "Database connection failed", npgsqlEx);
        }
        catch (TimeoutException timeoutEx)
        {
            _metrics.RecordConnectionFailure(dataSource.ConnectionString, timeoutEx);
            throw new DataStoreTimeoutException("CreateConnection", "Connection attempt timed out", timeoutEx);
        }
        catch (Exception ex) when (ex is not DataStoreException)
        {
            _metrics.RecordConnectionFailure(dataSource.ConnectionString, ex);
            throw new DataStoreException($"Unexpected error creating connection to data source '{dataSource.Id}'", "CONNECTION_ERROR", ex);
        }

        return connection;
    }

    private async Task<NpgsqlDataSource> GetOrCreateDataSourceAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        var connectionString = await DecryptConnectionStringAsync(dataSource.ConnectionString, cancellationToken).ConfigureAwait(false);
        var normalized = NormalizeConnectionString(connectionString);
        var lazyDataSource = _dataSources.GetOrAdd(normalized, key => new Lazy<NpgsqlDataSource>(
            () => CreateDataSource(key),
            LazyThreadSafetyMode.ExecutionAndPublication));

        return lazyDataSource.Value;
    }

    private async Task<string> DecryptConnectionStringAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (_encryptionService == null)
        {
            return connectionString;
        }

        // SECURITY FIX: Use SHA256 instead of GetHashCode() to prevent cache key collisions
        // GetHashCode() is non-cryptographic and can collide, potentially mixing connection strings between tenants
        var cacheKey = $"connstr_decrypt_{ComputeStableHash(connectionString)}";

        // Check cache first (double-check locking pattern for async)
        // CRITICAL FIX: Cache results (strings), not tasks
        // Previously cached Task<string> which meant faulted tasks stayed in cache forever,
        // causing permanent connection failure after a single transient decryption error
        //
        // NEW: Uses IMemoryCache with TTL to support credential rotation
        // - Absolute expiration: 1 hour (rotated credentials picked up within 1 hour)
        // - Sliding expiration: 30 minutes (frequently-used connections stay cached)
        if (_decryptionCache.TryGetValue<string>(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        // Acquire lock to prevent duplicate decryption work
        await _decryptionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check: another thread may have added the value while we were waiting
            if (_decryptionCache.TryGetValue<string>(cacheKey, out var cachedResult2))
            {
                return cachedResult2;
            }

            // Decrypt and only cache if successful
            // If decryption fails, the exception propagates and nothing is cached,
            // allowing the next request to retry (transient failures can recover)
            var decryptedValue = await _encryptionService.DecryptAsync(connectionString, cancellationToken).ConfigureAwait(false);

            // Cache with TTL to support credential rotation
            // Uses ForDecryptedConnectionStrings: 1 hour absolute + 30 min sliding, high priority
            var cacheOptions = CacheOptionsBuilder.ForDecryptedConnectionStrings().BuildMemory();

            _decryptionCache.Set(cacheKey, decryptedValue, cacheOptions);

            return decryptedValue;
        }
        finally
        {
            _decryptionLock.Release();
        }
    }

    private static string NormalizeConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Connection string must be provided.", nameof(connectionString));
        }

        // SECURITY: Validate connection string for SQL injection and malformed input
        ConnectionStringValidator.Validate(connectionString, PostgresDataStoreProvider.ProviderKey);

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            EnsureConnectionDefaults(builder);
            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            if (TryConvertFromUri(connectionString, out var uriBuilder))
            {
                EnsureConnectionDefaults(uriBuilder);
                return uriBuilder.ConnectionString;
            }

            var filtered = FilterUnsupportedKeywords(connectionString);
            var builder = new NpgsqlConnectionStringBuilder(filtered);
            EnsureConnectionDefaults(builder);
            return builder.ConnectionString;
        }
    }

    private static void EnsureConnectionDefaults(NpgsqlConnectionStringBuilder builder)
    {
        if (builder.ApplicationName.IsNullOrWhiteSpace())
        {
            builder.ApplicationName = "Honua.Server";
        }

        // Connection pooling defaults (Npgsql 7+ uses NpgsqlDataSource with pooling enabled by default)
        // These settings optimize for web server workloads with many concurrent requests

        // Minimum pool size - keep warm connections ready (default: 0)
        // Setting to 2 reduces cold-start latency for first requests
        if (builder.MinPoolSize == 0)
        {
            builder.MinPoolSize = 2;
        }

        // Maximum pool size - limit concurrent connections (default: 100)
        // Web servers typically need 10-20 connections per CPU core
        // This default works for small-medium deployments (adjust via connection string for large deployments)
        if (builder.MaxPoolSize == 100)
        {
            builder.MaxPoolSize = 50; // More conservative default
        }

        // Connection lifetime - recycle connections periodically (default: 0 = infinite)
        // Helps with load balancer failover and connection health
        if (builder.ConnectionLifetime == 0)
        {
            builder.ConnectionLifetime = 600; // 10 minutes
        }

        // Command timeout - prevent long-running queries from blocking pool (default: 30 seconds)
        // Geographic queries can be slow, but 60s is a reasonable upper bound
        if (builder.CommandTimeout == 30)
        {
            builder.CommandTimeout = 60;
        }

        // Connection timeout - fail fast if database unavailable (default: 15 seconds)
        // Keep default of 15s

        // Idle lifetime - close idle connections to reduce resource usage (default: 300 seconds)
        // Keep default of 5 minutes

        // Performance optimizations
        builder.NoResetOnClose = false; // Reset connection state on return to pool (safer default)
        builder.Multiplexing = false;   // Multiplexing not compatible with all features
    }

    private static bool TryConvertFromUri(string value, out NpgsqlConnectionStringBuilder builder)
    {
        builder = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host
        };

        if (uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

        var path = uri.AbsolutePath.Trim('/');
        if (!path.IsNullOrEmpty())
        {
            builder.Database = Uri.UnescapeDataString(path);
        }

        if (!uri.UserInfo.IsNullOrEmpty())
        {
            var parts = uri.UserInfo.Split(':', 2, StringSplitOptions.TrimEntries);
            builder.Username = Uri.UnescapeDataString(parts[0]);
            if (parts.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(parts[1]);
            }
        }

        if (!uri.Query.IsNullOrEmpty())
        {
            var query = uri.Query.TrimStart('?');
            foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = segment.Split('=', 2);
                var key = Uri.UnescapeDataString(pair[0]);
                var val = pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty;
                if (key.HasValue())
                {
                    builder[key] = val;
                }
            }
        }

        return true;
    }

    private static string FilterUnsupportedKeywords(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = new System.Collections.Generic.List<string>();

        foreach (var part in parts)
        {
            if (part.IsNullOrEmpty())
            {
                continue;
            }

            if (part.StartsWith("Server=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Host=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Port=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("User ID=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("User Id=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Uid=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Username=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Pwd=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Application Name=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("SSL Mode=", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("Sslmode=", StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(part);
            }
        }

        if (filtered.Count == 0)
        {
            throw new ArgumentException("Unsupported connection string format.", nameof(connectionString));
        }

        return string.Join(';', filtered);
    }

    private static NpgsqlDataSource CreateDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        return dataSourceBuilder.Build();
    }

    /// <summary>
    /// Computes a stable, collision-resistant hash for cache keys.
    /// Uses SHA256 to avoid the security issues with GetHashCode() (non-cryptographic, randomized).
    /// </summary>
    private static string ComputeStableHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        // Take first 16 bytes (128 bits) for shorter cache key while maintaining collision resistance
        return Convert.ToHexString(hashBytes.AsSpan(0, 16));
    }

    protected override void DisposeCore()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated)
            {
                continue;
            }

            entry.Value.Dispose();
        }

        _dataSources.Clear();
        // Note: Don't dispose _decryptionCache - it's a shared IMemoryCache instance
        _decryptionLock.Dispose();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2215:Dispose methods should call base class dispose", Justification = "DisposableBase handles base disposal")]
    protected override async ValueTask DisposeCoreAsync()
    {
        foreach (var entry in _dataSources.Values)
        {
            if (!entry.IsValueCreated)
            {
                continue;
            }

            await entry.Value.DisposeAsync().ConfigureAwait(false);
        }

        _dataSources.Clear();
        // Note: Don't dispose _decryptionCache - it's a shared IMemoryCache instance
        _decryptionLock.Dispose();
    }
}
