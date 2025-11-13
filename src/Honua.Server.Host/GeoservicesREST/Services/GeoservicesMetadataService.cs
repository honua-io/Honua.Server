// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer metadata operations.
/// Extracted from GeoservicesRESTFeatureServerController to follow Single Responsibility Principle.
/// </summary>
public sealed class GeoservicesMetadataService : IGeoservicesMetadataService
{
    private const double GeoServicesVersion = 10.81;

    private readonly ICatalogProjectionService catalog;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly ILogger<GeoservicesMetadataService> logger;

    public GeoservicesMetadataService(
        ICatalogProjectionService catalog,
        IMetadataRegistry metadataRegistry,
        ILogger<GeoservicesMetadataService> logger)
    {
        this.catalog = Guard.NotNull(catalog);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.logger = Guard.NotNull(logger);
    }

    public ActionResult<GeoservicesRESTFeatureServiceSummary> GetServiceSummary(CatalogServiceView serviceView)
    {
        var summary = GeoservicesRESTMetadataMapper.CreateFeatureServiceSummary(serviceView, GeoServicesVersion);
        return new OkObjectResult(summary);
    }

    public async Task<ActionResult<GeoservicesRESTLayerDetailResponse>> GetLayerDetailAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        int layerIndex,
        CancellationToken cancellationToken)
    {
        var style = await ResolveDefaultStyleAsync(layerView.Layer, cancellationToken).ConfigureAwait(false);
        var detail = GeoservicesRESTMetadataMapper.CreateLayerDetailResponse(
            serviceView,
            layerView,
            layerIndex,
            GeoServicesVersion,
            style);
        return new OkObjectResult(detail);
    }

    public CatalogServiceView? ResolveService(string folderId, string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return null;
        }

        var service = this.catalog.GetService(serviceId);
        if (service is null)
        {
            return null;
        }

        if (!service.Service.FolderId.EqualsIgnoreCase(folderId))
        {
            return null;
        }

        return SupportsFeatureServer(service.Service) ? service : null;
    }

    public CatalogLayerView? ResolveLayer(CatalogServiceView serviceView, int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= serviceView.Layers.Count)
        {
            return null;
        }

        return serviceView.Layers[layerIndex];
    }

    public bool IsVisibleAtScale(LayerDefinition layer, double? mapScale)
    {
        if (!mapScale.HasValue || mapScale.Value <= 0)
        {
            return true;
        }

        var scale = mapScale.Value;

        if (layer.MinScale is double minScale && minScale > 0 && scale > minScale)
        {
            return false;
        }

        if (layer.MaxScale is double maxScale && maxScale > 0 && scale < maxScale)
        {
            return false;
        }

        return true;
    }

    private async Task<StyleDefinition?> ResolveDefaultStyleAsync(LayerDefinition layer, CancellationToken cancellationToken)
    {
        var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(layer.DefaultStyleId) &&
            snapshot.TryGetStyle(layer.DefaultStyleId, out var defaultStyle))
        {
            return defaultStyle;
        }

        foreach (var candidate in layer.StyleIds)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    private static bool SupportsFeatureServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature");
    }
}
