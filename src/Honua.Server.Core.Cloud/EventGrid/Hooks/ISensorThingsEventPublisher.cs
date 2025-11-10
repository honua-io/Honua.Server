// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes SensorThings API events to Event Grid.
/// </summary>
public interface ISensorThingsEventPublisher
{
    /// <summary>
    /// Publish an observation created event.
    /// </summary>
    Task PublishObservationCreatedAsync(
        string datastreamId,
        string observationId,
        object result,
        DateTimeOffset phenomenonTime,
        Dictionary<string, object?>? parameters = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a batch of observation created events.
    /// </summary>
    Task PublishObservationBatchCreatedAsync(
        string datastreamId,
        IEnumerable<(string observationId, object result, DateTimeOffset phenomenonTime)> observations,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a Thing created event.
    /// </summary>
    Task PublishThingCreatedAsync(
        string thingId,
        string name,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a Thing updated event.
    /// </summary>
    Task PublishThingUpdatedAsync(
        string thingId,
        string name,
        Dictionary<string, object?>? properties = null,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a Location updated event.
    /// </summary>
    Task PublishLocationUpdatedAsync(
        string thingId,
        string locationId,
        double longitude,
        double latitude,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a Datastream updated event.
    /// </summary>
    Task PublishDatastreamUpdatedAsync(
        string datastreamId,
        string name,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
