// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Admin.Hubs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Background service that listens for metadata changes and invalidates the OGC collections cache.
/// </summary>
/// <remarks>
/// <para>
/// This service ensures cache consistency by invalidating cached collections list responses
/// when metadata changes occur. It subscribes to metadata change notifications and performs
/// selective or full cache invalidation based on the type of change.
/// </para>
/// <para>
/// <strong>Invalidation Strategy:</strong>
/// <list type="bullet">
/// <item>Service metadata changes: Invalidate all entries for that service</item>
/// <item>Layer metadata changes: Invalidate all entries for the parent service</item>
/// <item>Catalog-level changes: Invalidate all cached entries</item>
/// <item>Folder changes: No invalidation needed (doesn't affect collections)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class OgcCollectionsCacheInvalidationService : IHostedService, IDisposable
{
    private readonly IMetadataProvider metadataProvider;
    private readonly IOgcCollectionsCache collectionsCache;
    private readonly ILogger<OgcCollectionsCacheInvalidationService> logger;
    private IMetadataChangeNotifier? _changeNotifier;

    public OgcCollectionsCacheInvalidationService(
        IMetadataProvider metadataProvider,
        IOgcCollectionsCache collectionsCache,
        ILogger<OgcCollectionsCacheInvalidationService> logger)
    {
        this.metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        this.collectionsCache = collectionsCache ?? throw new ArgumentNullException(nameof(collectionsCache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Starting OGC Collections Cache Invalidation Service");

        // Check if the provider supports change notifications
        if (_metadataProvider is IMutableMetadataProvider mutable &&
            mutable is IMetadataChangeNotifier notifier &&
            notifier.SupportsChangeNotifications)
        {
            this.changeNotifier = notifier;
            this.changeNotifier.MetadataChanged += OnMetadataChanged;
            this.logger.LogInformation("Subscribed to metadata change notifications for OGC collections cache invalidation");
        }
        else
        {
            this.logger.LogInformation(
                "Metadata provider does not support real-time change notifications. " +
                "OGC collections cache will rely on TTL-based expiration.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Stopping OGC Collections Cache Invalidation Service");

        if (_changeNotifier != null)
        {
            this.changeNotifier.MetadataChanged -= OnMetadataChanged;
            this.logger.LogInformation("Unsubscribed from metadata change notifications");
        }

        return Task.CompletedTask;
    }

    private void OnMetadataChanged(object? sender, Honua.Server.Host.Admin.Hubs.MetadataChangedEventArgs e)
    {
        try
        {
            this.logger.LogDebug(
                "Metadata change detected: {ChangeType} {EntityType} {EntityId}",
                e.ChangeType,
                e.EntityType,
                e.EntityId);

            // Determine invalidation scope based on entity type
            switch (e.EntityType.ToLowerInvariant())
            {
                case "service":
                    // Service metadata changed - invalidate all collections for this service
                    this.collectionsCache.InvalidateService(e.EntityId);
                    this.logger.LogInformation(
                        "Invalidated OGC collections cache for service {ServiceId} due to {ChangeType}",
                        e.EntityId,
                        e.ChangeType);
                    break;

                case "layer":
                    // Layer metadata changed - need to invalidate parent service
                    // EntityId format is typically "serviceId:layerId" or just "layerId"
                    var serviceId = ExtractServiceIdFromLayerId(e.EntityId);
                    if (!string.IsNullOrWhiteSpace(serviceId))
                    {
                        this.collectionsCache.InvalidateService(serviceId);
                        this.logger.LogInformation(
                            "Invalidated OGC collections cache for service {ServiceId} due to layer {ChangeType}",
                            serviceId,
                            e.ChangeType);
                    }
                    else
                    {
                        // If we can't determine service, invalidate all to be safe
                        this.collectionsCache.InvalidateAll();
                        this.logger.LogWarning(
                            "Could not determine service for layer {LayerId}, invalidating all OGC collections cache entries",
                            e.EntityId);
                    }
                    break;

                case "catalog":
                    // Catalog-level change - invalidate everything
                    this.collectionsCache.InvalidateAll();
                    this.logger.LogInformation(
                        "Invalidated all OGC collections cache entries due to catalog {ChangeType}",
                        e.ChangeType);
                    break;

                case "folder":
                    // Folder changes don't affect collections list
                    this.logger.LogDebug(
                        "Folder {ChangeType} does not affect collections cache, no invalidation needed",
                        e.ChangeType);
                    break;

                case "layergroup":
                    // Layer group metadata changed - invalidate parent service
                    var groupServiceId = ExtractServiceIdFromLayerId(e.EntityId);
                    if (!string.IsNullOrWhiteSpace(groupServiceId))
                    {
                        this.collectionsCache.InvalidateService(groupServiceId);
                        this.logger.LogInformation(
                            "Invalidated OGC collections cache for service {ServiceId} due to layer group {ChangeType}",
                            groupServiceId,
                            e.ChangeType);
                    }
                    else
                    {
                        this.collectionsCache.InvalidateAll();
                        this.logger.LogWarning(
                            "Could not determine service for layer group {GroupId}, invalidating all OGC collections cache entries",
                            e.EntityId);
                    }
                    break;

                default:
                    // Unknown entity type - log and skip
                    this.logger.LogDebug(
                        "Unknown entity type {EntityType}, no cache invalidation performed",
                        e.EntityType);
                    break;
            }
        }
        catch (Exception ex)
        {
            this.logger.LogError(
                ex,
                "Error invalidating OGC collections cache for metadata change: {ChangeType} {EntityType} {EntityId}",
                e.ChangeType,
                e.EntityType,
                e.EntityId);
        }
    }

    /// <summary>
    /// Extracts the service ID from a composite entity ID.
    /// </summary>
    /// <param name="entityId">The entity ID, which may be in format "serviceId:entityId".</param>
    /// <returns>The service ID if found; otherwise, null.</returns>
    private static string? ExtractServiceIdFromLayerId(string entityId)
    {
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return null;
        }

        // Handle format "serviceId:layerId" or "serviceId::layerId"
        var separatorIndex = entityId.IndexOf(':');
        if (separatorIndex > 0)
        {
            return entityId.Substring(0, separatorIndex);
        }

        // If no separator, we can't determine the service
        return null;
    }

    public void Dispose()
    {
        if (_changeNotifier != null)
        {
            this.changeNotifier.MetadataChanged -= OnMetadataChanged;
        }
    }
}
