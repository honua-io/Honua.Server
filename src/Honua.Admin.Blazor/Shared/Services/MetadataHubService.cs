// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR.Client;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for managing SignalR connection to metadata change notification hub.
/// </summary>
public sealed class MetadataHubService : IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetadataHubService> _logger;
    private readonly AuthenticationService _authService;
    private HubConnection? _hubConnection;
    private bool _isInitialized = false;

    public event Func<MetadataChangedNotification, Task>? OnMetadataChanged;

    public MetadataHubService(
        IConfiguration configuration,
        ILogger<MetadataHubService> logger,
        AuthenticationService authService)
    {
        _configuration = configuration;
        _logger = logger;
        _authService = authService;
    }

    /// <summary>
    /// Gets whether the metadata provider supports real-time updates.
    /// </summary>
    public bool SupportsRealTimeUpdates { get; private set; }

    /// <summary>
    /// Gets whether the hub connection is connected.
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Initializes and starts the SignalR connection.
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        if (_isInitialized)
        {
            return SupportsRealTimeUpdates;
        }

        try
        {
            var baseUrl = _configuration["AdminApi:BaseUrl"] ?? "https://localhost:5001";
            var hubUrl = $"{baseUrl}/admin/hub/metadata";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await _authService.GetAccessTokenAsync();
                        return token;
                    };
                })
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
                .Build();

            // Subscribe to metadata change notifications
            _hubConnection.On<MetadataChangedNotification>("MetadataChanged", async (notification) =>
            {
                _logger.LogInformation(
                    "Received metadata change: {ChangeType} {EntityType} {EntityId}",
                    notification.ChangeType,
                    notification.EntityType,
                    notification.EntityId);

                if (OnMetadataChanged != null)
                {
                    await OnMetadataChanged.Invoke(notification);
                }
            });

            // Start connection
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established");

            // Check if server supports real-time updates
            SupportsRealTimeUpdates = await _hubConnection.InvokeAsync<bool>("GetSupportsRealTimeUpdatesAsync");
            _logger.LogInformation("Real-time updates supported: {Supported}", SupportsRealTimeUpdates);

            _isInitialized = true;
            return SupportsRealTimeUpdates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SignalR connection");
            SupportsRealTimeUpdates = false;
            _isInitialized = true;
            return false;
        }
    }

    /// <summary>
    /// Stops the SignalR connection.
    /// </summary>
    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            _logger.LogInformation("SignalR connection stopped");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}

/// <summary>
/// Notification model for metadata changes.
/// </summary>
public sealed class MetadataChangedNotification
{
    public required string ChangeType { get; init; }  // "Created", "Updated", "Deleted"
    public required string EntityType { get; init; }  // "Service", "Layer", "Folder"
    public required string EntityId { get; init; }
    public DateTime Timestamp { get; init; }
}
