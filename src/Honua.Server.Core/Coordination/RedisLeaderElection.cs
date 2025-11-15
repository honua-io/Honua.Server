// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Honua.Server.Core.Observability;

namespace Honua.Server.Core.Coordination;

/// <summary>
/// Redis-based implementation of distributed leader election.
/// Uses Redis SET NX EX for atomic leader acquisition with automatic expiry.
/// </summary>
/// <remarks>
/// This implementation provides distributed leader election for HA deployments:
///
/// Atomic Acquisition:
/// - Uses Redis SET with NX (not exists) and EX (expiry) options
/// - Ensures only one instance can acquire leadership at a time
/// - Automatic expiry prevents indefinite lock holding if leader crashes
///
/// Ownership Verification:
/// - Each instance has a unique ID: {MachineName}_{ProcessId}_{Guid}
/// - Lua scripts verify ownership before renewal/release
/// - Prevents split-brain scenarios and accidental release of other instance's locks
///
/// Fault Tolerance:
/// - Leadership automatically expires after LeaseDuration
/// - Other instances can take over leadership after expiry
/// - Graceful release enables immediate failover during shutdown
///
/// Observability:
/// - Comprehensive logging of all leadership events
/// - OpenTelemetry instrumentation for distributed tracing
/// - Health checks for monitoring leadership status
/// </remarks>
public sealed class RedisLeaderElection : ILeaderElection, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisLeaderElection> _logger;
    private readonly LeaderElectionOptions _options;
    private readonly string _instanceId;
    private static readonly ActivitySource ActivitySource = new("Honua.Coordination.LeaderElection");

    // Lua script for atomic renewal: check ownership and extend TTL
    private const string RenewScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('expire', KEYS[1], ARGV[2])
        else
            return 0
        end";

    // Lua script for atomic release: check ownership and delete
    private const string ReleaseScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end";

    public RedisLeaderElection(
        IConnectionMultiplexer redis,
        ILogger<RedisLeaderElection> logger,
        IOptions<LeaderElectionOptions> options)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _options.Validate();

        // Generate unique instance ID: {MachineName}_{ProcessId}_{Guid}
        _instanceId = GenerateInstanceId();
        _database = _redis.GetDatabase();

        _logger.LogInformation(
            "RedisLeaderElection initialized: InstanceId={InstanceId}, Resource={Resource}, Lease={Lease}s, Renewal={Renewal}s",
            _instanceId, _options.ResourceName, _options.LeaseDurationSeconds, _options.RenewalIntervalSeconds);
    }

    /// <inheritdoc />
    public string InstanceId => _instanceId;

    /// <inheritdoc />
    public async Task<bool> TryAcquireLeadershipAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        return await OperationInstrumentation.Create<bool>("LeaderElection.Acquire")
            .WithLogger(_logger)
            .WithTag("leader.resource", resourceName)
            .WithTag("leader.instance_id", _instanceId)
            .ExecuteAsync(async activity =>
            {
                var key = GetRedisKey(resourceName);

                try
                {
                    // Use SET with NX (not exists) and EX (expiry) for atomic lock acquisition
                    var acquired = await _database.StringSetAsync(
                        key,
                        _instanceId,
                        _options.LeaseDuration,
                        When.NotExists,
                        CommandFlags.None).ConfigureAwait(false);

                    activity?.SetTag("leader.acquired", acquired);

                    if (acquired)
                    {
                        _logger.LogInformation(
                            "Leadership acquired: Resource={Resource}, InstanceId={InstanceId}, Lease={Lease}s",
                            resourceName, _instanceId, _options.LeaseDurationSeconds);
                    }
                    else if (_options.EnableDetailedLogging)
                    {
                        _logger.LogDebug(
                            "Leadership acquisition failed (already held): Resource={Resource}, InstanceId={InstanceId}",
                            resourceName, _instanceId);
                    }

                    return acquired;
                }
                catch (RedisException ex)
                {
                    _logger.LogError(ex,
                        "Redis error while acquiring leadership: Resource={Resource}, InstanceId={InstanceId}",
                        resourceName, _instanceId);

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    return false;
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RenewLeadershipAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        return await OperationInstrumentation.Create<bool>("LeaderElection.Renew")
            .WithLogger(_logger)
            .WithTag("leader.resource", resourceName)
            .WithTag("leader.instance_id", _instanceId)
            .ExecuteAsync(async activity =>
            {
                var key = GetRedisKey(resourceName);

                try
                {
                    // Use Lua script to atomically verify ownership and extend TTL
                    var result = await _database.ScriptEvaluateAsync(
                        RenewScript,
                        new RedisKey[] { key },
                        new RedisValue[] { _instanceId, (int)_options.LeaseDuration.TotalSeconds }
                    ).ConfigureAwait(false);

                    var renewed = (long)result == 1;
                    activity?.SetTag("leader.renewed", renewed);

                    if (renewed)
                    {
                        if (_options.EnableDetailedLogging)
                        {
                            _logger.LogDebug(
                                "Leadership renewed: Resource={Resource}, InstanceId={InstanceId}, Lease={Lease}s",
                                resourceName, _instanceId, _options.LeaseDurationSeconds);
                        }
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Leadership renewal failed (no longer leader): Resource={Resource}, InstanceId={InstanceId}",
                            resourceName, _instanceId);
                    }

                    return renewed;
                }
                catch (RedisException ex)
                {
                    _logger.LogError(ex,
                        "Redis error while renewing leadership: Resource={Resource}, InstanceId={InstanceId}",
                        resourceName, _instanceId);

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    return false;
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseLeadershipAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        return await OperationInstrumentation.Create<bool>("LeaderElection.Release")
            .WithLogger(_logger)
            .WithTag("leader.resource", resourceName)
            .WithTag("leader.instance_id", _instanceId)
            .ExecuteAsync(async activity =>
            {
                var key = GetRedisKey(resourceName);

                try
                {
                    // Use Lua script to atomically verify ownership before deletion
                    // Prevents releasing a lock that expired and was acquired by another instance
                    var result = await _database.ScriptEvaluateAsync(
                        ReleaseScript,
                        new RedisKey[] { key },
                        new RedisValue[] { _instanceId }
                    ).ConfigureAwait(false);

                    var released = (long)result == 1;
                    activity?.SetTag("leader.released", released);

                    if (released)
                    {
                        _logger.LogInformation(
                            "Leadership released: Resource={Resource}, InstanceId={InstanceId}",
                            resourceName, _instanceId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Leadership release failed (not current leader or already expired): Resource={Resource}, InstanceId={InstanceId}",
                            resourceName, _instanceId);
                    }

                    return released;
                }
                catch (RedisException ex)
                {
                    _logger.LogError(ex,
                        "Redis error while releasing leadership: Resource={Resource}, InstanceId={InstanceId}",
                        resourceName, _instanceId);

                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    return false;
                }
            }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> IsLeaderAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var key = GetRedisKey(resourceName);

        try
        {
            var currentLeader = await _database.StringGetAsync(key).ConfigureAwait(false);
            var isLeader = currentLeader.HasValue && currentLeader == _instanceId;

            if (_options.EnableDetailedLogging)
            {
                _logger.LogDebug(
                    "Leadership check: Resource={Resource}, InstanceId={InstanceId}, IsLeader={IsLeader}",
                    resourceName, _instanceId, isLeader);
            }

            return isLeader;
        }
        catch (RedisException ex)
        {
            _logger.LogError(ex,
                "Redis error while checking leadership: Resource={Resource}, InstanceId={InstanceId}",
                resourceName, _instanceId);

            // On Redis error, assume not leader to prevent split-brain
            return false;
        }
    }

    private string GetRedisKey(string resourceName)
    {
        return $"{_options.KeyPrefix}{resourceName}";
    }

    private static string GenerateInstanceId()
    {
        // Format: {MachineName}_{ProcessId}_{Guid}
        // This ensures uniqueness across machines, processes, and restarts
        var machineName = Environment.MachineName;
        var processId = Environment.ProcessId;
        var uniqueId = Guid.NewGuid().ToString("N");
        return $"{machineName}_{processId}_{uniqueId}";
    }

    public void Dispose()
    {
        // Redis connection is managed by DI container, not disposed here
    }
}
