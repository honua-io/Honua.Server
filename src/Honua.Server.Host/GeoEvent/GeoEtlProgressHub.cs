// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Honua.Server.Host.GeoEvent;

/// <summary>
/// SignalR hub for real-time GeoETL workflow execution progress tracking
/// </summary>
/// <remarks>
/// Clients can connect to this hub to receive real-time workflow execution progress as it occurs.
///
/// **Client Connection (JavaScript)**:
/// <code>
/// const connection = new signalR.HubConnectionBuilder()
///     .withUrl("/hubs/geoetl-progress", {
///         accessTokenFactory: () => yourAuthToken
///     })
///     .withAutomaticReconnect()
///     .build();
///
/// connection.on("WorkflowStarted", (runId, metadata) => {
///     console.log("Workflow started:", runId, metadata);
/// });
///
/// connection.on("NodeStarted", (runId, nodeId, nodeName) => {
///     console.log("Node started:", nodeId, nodeName);
/// });
///
/// connection.on("NodeProgress", (runId, nodeId, percent, message) => {
///     console.log("Node progress:", nodeId, percent + "%", message);
/// });
///
/// connection.on("NodeCompleted", (runId, nodeId, result) => {
///     console.log("Node completed:", nodeId, result);
/// });
///
/// connection.on("NodeFailed", (runId, nodeId, error) => {
///     console.error("Node failed:", nodeId, error);
/// });
///
/// connection.on("WorkflowCompleted", (runId, summary) => {
///     console.log("Workflow completed:", summary);
/// });
///
/// connection.on("WorkflowFailed", (runId, error) => {
///     console.error("Workflow failed:", error);
/// });
///
/// await connection.start();
///
/// // Subscribe to specific workflow run
/// await connection.invoke("SubscribeToWorkflow", "run-id-here");
/// </code>
/// </remarks>
[Authorize]
public class GeoEtlProgressHub : Hub
{
    private readonly ILogger<GeoEtlProgressHub> _logger;

    public GeoEtlProgressHub(ILogger<GeoEtlProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to progress updates for a specific workflow run
    /// </summary>
    /// <param name="runId">Workflow run ID to subscribe to</param>
    public async Task SubscribeToWorkflow(Guid runId)
    {
        var groupName = GetWorkflowGroupName(runId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to workflow run {RunId}",
            Context.ConnectionId,
            runId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "workflow",
            runId = runId,
            message = $"Subscribed to workflow run {runId}"
        });
    }

    /// <summary>
    /// Unsubscribe from progress updates for a specific workflow run
    /// </summary>
    public async Task UnsubscribeFromWorkflow(Guid runId)
    {
        var groupName = GetWorkflowGroupName(runId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from workflow run {RunId}",
            Context.ConnectionId,
            runId);
    }

    /// <summary>
    /// Subscribe to all workflow execution events (requires admin permission)
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all-workflows");

        _logger.LogInformation(
            "Client {ConnectionId} subscribed to all workflow events",
            Context.ConnectionId);

        await Clients.Caller.SendAsync("Subscribed", new
        {
            type = "all",
            message = "Subscribed to all workflow execution events"
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to GeoETL Progress hub", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation(
            "Client {ConnectionId} disconnected from GeoETL Progress hub. Exception: {Exception}",
            Context.ConnectionId,
            exception?.Message);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gets the SignalR group name for a workflow run
    /// </summary>
    internal static string GetWorkflowGroupName(Guid runId) => $"workflow:{runId}";
}
