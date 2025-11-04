// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Data;

/// <summary>
/// Hosted service that warms up database connection pools during application startup.
/// This reduces first-request latency in serverless deployments by pre-establishing connections.
/// </summary>
/// <remarks>
/// Cold start optimization: In serverless environments, the first request after deployment
/// can take 5-10 seconds due to connection pool initialization. This service warms up pools
/// in the background, reducing first-request latency to sub-500ms.
///
/// The service runs in parallel (non-blocking) and never fails startup if warmup fails.
/// Warmup failures are logged as warnings, not errors, since the app can still function.
/// </remarks>
public sealed class ConnectionPoolWarmupService : IHostedService
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly IDataStoreProviderFactory _providerFactory;
    private readonly ILogger<ConnectionPoolWarmupService> _logger;
    private readonly ConnectionPoolWarmupOptions _options;
    private readonly IHostEnvironment _environment;

    public ConnectionPoolWarmupService(
        IMetadataRegistry metadataRegistry,
        IDataStoreProviderFactory providerFactory,
        ILogger<ConnectionPoolWarmupService> logger,
        IOptions<ConnectionPoolWarmupOptions> options,
        IHostEnvironment environment)
    {
        _metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new ConnectionPoolWarmupOptions();
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Skip warmup if disabled or in development (unless forced)
        if (!_options.Enabled)
        {
            _logger.LogInformation("Connection pool warmup is disabled via configuration");
            return;
        }

        if (_environment.IsDevelopment() && !_options.EnableInDevelopment)
        {
            _logger.LogDebug("Skipping connection pool warmup in development environment");
            return;
        }

        // Don't block startup - run warmup in background
        _ = Task.Run(async () => await WarmupConnectionPoolsAsync(cancellationToken), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task WarmupConnectionPoolsAsync(CancellationToken cancellationToken)
    {
        var overallStopwatch = Stopwatch.StartNew();

        try
        {
            // Wait for startup delay to let app start accepting requests first
            if (_options.StartupDelayMs > 0)
            {
                await Task.Delay(_options.StartupDelayMs, cancellationToken);
            }

            _logger.LogInformation("Starting connection pool warmup...");

            // Ensure metadata is loaded
            if (!_metadataRegistry.IsInitialized)
            {
                _logger.LogDebug("Waiting for metadata registry initialization...");
                await _metadataRegistry.EnsureInitializedAsync(cancellationToken);
            }

            var snapshot = await _metadataRegistry.GetSnapshotAsync(cancellationToken);
            if (snapshot == null)
            {
                _logger.LogWarning("Cannot warm up connection pools: metadata snapshot is null");
                return;
            }

            // Get unique data sources from snapshot
            var dataSources = snapshot.DataSources
                .Where(ds => ds != null && !string.IsNullOrWhiteSpace(ds.ConnectionString))
                .Take(_options.MaxDataSources)
                .ToList();

            if (dataSources.Count == 0)
            {
                _logger.LogInformation("No data sources found to warm up");
                return;
            }

            _logger.LogInformation("Warming up {DataSourceCount} connection pools", dataSources.Count);

            // Warm up pools in parallel with limited concurrency
            var semaphore = new SemaphoreSlim(_options.MaxConcurrentWarmups);
            var warmupTasks = new List<Task>();

            foreach (var dataSource in dataSources)
            {
                warmupTasks.Add(WarmupDataSourceAsync(dataSource, semaphore, cancellationToken));
            }

            await Task.WhenAll(warmupTasks);

            overallStopwatch.Stop();
            _logger.LogInformation(
                "Connection pool warmup completed in {ElapsedMs}ms",
                overallStopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection pool warmup was cancelled");
        }
        catch (Exception ex)
        {
            // IMPORTANT: Never fail startup due to warmup errors
            _logger.LogWarning(ex, "Connection pool warmup encountered an error but app will continue");
        }
    }

    private async Task WarmupDataSourceAsync(
        DataSourceDefinition dataSource,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var provider = _providerFactory.Create(dataSource.Provider);

            using var timeoutCts = new CancellationTokenSource(_options.TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await provider.TestConnectivityAsync(dataSource, linkedCts.Token);

            stopwatch.Stop();
            _logger.LogDebug(
                "Warmed up connection pool for data source '{DataSourceId}' ({Provider}) in {ElapsedMs}ms",
                dataSource.Id,
                dataSource.Provider,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout occurred
            _logger.LogWarning(
                "Connection pool warmup timed out for data source '{DataSourceId}' after {TimeoutMs}ms",
                dataSource.Id,
                _options.TimeoutMs);
        }
        catch (Exception ex)
        {
            // Log but don't fail - warmup is best-effort
            _logger.LogWarning(
                ex,
                "Failed to warm up connection pool for data source '{DataSourceId}': {Message}",
                dataSource.Id,
                ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }
}

/// <summary>
/// Configuration options for connection pool warmup behavior.
/// </summary>
public sealed class ConnectionPoolWarmupOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "ConnectionPoolWarmup";

    /// <summary>
    /// Whether connection pool warmup is enabled.
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to enable warmup in development environment.
    /// Default: false (warmup is typically only needed in production/serverless)
    /// </summary>
    public bool EnableInDevelopment { get; set; } = false;

    /// <summary>
    /// Delay in milliseconds before starting warmup.
    /// Allows the app to start accepting requests before warmup begins.
    /// Default: 1000ms (1 second)
    /// </summary>
    public int StartupDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum number of concurrent warmup operations.
    /// Default: 3
    /// </summary>
    public int MaxConcurrentWarmups { get; set; } = 3;

    /// <summary>
    /// Maximum number of data sources to warm up.
    /// Prevents excessive warmup time with many data sources.
    /// Default: 10
    /// </summary>
    public int MaxDataSources { get; set; } = 10;

    /// <summary>
    /// Timeout in milliseconds for each warmup operation.
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}
