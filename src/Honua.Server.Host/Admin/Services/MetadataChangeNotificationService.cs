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
    private readonly IMetadataProvider metadataProvider;
    private readonly IHubContext<MetadataChangeNotificationHub> hubContext;
    private readonly ILogger<MetadataChangeNotificationService> logger;
    private IMetadataChangeNotifier? _changeNotifier;

    public MetadataChangeNotificationService(
        IMetadataProvider metadataProvider,
        IHubContext<MetadataChangeNotificationHub> hubContext,
        ILogger<MetadataChangeNotificationService> logger)
    {
        this.metadataProvider = metadataProvider;
        this.hubContext = hubContext;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Starting Metadata Change Notification Service");

        // Check if the provider supports change notifications
        if (this.metadataProvider is IMutableMetadataProvider mutable &&
            mutable is IMetadataChangeNotifier notifier &&
            notifier.SupportsChangeNotifications)
        {
            this._changeNotifier = notifier;
            this._changeNotifier.MetadataChanged += OnMetadataChanged;
            this.logger.LogInformation("Subscribed to metadata change notifications");
        }
        else
        {
            this.logger.LogInformation("Metadata provider does not support real-time change notifications");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Stopping Metadata Change Notification Service");

        if (this._changeNotifier != null)
        {
            this._changeNotifier.MetadataChanged -= OnMetadataChanged;
            this.logger.LogInformation("Unsubscribed from metadata change notifications");
        }

        return Task.CompletedTask;
    }

    private void OnMetadataChanged(
        object? sender,
        Honua.Server.Host.Admin.Hubs.MetadataChangedEventArgs e)
    {
        // Fire and forget with proper exception handling
        _ = OnMetadataChangedAsync(e);
    }

    private async Task OnMetadataChangedAsync(
        Honua.Server.Host.Admin.Hubs.MetadataChangedEventArgs e)
    {
        try
        {
            this.logger.LogInformation(
                "Metadata changed: {ChangeType} {EntityType} {EntityId}",
                e.ChangeType,
                e.EntityType,
                e.EntityId);

            // Broadcast to all connected Admin UI clients
            await this.hubContext.Clients.All.SendAsync("MetadataChanged", new
            {
                e.ChangeType,
                e.EntityType,
                e.EntityId,
                e.Timestamp
            });

            this.logger.LogDebug("Broadcasted metadata change to all SignalR clients");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error broadcasting metadata change notification");
        }
    }

    public void Dispose()
    {
        if (this._changeNotifier != null)
        {
            this._changeNotifier.MetadataChanged -= OnMetadataChanged;
        }
    }
}
