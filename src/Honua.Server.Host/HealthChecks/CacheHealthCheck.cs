// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text;

namespace Honua.Server.Host.HealthChecks;

/// <summary>
/// Health check for distributed cache (Redis) or memory cache.
/// Tests cache connectivity and basic operations (set/get).
/// </summary>
public class CacheHealthCheck : IHealthCheck
{
    private readonly IDistributedCache? distributedCache;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<CacheHealthCheck> logger;
    private readonly IHostEnvironment environment;

    public CacheHealthCheck(
        IDistributedCache? distributedCache,
        IServiceProvider serviceProvider,
        ILogger<CacheHealthCheck> logger,
        IHostEnvironment environment)
    {
        this.distributedCache = distributedCache;
        this.serviceProvider = serviceProvider;
        this.logger = logger;
        this.environment = environment;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();

        try
        {
            // Try to resolve IConnectionMultiplexer optionally
            var redisConnection = this.serviceProvider.GetService<IConnectionMultiplexer>();

            // Check if Redis is configured
            if (redisConnection != null)
            {
                // Redis is configured - check connection
                if (!redisConnection.IsConnected)
                {
                    this.logger.LogWarning("Redis connection is not connected");

                    // In production, Redis unavailability is unhealthy
                    if (this.environment.IsProduction())
                    {
                        data["cacheType"] = "Redis";
                        data["status"] = "Disconnected";
                        return HealthCheckResult.Unhealthy(
                            "Redis cache is not connected",
                            data: data);
                    }
                    else
                    {
                        // In development, it's degraded but not critical
                        data["cacheType"] = "Redis";
                        data["status"] = "Disconnected (Development)";
                        return HealthCheckResult.Degraded(
                            "Redis cache is not connected (development environment)",
                            data: data);
                    }
                }

                // Test basic Redis operations
                var database = redisConnection.GetDatabase();
                var testKey = $"healthcheck:{Guid.NewGuid()}";
                var testValue = $"test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                try
                {
                    // Set a test value
                    await database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));

                    // Get the test value
                    var retrievedValue = await database.StringGetAsync(testKey);

                    // Delete the test key
                    await database.KeyDeleteAsync(testKey);

                    if (retrievedValue != testValue)
                    {
                        this.logger.LogWarning("Redis set/get test failed - value mismatch");
                        data["cacheType"] = "Redis";
                        data["status"] = "ValueMismatch";
                        return HealthCheckResult.Degraded(
                            "Redis cache operations returned unexpected results",
                            data: data);
                    }

                    // Get server info
                    var endpoints = redisConnection.GetEndPoints();
                    var serverInfo = new List<string>();
                    foreach (var endpoint in endpoints)
                    {
                        var server = redisConnection.GetServer(endpoint);
                        serverInfo.Add($"{endpoint} (Connected: {server.IsConnected})");
                    }

                    data["cacheType"] = "Redis";
                    data["status"] = "Healthy";
                    data["endpoints"] = serverInfo;
                    data["testDuration"] = "< 10s";

                    return HealthCheckResult.Healthy(
                        "Redis cache is operational",
                        data: data);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Redis operation test failed");
                    data["cacheType"] = "Redis";
                    data["status"] = "OperationFailed";
                    data["error"] = ex.Message;

                    return HealthCheckResult.Unhealthy(
                        "Redis cache operations failed: " + ex.Message,
                        exception: ex,
                        data: data);
                }
            }
            else if (_distributedCache != null)
            {
                // Fallback to IDistributedCache (could be in-memory or Redis)
                var testKey = $"healthcheck:{Guid.NewGuid()}";
                var testValue = Encoding.UTF8.GetBytes($"test-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");

                try
                {
                    // Set a test value
                    await this.distributedCache.SetAsync(testKey, testValue, new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                    }, cancellationToken);

                    // Get the test value
                    var retrievedValue = await this.distributedCache.GetAsync(testKey, cancellationToken);

                    // Remove the test key
                    await this.distributedCache.RemoveAsync(testKey, cancellationToken);

                    if (retrievedValue == null || !testValue.SequenceEqual(retrievedValue))
                    {
                        this.logger.LogWarning("Distributed cache set/get test failed");
                        data["cacheType"] = "DistributedCache";
                        data["status"] = "ValueMismatch";
                        return HealthCheckResult.Degraded(
                            "Distributed cache operations returned unexpected results",
                            data: data);
                    }

                    data["cacheType"] = "DistributedCache";
                    data["status"] = "Healthy";

                    return HealthCheckResult.Healthy(
                        "Distributed cache is operational",
                        data: data);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Distributed cache operation test failed");
                    data["cacheType"] = "DistributedCache";
                    data["status"] = "OperationFailed";
                    data["error"] = ex.Message;

                    return HealthCheckResult.Unhealthy(
                        "Distributed cache operations failed: " + ex.Message,
                        exception: ex,
                        data: data);
                }
            }
            else
            {
                // No cache configured
                this.logger.LogWarning("No distributed cache configured");

                if (this.environment.IsProduction())
                {
                    // In production, lack of cache is degraded
                    data["cacheType"] = "None";
                    data["status"] = "NotConfigured";
                    return HealthCheckResult.Degraded(
                        "No distributed cache configured (production environment)",
                        data: data);
                }
                else
                {
                    // In development, it's acceptable
                    data["cacheType"] = "None";
                    data["status"] = "NotConfigured (Development)";
                    return HealthCheckResult.Healthy(
                        "No distributed cache configured (development environment)",
                        data: data);
                }
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Cache health check failed");
            return HealthCheckResult.Unhealthy(
                "Cache health check failed: " + ex.Message,
                exception: ex,
                data: data);
        }
    }
}
