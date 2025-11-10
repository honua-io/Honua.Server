// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes feature lifecycle events to Event Grid.
/// Designed to be called from OGC Features API handlers.
/// </summary>
public interface IFeatureEventPublisher
{
    /// <summary>
    /// Publish a feature created event.
    /// </summary>
    Task PublishFeatureCreatedAsync(
        string collectionId,
        string featureId,
        Dictionary<string, object?> properties,
        Geometry? geometry,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a feature updated event.
    /// </summary>
    Task PublishFeatureUpdatedAsync(
        string collectionId,
        string featureId,
        Dictionary<string, object?> properties,
        Geometry? geometry,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a feature deleted event.
    /// </summary>
    Task PublishFeatureDeletedAsync(
        string collectionId,
        string featureId,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a batch of feature created events.
    /// </summary>
    Task PublishFeatureBatchCreatedAsync(
        string collectionId,
        IEnumerable<(string featureId, Dictionary<string, object?> properties, Geometry? geometry)> features,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a batch of feature updated events.
    /// </summary>
    Task PublishFeatureBatchUpdatedAsync(
        string collectionId,
        IEnumerable<(string featureId, Dictionary<string, object?> properties, Geometry? geometry)> features,
        string? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a batch of feature deleted events.
    /// </summary>
    Task PublishFeatureBatchDeletedAsync(
        string collectionId,
        IEnumerable<string> featureIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}
