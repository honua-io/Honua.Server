// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Host.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check for Redis-backed distributed stores.
/// Verifies Redis connectivity and reports on store availability.
/// </summary>
public sealed class RedisStoresHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer? redis;
    private readonly ILogger<RedisStoresHealthCheck> logger;

    public RedisStoresHealthCheck(
        ILogger<RedisStoresHealthCheck> logger,
        IConnectionMultiplexer? redis = null)
    {
        this.logger = Guard.NotNull(logger);
        this.redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var healthData = new Dictionary<string, object>();

        try
        {
            if (_redis == null)
            {
                // Redis not configured - stores will use in-memory implementations
                healthData["redis.configured"] = false;
                healthData["stores.mode"] = "in-memory";
                healthData["stores.distributed"] = false;

                this.logger.LogDebug("Redis not configured - using in-memory stores");
                return HealthCheckResult.Healthy(
                    "Using in-memory stores (Redis not configured)",
                    healthData);
            }

            // Check Redis connectivity
            if (!this.redis.IsConnected)
            {
                healthData["redis.configured"] = true;
                healthData["redis.connected"] = false;
                healthData["stores.mode"] = "in-memory (fallback)";
                healthData["stores.distributed"] = false;

                this.logger.LogWarning("Redis configured but not connected - falling back to in-memory stores");
                return HealthCheckResult.Degraded(
                    "Redis not connected - using in-memory stores as fallback",
                    data: healthData);
            }

            // Test Redis with ping
            var db = this.redis.GetDatabase();
            var pong = await db.PingAsync();

            healthData["redis.configured"] = true;
            healthData["redis.connected"] = true;
            healthData["redis.latency_ms"] = pong.TotalMilliseconds;
            healthData["stores.mode"] = "distributed";
            healthData["stores.distributed"] = true;

            var allowAdmin = GetAllowAdminFlag(_redis);
            healthData["redis.allow_admin"] = allowAdmin;

            var endpoints = this.redis.GetEndPoints();
            healthData["redis.endpoints"] = endpoints.Length;

            if (allowAdmin)
            {
                var connectedEndpoints = 0;
                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var server = this.redis.GetServer(endpoint);
                        if (server.IsConnected)
                        {
                            connectedEndpoints++;
                        }
                    }
                    catch (RedisServerException ex)
                    {
                        this.logger.LogDebug(ex, "Failed to query Redis server information for endpoint {Endpoint}", endpoint);
                    }
                }

                healthData["redis.connected_endpoints"] = connectedEndpoints;

                if (connectedEndpoints < endpoints.Length)
                {
                    this.logger.LogWarning(
                        "Not all Redis endpoints are connected: {Connected}/{Total}",
                        connectedEndpoints,
                        endpoints.Length);
                    return HealthCheckResult.Degraded(
                        $"Only {connectedEndpoints}/{endpoints.Length} Redis endpoints connected",
                        data: healthData);
                }
            }

            // Check latency
            if (pong.TotalMilliseconds > ApiLimitsAndConstants.RedisHealthCheckThresholdMilliseconds)
            {
                this.logger.LogWarning(
                    "Redis latency is high: {Latency}ms - distributed stores may experience performance degradation",
                    pong.TotalMilliseconds);
                return HealthCheckResult.Degraded(
                    $"Redis latency is high: {pong.TotalMilliseconds:F2}ms",
                    data: healthData);
            }

            this.logger.LogDebug(
                "Redis stores health check passed. Latency: {Latency}ms, Endpoints: {Endpoints}",
                pong.TotalMilliseconds,
                endpoints.Length);

            return HealthCheckResult.Healthy(
                "Distributed stores using Redis",
                healthData);
        }
        catch (RedisConnectionException ex)
        {
            healthData["redis.configured"] = true;
            healthData["redis.connected"] = false;
            healthData["redis.error"] = ex.Message;
            healthData["stores.mode"] = "in-memory (fallback)";
            healthData["stores.distributed"] = false;

            this.logger.LogError(ex, "Redis connection failed - using in-memory stores as fallback");
            return HealthCheckResult.Degraded(
                $"Redis connection failed: {ex.Message} - using in-memory stores",
                ex,
                healthData);
        }
        catch (Exception ex)
        {
            healthData["redis.configured"] = true;
            healthData["redis.error"] = ex.Message;
            healthData["stores.mode"] = "unknown";

            this.logger.LogError(ex, "Redis stores health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Redis stores health check failed",
                ex,
                healthData);
        }
    }

    private static bool GetAllowAdminFlag(IConnectionMultiplexer multiplexer)
    {
        try
        {
            if (multiplexer.Configuration.HasValue())
            {
                var options = ConfigurationOptions.Parse(multiplexer.Configuration);
                return options.AllowAdmin;
            }
        }
        catch
        {
            // Ignore parsing errors; assume allowAdmin is disabled
        }

        return false;
    }
}
