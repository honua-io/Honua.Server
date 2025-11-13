// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Carto;

internal sealed class CartoDatasetResolver
{
    private readonly ICatalogProjectionService catalog;

    public CartoDatasetResolver(ICatalogProjectionService catalog)
    {
        this.catalog = Guard.NotNull(catalog);
    }

    public IReadOnlyList<CartoDatasetContext> GetDatasets()
    {
        var snapshot = this.catalog.GetSnapshot();
        var contexts = new List<CartoDatasetContext>();

        foreach (var service in snapshot.ServiceIndex.Values)
        {
            foreach (var layerView in service.Layers)
            {
                var datasetId = BuildDatasetId(service.Service.Id, layerView.Layer.Id);
                contexts.Add(new CartoDatasetContext(
                    datasetId,
                    service.Service.Id,
                    layerView.Layer.Id,
                    service.Service,
                    layerView.Layer,
                    layerView));
            }
        }

        return contexts;
    }

    public bool TryResolve(string datasetId, out CartoDatasetContext context)
    {
        context = null!;
        if (string.IsNullOrWhiteSpace(datasetId))
        {
            return false;
        }

        if (!TryParseDatasetId(datasetId, out var serviceId, out var layerId))
        {
            return false;
        }

        var serviceView = this.catalog.GetService(serviceId);
        if (serviceView is null)
        {
            return false;
        }

        var layerView = serviceView.Layers.FirstOrDefault(layer =>
            string.Equals(layer.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase));
        if (layerView is null)
        {
            return false;
        }

        context = new CartoDatasetContext(
            BuildDatasetId(serviceId, layerView.Layer.Id),
            serviceId,
            layerView.Layer.Id,
            serviceView.Service,
            layerView.Layer,
            layerView);
        return true;
    }

    internal static bool TryParseDatasetId(string datasetId, out string serviceId, out string layerId)
    {
        serviceId = string.Empty;
        layerId = string.Empty;

        if (string.IsNullOrWhiteSpace(datasetId))
        {
            return false;
        }

        var trimmed = datasetId.Trim();
        var separatorIndex = trimmed.IndexOf('.', StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex >= trimmed.Length - 1)
        {
            return false;
        }

        serviceId = trimmed.Substring(0, separatorIndex);
        layerId = trimmed.Substring(separatorIndex + 1);
        return !string.IsNullOrWhiteSpace(serviceId) && !string.IsNullOrWhiteSpace(layerId);
    }

    internal static string BuildDatasetId(string serviceId, string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        return $"{serviceId}.{layerId}";
    }
}
