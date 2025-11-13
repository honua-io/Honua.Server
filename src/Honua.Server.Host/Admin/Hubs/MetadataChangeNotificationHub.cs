// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.Admin.Hubs;

/// <summary>
/// SignalR hub for broadcasting metadata change notifications to Admin UI clients.
/// </summary>
public sealed class MetadataChangeNotificationHub : Hub
{
    private readonly IMetadataProvider metadataProvider;
    private readonly ILogger<MetadataChangeNotificationHub> logger;

    public MetadataChangeNotificationHub(
        IMetadataProvider metadataProvider,
        ILogger<MetadataChangeNotificationHub> logger)
    {
        this.metadataProvider = metadataProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        this.logger.LogInformation("Admin UI client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            this.logger.LogWarning(exception, "Admin UI client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            this.logger.LogInformation("Admin UI client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Checks if the metadata provider supports real-time change notifications.
    /// </summary>
    public Task<bool> GetSupportsRealTimeUpdatesAsync()
    {
        // Check if provider implements change notification
        var supportsMutable = this.metadataProvider is IMutableMetadataProvider mutable
            && (mutable as IMetadataChangeNotifier)?.SupportsChangeNotifications == true;

        this.logger.LogDebug("Real-time updates supported: {Supported}", supportsMutable);
        return Task.FromResult(supportsMutable);
    }

    /// <summary>
    /// Pings the hub to check connectivity.
    /// </summary>
    public Task<string> PingAsync()
    {
        return Task.FromResult("pong");
    }
}

/// <summary>
/// Interface for metadata providers that support change notifications.
/// This is used to detect if the provider can notify about metadata changes.
/// </summary>
public interface IMetadataChangeNotifier
{
    /// <summary>
    /// Gets whether this provider supports change notifications.
    /// </summary>
    bool SupportsChangeNotifications { get; }

    /// <summary>
    /// Event raised when metadata changes.
    /// </summary>
    event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
}

/// <summary>
/// Event args for metadata change notifications.
/// </summary>
public sealed class MetadataChangedEventArgs : EventArgs
{
    public required string ChangeType { get; init; }  // "Created", "Updated", "Deleted"
    public required string EntityType { get; init; }  // "Service", "Layer", "Folder"
    public required string EntityId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
