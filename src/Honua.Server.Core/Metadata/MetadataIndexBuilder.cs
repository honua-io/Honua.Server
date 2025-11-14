// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Builds lookup indexes for metadata entities.
/// </summary>
internal static class MetadataIndexBuilder
{
    /// <summary>
    /// Builds all metadata indexes.
    /// </summary>
    /// <param name="services">The service definitions.</param>
    /// <param name="layers">The layer definitions.</param>
    /// <param name="styles">The style definitions.</param>
    /// <param name="layerGroups">The layer group definitions.</param>
    /// <returns>A new <see cref="MetadataIndexes"/> instance containing all indexes.</returns>
    public static MetadataIndexes Build(
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<StyleDefinition> styles,
        IReadOnlyList<LayerGroupDefinition> layerGroups)
    {
        var serviceIndex = BuildServiceIndex(services, layers);
        var styleIndex = BuildStyleIndex(styles);
        var layerGroupIndex = BuildLayerGroupIndex(layerGroups);

        return new MetadataIndexes(serviceIndex, styleIndex, layerGroupIndex);
    }

    /// <summary>
    /// Builds the service index mapping service IDs to service definitions with attached layers.
    /// </summary>
    /// <param name="services">The service definitions.</param>
    /// <param name="layers">The layer definitions.</param>
    /// <returns>A dictionary mapping service IDs to service definitions.</returns>
    private static IReadOnlyDictionary<string, ServiceDefinition> BuildServiceIndex(
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers)
    {
        var serviceMap = new Dictionary<string, ServiceDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            if (service is null)
            {
                continue;
            }

            var attachedLayers = layers
                .Where(l => string.Equals(l.ServiceId, service.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var serviceWithLayers = service with
            {
                Layers = new ReadOnlyCollection<LayerDefinition>(attachedLayers)
            };

            serviceMap[serviceWithLayers.Id] = serviceWithLayers;
        }

        return serviceMap;
    }

    /// <summary>
    /// Builds the style index mapping style IDs to style definitions.
    /// </summary>
    /// <param name="styles">The style definitions.</param>
    /// <returns>A dictionary mapping style IDs to style definitions.</returns>
    private static IReadOnlyDictionary<string, StyleDefinition> BuildStyleIndex(
        IReadOnlyList<StyleDefinition> styles)
    {
        var styleMap = new Dictionary<string, StyleDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var style in styles)
        {
            if (style is null)
            {
                continue;
            }

            styleMap[style.Id] = style;
        }

        return styleMap;
    }

    /// <summary>
    /// Builds the layer group index mapping layer group IDs to layer group definitions.
    /// </summary>
    /// <param name="layerGroups">The layer group definitions.</param>
    /// <returns>A dictionary mapping layer group IDs to layer group definitions.</returns>
    private static IReadOnlyDictionary<string, LayerGroupDefinition> BuildLayerGroupIndex(
        IReadOnlyList<LayerGroupDefinition> layerGroups)
    {
        var layerGroupMap = new Dictionary<string, LayerGroupDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var layerGroup in layerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            layerGroupMap[layerGroup.Id] = layerGroup;
        }

        return layerGroupMap;
    }
}
