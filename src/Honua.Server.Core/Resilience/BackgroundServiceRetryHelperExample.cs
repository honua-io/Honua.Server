// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Resilience;

/// <summary>
/// Example background service demonstrating the use of BackgroundServiceRetryHelper.
/// This is a reference implementation - not meant for production use.
/// </summary>
/// <example>
/// Register in Startup.cs:
/// <code>
/// services.AddHostedService&lt;ExampleResilientBackgroundService&gt;();
/// </code>
/// </example>
public sealed class ExampleResilientBackgroundService : BackgroundService
{
    private readonly BackgroundServiceRetryHelper _retryHelper;
    private readonly ILogger<ExampleResilientBackgroundService> _logger;

    public ExampleResilientBackgroundService(ILogger<ExampleResilientBackgroundService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Configure retry helper with sensible defaults for background services:
        // - Unlimited retries (int.MaxValue)
        // - Start with 1 second delay
        // - Max delay of 5 minutes to avoid excessive wait times
        _retryHelper = new BackgroundServiceRetryHelper(
            logger,
            maxRetries: int.MaxValue,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromMinutes(5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExampleResilientBackgroundService starting");

        try
        {
            // Example 1: One-time initialization with retry
            await _retryHelper.ExecuteAsync(
                InitializeAsync,
                "InitializeService",
                stoppingToken);

            // Example 2: Periodic operation (polling loop)
            // This will run every 5 minutes with automatic retry on failures
            await _retryHelper.ExecutePeriodicAsync(
                ProcessQueueAsync,
                interval: TimeSpan.FromMinutes(5),
                operationName: "ProcessQueue",
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ExampleResilientBackgroundService cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ExampleResilientBackgroundService failed with unrecoverable error");
            throw;
        }
    }

    /// <summary>
    /// Example initialization that might fail transiently (e.g., database connection).
    /// The retry helper will automatically retry with exponential backoff.
    /// </summary>
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing service...");

        // Simulate work that might fail transiently
        // In real code, this might be:
        // - Connecting to database
        // - Loading configuration
        // - Warming up caches
        await Task.Delay(100, cancellationToken);

        _logger.LogInformation("Service initialized successfully");
    }

    /// <summary>
    /// Example periodic work that processes a queue.
    /// The retry helper will automatically retry failures with exponential backoff.
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing queue...");

        // Simulate work that might fail transiently
        // In real code, this might be:
        // - Fetching items from queue
        // - Processing messages
        // - Updating database
        // - Calling external APIs
        await Task.Delay(1000, cancellationToken);

        _logger.LogInformation("Queue processed successfully");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExampleResilientBackgroundService stopping");
        await base.StopAsync(cancellationToken);
    }
}

/// <summary>
/// Example of a more complex background service with multiple periodic operations.
/// Demonstrates how to run multiple operations with different intervals.
/// </summary>
public sealed class ExampleMultiOperationBackgroundService : BackgroundService
{
    private readonly BackgroundServiceRetryHelper _retryHelper;
    private readonly ILogger<ExampleMultiOperationBackgroundService> _logger;

    public ExampleMultiOperationBackgroundService(ILogger<ExampleMultiOperationBackgroundService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _retryHelper = new BackgroundServiceRetryHelper(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExampleMultiOperationBackgroundService starting");

        // Run multiple operations in parallel
        var tasks = new[]
        {
            // Fast operation: Check health every 30 seconds
            _retryHelper.ExecutePeriodicAsync(
                CheckHealthAsync,
                TimeSpan.FromSeconds(30),
                "CheckHealth",
                stoppingToken),

            // Medium operation: Process queue every 5 minutes
            _retryHelper.ExecutePeriodicAsync(
                ProcessQueueAsync,
                TimeSpan.FromMinutes(5),
                "ProcessQueue",
                stoppingToken),

            // Slow operation: Cleanup old data every hour
            _retryHelper.ExecutePeriodicAsync(
                CleanupOldDataAsync,
                TimeSpan.FromHours(1),
                "CleanupOldData",
                stoppingToken)
        };

        // Wait for all operations (they run until cancellation)
        await Task.WhenAll(tasks);
    }

    private async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Checking health...");
        await Task.Delay(100, cancellationToken);
        _logger.LogDebug("Health check completed");
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing queue...");
        await Task.Delay(1000, cancellationToken);
        _logger.LogInformation("Queue processed");
    }

    private async Task CleanupOldDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cleaning up old data...");
        await Task.Delay(5000, cancellationToken);
        _logger.LogInformation("Cleanup completed");
    }
}
