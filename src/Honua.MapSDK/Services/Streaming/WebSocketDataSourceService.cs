// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Streaming;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Streaming;

/// <summary>
/// Service for managing WebSocket connections to streaming data sources.
/// Handles connection lifecycle, message parsing, automatic reconnection, and health monitoring.
/// </summary>
public class WebSocketDataSourceService : IAsyncDisposable
{
    private readonly ILogger<WebSocketDataSourceService> _logger;
    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections = new();
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketDataSourceService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics.</param>
    public WebSocketDataSourceService(ILogger<WebSocketDataSourceService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connects to a WebSocket endpoint with the specified configuration.
    /// </summary>
    /// <param name="dataSource">Data source configuration.</param>
    /// <param name="onMessage">Callback invoked when a message is received.</param>
    /// <param name="onConnected">Callback invoked when connection is established.</param>
    /// <param name="onDisconnected">Callback invoked when connection is closed.</param>
    /// <param name="onError">Callback invoked when an error occurs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection ID.</returns>
    public async Task<string> ConnectAsync(
        StreamingDataSource dataSource,
        Func<StreamingUpdate, Task> onMessage,
        Func<Task>? onConnected = null,
        Func<Task>? onDisconnected = null,
        Func<Exception, Task>? onError = null,
        CancellationToken cancellationToken = default)
    {
        if (dataSource == null)
            throw new ArgumentNullException(nameof(dataSource));

        if (string.IsNullOrEmpty(dataSource.WebSocketUrl))
            throw new ArgumentException("WebSocket URL is required", nameof(dataSource));

        if (onMessage == null)
            throw new ArgumentNullException(nameof(onMessage));

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            // Check if already connected
            if (_connections.TryGetValue(dataSource.Id, out var existingConnection))
            {
                if (existingConnection.State == ConnectionState.Connected ||
                    existingConnection.State == ConnectionState.Connecting)
                {
                    _logger.LogWarning("Connection {ConnectionId} is already active", dataSource.Id);
                    return dataSource.Id;
                }

                // Clean up old connection
                await DisconnectInternalAsync(dataSource.Id, cancellationToken);
            }

            var connection = new WebSocketConnection(dataSource, onMessage, onConnected, onDisconnected, onError, _logger);
            _connections[dataSource.Id] = connection;

            // Start connection in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await connection.ConnectAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error establishing connection {ConnectionId}", dataSource.Id);
                    if (onError != null)
                        await onError(ex);
                }
            }, cancellationToken);

            return dataSource.Id;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    /// Disconnects from a WebSocket endpoint.
    /// </summary>
    /// <param name="connectionId">Connection ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DisconnectAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectInternalAsync(connectionId, cancellationToken);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisconnectInternalAsync(string connectionId, CancellationToken cancellationToken)
    {
        if (_connections.TryRemove(connectionId, out var connection))
        {
            await connection.DisconnectAsync(cancellationToken);
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Gets the current statistics for a connection.
    /// </summary>
    /// <param name="connectionId">Connection ID.</param>
    /// <returns>Connection statistics, or null if not found.</returns>
    public StreamingStatistics? GetStatistics(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection)
            ? connection.GetStatistics()
            : null;
    }

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    /// <param name="connectionId">Connection ID.</param>
    /// <returns>Connection state, or Disconnected if not found.</returns>
    public ConnectionState GetConnectionState(string connectionId)
    {
        return _connections.TryGetValue(connectionId, out var connection)
            ? connection.State
            : ConnectionState.Disconnected;
    }

    /// <summary>
    /// Sends a message through the WebSocket connection.
    /// </summary>
    /// <param name="connectionId">Connection ID.</param>
    /// <param name="message">Message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendMessageAsync(string connectionId, string message, CancellationToken cancellationToken = default)
    {
        if (_connections.TryGetValue(connectionId, out var connection))
        {
            await connection.SendAsync(message, cancellationToken);
        }
        else
        {
            throw new InvalidOperationException($"Connection {connectionId} not found");
        }
    }

    /// <summary>
    /// Disposes all active connections.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var tasks = new List<Task>();
        foreach (var connection in _connections.Values)
        {
            tasks.Add(connection.DisposeAsync().AsTask());
        }

        _connections.Clear();
        await Task.WhenAll(tasks);
        _connectionLock.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Internal class representing a single WebSocket connection.
    /// </summary>
    private class WebSocketConnection : IAsyncDisposable
    {
        private readonly StreamingDataSource _config;
        private readonly Func<StreamingUpdate, Task> _onMessage;
        private readonly Func<Task>? _onConnected;
        private readonly Func<Task>? _onDisconnected;
        private readonly Func<Exception, Task>? _onError;
        private readonly ILogger _logger;
        private readonly StreamingStatistics _statistics = new();
        private readonly ConcurrentQueue<string> _messageBuffer = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _lifetimeCts;
        private Task? _receiveTask;
        private Task? _heartbeatTask;
        private Task? _processingTask;
        private int _reconnectAttempts;
        private DateTime _lastMessageTime = DateTime.UtcNow;

        public ConnectionState State => _statistics.State;

        public WebSocketConnection(
            StreamingDataSource config,
            Func<StreamingUpdate, Task> onMessage,
            Func<Task>? onConnected,
            Func<Task>? onDisconnected,
            Func<Exception, Task>? onError,
            ILogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
            _onConnected = onConnected;
            _onDisconnected = onDisconnected;
            _onError = onError;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            while (!_lifetimeCts.Token.IsCancellationRequested)
            {
                try
                {
                    _statistics.State = _reconnectAttempts > 0 ? ConnectionState.Reconnecting : ConnectionState.Connecting;
                    _statistics.ReconnectAttempts = _reconnectAttempts;

                    _webSocket = new ClientWebSocket();

                    // Configure WebSocket options
                    if (!string.IsNullOrEmpty(_config.AuthToken))
                    {
                        _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_config.AuthToken}");
                    }

                    foreach (var header in _config.CustomHeaders)
                    {
                        _webSocket.Options.SetRequestHeader(header.Key, header.Value);
                    }

                    if (!string.IsNullOrEmpty(_config.SubProtocol))
                    {
                        _webSocket.Options.AddSubProtocol(_config.SubProtocol);
                    }

                    // Connect
                    var uri = new Uri(_config.WebSocketUrl);
                    await _webSocket.ConnectAsync(uri, _lifetimeCts.Token);

                    _statistics.State = ConnectionState.Connected;
                    _statistics.ConnectedAt = DateTime.UtcNow;
                    _reconnectAttempts = 0;

                    _logger.LogInformation("WebSocket connected to {Url}", _config.WebSocketUrl);

                    if (_onConnected != null)
                        await _onConnected();

                    // Start receive and processing tasks
                    _receiveTask = ReceiveMessagesAsync(_lifetimeCts.Token);
                    _processingTask = ProcessMessagesAsync(_lifetimeCts.Token);

                    if (_config.EnableHeartbeat)
                    {
                        _heartbeatTask = HeartbeatMonitorAsync(_lifetimeCts.Token);
                    }

                    // Wait for disconnection
                    await _receiveTask;

                    // Connection closed
                    _statistics.State = ConnectionState.Disconnected;

                    if (_onDisconnected != null)
                        await _onDisconnected();

                    // Check if we should reconnect
                    if (!_config.EnableAutoReconnect)
                        break;

                    if (_config.MaxReconnectAttempts > 0 && _reconnectAttempts >= _config.MaxReconnectAttempts)
                    {
                        _logger.LogWarning("Max reconnection attempts ({Count}) reached for {Url}", _config.MaxReconnectAttempts, _config.WebSocketUrl);
                        break;
                    }

                    // Calculate delay with exponential backoff
                    _reconnectAttempts++;
                    var delay = Math.Min(
                        _config.ReconnectDelay * Math.Pow(1.5, _reconnectAttempts - 1),
                        _config.MaxReconnectDelay
                    );

                    _logger.LogInformation("Reconnecting in {Delay}ms (attempt {Attempt})", delay, _reconnectAttempts);
                    await Task.Delay(TimeSpan.FromMilliseconds(delay), _lifetimeCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _statistics.State = ConnectionState.Failed;
                    _statistics.ErrorCount++;

                    _logger.LogError(ex, "Error in WebSocket connection");

                    if (_onError != null)
                        await _onError(ex);

                    if (!_config.EnableAutoReconnect)
                        break;

                    // Retry with backoff
                    _reconnectAttempts++;
                    var delay = Math.Min(
                        _config.ReconnectDelay * Math.Pow(1.5, _reconnectAttempts - 1),
                        _config.MaxReconnectDelay
                    );

                    await Task.Delay(TimeSpan.FromMilliseconds(delay), _lifetimeCts.Token);
                }
            }
        }

        private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
        {
            if (_webSocket == null) return;

            var buffer = new byte[8192];

            try
            {
                while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _lastMessageTime = DateTime.UtcNow;
                        _statistics.MessagesReceived++;
                        _statistics.BytesReceived += result.Count;
                        _statistics.LastMessageAt = DateTime.UtcNow;

                        // Add to buffer for processing
                        _messageBuffer.Enqueue(message);

                        // Limit buffer size
                        while (_messageBuffer.Count > _config.BufferSize)
                        {
                            _messageBuffer.TryDequeue(out _);
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // Handle binary messages (future implementation)
                        _logger.LogWarning("Binary messages not yet supported");
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocket error during receive");
                _statistics.ErrorCount++;
            }
        }

        private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
        {
            var lastProcessTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Throttle processing based on UpdateThrottle
                    var elapsed = (DateTime.UtcNow - lastProcessTime).TotalMilliseconds;
                    if (elapsed < _config.UpdateThrottle)
                    {
                        await Task.Delay((int)(_config.UpdateThrottle - elapsed), cancellationToken);
                    }

                    lastProcessTime = DateTime.UtcNow;

                    // Process all buffered messages
                    var processedCount = 0;
                    while (_messageBuffer.TryDequeue(out var message) && processedCount < 100)
                    {
                        try
                        {
                            var update = ParseMessage(message);
                            if (update != null)
                            {
                                await _onMessage(update);

                                // Update statistics
                                if (update.Type == UpdateType.Feature)
                                {
                                    _statistics.FeaturesUpdated++;
                                }
                                else if (update.Type == UpdateType.Delete)
                                {
                                    _statistics.FeaturesDeleted++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message");
                            _statistics.ErrorCount++;
                        }

                        processedCount++;
                    }

                    // Calculate messages per second
                    if (_statistics.ConnectedAt.HasValue)
                    {
                        var duration = (DateTime.UtcNow - _statistics.ConnectedAt.Value).TotalSeconds;
                        if (duration > 0)
                        {
                            _statistics.MessagesPerSecond = _statistics.MessagesReceived / duration;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in message processing loop");
                }
            }
        }

        private StreamingUpdate? ParseMessage(string message)
        {
            try
            {
                if (_config.Format == MessageFormat.GeoJson)
                {
                    // Parse GeoJSON
                    var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;

                    // Check if it's a Feature or FeatureCollection
                    if (root.TryGetProperty("type", out var typeElement))
                    {
                        var type = typeElement.GetString();
                        if (type == "Feature" || type == "FeatureCollection")
                        {
                            return new StreamingUpdate
                            {
                                Type = UpdateType.Feature,
                                Data = root,
                                Timestamp = DateTime.UtcNow
                            };
                        }
                    }

                    // Check for custom update format
                    if (root.TryGetProperty("action", out var actionElement))
                    {
                        var action = actionElement.GetString();
                        var update = new StreamingUpdate
                        {
                            Timestamp = DateTime.UtcNow
                        };

                        if (action == "update" || action == "add")
                        {
                            update.Type = UpdateType.Feature;
                        }
                        else if (action == "delete")
                        {
                            update.Type = UpdateType.Delete;
                        }
                        else if (action == "clear")
                        {
                            update.Type = UpdateType.Clear;
                        }

                        if (root.TryGetProperty("id", out var idElement))
                        {
                            update.FeatureId = idElement.GetString();
                        }

                        if (root.TryGetProperty("data", out var dataElement))
                        {
                            update.Data = dataElement;
                        }

                        return update;
                    }
                }
                else if (_config.Format == MessageFormat.Json)
                {
                    // Parse custom JSON format
                    var doc = JsonDocument.Parse(message);
                    return new StreamingUpdate
                    {
                        Type = UpdateType.Feature,
                        Data = doc.RootElement,
                        Timestamp = DateTime.UtcNow
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing message: {Message}", message);
                return null;
            }
        }

        private async Task HeartbeatMonitorAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await Task.Delay(_config.HeartbeatInterval, cancellationToken);

                    // Check if we've received any messages recently
                    var timeSinceLastMessage = (DateTime.UtcNow - _lastMessageTime).TotalMilliseconds;
                    if (timeSinceLastMessage > _config.HeartbeatInterval + _config.HeartbeatTimeout)
                    {
                        _logger.LogWarning("No messages received in {Time}ms, connection may be stale", timeSinceLastMessage);

                        // Send ping
                        await SendAsync("{\"type\":\"ping\"}", cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in heartbeat monitor");
                }
            }
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            if (_webSocket?.State != WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket is not open");
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (_lifetimeCts != null && !_lifetimeCts.IsCancellationRequested)
            {
                _lifetimeCts.Cancel();
            }

            if (_webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing WebSocket");
                }
            }
        }

        public StreamingStatistics GetStatistics()
        {
            return _statistics;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None);

            _lifetimeCts?.Dispose();
            _webSocket?.Dispose();
            _sendLock.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
