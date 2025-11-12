// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Honua.Server.Enterprise.IoT.Azure.Events;
using Honua.Server.Enterprise.IoT.Azure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Honua.Server.Enterprise.IoT.Azure;

/// <summary>
/// Service collection extensions for Azure Digital Twins integration.
/// </summary>
public static class AzureDigitalTwinsServiceExtensions
{
    /// <summary>
    /// Adds Azure Digital Twins integration services to the service collection.
    /// </summary>
    public static IServiceCollection AddAzureDigitalTwins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<AzureDigitalTwinsOptions>(
            configuration.GetSection(AzureDigitalTwinsOptions.SectionName));

        // Validate configuration
        services.AddOptions<AzureDigitalTwinsOptions>()
            .BindConfiguration(AzureDigitalTwinsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register core services
        services.AddSingleton<IAzureDigitalTwinsClient, AzureDigitalTwinsClientWrapper>();
        services.AddSingleton<IDtdlModelMapper, DtdlModelMapper>();
        services.AddSingleton<ITwinSynchronizationService, TwinSynchronizationService>();

        // Register event services
        services.AddSingleton<IEventGridPublisher, EventGridPublisher>();
        services.AddSingleton<IAdtEventHandler, AdtEventHandler>();

        // Register background services if enabled
        var options = configuration
            .GetSection(AzureDigitalTwinsOptions.SectionName)
            .Get<AzureDigitalTwinsOptions>();

        if (options?.Sync.Enabled == true && options.Sync.BatchSyncIntervalMinutes > 0)
        {
            services.AddHostedService<BatchSyncBackgroundService>();
        }

        return services;
    }

    /// <summary>
    /// Adds Azure Digital Twins integration services with custom options.
    /// </summary>
    public static IServiceCollection AddAzureDigitalTwins(
        this IServiceCollection services,
        Action<AzureDigitalTwinsOptions> configureOptions)
    {
        services.Configure(configureOptions);

        // Validate configuration
        services.AddOptions<AzureDigitalTwinsOptions>()
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register core services
        services.AddSingleton<IAzureDigitalTwinsClient, AzureDigitalTwinsClientWrapper>();
        services.AddSingleton<IDtdlModelMapper, DtdlModelMapper>();
        services.AddSingleton<ITwinSynchronizationService, TwinSynchronizationService>();

        // Register event services
        services.AddSingleton<IEventGridPublisher, EventGridPublisher>();
        services.AddSingleton<IAdtEventHandler, AdtEventHandler>();

        return services;
    }
}

/// <summary>
/// Background service for periodic batch synchronization.
/// </summary>
internal sealed class BatchSyncBackgroundService : BackgroundService
{
    private readonly ITwinSynchronizationService _syncService;
    private readonly IConfiguration _configuration;
    private readonly Microsoft.Extensions.Logging.ILogger<BatchSyncBackgroundService> _logger;
    private readonly AzureDigitalTwinsOptions _options;

    public BatchSyncBackgroundService(
        ITwinSynchronizationService syncService,
        IConfiguration configuration,
        Microsoft.Extensions.Logging.ILogger<BatchSyncBackgroundService> logger,
        Microsoft.Extensions.Options.IOptions<AzureDigitalTwinsOptions> options)
    {
        _syncService = syncService;
        _configuration = configuration;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.Sync.BatchSyncIntervalMinutes <= 0)
        {
            _logger.LogInformation("Batch sync disabled (interval = 0)");
            return;
        }

        var interval = TimeSpan.FromMinutes(_options.Sync.BatchSyncIntervalMinutes);
        _logger.LogInformation(
            "Batch sync background service started with interval: {Interval}",
            interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                _logger.LogInformation("Starting scheduled batch sync");

                // Sync all configured layer mappings
                foreach (var mapping in _options.LayerMappings)
                {
                    try
                    {
                        var stats = await _syncService.PerformBatchSyncAsync(
                            mapping.ServiceId,
                            mapping.LayerId,
                            stoppingToken);

                        _logger.LogInformation(
                            "Batch sync completed for {ServiceId}/{LayerId}: " +
                            "{Succeeded} succeeded, {Failed} failed, {Skipped} skipped",
                            mapping.ServiceId,
                            mapping.LayerId,
                            stats.Succeeded,
                            stats.Failed,
                            stats.Skipped);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error during batch sync for {ServiceId}/{LayerId}",
                            mapping.ServiceId,
                            mapping.LayerId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch sync background service");
            }
        }

        _logger.LogInformation("Batch sync background service stopped");
    }
}
