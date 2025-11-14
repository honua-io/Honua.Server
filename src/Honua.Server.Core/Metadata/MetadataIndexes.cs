// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Honua.Server.Core.Exceptions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Encapsulates metadata lookup indexes for fast access to services, styles, and layer groups.
/// </summary>
internal sealed class MetadataIndexes
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataIndexes"/> class.
    /// </summary>
    /// <param name="serviceIndex">Dictionary mapping service IDs to service definitions.</param>
    /// <param name="styleIndex">Dictionary mapping style IDs to style definitions.</param>
    /// <param name="layerGroupIndex">Dictionary mapping layer group IDs to layer group definitions.</param>
    public MetadataIndexes(
        IReadOnlyDictionary<string, ServiceDefinition> serviceIndex,
        IReadOnlyDictionary<string, StyleDefinition> styleIndex,
        IReadOnlyDictionary<string, LayerGroupDefinition> layerGroupIndex)
    {
        ServiceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));
        StyleIndex = styleIndex ?? throw new ArgumentNullException(nameof(styleIndex));
        LayerGroupIndex = layerGroupIndex ?? throw new ArgumentNullException(nameof(layerGroupIndex));
    }

    /// <summary>
    /// Gets the service index mapping service IDs to service definitions.
    /// </summary>
    public IReadOnlyDictionary<string, ServiceDefinition> ServiceIndex { get; }

    /// <summary>
    /// Gets the style index mapping style IDs to style definitions.
    /// </summary>
    public IReadOnlyDictionary<string, StyleDefinition> StyleIndex { get; }

    /// <summary>
    /// Gets the layer group index mapping layer group IDs to layer group definitions.
    /// </summary>
    public IReadOnlyDictionary<string, LayerGroupDefinition> LayerGroupIndex { get; }

    /// <summary>
    /// Gets a service by ID.
    /// </summary>
    /// <param name="id">The service ID.</param>
    /// <returns>The service definition.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is null.</exception>
    /// <exception cref="ServiceNotFoundException">Thrown when the service is not found.</exception>
    public ServiceDefinition GetService(string id)
    {
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (!ServiceIndex.TryGetValue(id, out var service))
        {
            throw new ServiceNotFoundException(id);
        }

        return service;
    }

    /// <summary>
    /// Tries to get a service by ID.
    /// </summary>
    /// <param name="id">The service ID.</param>
    /// <param name="service">The service definition, if found.</param>
    /// <returns><c>true</c> if the service was found; otherwise, <c>false</c>.</returns>
    public bool TryGetService(string id, out ServiceDefinition service)
    {
        if (id is null)
        {
            service = null!;
            return false;
        }

        return ServiceIndex.TryGetValue(id, out service!);
    }

    /// <summary>
    /// Tries to get a layer by service ID and layer ID.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="layerId">The layer ID.</param>
    /// <param name="layer">The layer definition, if found.</param>
    /// <returns><c>true</c> if the layer was found; otherwise, <c>false</c>.</returns>
    public bool TryGetLayer(string serviceId, string layerId, out LayerDefinition layer)
    {
        layer = null!;
        if (!TryGetService(serviceId, out var service))
        {
            return false;
        }

        var match = service.Layers.FirstOrDefault(l =>
            string.Equals(l.Id, layerId, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        layer = match;
        return true;
    }

    /// <summary>
    /// Gets a style by ID.
    /// </summary>
    /// <param name="styleId">The style ID.</param>
    /// <returns>The style definition.</returns>
    /// <exception cref="StyleNotFoundException">Thrown when the style is not found.</exception>
    public StyleDefinition GetStyle(string styleId)
    {
        if (TryGetStyle(styleId, out var style))
        {
            return style;
        }

        throw new StyleNotFoundException(styleId);
    }

    /// <summary>
    /// Tries to get a style by ID.
    /// </summary>
    /// <param name="styleId">The style ID.</param>
    /// <param name="style">The style definition, if found.</param>
    /// <returns><c>true</c> if the style was found; otherwise, <c>false</c>.</returns>
    public bool TryGetStyle(string styleId, out StyleDefinition style)
    {
        return StyleIndex.TryGetValue(styleId, out style!);
    }

    /// <summary>
    /// Gets a layer group by service ID and layer group ID.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="layerGroupId">The layer group ID.</param>
    /// <returns>The layer group definition.</returns>
    /// <exception cref="InvalidDataException">Thrown when the layer group is not found.</exception>
    public LayerGroupDefinition GetLayerGroup(string serviceId, string layerGroupId)
    {
        if (TryGetLayerGroup(serviceId, layerGroupId, out var layerGroup))
        {
            return layerGroup;
        }

        throw new InvalidDataException($"Layer group '{layerGroupId}' not found in service '{serviceId}'.");
    }

    /// <summary>
    /// Tries to get a layer group by service ID and layer group ID.
    /// </summary>
    /// <param name="serviceId">The service ID.</param>
    /// <param name="layerGroupId">The layer group ID.</param>
    /// <param name="layerGroup">The layer group definition, if found.</param>
    /// <returns><c>true</c> if the layer group was found; otherwise, <c>false</c>.</returns>
    public bool TryGetLayerGroup(string serviceId, string layerGroupId, out LayerGroupDefinition layerGroup)
    {
        layerGroup = null!;
        if (!LayerGroupIndex.TryGetValue(layerGroupId, out var group))
        {
            return false;
        }

        // Verify the layer group belongs to the specified service
        if (!string.Equals(group.ServiceId, serviceId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        layerGroup = group;
        return true;
    }
}
