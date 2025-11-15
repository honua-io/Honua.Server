// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Honua.Server.Enterprise.Events.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.Events.Queue.Services;

/// <summary>
/// Azure Service Bus publisher for geofence events (optional enterprise feature)
/// </summary>
/// <remarks>
/// This service provides enterprise-grade event delivery with:
/// - Guaranteed message ordering (FIFO) per geofence
/// - Built-in dead letter queue
/// - Automatic duplicate detection
/// - High availability and disaster recovery
/// </remarks>
public class AzureServiceBusEventPublisher : IDisposable
{
    private readonly ServiceBusSender? _sender;
    private readonly ServiceBusClient? _client;
    private readonly ILogger<AzureServiceBusEventPublisher> _logger;
    private readonly GeoEventQueueOptions _options;
    private readonly bool _isEnabled;

    public AzureServiceBusEventPublisher(
        IOptions<GeoEventQueueOptions> options,
        ILogger<AzureServiceBusEventPublisher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _isEnabled = _options.EnableServiceBus &&
                     !string.IsNullOrEmpty(_options.ServiceBusConnectionString) &&
                     !string.IsNullOrEmpty(_options.ServiceBusTopicName);

        if (_isEnabled)
        {
            try
            {
                _client = new ServiceBusClient(_options.ServiceBusConnectionString);
                _sender = _client.CreateSender(_options.ServiceBusTopicName);

                _logger.LogInformation(
                    "Azure Service Bus publisher initialized for topic: {TopicName}",
                    _options.ServiceBusTopicName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure Service Bus client");
                _isEnabled = false;
            }
        }
        else
        {
            _logger.LogInformation("Azure Service Bus integration is disabled");
        }
    }

    /// <summary>
    /// Publish a geofence event to Azure Service Bus
    /// </summary>
    public async Task PublishEventAsync(
        GeofenceEvent geofenceEvent,
        Geofence geofence,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _sender == null)
        {
            _logger.LogDebug("Service Bus is disabled, skipping event {EventId}", geofenceEvent.Id);
            return;
        }

        try
        {
            // Create message payload
            var payload = new
            {
                eventId = geofenceEvent.Id,
                eventType = geofenceEvent.EventType.ToString(),
                eventTime = geofenceEvent.EventTime,
                entityId = geofenceEvent.EntityId,
                entityType = geofenceEvent.EntityType,
                geofenceId = geofence.Id,
                geofenceName = geofence.Name,
                location = new
                {
                    type = "Point",
                    coordinates = new[] { geofenceEvent.Location.X, geofenceEvent.Location.Y }
                },
                properties = geofenceEvent.Properties,
                dwellTimeSeconds = geofenceEvent.DwellTimeSeconds,
                tenantId = geofenceEvent.TenantId,
                processedAt = geofenceEvent.ProcessedAt
            };

            var messageBody = JsonSerializer.Serialize(payload);

            // Create Service Bus message
            var message = new ServiceBusMessage(messageBody)
            {
                MessageId = geofenceEvent.Id.ToString(),
                Subject = geofenceEvent.EventType.ToString(),
                ContentType = "application/json",

                // Use geofence_id as partition key for FIFO ordering per geofence
                PartitionKey = geofence.Id.ToString(),

                // Add custom properties for filtering
                ApplicationProperties =
                {
                    { "EntityId", geofenceEvent.EntityId },
                    { "GeofenceId", geofence.Id.ToString() },
                    { "EventType", geofenceEvent.EventType.ToString() },
                    { "TenantId", geofenceEvent.TenantId ?? string.Empty }
                }
            };

            // Send message
            await _sender.SendMessageAsync(message, cancellationToken);

            _logger.LogDebug(
                "Published event {EventId} to Service Bus topic {TopicName}",
                geofenceEvent.Id,
                _options.ServiceBusTopicName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event {EventId} to Service Bus",
                geofenceEvent.Id);

            // Don't throw - Service Bus is optional, main delivery is via queue
        }
    }

    /// <summary>
    /// Publish multiple events in batch
    /// </summary>
    public async Task PublishEventsAsync(
        List<(GeofenceEvent Event, Geofence Geofence)> events,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled || _sender == null)
        {
            return;
        }

        foreach (var (geofenceEvent, geofence) in events)
        {
            await PublishEventAsync(geofenceEvent, geofence, cancellationToken);
        }
    }

    public void Dispose()
    {
        _sender?.DisposeAsync().AsTask().Wait();
        _client?.DisposeAsync().AsTask().Wait();
    }
}
