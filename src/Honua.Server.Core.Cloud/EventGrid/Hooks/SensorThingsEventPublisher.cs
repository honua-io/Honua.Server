// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Models;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes SensorThings API events to Event Grid.
/// </summary>
public class SensorThingsEventPublisher : ISensorThingsEventPublisher
{
    private readonly IEventGridPublisher _publisher;
    private readonly ILogger<SensorThingsEventPublisher> _logger;

    public SensorThingsEventPublisher(
        IEventGridPublisher publisher,
        ILogger<SensorThingsEventPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishObservationCreatedAsync(
        string datastreamId,
        string observationId,
        object result,
        DateTimeOffset phenomenonTime,
        Dictionary<string, object?>? parameters = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/sensorthings/datastreams/{datastreamId}")
                .WithType(HonuaEventTypes.ObservationCreated)
                .WithSubject(observationId)
                .WithTenantId(tenantId)
                .WithCollection(datastreamId)
                .WithTime(phenomenonTime)
                .WithData(new
                {
                    datastream_id = datastreamId,
                    observation_id = observationId,
                    result,
                    phenomenon_time = phenomenonTime,
                    parameters
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published observation created event: Datastream={Datastream}, Observation={Observation}",
                datastreamId, observationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing observation created event: Datastream={Datastream}, Observation={Observation}",
                datastreamId, observationId);
        }
    }

    public async Task PublishObservationBatchCreatedAsync(
        string datastreamId,
        IEnumerable<(string observationId, object result, DateTimeOffset phenomenonTime)> observations,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var observationList = observations.ToList();

            var events = observationList.Select(obs =>
            {
                return new HonuaCloudEventBuilder()
                    .WithId(Guid.NewGuid().ToString())
                    .WithSource($"honua.io/sensorthings/datastreams/{datastreamId}")
                    .WithType(HonuaEventTypes.ObservationCreated)
                    .WithSubject(obs.observationId)
                    .WithTenantId(tenantId)
                    .WithCollection(datastreamId)
                    .WithTime(obs.phenomenonTime)
                    .WithData(new
                    {
                        datastream_id = datastreamId,
                        observation_id = obs.observationId,
                        result = obs.result,
                        phenomenon_time = obs.phenomenonTime
                    })
                    .Build();
            }).ToList();

            await _publisher.PublishBatchAsync(events, cancellationToken);

            _logger.LogInformation("Published observation batch created events: Datastream={Datastream}, Count={Count}",
                datastreamId, observationList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing observation batch created events: Datastream={Datastream}",
                datastreamId);
        }
    }

    public async Task PublishThingCreatedAsync(
        string thingId,
        string name,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource("honua.io/sensorthings/things")
                .WithType(HonuaEventTypes.ThingCreated)
                .WithSubject(thingId)
                .WithTenantId(tenantId)
                .WithData(new
                {
                    thing_id = thingId,
                    name,
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published Thing created event: Thing={Thing}", thingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing Thing created event: Thing={Thing}", thingId);
        }
    }

    public async Task PublishThingUpdatedAsync(
        string thingId,
        string name,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource("honua.io/sensorthings/things")
                .WithType(HonuaEventTypes.ThingUpdated)
                .WithSubject(thingId)
                .WithTenantId(tenantId)
                .WithData(new
                {
                    thing_id = thingId,
                    name,
                    properties
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published Thing updated event: Thing={Thing}", thingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing Thing updated event: Thing={Thing}", thingId);
        }
    }

    public async Task PublishLocationUpdatedAsync(
        string thingId,
        string locationId,
        double longitude,
        double latitude,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/sensorthings/things/{thingId}/locations")
                .WithType(HonuaEventTypes.LocationUpdated)
                .WithSubject(locationId)
                .WithTenantId(tenantId)
                .WithBoundingBox(new[] { longitude, latitude, longitude, latitude })
                .WithData(new
                {
                    thing_id = thingId,
                    location_id = locationId,
                    longitude,
                    latitude
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published Location updated event: Thing={Thing}, Location={Location}",
                thingId, locationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing Location updated event: Thing={Thing}, Location={Location}",
                thingId, locationId);
        }
    }

    public async Task PublishDatastreamUpdatedAsync(
        string datastreamId,
        string name,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource("honua.io/sensorthings/datastreams")
                .WithType(HonuaEventTypes.DatastreamUpdated)
                .WithSubject(datastreamId)
                .WithTenantId(tenantId)
                .WithCollection(datastreamId)
                .WithData(new
                {
                    datastream_id = datastreamId,
                    name
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published Datastream updated event: Datastream={Datastream}", datastreamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing Datastream updated event: Datastream={Datastream}", datastreamId);
        }
    }
}
