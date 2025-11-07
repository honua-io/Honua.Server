using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Honua.Admin.Blazor.Services;

/// <summary>
/// Service for managing SignalR connection to GeoETL progress hub and handling real-time workflow execution updates
/// </summary>
public class GeoEtlProgressService : IAsyncDisposable
{
    private readonly ILogger<GeoEtlProgressService> _logger;
    private HubConnection? _hubConnection;
    private readonly List<IDisposable> _eventSubscriptions = new();

    public GeoEtlProgressService(ILogger<GeoEtlProgressService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the connection is currently active
    /// </summary>
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Initialize the SignalR connection
    /// </summary>
    public async Task InitializeAsync(string baseUrl)
    {
        if (_hubConnection != null)
        {
            await DisposeAsync();
        }

        var hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/geoetl-progress";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR connection lost. Reconnecting... Error: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected. Connection ID: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogError("SignalR connection closed. Error: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established to {HubUrl}", hubUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish SignalR connection to {HubUrl}", hubUrl);
            throw;
        }
    }

    /// <summary>
    /// Subscribe to a specific workflow run
    /// </summary>
    public async Task SubscribeToWorkflowAsync(Guid runId)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        await _hubConnection.InvokeAsync("SubscribeToWorkflow", runId);
        _logger.LogDebug("Subscribed to workflow run {RunId}", runId);
    }

    /// <summary>
    /// Unsubscribe from a specific workflow run
    /// </summary>
    public async Task UnsubscribeFromWorkflowAsync(Guid runId)
    {
        if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
        {
            return;
        }

        await _hubConnection.InvokeAsync("UnsubscribeFromWorkflow", runId);
        _logger.LogDebug("Unsubscribed from workflow run {RunId}", runId);
    }

    /// <summary>
    /// Register event handler for workflow started
    /// </summary>
    public IDisposable OnWorkflowStarted(Action<WorkflowStartedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("WorkflowStarted", data =>
        {
            var evt = ParseWorkflowStartedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for node started
    /// </summary>
    public IDisposable OnNodeStarted(Action<NodeStartedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("NodeStarted", data =>
        {
            var evt = ParseNodeStartedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for node progress
    /// </summary>
    public IDisposable OnNodeProgress(Action<NodeProgressEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("NodeProgress", data =>
        {
            var evt = ParseNodeProgressEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for node completed
    /// </summary>
    public IDisposable OnNodeCompleted(Action<NodeCompletedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("NodeCompleted", data =>
        {
            var evt = ParseNodeCompletedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for node failed
    /// </summary>
    public IDisposable OnNodeFailed(Action<NodeFailedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("NodeFailed", data =>
        {
            var evt = ParseNodeFailedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for workflow completed
    /// </summary>
    public IDisposable OnWorkflowCompleted(Action<WorkflowCompletedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("WorkflowCompleted", data =>
        {
            var evt = ParseWorkflowCompletedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for workflow failed
    /// </summary>
    public IDisposable OnWorkflowFailed(Action<WorkflowFailedEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("WorkflowFailed", data =>
        {
            var evt = ParseWorkflowFailedEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    /// <summary>
    /// Register event handler for workflow cancelled
    /// </summary>
    public IDisposable OnWorkflowCancelled(Action<WorkflowCancelledEvent> handler)
    {
        if (_hubConnection == null)
        {
            throw new InvalidOperationException("SignalR connection not established. Call InitializeAsync first.");
        }

        var subscription = _hubConnection.On<object>("WorkflowCancelled", data =>
        {
            var evt = ParseWorkflowCancelledEvent(data);
            handler(evt);
        });

        _eventSubscriptions.Add(subscription);
        return subscription;
    }

    private WorkflowStartedEvent ParseWorkflowStartedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<WorkflowStartedEvent>(json)!;
    }

    private NodeStartedEvent ParseNodeStartedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<NodeStartedEvent>(json)!;
    }

    private NodeProgressEvent ParseNodeProgressEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<NodeProgressEvent>(json)!;
    }

    private NodeCompletedEvent ParseNodeCompletedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<NodeCompletedEvent>(json)!;
    }

    private NodeFailedEvent ParseNodeFailedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<NodeFailedEvent>(json)!;
    }

    private WorkflowCompletedEvent ParseWorkflowCompletedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<WorkflowCompletedEvent>(json)!;
    }

    private WorkflowFailedEvent ParseWorkflowFailedEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<WorkflowFailedEvent>(json)!;
    }

    private WorkflowCancelledEvent ParseWorkflowCancelledEvent(object data)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        return System.Text.Json.JsonSerializer.Deserialize<WorkflowCancelledEvent>(json)!;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var subscription in _eventSubscriptions)
        {
            subscription.Dispose();
        }
        _eventSubscriptions.Clear();

        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}

// Event models
public record WorkflowStartedEvent(
    Guid RunId,
    Guid WorkflowId,
    string WorkflowName,
    int TotalNodes,
    DateTimeOffset StartedAt);

public record NodeStartedEvent(
    Guid RunId,
    string NodeId,
    string NodeName,
    string NodeType,
    DateTimeOffset StartedAt);

public record NodeProgressEvent(
    Guid RunId,
    string NodeId,
    int Percent,
    string? Message,
    long? FeaturesProcessed,
    long? TotalFeatures);

public record NodeCompletedEvent(
    Guid RunId,
    string NodeId,
    long DurationMs,
    long? FeaturesProcessed,
    DateTimeOffset CompletedAt);

public record NodeFailedEvent(
    Guid RunId,
    string NodeId,
    string Error,
    DateTimeOffset FailedAt);

public record WorkflowCompletedEvent(
    Guid RunId,
    DateTimeOffset CompletedAt,
    long TotalDurationMs,
    int NodesCompleted,
    int TotalNodes,
    long? TotalFeaturesProcessed);

public record WorkflowFailedEvent(
    Guid RunId,
    string Error,
    DateTimeOffset FailedAt);

public record WorkflowCancelledEvent(
    Guid RunId,
    string Reason,
    DateTimeOffset CancelledAt);
