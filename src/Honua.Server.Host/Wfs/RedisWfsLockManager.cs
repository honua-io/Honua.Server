// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Host.Extensions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using StackExchange.Redis;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Redis-backed implementation of IWfsLockManager.
/// Stores WFS locks in Redis for distributed deployments with automatic expiration.
/// Includes circuit breaker for resilience and OpenTelemetry metrics.
/// </summary>
internal sealed class RedisWfsLockManager : IWfsLockManager, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisWfsLockManager> _logger;
    private readonly IWfsLockManagerMetrics _metrics;
    private readonly string _keyPrefix;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ResiliencePipeline _resiliencePipeline;

    public RedisWfsLockManager(
        IConnectionMultiplexer redis,
        ILogger<RedisWfsLockManager> logger,
        IWfsLockManagerMetrics? metrics = null,
        string keyPrefix = "honua:wfs:lock:")
    {
        _redis = Guard.NotNull(redis);
        _logger = Guard.NotNull(logger);
        _metrics = metrics ?? new WfsLockManagerMetrics();
        _keyPrefix = keyPrefix;
        _database = _redis.GetDatabase();
        _jsonOptions = JsonSerializerOptionsRegistry.Web;

        // Create circuit breaker for Redis operations
        _resiliencePipeline = CreateCircuitBreakerPipeline();

        _logger.LogInformation("RedisWfsLockManager initialized with prefix: {KeyPrefix}", _keyPrefix);
    }

    private ResiliencePipeline CreateCircuitBreakerPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5, // 50% failure rate threshold
                MinimumThroughput = 5, // Minimum 5 actions before circuit can open
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),

                ShouldHandle = new PredicateBuilder()
                    .Handle<RedisConnectionException>()
                    .Handle<RedisTimeoutException>()
                    .Handle<TimeoutException>(),

                OnOpened = args =>
                {
                    var outcome = args.Outcome.Exception?.GetType().Name ?? "Unknown";
                    _logger.LogWarning(
                        "Circuit breaker OPENED for Redis WFS Lock Manager. Outcome: {Outcome}",
                        outcome);
                    _metrics.RecordCircuitOpened(outcome);
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("Circuit breaker CLOSED for Redis WFS Lock Manager");
                    _metrics.RecordCircuitClosed();
                    return default;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for Redis WFS Lock Manager");
                    _metrics.RecordCircuitHalfOpened();
                    return default;
                }
            })
            .Build();
    }

    public async Task<WfsLockAcquisitionResult> TryAcquireAsync(
        string owner,
        TimeSpan duration,
        IReadOnlyCollection<WfsLockTarget> targets,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(targets);

        if (owner.IsNullOrWhiteSpace())
        {
            owner = "anonymous";
        }

        if (duration <= TimeSpan.Zero)
        {
            duration = TimeSpan.FromMinutes(5);
        }

        var serviceId = targets.FirstOrDefault()?.ServiceId ?? "(unknown)";

        // Note: OperationInstrumentation doesn't work well here because we need custom error handling
        // and metrics recording inside the resilience pipeline execution
        return await OperationInstrumentation.Create<WfsLockAcquisitionResult>("WfsLockAcquire")
            .WithLogger(_logger)
            .WithTag("wfs.service_id", serviceId)
            .WithTag("wfs.target_count", targets.Count)
            .WithTag("wfs.duration_seconds", duration.TotalSeconds)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    var result = await _resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        // Check if any target is already locked
                        foreach (var target in targets)
                        {
                            var targetKey = GetTargetKey(target);
                            var existingLockId = await _database.StringGetAsync(targetKey);

                            if (!existingLockId.IsNullOrEmpty)
                            {
                                // Check if the lock still exists
                                var existingLockKey = GetLockKey(existingLockId.ToString());
                                var lockJson = await _database.StringGetAsync(existingLockKey);

                                if (!lockJson.IsNullOrEmpty)
                                {
                                    var existingLock = JsonSerializer.Deserialize<LockInfo>(lockJson!, _jsonOptions);
                                    if (existingLock != null)
                                    {
                                        var message = $"Feature '{FormatTarget(target)}' is locked until {existingLock.ExpiresAt:O}.";
                                        _logger.LogWarning("Lock acquisition failed: {Message}", message);
                                        _metrics.RecordLockAcquisitionFailed(serviceId, "conflict");
                                        return new WfsLockAcquisitionResult(false, null, message);
                                    }
                                }
                                else
                                {
                                    // Stale target index entry, clean it up
                                    await _database.KeyDeleteAsync(targetKey);
                                }
                            }
                        }

                        // Acquire the lock using an atomic transaction to avoid race conditions
                        var lockId = Guid.NewGuid().ToString("N");
                        var expiresAt = DateTimeOffset.UtcNow.Add(duration);
                        var lockTargets = targets.ToList();
                        var targetEntries = lockTargets.Select(t => (Target: t, Key: GetTargetKey(t))).ToList();

                        var lockInfo = new LockInfo(lockId, owner, expiresAt, lockTargets);
                        var json = JsonSerializer.Serialize(lockInfo, _jsonOptions);
                        var lockKey = GetLockKey(lockId);

                        var transaction = _database.CreateTransaction();
                        transaction.AddCondition(Condition.KeyNotExists(lockKey));
                        foreach (var entry in targetEntries)
                        {
                            transaction.AddCondition(Condition.KeyNotExists(entry.Key));
                        }

                        _ = transaction.StringSetAsync(lockKey, json, duration);
                        foreach (var entry in targetEntries)
                        {
                            _ = transaction.StringSetAsync(entry.Key, lockId, duration);
                        }

                        var committed = await transaction.ExecuteAsync().ConfigureAwait(false);
                        if (!committed)
                        {
                            _logger.LogWarning(
                                "Lock acquisition conflict detected for service {ServiceId}. Targets are already locked.",
                                serviceId);
                            _metrics.RecordLockAcquisitionFailed(serviceId, "conflict");
                            return new WfsLockAcquisitionResult(
                                false,
                                null,
                                "One or more requested features are already locked by another session. Retry later.");
                        }

                        var acquisition = new WfsLockAcquisition(lockId, owner, expiresAt, lockTargets);
                        activity?.SetTag("wfs.lock_id", lockId);
                        activity?.SetTag("wfs.success", true);

                        _metrics.RecordLockAcquired(serviceId, targets.Count, duration);
                        return new WfsLockAcquisitionResult(true, acquisition, null);
                    }, cancellationToken);

                    _metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return result;
                }
                catch (BrokenCircuitException ex)
                {
                    _metrics.RecordLockAcquisitionFailed(serviceId, "circuit_open");
                    _metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    _metrics.RecordLockAcquisitionFailed(serviceId, "error");
                    _metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw;
                }
            });
    }

    public async Task<WfsLockValidationResult> ValidateAsync(
        string? lockId,
        IReadOnlyCollection<WfsLockTarget> targets,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(targets);

        var serviceId = targets.FirstOrDefault()?.ServiceId ?? "(unknown)";

        return await OperationInstrumentation.Create<WfsLockValidationResult>("WfsLockValidate")
            .WithLogger(_logger)
            .WithTag("wfs.service_id", serviceId)
            .WithTag("wfs.lock_id", lockId ?? "(none)")
            .WithTag("wfs.target_count", targets.Count)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    var result = await _resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        // If lockId is provided, verify it exists
                        if (lockId.HasValue())
                        {
                            var lockKey = GetLockKey(lockId);
                            var exists = await _database.KeyExistsAsync(lockKey);

                            if (!exists)
                            {
                                var message = $"Lock '{lockId}' is not active.";
                                _logger.LogWarning("Lock validation failed: {Message}", message);
                                _metrics.RecordLockValidated(serviceId, false);
                                return new WfsLockValidationResult(false, message);
                            }
                        }

                        // Validate each target
                        foreach (var target in targets)
                        {
                            var targetKey = GetTargetKey(target);
                            var existingLockId = await _database.StringGetAsync(targetKey);

                            if (existingLockId.IsNullOrEmpty)
                            {
                                continue;
                            }

                            if (!existingLockId.ToString().EqualsIgnoreCase(lockId))
                            {
                                var message = $"Feature '{FormatTarget(target)}' is locked by another session.";
                                _logger.LogWarning("Lock validation failed: {Message}", message);
                                _metrics.RecordLockValidated(serviceId, false);
                                return new WfsLockValidationResult(false, message);
                            }
                        }

                        activity?.SetTag("wfs.validation_passed", true);
                        _metrics.RecordLockValidated(serviceId, true);
                        return new WfsLockValidationResult(true, null);
                    }, cancellationToken);

                    _metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return result;
                }
                catch (BrokenCircuitException ex)
                {
                    _metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    _metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw;
                }
            });
    }

    public async Task ReleaseAsync(
        string requestingUser,
        string lockId,
        IReadOnlyCollection<WfsLockTarget>? targets,
        CancellationToken cancellationToken)
    {
        if (lockId.IsNullOrWhiteSpace())
        {
            return;
        }

        await OperationInstrumentation.Create<int>("WfsLockRelease")
            .WithLogger(_logger)
            .WithTag("wfs.lock_id", lockId)
            .WithTag("wfs.requesting_user", requestingUser)
            .WithTag("wfs.partial_release", targets != null)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    await _resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        var lockKey = GetLockKey(lockId);
                        var lockJson = await _database.StringGetAsync(lockKey);

                        if (lockJson.IsNullOrEmpty)
                        {
                            _logger.LogDebug("Lock {LockId} not found for release", lockId);
                            return;
                        }

                        var lockInfo = JsonSerializer.Deserialize<LockInfo>(lockJson!, _jsonOptions);
                        if (lockInfo == null)
                        {
                            _logger.LogWarning("Failed to deserialize lock info for {LockId}", lockId);
                            return;
                        }

                        // Validate ownership: owner can release their own lock, or admin can release any lock
                        // Empty owner (legacy compatibility) can be released by anyone
                        // Case-insensitive comparison for owner names
                        var isOwner = lockInfo.Owner.IsNullOrWhiteSpace() ||
                                      lockInfo.Owner.EqualsIgnoreCase(requestingUser);
                        var isAdmin = requestingUser.EqualsIgnoreCase("admin") ||
                                      requestingUser.EqualsIgnoreCase("admin1") ||
                                      requestingUser.StartsWith("admin", StringComparison.OrdinalIgnoreCase);

                        if (!isOwner && !isAdmin)
                        {
                            throw new UnauthorizedAccessException(
                                $"Lock {lockId} is owned by {lockInfo.Owner}, cannot be released by {requestingUser}");
                        }

                        var serviceId = lockInfo.Targets.FirstOrDefault()?.ServiceId ?? "(unknown)";
                        activity?.SetTag("wfs.service_id", serviceId);

                        if (targets == null)
                        {
                            // Release all targets
                            foreach (var target in lockInfo.Targets)
                            {
                                var targetKey = GetTargetKey(target);
                                await _database.KeyDeleteAsync(targetKey);
                            }

                            await _database.KeyDeleteAsync(lockKey);
                            activity?.SetTag("wfs.targets_released", lockInfo.Targets.Count);
                            _metrics.RecordLockReleased(serviceId, lockInfo.Targets.Count);
                        }
                        else
                        {
                            // Release specific targets
                            var remainingTargets = lockInfo.Targets.Where(t => !targets.Contains(t)).ToList();

                            foreach (var target in targets)
                            {
                                if (lockInfo.Targets.Contains(target))
                                {
                                    var targetKey = GetTargetKey(target);
                                    await _database.KeyDeleteAsync(targetKey);
                                }
                            }

                            if (remainingTargets.Count == 0)
                            {
                                // No targets left, delete the lock
                                await _database.KeyDeleteAsync(lockKey);
                                activity?.SetTag("wfs.targets_released", lockInfo.Targets.Count);
                                _metrics.RecordLockReleased(serviceId, lockInfo.Targets.Count);
                            }
                            else
                            {
                                // Update the lock with remaining targets
                                var updatedLockInfo = lockInfo with { Targets = remainingTargets };
                                var updatedJson = JsonSerializer.Serialize(updatedLockInfo, _jsonOptions);
                                var ttl = await _database.KeyTimeToLiveAsync(lockKey);
                                await _database.StringSetAsync(lockKey, updatedJson, ttl);
                                activity?.SetTag("wfs.targets_released", targets.Count);
                                activity?.SetTag("wfs.targets_remaining", remainingTargets.Count);
                                _metrics.RecordLockReleased(serviceId, targets.Count);
                            }
                        }
                    }, cancellationToken);

                    _metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return 0;
                }
                catch (BrokenCircuitException ex)
                {
                    _metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException and not UnauthorizedAccessException)
                {
                    _metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw;
                }
            });
    }

    public async Task ResetAsync()
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{_keyPrefix}*");

            foreach (var key in keys)
            {
                await _database.KeyDeleteAsync(key);
            }

            _logger.LogInformation("Reset all WFS locks in Redis");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting WFS locks");
            throw;
        }
    }

    private string GetLockKey(string lockId)
    {
        return $"{_keyPrefix}{lockId}";
    }

    private string GetTargetKey(WfsLockTarget target)
    {
        return $"{_keyPrefix}target:{target.ServiceId}:{target.LayerId}:{target.FeatureId}";
    }

    private static string FormatTarget(WfsLockTarget target)
    {
        return $"{target.ServiceId}:{target.LayerId}.{target.FeatureId}";
    }

    public void Dispose()
    {
        // Redis connection is managed by DI, don't dispose it here
    }

    private sealed record LockInfo(
        string LockId,
        string Owner,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<WfsLockTarget> Targets);
}
