// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;

namespace Honua.Server.Core.Locking;

/// <summary>
/// Redis-based distributed lock implementation using SET NX EX pattern.
/// Provides distributed coordination for multi-instance deployments.
/// </summary>
/// <remarks>
/// Uses Redis SET with NX (not exists) and EX (expiry) options for atomic lock acquisition.
/// Implements proper lock release verification to prevent releasing locks owned by other instances.
/// Includes OpenTelemetry instrumentation for observability.
/// </remarks>
public sealed class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisDistributedLock> _logger;
    private readonly string _keyPrefix;
    private static readonly ActivitySource ActivitySource = new("Honua.Locking.Redis");

    public RedisDistributedLock(
        IConnectionMultiplexer redis,
        ILogger<RedisDistributedLock> logger,
        string keyPrefix = "honua:lock:")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keyPrefix = keyPrefix;
        _database = _redis.GetDatabase();
    }

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (timeout <= TimeSpan.Zero)
            throw new ArgumentException("Timeout must be positive", nameof(timeout));

        if (expiry <= TimeSpan.Zero)
            throw new ArgumentException("Expiry must be positive", nameof(expiry));

        return await OperationInstrumentation.Create<IDistributedLockHandle?>("DistributedLock.Acquire")
            .WithLogger(_logger)
            .WithTag("lock.key", key)
            .WithTag("lock.timeout_ms", timeout.TotalMilliseconds)
            .WithTag("lock.expiry_ms", expiry.TotalMilliseconds)
            .ExecuteAsync(async activity =>
            {
                var redisKey = GetRedisKey(key);
                var lockValue = GenerateLockValue();
                var startTime = DateTimeOffset.UtcNow;
                var deadline = startTime + timeout;

                _logger.LogDebug(
                    "Attempting to acquire distributed lock: Key={Key}, Timeout={Timeout}, Expiry={Expiry}",
                    key, timeout, expiry);

                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // Use SET with NX (not exists) and EX (expiry) for atomic lock acquisition
                        var acquired = await _database.StringSetAsync(
                            redisKey,
                            lockValue,
                            expiry,
                            When.NotExists,
                            CommandFlags.None).ConfigureAwait(false);

                        if (acquired)
                        {
                            var now = DateTimeOffset.UtcNow;
                            var handle = new RedisDistributedLockHandle(
                                key,
                                redisKey,
                                lockValue,
                                now,
                                now + expiry,
                                _database,
                                _logger);

                            activity?.SetTag("lock.acquired", true);
                            activity?.SetTag("lock.wait_time_ms", (now - startTime).TotalMilliseconds);

                            _logger.LogDebug(
                                "Distributed lock acquired: Key={Key}, Value={Value}, ExpiresAt={ExpiresAt}",
                                key, lockValue, handle.ExpiresAt);

                            return handle;
                        }

                        // Lock is held by another instance, wait and retry
                        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                    }
                    catch (RedisException ex)
                    {
                        _logger.LogWarning(ex,
                            "Redis error while acquiring lock: Key={Key}. Will retry if time permits.",
                            key);

                        // Wait before retry on error
                        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
                    }
                }

                // Timeout - could not acquire lock
                activity?.SetTag("lock.acquired", false);
                activity?.SetTag("lock.timeout", true);

                _logger.LogWarning(
                    "Failed to acquire distributed lock within timeout: Key={Key}, Timeout={Timeout}",
                    key, timeout);

                return null;
            }).ConfigureAwait(false);
    }

    private string GetRedisKey(string key)
    {
        return $"{_keyPrefix}{key}";
    }

    private static string GenerateLockValue()
    {
        // Unique value per lock acquisition to prevent releasing locks owned by other instances
        // Format: {machineId}_{processId}_{guid}
        var machineId = Environment.MachineName;
        var processId = Environment.ProcessId;
        var uniqueId = Guid.NewGuid().ToString("N");
        return $"{machineId}_{processId}_{uniqueId}";
    }
}

/// <summary>
/// Handle for a Redis-based distributed lock.
/// Ensures proper lock release with ownership verification.
/// </summary>
internal sealed class RedisDistributedLockHandle : IDistributedLockHandle
{
    private readonly string _redisKey;
    private readonly string _lockValue;
    private readonly IDatabase _database;
    private readonly ILogger _logger;
    private int _disposed;

    public RedisDistributedLockHandle(
        string key,
        string redisKey,
        string lockValue,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt,
        IDatabase database,
        ILogger logger)
    {
        Key = key;
        _redisKey = redisKey;
        _lockValue = lockValue;
        AcquiredAt = acquiredAt;
        ExpiresAt = expiresAt;
        _database = database;
        _logger = logger;
    }

    public string Key { get; }
    public DateTimeOffset AcquiredAt { get; }
    public DateTimeOffset ExpiresAt { get; }

    public void Dispose()
    {
        // BLOCKING ASYNC CALL: Required to implement IDisposable interface.
        // ASP.NET Core best practice: Provide both Dispose and DisposeAsync for resources.
        // Callers should prefer using DisposeAsync (via 'await using') when possible.
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        try
        {
            // Only delete the key if it still holds our lock value
            // This prevents releasing a lock that was already expired and re-acquired by another instance
            var script = @"
                if redis.call('get', KEYS[1]) == ARGV[1] then
                    return redis.call('del', KEYS[1])
                else
                    return 0
                end";

            var result = await _database.ScriptEvaluateAsync(
                script,
                new RedisKey[] { _redisKey },
                new RedisValue[] { _lockValue }).ConfigureAwait(false);

            if ((long)result == 1)
            {
                _logger.LogDebug(
                    "Distributed lock released: Key={Key}, Value={Value}",
                    Key, _lockValue);
            }
            else
            {
                _logger.LogWarning(
                    "Distributed lock was already expired or acquired by another instance: Key={Key}, Value={Value}",
                    Key, _lockValue);
            }
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error while releasing distributed lock: Key={Key}",
                Key);
        }
    }
}
