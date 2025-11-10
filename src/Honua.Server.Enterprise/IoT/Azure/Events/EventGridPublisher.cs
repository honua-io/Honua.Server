// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Azure;
using Azure.Messaging.EventGrid;
using Honua.Server.Enterprise.IoT.Azure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Enterprise.IoT.Azure.Events;

/// <summary>
/// Publishes Honua feature change events to Azure Event Grid.
/// </summary>
public interface IEventGridPublisher
{
    /// <summary>
    /// Publishes a feature created event.
    /// </summary>
    Task PublishFeatureCreatedAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a feature updated event.
    /// </summary>
    Task PublishFeatureUpdatedAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a feature deleted event.
    /// </summary>
    Task PublishFeatureDeletedAsync(
        string serviceId,
        string layerId,
        string featureId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of Event Grid publisher for Honua events.
/// </summary>
public sealed class EventGridPublisher : IEventGridPublisher, IDisposable
{
    private readonly EventGridPublisherClient? _client;
    private readonly ILogger<EventGridPublisher> _logger;
    private readonly AzureDigitalTwinsOptions _options;

    public EventGridPublisher(
        ILogger<EventGridPublisher> logger,
        IOptions<AzureDigitalTwinsOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        if (!string.IsNullOrWhiteSpace(_options.EventGrid.TopicEndpoint) &&
            !string.IsNullOrWhiteSpace(_options.EventGrid.TopicAccessKey))
        {
            var credential = new AzureKeyCredential(_options.EventGrid.TopicAccessKey);
            _client = new EventGridPublisherClient(
                new Uri(_options.EventGrid.TopicEndpoint),
                credential);

            _logger.LogInformation(
                "Event Grid publisher initialized for topic: {TopicEndpoint}",
                _options.EventGrid.TopicEndpoint);
        }
        else
        {
            _logger.LogWarning("Event Grid publisher not configured. Events will not be published.");
        }
    }

    public async Task PublishFeatureCreatedAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default)
    {
        await PublishEventAsync(
            "Honua.Features.FeatureCreated",
            serviceId,
            layerId,
            featureId,
            attributes,
            cancellationToken);
    }

    public async Task PublishFeatureUpdatedAsync(
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?> attributes,
        CancellationToken cancellationToken = default)
    {
        await PublishEventAsync(
            "Honua.Features.FeatureUpdated",
            serviceId,
            layerId,
            featureId,
            attributes,
            cancellationToken);
    }

    public async Task PublishFeatureDeletedAsync(
        string serviceId,
        string layerId,
        string featureId,
        CancellationToken cancellationToken = default)
    {
        await PublishEventAsync(
            "Honua.Features.FeatureDeleted",
            serviceId,
            layerId,
            featureId,
            null,
            cancellationToken);
    }

    private async Task PublishEventAsync(
        string eventType,
        string serviceId,
        string layerId,
        string featureId,
        Dictionary<string, object?>? attributes,
        CancellationToken cancellationToken)
    {
        if (_client == null)
        {
            _logger.LogDebug("Event Grid client not configured, skipping event publication");
            return;
        }

        try
        {
            var eventData = new EventGridEvent(
                subject: $"honua/{serviceId}/{layerId}/{featureId}",
                eventType: eventType,
                dataVersion: "1.0",
                data: new
                {
                    serviceId,
                    layerId,
                    featureId,
                    attributes,
                    timestamp = DateTimeOffset.UtcNow
                })
            {
                Id = Guid.NewGuid().ToString(),
                EventTime = DateTimeOffset.UtcNow
            };

            await _client.SendEventAsync(eventData, cancellationToken);

            _logger.LogInformation(
                "Published event {EventType} for feature {ServiceId}/{LayerId}/{FeatureId}",
                eventType,
                serviceId,
                layerId,
                featureId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error publishing event {EventType} for feature {ServiceId}/{LayerId}/{FeatureId}",
                eventType,
                serviceId,
                layerId,
                featureId);
            throw;
        }
    }

    public void Dispose()
    {
        // EventGridPublisherClient doesn't implement IDisposable
    }
}
