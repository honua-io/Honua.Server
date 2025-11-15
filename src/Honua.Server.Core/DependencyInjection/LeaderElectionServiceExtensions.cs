// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using Honua.Server.Core.Coordination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Honua.Server.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering distributed leader election services.
/// Enables high availability deployments with Redis-based leader coordination.
/// </summary>
public static class LeaderElectionServiceExtensions
{
    /// <summary>
    /// Adds distributed leader election infrastructure to the service collection.
    /// Requires Redis connection to be configured via AddHonuaCaching or equivalent.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration containing LeaderElection section.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configuration is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when Redis IConnectionMultiplexer is not registered.</exception>
    /// <remarks>
    /// This method registers the following services:
    /// - ILeaderElection (singleton) - Core leader election functionality
    /// - LeaderElectionService (hosted service) - Automatic leadership maintenance
    /// - Health check for monitoring leadership status
    ///
    /// Configuration:
    /// Add a "LeaderElection" section to appsettings.json:
    /// <code>
    /// {
    ///   "LeaderElection": {
    ///     "ResourceName": "honua-server",
    ///     "LeaseDurationSeconds": 30,
    ///     "RenewalIntervalSeconds": 10,
    ///     "KeyPrefix": "honua:leader:",
    ///     "EnableDetailedLogging": false
    ///   }
    /// }
    /// </code>
    ///
    /// Usage:
    /// 1. Call this method in Startup.ConfigureServices or Program.cs
    /// 2. Inject ILeaderElection or LeaderElectionService into your services
    /// 3. Check leadership status before processing singleton tasks
    ///
    /// Example:
    /// <code>
    /// public class MyBackgroundService : BackgroundService
    /// {
    ///     private readonly LeaderElectionService _leaderElection;
    ///
    ///     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    ///     {
    ///         while (!stoppingToken.IsCancellationRequested)
    ///         {
    ///             if (_leaderElection.IsLeader)
    ///             {
    ///                 // Only process if this instance is the leader
    ///                 await ProcessTaskAsync(stoppingToken);
    ///             }
    ///             await Task.Delay(1000, stoppingToken);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddLeaderElection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Register leader election options
        services.Configure<LeaderElectionOptions>(
            configuration.GetSection(LeaderElectionOptions.SectionName));

        // Validate options on startup
        services.AddSingleton<IValidateOptions<LeaderElectionOptions>, LeaderElectionOptionsValidator>();

        // Register ILeaderElection implementation
        services.AddSingleton<ILeaderElection>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<RedisLeaderElection>>();
            var options = sp.GetRequiredService<IOptions<LeaderElectionOptions>>();

            // Get Redis connection multiplexer
            var redis = sp.GetService<IConnectionMultiplexer>();
            if (redis == null)
            {
                throw new InvalidOperationException(
                    "Redis IConnectionMultiplexer is not registered. " +
                    "Ensure AddHonuaCaching or AddStackExchangeRedisCache is called before AddLeaderElection.");
            }

            logger.LogInformation(
                "Initializing distributed leader election: Resource={Resource}, Lease={Lease}s, Renewal={Renewal}s",
                options.Value.ResourceName,
                options.Value.LeaseDurationSeconds,
                options.Value.RenewalIntervalSeconds);

            return new RedisLeaderElection(redis, logger, options);
        });

        // Register LeaderElectionService as hosted service
        services.AddSingleton<LeaderElectionService>();
        services.AddHostedService(sp => sp.GetRequiredService<LeaderElectionService>());

        // Register health check
        services.AddHealthChecks()
            .AddCheck<LeaderElectionHealthCheck>(
                "leader_election",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "leader", "coordination", "ha" });

        return services;
    }

    /// <summary>
    /// Validates LeaderElectionOptions configuration.
    /// </summary>
    private sealed class LeaderElectionOptionsValidator : IValidateOptions<LeaderElectionOptions>
    {
        public ValidateOptionsResult Validate(string? name, LeaderElectionOptions options)
        {
            try
            {
                options.Validate();
                return ValidateOptionsResult.Success;
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail(ex.Message);
            }
        }
    }

    /// <summary>
    /// Health check for monitoring leader election status.
    /// Reports healthy if leadership is maintained, degraded if not leader.
    /// </summary>
    private sealed class LeaderElectionHealthCheck : IHealthCheck
    {
        private readonly LeaderElectionService _leaderElectionService;
        private readonly ILogger<LeaderElectionHealthCheck> _logger;

        public LeaderElectionHealthCheck(
            LeaderElectionService leaderElectionService,
            ILogger<LeaderElectionHealthCheck> logger)
        {
            _leaderElectionService = leaderElectionService ?? throw new ArgumentNullException(nameof(leaderElectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var isLeader = _leaderElectionService.IsLeader;
                var instanceId = _leaderElectionService.InstanceId;

                var data = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["is_leader"] = isLeader,
                    ["instance_id"] = instanceId
                };

                if (isLeader)
                {
                    return Task.FromResult(
                        HealthCheckResult.Healthy(
                            $"This instance is the current leader (InstanceId: {instanceId})",
                            data));
                }
                else
                {
                    return Task.FromResult(
                        HealthCheckResult.Degraded(
                            $"This instance is not the leader (InstanceId: {instanceId})",
                            null,
                            data));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking leader election health");
                return Task.FromResult(
                    HealthCheckResult.Unhealthy(
                        "Error checking leader election status",
                        ex));
            }
        }
    }
}
