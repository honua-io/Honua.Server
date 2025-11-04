// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer metadata operations.
/// </summary>
public interface IGeoservicesMetadataService
{
    /// <summary>
    /// Gets the feature service summary metadata.
    /// </summary>
    ActionResult<GeoservicesRESTFeatureServiceSummary> GetServiceSummary(CatalogServiceView serviceView);

    /// <summary>
    /// Gets detailed metadata for a specific layer.
    /// </summary>
    Task<ActionResult<GeoservicesRESTLayerDetailResponse>> GetLayerDetailAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        int layerIndex,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a service by folder and service IDs.
    /// </summary>
    CatalogServiceView? ResolveService(string folderId, string serviceId);

    /// <summary>
    /// Resolves a layer from a service view by index.
    /// </summary>
    CatalogLayerView? ResolveLayer(CatalogServiceView serviceView, int layerIndex);

    /// <summary>
    /// Checks if a layer is visible at the given map scale.
    /// </summary>
    bool IsVisibleAtScale(LayerDefinition layer, double? mapScale);
}
