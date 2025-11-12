// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Examples;

/// <summary>
/// Mock WebSocket server for testing real-time data streaming.
/// Simulates GPS tracking data for multiple vehicles with configurable update rates.
/// </summary>
public class MockStreamingServer : IAsyncDisposable
{
    private readonly ILogger<MockStreamingServer> _logger;
    private readonly HttpListener _httpListener;
    private readonly ConcurrentBag<WebSocket> _connectedClients = new();
    private readonly List<Vehicle> _vehicles = new();
    private readonly Random _random = new();
    private CancellationTokenSource? _serverCts;
    private Task? _serverTask;
    private Task? _dataGenerationTask;

    /// <summary>
    /// Server URL (e.g., http://localhost:8080/)
    /// </summary>
    public string ServerUrl { get; }

    /// <summary>
    /// WebSocket endpoint URL (e.g., ws://localhost:8080/stream)
    /// </summary>
    public string WebSocketUrl => ServerUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "stream";

    /// <summary>
    /// Number of vehicles to simulate
    /// </summary>
    public int VehicleCount { get; set; } = 10;

    /// <summary>
    /// Update interval in milliseconds
    /// </summary>
    public int UpdateInterval { get; set; } = 1000;

    /// <summary>
    /// Initial center point (latitude)
    /// </summary>
    public double CenterLatitude { get; set; } = 21.3099;

    /// <summary>
    /// Initial center point (longitude)
    /// </summary>
    public double CenterLongitude { get; set; } = -157.8581;

    /// <summary>
    /// Movement radius in degrees
    /// </summary>
    public double MovementRadius { get; set; } = 0.05;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockStreamingServer"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="port">Server port (default 8080).</param>
    public MockStreamingServer(ILogger<MockStreamingServer> logger, int port = 8080)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ServerUrl = $"http://localhost:{port}/";

        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add(ServerUrl);
    }

    /// <summary>
    /// Starts the mock WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_serverTask != null)
        {
            _logger.LogWarning("Server is already running");
            return;
        }

        _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Initialize vehicles
        InitializeVehicles();

        try
        {
            _httpListener.Start();
            _logger.LogInformation("Mock streaming server started at {Url}", ServerUrl);
            _logger.LogInformation("WebSocket endpoint: {Url}", WebSocketUrl);

            // Start server task
            _serverTask = Task.Run(async () => await AcceptClientsAsync(_serverCts.Token), _serverCts.Token);

            // Start data generation task
            _dataGenerationTask = Task.Run(async () => await GenerateDataAsync(_serverCts.Token), _serverCts.Token);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting server");
            throw;
        }
    }

    /// <summary>
    /// Stops the mock WebSocket server.
    /// </summary>
    public async Task StopAsync()
    {
        if (_serverCts != null && !_serverCts.IsCancellationRequested)
        {
            _serverCts.Cancel();
        }

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        if (_dataGenerationTask != null)
        {
            try
            {
                await _dataGenerationTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Close all connected clients
        foreach (var client in _connectedClients)
        {
            try
            {
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutdown", CancellationToken.None);
            }
            catch
            {
                // Ignore
            }
        }

        _connectedClients.Clear();
        _httpListener.Stop();

        _logger.LogInformation("Mock streaming server stopped");
    }

    private void InitializeVehicles()
    {
        var vehicleTypes = new[] { "Car", "Truck", "Bus", "Van", "Taxi", "Delivery" };
        var colors = new[] { "#FF5733", "#33FF57", "#3357FF", "#FF33F5", "#F5FF33", "#33FFF5", "#FF8C33", "#8C33FF", "#33FF8C", "#FF3333" };

        for (int i = 0; i < VehicleCount; i++)
        {
            var angle = _random.NextDouble() * 2 * Math.PI;
            var distance = _random.NextDouble() * MovementRadius;

            _vehicles.Add(new Vehicle
            {
                Id = $"vehicle-{i + 1:D3}",
                Name = $"Vehicle {i + 1}",
                Type = vehicleTypes[_random.Next(vehicleTypes.Length)],
                Color = colors[i % colors.Length],
                Latitude = CenterLatitude + Math.Cos(angle) * distance,
                Longitude = CenterLongitude + Math.Sin(angle) * distance,
                Speed = _random.Next(20, 80),
                Heading = _random.Next(0, 360),
                Status = "active"
            });
        }

        _logger.LogInformation("Initialized {Count} vehicles", VehicleCount);
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(async () => await HandleWebSocketAsync(context), cancellationToken);
                }
                else
                {
                    // Return simple HTML page with info
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "text/html";
                    var html = $@"
                        <html>
                            <head><title>Mock Streaming Server</title></head>
                            <body>
                                <h1>Mock Streaming Server</h1>
                                <p>WebSocket endpoint: <code>{WebSocketUrl}</code></p>
                                <p>Simulating {VehicleCount} vehicles</p>
                                <p>Update interval: {UpdateInterval}ms</p>
                                <p>Connected clients: {_connectedClients.Count}</p>
                            </body>
                        </html>
                    ";
                    var buffer = Encoding.UTF8.GetBytes(html);
                    await context.Response.OutputStream.WriteAsync(buffer, cancellationToken);
                    context.Response.Close();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client");
            }
        }
    }

    private async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        WebSocket? webSocket = null;
        try
        {
            var webSocketContext = await context.AcceptWebSocketAsync(null);
            webSocket = webSocketContext.WebSocket;

            _connectedClients.Add(webSocket);
            _logger.LogInformation("Client connected. Total clients: {Count}", _connectedClients.Count);

            // Send initial data
            await SendInitialDataAsync(webSocket);

            // Keep connection alive and handle incoming messages
            var buffer = new byte[1024];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    // Echo pings
                    if (message.Contains("ping"))
                    {
                        await SendMessageAsync(webSocket, "{\"type\":\"pong\"}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
        }
        finally
        {
            if (webSocket != null)
            {
                _connectedClients.TryTake(out _);
                _logger.LogInformation("Client disconnected. Total clients: {Count}", _connectedClients.Count);
            }
        }
    }

    private async Task SendInitialDataAsync(WebSocket webSocket)
    {
        var features = _vehicles.Select(v => CreateFeature(v)).ToList();
        var featureCollection = new
        {
            type = "FeatureCollection",
            features = features
        };

        var json = JsonSerializer.Serialize(featureCollection);
        await SendMessageAsync(webSocket, json);

        _logger.LogInformation("Sent initial data with {Count} vehicles", features.Count);
    }

    private async Task GenerateDataAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(UpdateInterval, cancellationToken);

                // Update vehicle positions
                foreach (var vehicle in _vehicles)
                {
                    UpdateVehiclePosition(vehicle);
                }

                // Send updates to all connected clients
                if (_connectedClients.Count > 0)
                {
                    // Randomly choose 1-3 vehicles to update
                    var updateCount = _random.Next(1, Math.Min(4, _vehicles.Count + 1));
                    var vehiclesToUpdate = _vehicles.OrderBy(_ => _random.Next()).Take(updateCount).ToList();

                    foreach (var vehicle in vehiclesToUpdate)
                    {
                        var feature = CreateFeature(vehicle);
                        var json = JsonSerializer.Serialize(feature);

                        await BroadcastMessageAsync(json, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating data");
            }
        }
    }

    private void UpdateVehiclePosition(Vehicle vehicle)
    {
        // Calculate movement based on speed and heading
        var speedDegrees = (vehicle.Speed / 111000.0) * (UpdateInterval / 1000.0); // Convert to degrees per update

        var headingRadians = vehicle.Heading * Math.PI / 180.0;
        vehicle.Latitude += Math.Cos(headingRadians) * speedDegrees;
        vehicle.Longitude += Math.Sin(headingRadians) * speedDegrees;

        // Randomly adjust heading (simulate turning)
        vehicle.Heading += _random.Next(-15, 16);
        if (vehicle.Heading < 0) vehicle.Heading += 360;
        if (vehicle.Heading >= 360) vehicle.Heading -= 360;

        // Randomly adjust speed slightly
        vehicle.Speed += _random.Next(-5, 6);
        vehicle.Speed = Math.Max(10, Math.Min(100, vehicle.Speed));

        // Keep vehicles within bounds
        var distanceFromCenter = Math.Sqrt(
            Math.Pow(vehicle.Latitude - CenterLatitude, 2) +
            Math.Pow(vehicle.Longitude - CenterLongitude, 2)
        );

        if (distanceFromCenter > MovementRadius)
        {
            // Turn vehicle back towards center
            var angleToCenter = Math.Atan2(
                CenterLongitude - vehicle.Longitude,
                CenterLatitude - vehicle.Latitude
            ) * 180.0 / Math.PI;

            vehicle.Heading = (int)angleToCenter;
            if (vehicle.Heading < 0) vehicle.Heading += 360;
        }

        // Update timestamp
        vehicle.LastUpdate = DateTime.UtcNow;
    }

    private object CreateFeature(Vehicle vehicle)
    {
        return new
        {
            type = "Feature",
            id = vehicle.Id,
            geometry = new
            {
                type = "Point",
                coordinates = new[] { vehicle.Longitude, vehicle.Latitude }
            },
            properties = new
            {
                id = vehicle.Id,
                name = vehicle.Name,
                type = vehicle.Type,
                color = vehicle.Color,
                speed = vehicle.Speed,
                heading = vehicle.Heading,
                status = vehicle.Status,
                timestamp = vehicle.LastUpdate.ToString("o")
            }
        };
    }

    private async Task BroadcastMessageAsync(string message, CancellationToken cancellationToken)
    {
        var tasks = _connectedClients
            .Where(ws => ws.State == WebSocketState.Open)
            .Select(ws => SendMessageAsync(ws, message, cancellationToken));

        await Task.WhenAll(tasks);
    }

    private async Task SendMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken = default)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        try
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
        }
    }

    /// <summary>
    /// Adds a new vehicle at runtime
    /// </summary>
    public void AddVehicle(Vehicle vehicle)
    {
        _vehicles.Add(vehicle);
        _logger.LogInformation("Added vehicle {Id}", vehicle.Id);
    }

    /// <summary>
    /// Removes a vehicle
    /// </summary>
    public void RemoveVehicle(string vehicleId)
    {
        var vehicle = _vehicles.FirstOrDefault(v => v.Id == vehicleId);
        if (vehicle != null)
        {
            _vehicles.Remove(vehicle);
            _logger.LogInformation("Removed vehicle {Id}", vehicleId);

            // Send delete message
            var deleteMessage = JsonSerializer.Serialize(new
            {
                action = "delete",
                id = vehicleId
            });

            _ = BroadcastMessageAsync(deleteMessage, CancellationToken.None);
        }
    }

    /// <summary>
    /// Gets current vehicle list
    /// </summary>
    public IReadOnlyList<Vehicle> GetVehicles() => _vehicles.AsReadOnly();

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _serverCts?.Dispose();
        _httpListener.Close();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents a simulated vehicle
    /// </summary>
    public class Vehicle
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Color { get; set; } = "#0080ff";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Speed { get; set; }
        public int Heading { get; set; }
        public string Status { get; set; } = "active";
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    }
}
