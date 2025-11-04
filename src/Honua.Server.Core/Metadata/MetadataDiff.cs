// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public static class MetadataDiff
{
    public static MetadataDiffResult Compute(MetadataSnapshot current, MetadataSnapshot proposed)
    {
        Guard.NotNull(current);
        Guard.NotNull(proposed);

        var currentServices = current.Services.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var proposedServices = proposed.Services.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);

        var addedServices = proposedServices.Keys
            .Where(id => !currentServices.ContainsKey(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var removedServices = currentServices.Keys
            .Where(id => !proposedServices.ContainsKey(id))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var updatedServices = proposedServices.Keys
            .Where(id => currentServices.TryGetValue(id, out var existing) && ServiceChanged(existing, proposedServices[id]))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var layerChanges = new Dictionary<string, LayerDiffResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var serviceId in proposedServices.Keys.Intersect(currentServices.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var currentLayers = currentServices[serviceId].Layers.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);
            var proposedLayers = proposedServices[serviceId].Layers.ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);

            var addedLayers = proposedLayers.Keys
                .Where(id => !currentLayers.ContainsKey(id))
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var removedLayers = currentLayers.Keys
                .Where(id => !proposedLayers.ContainsKey(id))
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var updatedLayers = proposedLayers.Keys
                .Where(id => currentLayers.TryGetValue(id, out var existing) && LayerChanged(existing, proposedLayers[id]))
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (addedLayers.Length > 0 || removedLayers.Length > 0 || updatedLayers.Length > 0)
            {
                layerChanges[serviceId] = new LayerDiffResult(addedLayers, removedLayers, updatedLayers);
            }
        }

        var catalogDiff = new CatalogDiff(current.Catalog.Id, proposed.Catalog.Id);
        var serviceDiff = new EntityDiffResult(addedServices, removedServices, updatedServices);

        return new MetadataDiffResult(catalogDiff, serviceDiff, layerChanges);
    }

    private static bool ServiceChanged(ServiceDefinition current, ServiceDefinition proposed)
    {
        if (!string.Equals(current.Title, proposed.Title, StringComparison.Ordinal)) return true;
        if (!string.Equals(current.FolderId, proposed.FolderId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(current.ServiceType, proposed.ServiceType, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(current.DataSourceId, proposed.DataSourceId, StringComparison.OrdinalIgnoreCase)) return true;
        if (current.Enabled != proposed.Enabled) return true;
        return false;
    }

    private static bool LayerChanged(LayerDefinition current, LayerDefinition proposed)
    {
        if (!string.Equals(current.Title, proposed.Title, StringComparison.Ordinal)) return true;
        if (!string.Equals(current.GeometryType, proposed.GeometryType, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(current.IdField, proposed.IdField, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(current.DisplayField, proposed.DisplayField, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.Equals(current.GeometryField, proposed.GeometryField, StringComparison.OrdinalIgnoreCase)) return true;

        static bool SequenceEquals(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        if (!SequenceEquals(current.Crs, proposed.Crs)) return true;

        return false;
    }
}

public sealed record MetadataDiffResult(
    CatalogDiff Catalog,
    EntityDiffResult Services,
    IReadOnlyDictionary<string, LayerDiffResult> Layers);

public sealed record CatalogDiff(string Current, string Proposed);

public sealed record EntityDiffResult(string[] Added, string[] Removed, string[] Updated);

public sealed record LayerDiffResult(string[] Added, string[] Removed, string[] Updated);
