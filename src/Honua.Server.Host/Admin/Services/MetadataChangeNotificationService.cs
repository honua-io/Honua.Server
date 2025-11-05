// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.Admin.Services;

/// <summary>
/// Background service that listens for metadata changes and broadcasts them to SignalR clients.
/// </summary>
public sealed class MetadataChangeNotificationService : IHostedService, IDisposable
{
    private readonly IMetadataProvider _metadataProvider;
    private readonly IHubContext<MetadataChangeNotificationHub> _hubContext;
    private readonly ILogger<MetadataChangeNotificationService> _logger;
    private IMetadataChangeNotifier? _changeNotifier;

    public MetadataChangeNotificationService(
        IMetadataProvider metadataProvider,
        IHubContext<MetadataChangeNotificationHub> hubContext,
        ILogger<MetadataChangeNotificationService> logger)
    {
        _metadataProvider = metadataProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Metadata Change Notification Service");

        // Check if the provider supports change notifications
        if (_metadataProvider is IMutableMetadataProvider mutable &&
            mutable is IMetadataChangeNotifier notifier &&
            notifier.SupportsChangeNotifications)
        {
            _changeNotifier = notifier;
            _changeNotifier.MetadataChanged += OnMetadataChanged;
            _logger.LogInformation("Subscribed to metadata change notifications");
        }
        else
        {
            _logger.LogInformation("Metadata provider does not support real-time change notifications");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Metadata Change Notification Service");

        if (_changeNotifier != null)
        {
            _changeNotifier.MetadataChanged -= OnMetadataChanged;
            _logger.LogInformation("Unsubscribed from metadata change notifications");
        }

        return Task.CompletedTask;
    }

    private async void OnMetadataChanged(object? sender, MetadataChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation(
                "Metadata changed: {ChangeType} {EntityType} {EntityId}",
                e.ChangeType,
                e.EntityType,
                e.EntityId);

            // Broadcast to all connected Admin UI clients
            await _hubContext.Clients.All.SendAsync("MetadataChanged", new
            {
                e.ChangeType,
                e.EntityType,
                e.EntityId,
                e.Timestamp
            });

            _logger.LogDebug("Broadcasted metadata change to all SignalR clients");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting metadata change notification");
        }
    }

    public void Dispose()
    {
        if (_changeNotifier != null)
        {
            _changeNotifier.MetadataChanged -= OnMetadataChanged;
        }
    }
}
