// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Cloud.EventGrid.Models;
using Honua.Server.Core.Cloud.EventGrid.Services;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.Server.Core.Cloud.EventGrid.Hooks;

/// <summary>
/// Publishes feature lifecycle events to Event Grid.
/// </summary>
public class FeatureEventPublisher : IFeatureEventPublisher
{
    private readonly IEventGridPublisher _publisher;
    private readonly ILogger<FeatureEventPublisher> _logger;

    public FeatureEventPublisher(
        IEventGridPublisher publisher,
        ILogger<FeatureEventPublisher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task PublishFeatureCreatedAsync(
        string collectionId,
        string featureId,
        Dictionary<string, object?> properties,
        Geometry? geometry,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/features/{collectionId}")
                .WithType(HonuaEventTypes.FeatureCreated)
                .WithSubject(featureId)
                .WithTenantId(tenantId)
                .WithCollection(collectionId)
                .WithBoundingBox(geometry?.EnvelopeInternal)
                .WithCrs($"EPSG:{geometry?.SRID ?? 4326}")
                .WithData(new
                {
                    collection_id = collectionId,
                    feature_id = featureId,
                    properties,
                    geometry = geometry != null ? new
                    {
                        type = geometry.GeometryType,
                        srid = geometry.SRID,
                        coordinates_count = geometry.NumPoints
                    } : null
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published feature created event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature created event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
            // Don't throw - events should not break the API
        }
    }

    public async Task PublishFeatureUpdatedAsync(
        string collectionId,
        string featureId,
        Dictionary<string, object?> properties,
        Geometry? geometry,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/features/{collectionId}")
                .WithType(HonuaEventTypes.FeatureUpdated)
                .WithSubject(featureId)
                .WithTenantId(tenantId)
                .WithCollection(collectionId)
                .WithBoundingBox(geometry?.EnvelopeInternal)
                .WithCrs($"EPSG:{geometry?.SRID ?? 4326}")
                .WithData(new
                {
                    collection_id = collectionId,
                    feature_id = featureId,
                    properties,
                    geometry = geometry != null ? new
                    {
                        type = geometry.GeometryType,
                        srid = geometry.SRID,
                        coordinates_count = geometry.NumPoints
                    } : null
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published feature updated event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature updated event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
        }
    }

    public async Task PublishFeatureDeletedAsync(
        string collectionId,
        string featureId,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudEvent = new HonuaCloudEventBuilder()
                .WithId(Guid.NewGuid().ToString())
                .WithSource($"honua.io/features/{collectionId}")
                .WithType(HonuaEventTypes.FeatureDeleted)
                .WithSubject(featureId)
                .WithTenantId(tenantId)
                .WithCollection(collectionId)
                .WithData(new
                {
                    collection_id = collectionId,
                    feature_id = featureId
                })
                .Build();

            await _publisher.PublishAsync(cloudEvent, cancellationToken);

            _logger.LogDebug("Published feature deleted event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature deleted event: Collection={Collection}, Feature={Feature}",
                collectionId, featureId);
        }
    }

    public async Task PublishFeatureBatchCreatedAsync(
        string collectionId,
        IEnumerable<(string featureId, Dictionary<string, object?> properties, Geometry? geometry)> features,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureList = features.ToList();

            // Publish individual events for each feature
            var events = featureList.Select(f =>
            {
                return new HonuaCloudEventBuilder()
                    .WithId(Guid.NewGuid().ToString())
                    .WithSource($"honua.io/features/{collectionId}")
                    .WithType(HonuaEventTypes.FeatureCreated)
                    .WithSubject(f.featureId)
                    .WithTenantId(tenantId)
                    .WithCollection(collectionId)
                    .WithBoundingBox(f.geometry?.EnvelopeInternal)
                    .WithCrs($"EPSG:{f.geometry?.SRID ?? 4326}")
                    .WithData(new
                    {
                        collection_id = collectionId,
                        feature_id = f.featureId,
                        properties = f.properties,
                        geometry = f.geometry != null ? new
                        {
                            type = f.geometry.GeometryType,
                            srid = f.geometry.SRID,
                            coordinates_count = f.geometry.NumPoints
                        } : null
                    })
                    .Build();
            }).ToList();

            await _publisher.PublishBatchAsync(events, cancellationToken);

            _logger.LogInformation("Published feature batch created events: Collection={Collection}, Count={Count}",
                collectionId, featureList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature batch created events: Collection={Collection}",
                collectionId);
        }
    }

    public async Task PublishFeatureBatchUpdatedAsync(
        string collectionId,
        IEnumerable<(string featureId, Dictionary<string, object?> properties, Geometry? geometry)> features,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureList = features.ToList();

            var events = featureList.Select(f =>
            {
                return new HonuaCloudEventBuilder()
                    .WithId(Guid.NewGuid().ToString())
                    .WithSource($"honua.io/features/{collectionId}")
                    .WithType(HonuaEventTypes.FeatureUpdated)
                    .WithSubject(f.featureId)
                    .WithTenantId(tenantId)
                    .WithCollection(collectionId)
                    .WithBoundingBox(f.geometry?.EnvelopeInternal)
                    .WithCrs($"EPSG:{f.geometry?.SRID ?? 4326}")
                    .WithData(new
                    {
                        collection_id = collectionId,
                        feature_id = f.featureId,
                        properties = f.properties,
                        geometry = f.geometry != null ? new
                        {
                            type = f.geometry.GeometryType,
                            srid = f.geometry.SRID,
                            coordinates_count = f.geometry.NumPoints
                        } : null
                    })
                    .Build();
            }).ToList();

            await _publisher.PublishBatchAsync(events, cancellationToken);

            _logger.LogInformation("Published feature batch updated events: Collection={Collection}, Count={Count}",
                collectionId, featureList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature batch updated events: Collection={Collection}",
                collectionId);
        }
    }

    public async Task PublishFeatureBatchDeletedAsync(
        string collectionId,
        IEnumerable<string> featureIds,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var featureIdList = featureIds.ToList();

            var events = featureIdList.Select(featureId =>
            {
                return new HonuaCloudEventBuilder()
                    .WithId(Guid.NewGuid().ToString())
                    .WithSource($"honua.io/features/{collectionId}")
                    .WithType(HonuaEventTypes.FeatureDeleted)
                    .WithSubject(featureId)
                    .WithTenantId(tenantId)
                    .WithCollection(collectionId)
                    .WithData(new
                    {
                        collection_id = collectionId,
                        feature_id = featureId
                    })
                    .Build();
            }).ToList();

            await _publisher.PublishBatchAsync(events, cancellationToken);

            _logger.LogInformation("Published feature batch deleted events: Collection={Collection}, Count={Count}",
                collectionId, featureIdList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing feature batch deleted events: Collection={Collection}",
                collectionId);
        }
    }
}
