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
    private readonly IConnectionMultiplexer redis;
    private readonly IDatabase database;
    private readonly ILogger<RedisWfsLockManager> logger;
    private readonly IWfsLockManagerMetrics metrics;
    private readonly string keyPrefix;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly ResiliencePipeline resiliencePipeline;

    public RedisWfsLockManager(
        IConnectionMultiplexer redis,
        ILogger<RedisWfsLockManager> logger,
        IWfsLockManagerMetrics? metrics = null,
        string keyPrefix = "honua:wfs:lock:")
    {
        this.redis = Guard.NotNull(redis);
        this.logger = Guard.NotNull(logger);
        this.metrics = metrics ?? new WfsLockManagerMetrics();
        this.keyPrefix = keyPrefix;
        this.database = this.redis.GetDatabase();
        this.jsonOptions = JsonSerializerOptionsRegistry.Web;

        // Create circuit breaker for Redis operations
        this.resiliencePipeline = CreateCircuitBreakerPipeline();

        this.logger.LogInformation("RedisWfsLockManager initialized with prefix: {KeyPrefix}", this.keyPrefix);
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
                    this.logger.LogWarning(
                        "Circuit breaker OPENED for Redis WFS Lock Manager. Outcome: {Outcome}",
                        outcome);
                    this.metrics.RecordCircuitOpened(outcome);
                    return default;
                },
                OnClosed = args =>
                {
                    this.logger.LogInformation("Circuit breaker CLOSED for Redis WFS Lock Manager");
                    this.metrics.RecordCircuitClosed();
                    return default;
                },
                OnHalfOpened = args =>
                {
                    this.logger.LogInformation("Circuit breaker HALF-OPEN for Redis WFS Lock Manager");
                    this.metrics.RecordCircuitHalfOpened();
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
            .WithLogger(this.logger)
            .WithTag("wfs.service_id", serviceId)
            .WithTag("wfs.target_count", targets.Count)
            .WithTag("wfs.duration_seconds", duration.TotalSeconds)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    var result = await this.resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        // Check if any target is already locked
                        foreach (var target in targets)
                        {
                            var targetKey = GetTargetKey(target);
                            var existingLockId = await this.database.StringGetAsync(targetKey);

                            if (!existingLockId.IsNullOrEmpty)
                            {
                                // Check if the lock still exists
                                var existingLockKey = GetLockKey(existingLockId.ToString());
                                var lockJson = await this.database.StringGetAsync(existingLockKey);

                                if (!lockJson.IsNullOrEmpty)
                                {
                                    var existingLock = JsonSerializer.Deserialize<LockInfo>(lockJson!, this.jsonOptions);
                                    if (existingLock != null)
                                    {
                                        var message = $"Feature '{FormatTarget(target)}' is locked until {existingLock.ExpiresAt:O}.";
                                        this.logger.LogWarning("Lock acquisition failed: {Message}", message);
                                        this.metrics.RecordLockAcquisitionFailed(serviceId, "conflict");
                                        return new WfsLockAcquisitionResult(false, null, message);
                                    }
                                }
                                else
                                {
                                    // Stale target index entry, clean it up
                                    await this.database.KeyDeleteAsync(targetKey);
                                }
                            }
                        }

                        // Acquire the lock using an atomic transaction to avoid race conditions
                        var lockId = Guid.NewGuid().ToString("N");
                        var expiresAt = DateTimeOffset.UtcNow.Add(duration);
                        var lockTargets = targets.ToList();
                        var targetEntries = lockTargets.Select(t => (Target: t, Key: GetTargetKey(t))).ToList();

                        var lockInfo = new LockInfo(lockId, owner, expiresAt, lockTargets);
                        var json = JsonSerializer.Serialize(lockInfo, this.jsonOptions);
                        var lockKey = GetLockKey(lockId);

                        var transaction = this.database.CreateTransaction();
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
                            this.logger.LogWarning(
                                "Lock acquisition conflict detected for service {ServiceId}. Targets are already locked.",
                                serviceId);
                            this.metrics.RecordLockAcquisitionFailed(serviceId, "conflict");
                            return new WfsLockAcquisitionResult(
                                false,
                                null,
                                "One or more requested features are already locked by another session. Retry later.");
                        }

                        var acquisition = new WfsLockAcquisition(lockId, owner, expiresAt, lockTargets);
                        activity?.SetTag("wfs.lock_id", lockId);
                        activity?.SetTag("wfs.success", true);

                        this.metrics.RecordLockAcquired(serviceId, targets.Count, duration);
                        return new WfsLockAcquisitionResult(true, acquisition, null);
                    }, cancellationToken);

                    this.metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return result;
                }
                catch (BrokenCircuitException ex)
                {
                    this.metrics.RecordLockAcquisitionFailed(serviceId, "circuit_open");
                    this.metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    this.metrics.RecordLockAcquisitionFailed(serviceId, "error");
                    this.metrics.RecordOperationLatency("acquire", Activity.Current?.Duration ?? TimeSpan.Zero);
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
            .WithLogger(this.logger)
            .WithTag("wfs.service_id", serviceId)
            .WithTag("wfs.lock_id", lockId ?? "(none)")
            .WithTag("wfs.target_count", targets.Count)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    var result = await this.resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        // If lockId is provided, verify it exists
                        if (lockId.HasValue())
                        {
                            var lockKey = GetLockKey(lockId);
                            var exists = await this.database.KeyExistsAsync(lockKey);

                            if (!exists)
                            {
                                var message = $"Lock '{lockId}' is not active.";
                                this.logger.LogWarning("Lock validation failed: {Message}", message);
                                this.metrics.RecordLockValidated(serviceId, false);
                                return new WfsLockValidationResult(false, message);
                            }
                        }

                        // Validate each target
                        foreach (var target in targets)
                        {
                            var targetKey = GetTargetKey(target);
                            var existingLockId = await this.database.StringGetAsync(targetKey);

                            if (existingLockId.IsNullOrEmpty)
                            {
                                continue;
                            }

                            if (!existingLockId.ToString().EqualsIgnoreCase(lockId))
                            {
                                var message = $"Feature '{FormatTarget(target)}' is locked by another session.";
                                this.logger.LogWarning("Lock validation failed: {Message}", message);
                                this.metrics.RecordLockValidated(serviceId, false);
                                return new WfsLockValidationResult(false, message);
                            }
                        }

                        activity?.SetTag("wfs.validation_passed", true);
                        this.metrics.RecordLockValidated(serviceId, true);
                        return new WfsLockValidationResult(true, null);
                    }, cancellationToken);

                    this.metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return result;
                }
                catch (BrokenCircuitException ex)
                {
                    this.metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException)
                {
                    this.metrics.RecordOperationLatency("validate", Activity.Current?.Duration ?? TimeSpan.Zero);
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
            .WithLogger(this.logger)
            .WithTag("wfs.lock_id", lockId)
            .WithTag("wfs.requesting_user", requestingUser)
            .WithTag("wfs.partial_release", targets != null)
            .ExecuteAsync(async activity =>
            {
                try
                {
                    await this.resiliencePipeline.ExecuteAsync(async ct =>
                    {
                        var lockKey = GetLockKey(lockId);
                        var lockJson = await this.database.StringGetAsync(lockKey);

                        if (lockJson.IsNullOrEmpty)
                        {
                            this.logger.LogDebug("Lock {LockId} not found for release", lockId);
                            return;
                        }

                        var lockInfo = JsonSerializer.Deserialize<LockInfo>(lockJson!, this.jsonOptions);
                        if (lockInfo == null)
                        {
                            this.logger.LogWarning("Failed to deserialize lock info for {LockId}", lockId);
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
                                await this.database.KeyDeleteAsync(targetKey);
                            }

                            await this.database.KeyDeleteAsync(lockKey);
                            activity?.SetTag("wfs.targets_released", lockInfo.Targets.Count);
                            this.metrics.RecordLockReleased(serviceId, lockInfo.Targets.Count);
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
                                    await this.database.KeyDeleteAsync(targetKey);
                                }
                            }

                            if (remainingTargets.Count == 0)
                            {
                                // No targets left, delete the lock
                                await this.database.KeyDeleteAsync(lockKey);
                                activity?.SetTag("wfs.targets_released", lockInfo.Targets.Count);
                                this.metrics.RecordLockReleased(serviceId, lockInfo.Targets.Count);
                            }
                            else
                            {
                                // Update the lock with remaining targets
                                var updatedLockInfo = lockInfo with { Targets = remainingTargets };
                                var updatedJson = JsonSerializer.Serialize(updatedLockInfo, this.jsonOptions);
                                var ttl = await this.database.KeyTimeToLiveAsync(lockKey);
                                await this.database.StringSetAsync(lockKey, updatedJson, ttl);
                                activity?.SetTag("wfs.targets_released", targets.Count);
                                activity?.SetTag("wfs.targets_remaining", remainingTargets.Count);
                                this.metrics.RecordLockReleased(serviceId, targets.Count);
                            }
                        }
                    }, cancellationToken);

                    this.metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    return 0;
                }
                catch (BrokenCircuitException ex)
                {
                    this.metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw new InvalidOperationException("WFS lock service is temporarily unavailable", ex);
                }
                catch (Exception ex) when (ex is not InvalidOperationException and not UnauthorizedAccessException)
                {
                    this.metrics.RecordOperationLatency("release", Activity.Current?.Duration ?? TimeSpan.Zero);
                    throw;
                }
            });
    }

    public async Task ResetAsync()
    {
        try
        {
            var server = this.redis.GetServer(this.redis.GetEndPoints().First());
            var keys = server.Keys(pattern: $"{this.keyPrefix}*");

            foreach (var key in keys)
            {
                await this.database.KeyDeleteAsync(key);
            }

            this.logger.LogInformation("Reset all WFS locks in Redis");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error resetting WFS locks");
            throw;
        }
    }

    private string GetLockKey(string lockId)
    {
        return $"{this.keyPrefix}{lockId}";
    }

    private string GetTargetKey(WfsLockTarget target)
    {
        return $"{this.keyPrefix}target:{target.ServiceId}:{target.LayerId}:{target.FeatureId}";
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
