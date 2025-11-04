// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Configuration options for graceful shutdown.
/// </summary>
public sealed class GracefulShutdownOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "GracefulShutdown";

    /// <summary>
    /// Maximum time to wait for in-flight requests to complete.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay before starting shutdown to allow load balancers to remove instance from rotation.
    /// Default: 5 seconds.
    /// </summary>
    public TimeSpan PreShutdownDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Enable detailed logging during shutdown.
    /// Default: true.
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = true;

    /// <summary>
    /// Wait for background tasks to complete before shutting down.
    /// Default: true.
    /// </summary>
    public bool WaitForBackgroundTasks { get; set; } = true;
}

/// <summary>
/// Hosted service that implements graceful shutdown behavior.
/// </summary>
internal sealed class GracefulShutdownService : IHostedService
{
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly GracefulShutdownOptions _options;

    public GracefulShutdownService(
        IHostApplicationLifetime applicationLifetime,
        ILogger<GracefulShutdownService> logger,
        IOptions<GracefulShutdownOptions> options)
    {
        _applicationLifetime = Guard.NotNull(applicationLifetime);
        _logger = Guard.NotNull(logger);
        _options = options?.Value ?? new GracefulShutdownOptions();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Register callbacks for application lifecycle events
        _applicationLifetime.ApplicationStarted.Register(OnStarted);
        _applicationLifetime.ApplicationStopping.Register(OnStopping);
        _applicationLifetime.ApplicationStopped.Register(OnStopped);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // This is called when the application is shutting down
        // Most cleanup should happen in ApplicationStopping callback
        return Task.CompletedTask;
    }

    private void OnStarted()
    {
        if (_options.EnableDetailedLogging)
        {
            _logger.LogInformation(
                "Honua Server started successfully. " +
                "Graceful shutdown configured with timeout: {ShutdownTimeout}s, pre-shutdown delay: {PreShutdownDelay}s",
                _options.ShutdownTimeout.TotalSeconds,
                _options.PreShutdownDelay.TotalSeconds);
        }
    }

    private void OnStopping()
    {
        if (_options.EnableDetailedLogging)
        {
            _logger.LogInformation(
                "Honua Server is shutting down. " +
                "Waiting {PreShutdownDelay}s to allow load balancers to drain connections...",
                _options.PreShutdownDelay.TotalSeconds);
        }

        // Wait for pre-shutdown delay to allow load balancers to remove this instance from rotation
        // This prevents new connections while we drain existing ones
        if (_options.PreShutdownDelay > TimeSpan.Zero)
        {
            try
            {
                Thread.Sleep(_options.PreShutdownDelay);

                if (_options.EnableDetailedLogging)
                {
                    _logger.LogInformation(
                        "Pre-shutdown delay completed. Now draining in-flight requests (timeout: {ShutdownTimeout}s)...",
                        _options.ShutdownTimeout.TotalSeconds);
                }
            }
            catch (ThreadInterruptedException)
            {
                _logger.LogWarning("Pre-shutdown delay was interrupted. Proceeding with shutdown.");
            }
        }

        // Note: Actual request draining is handled by Kestrel automatically
        // when IHostApplicationLifetime.StopApplication() is called.
        // Kestrel waits for existing connections to complete or timeout.
    }

    private void OnStopped()
    {
        if (_options.EnableDetailedLogging)
        {
            _logger.LogInformation(
                "Honua Server has stopped. All requests have been drained or timed out.");
        }
    }
}

/// <summary>
/// Extension methods for registering graceful shutdown service.
/// </summary>
public static class GracefulShutdownServiceExtensions
{
    /// <summary>
    /// Adds graceful shutdown service with configurable timeouts and logging.
    /// </summary>
    public static IServiceCollection AddGracefulShutdown(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<GracefulShutdownOptions>(
            configuration.GetSection(GracefulShutdownOptions.SectionName));

        // Register the hosted service
        services.AddHostedService<GracefulShutdownService>();

        return services;
    }

    /// <summary>
    /// Configures Kestrel shutdown timeout.
    /// </summary>
    public static IServiceCollection ConfigureShutdownTimeout(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = new GracefulShutdownOptions();
        configuration.GetSection(GracefulShutdownOptions.SectionName).Bind(options);

        // Configure host options with shutdown timeout
        services.Configure<HostOptions>(hostOptions =>
        {
            hostOptions.ShutdownTimeout = options.ShutdownTimeout;
        });

        return services;
    }
}
