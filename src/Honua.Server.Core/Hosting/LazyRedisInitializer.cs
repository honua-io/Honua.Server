// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Honua.Server.Core.Hosting;

/// <summary>
/// Lazy initializer for Redis connections to avoid blocking startup.
/// Redis connections are established in the background after the app starts accepting requests.
/// </summary>
/// <remarks>
/// Cold start optimization: Redis connection establishment can take 500-2000ms depending on network latency.
/// By deferring this to a background task, we reduce cold start time and allow the app to start serving
/// requests immediately. Services that depend on Redis will gracefully degrade or use in-memory fallbacks
/// until the connection is established.
/// </remarks>
public sealed class LazyRedisInitializer : IHostedService, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LazyRedisInitializer> _logger;
    private readonly IHostEnvironment _environment;
    private IConnectionMultiplexer? _redis;
    private bool _disposed;

    public LazyRedisInitializer(
        IConfiguration configuration,
        ILogger<LazyRedisInitializer> logger,
        IHostEnvironment environment)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Gets the Redis connection multiplexer, or null if not yet connected.
    /// </summary>
    public IConnectionMultiplexer? Redis => _redis;

    /// <summary>
    /// Gets whether Redis is connected.
    /// </summary>
    public bool IsConnected => _redis?.IsConnected ?? false;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var redisConnectionString = _configuration.GetConnectionString("Redis");

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            if (_environment.IsProduction())
            {
                _logger.LogWarning(
                    "Redis connection string not configured in Production. " +
                    "Distributed features (rate limiting, caching, WFS locks) will use in-memory fallbacks. " +
                    "This is not suitable for multi-instance deployments.");
            }
            else
            {
                _logger.LogDebug("Redis not configured - using in-memory fallbacks (acceptable for development)");
            }
            return Task.CompletedTask;
        }

        // Don't block startup - initialize Redis in background
        _ = Task.Run(async () => await InitializeRedisAsync(redisConnectionString, cancellationToken), cancellationToken);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _redis?.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _redis?.Dispose();
        _disposed = true;
    }

    private async Task InitializeRedisAsync(string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            // Wait a bit to let the app start accepting requests first
            await Task.Delay(1500, cancellationToken);

            _logger.LogInformation("Establishing Redis connection in background...");

            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.AbortOnConnectFail = false; // Allow graceful degradation
            configOptions.ConnectTimeout = 5000; // 5 second timeout
            configOptions.ConnectRetry = 3;
            configOptions.AsyncTimeout = 5000;
            configOptions.SyncTimeout = 5000;

            _redis = await ConnectionMultiplexer.ConnectAsync(configOptions);

            if (_redis.IsConnected)
            {
                _logger.LogInformation("Redis connection established successfully");
            }
            else
            {
                _logger.LogWarning("Redis connection was created but is not connected. Will retry automatically.");
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to establish Redis connection: {Message}. " +
                "App will continue with in-memory fallbacks. " +
                "Redis will retry connection automatically in the background.",
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during Redis initialization: {Message}",
                ex.Message);
        }
    }
}
