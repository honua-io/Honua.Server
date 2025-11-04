// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Helper class for resolving services and layers in GeoServices REST controllers.
/// Centralizes the common pattern of ResolveService + ResolveLayer + NotFound responses.
/// </summary>
internal static class GeoservicesRESTServiceResolutionHelper
{
    /// <summary>
    /// Result of service and layer resolution.
    /// Contains the resolved views or an error result if resolution failed.
    /// </summary>
    public sealed record ServiceLayerResolution(
        CatalogServiceView? ServiceView,
        CatalogLayerView? LayerView,
        IActionResult? Error);

    /// <summary>
    /// Resolves a service by folder ID and service ID.
    /// Returns null if the service is not found or doesn't match the folder.
    /// FolderId is optional - if null, empty, or "root", folder validation is skipped.
    /// </summary>
    public static CatalogServiceView? ResolveService(
        ICatalogProjectionService catalog,
        string? folderId,
        string serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return null;
        }

        var service = catalog.GetService(serviceId);
        if (service is null)
        {
            return null;
        }

        // Skip folder validation if folderId is null, empty, whitespace, or "root"
        var validateFolder = !string.IsNullOrWhiteSpace(folderId) &&
                             !string.Equals(folderId, "root", StringComparison.OrdinalIgnoreCase);

        if (validateFolder && !service.Service.FolderId.EqualsIgnoreCase(folderId))
        {
            return null;
        }

        return service;
    }

    /// <summary>
    /// Resolves a layer by index within a service.
    /// Returns null if the layer index is out of bounds.
    /// </summary>
    public static CatalogLayerView? ResolveLayer(
        CatalogServiceView serviceView,
        int layerIndex)
    {
        if (layerIndex < 0 || layerIndex >= serviceView.Layers.Count)
        {
            return null;
        }

        return serviceView.Layers[layerIndex];
    }

    /// <summary>
    /// Resolves both service and layer, returning a NotFound error if either fails.
    /// This is the most common pattern across FeatureServer, MapServer, and ImageServer controllers.
    /// </summary>
    public static ServiceLayerResolution ResolveServiceAndLayer(
        ControllerBase controller,
        ICatalogProjectionService catalog,
        string? folderId,
        string serviceId,
        int layerIndex)
    {
        var serviceView = ResolveService(catalog, folderId, serviceId);
        if (serviceView is null)
        {
            return new ServiceLayerResolution(null, null, controller.NotFound());
        }

        var layerView = ResolveLayer(serviceView, layerIndex);
        if (layerView is null)
        {
            return new ServiceLayerResolution(serviceView, null, controller.NotFound());
        }

        return new ServiceLayerResolution(serviceView, layerView, null);
    }

    /// <summary>
    /// Resolves only the service, returning a NotFound error if it fails.
    /// </summary>
    public static (CatalogServiceView? ServiceView, IActionResult? Error) ResolveServiceOnly(
        ControllerBase controller,
        ICatalogProjectionService catalog,
        string? folderId,
        string serviceId)
    {
        var serviceView = ResolveService(catalog, folderId, serviceId);
        if (serviceView is null)
        {
            return (null, controller.NotFound());
        }

        return (serviceView, null);
    }
}
