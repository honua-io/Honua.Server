// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure.Messaging.EventGrid;
using Honua.Integration.Azure.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Honua.Integration.Azure.Events;

/// <summary>
/// Handles Azure Digital Twins lifecycle events from Event Grid.
/// </summary>
public interface IAdtEventHandler
{
    /// <summary>
    /// Handles an Event Grid event from Azure Digital Twins.
    /// </summary>
    Task HandleEventAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles multiple Event Grid events in batch.
    /// </summary>
    Task HandleEventsAsync(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of ADT event handler.
/// </summary>
public sealed class AdtEventHandler : IAdtEventHandler
{
    private readonly ITwinSynchronizationService _syncService;
    private readonly ILogger<AdtEventHandler> _logger;

    public AdtEventHandler(
        ITwinSynchronizationService syncService,
        ILogger<AdtEventHandler> logger)
    {
        _syncService = syncService;
        _logger = logger;
    }

    public async Task HandleEventAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Processing ADT event: {EventType} for subject {Subject}",
                eventGridEvent.EventType,
                eventGridEvent.Subject);

            switch (eventGridEvent.EventType)
            {
                case "Microsoft.DigitalTwins.Twin.Create":
                    await HandleTwinCreatedAsync(eventGridEvent, cancellationToken);
                    break;

                case "Microsoft.DigitalTwins.Twin.Update":
                    await HandleTwinUpdatedAsync(eventGridEvent, cancellationToken);
                    break;

                case "Microsoft.DigitalTwins.Twin.Delete":
                    await HandleTwinDeletedAsync(eventGridEvent, cancellationToken);
                    break;

                case "Microsoft.DigitalTwins.Relationship.Create":
                    await HandleRelationshipCreatedAsync(eventGridEvent, cancellationToken);
                    break;

                case "Microsoft.DigitalTwins.Relationship.Update":
                    await HandleRelationshipUpdatedAsync(eventGridEvent, cancellationToken);
                    break;

                case "Microsoft.DigitalTwins.Relationship.Delete":
                    await HandleRelationshipDeletedAsync(eventGridEvent, cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown event type: {EventType}", eventGridEvent.EventType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error handling ADT event {EventType} for subject {Subject}",
                eventGridEvent.EventType,
                eventGridEvent.Subject);
            throw;
        }
    }

    public async Task HandleEventsAsync(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default)
    {
        var tasks = events.Select(evt => HandleEventAsync(evt, cancellationToken));
        await Task.WhenAll(tasks);
    }

    private async Task HandleTwinCreatedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        var twinId = ExtractTwinId(eventGridEvent.Subject);
        if (string.IsNullOrWhiteSpace(twinId))
        {
            _logger.LogWarning("Could not extract twin ID from subject: {Subject}", eventGridEvent.Subject);
            return;
        }

        _logger.LogInformation("Handling twin created event for twin: {TwinId}", twinId);

        // Sync twin back to Honua
        var result = await _syncService.SyncTwinToFeatureAsync(twinId, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Successfully synced twin {TwinId} to Honua", twinId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to sync twin {TwinId} to Honua: {ErrorMessage}",
                twinId,
                result.ErrorMessage);
        }
    }

    private async Task HandleTwinUpdatedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        var twinId = ExtractTwinId(eventGridEvent.Subject);
        if (string.IsNullOrWhiteSpace(twinId))
        {
            _logger.LogWarning("Could not extract twin ID from subject: {Subject}", eventGridEvent.Subject);
            return;
        }

        // Parse the event data to check if this update originated from Honua
        var data = eventGridEvent.Data?.ToString();
        if (!string.IsNullOrWhiteSpace(data))
        {
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                // Check if this update has Honua metadata to avoid circular sync
                if (root.TryGetProperty("honuaServiceId", out _))
                {
                    _logger.LogDebug(
                        "Skipping twin update event for {TwinId} - originated from Honua",
                        twinId);
                    return;
                }
            }
            catch (JsonException)
            {
                // Ignore parsing errors and proceed with sync
            }
        }

        _logger.LogInformation("Handling twin updated event for twin: {TwinId}", twinId);

        // Sync twin back to Honua
        var result = await _syncService.SyncTwinToFeatureAsync(twinId, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation("Successfully synced twin {TwinId} to Honua", twinId);
        }
        else
        {
            _logger.LogWarning(
                "Failed to sync twin {TwinId} to Honua: {ErrorMessage}",
                twinId,
                result.ErrorMessage);
        }
    }

    private Task HandleTwinDeletedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        var twinId = ExtractTwinId(eventGridEvent.Subject);
        if (string.IsNullOrWhiteSpace(twinId))
        {
            _logger.LogWarning("Could not extract twin ID from subject: {Subject}", eventGridEvent.Subject);
            return Task.CompletedTask;
        }

        _logger.LogInformation("Handling twin deleted event for twin: {TwinId}", twinId);

        // TODO: Delete corresponding Honua feature if configured
        // This requires integration with Honua's feature deletion API

        return Task.CompletedTask;
    }

    private Task HandleRelationshipCreatedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling relationship created event: {Subject}", eventGridEvent.Subject);
        // TODO: Sync relationship to Honua if applicable
        return Task.CompletedTask;
    }

    private Task HandleRelationshipUpdatedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling relationship updated event: {Subject}", eventGridEvent.Subject);
        // TODO: Sync relationship to Honua if applicable
        return Task.CompletedTask;
    }

    private Task HandleRelationshipDeletedAsync(EventGridEvent eventGridEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling relationship deleted event: {Subject}", eventGridEvent.Subject);
        // TODO: Delete relationship in Honua if applicable
        return Task.CompletedTask;
    }

    private static string? ExtractTwinId(string subject)
    {
        // Subject format: /digitaltwins/{twinId} or similar
        var parts = subject.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : null;
    }
}
