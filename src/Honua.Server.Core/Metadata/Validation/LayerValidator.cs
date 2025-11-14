// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Honua.Server.Core.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates layer metadata definitions including storage, SQL views, styles, and scale ranges.
/// </summary>
internal static class LayerValidator
{
    /// <summary>
    /// Validates layer definitions and returns a set of layer IDs.
    /// </summary>
    /// <param name="layers">The layers to validate.</param>
    /// <param name="serviceIds">The set of valid service IDs.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>A set of valid layer IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when layer validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(
        IReadOnlyList<LayerDefinition> layers,
        HashSet<string> serviceIds,
        HashSet<string> styleIds,
        ILogger? logger)
    {
        var layerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            if (layer.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layers must include an id.");
            }

            if (!layerIds.Add(layer.Id))
            {
                throw new InvalidDataException($"Duplicate layer id '{layer.Id}'.");
            }

            if (layer.ServiceId.IsNullOrWhiteSpace() || !serviceIds.Contains(layer.ServiceId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown service '{layer.ServiceId}'.");
            }

            ValidateRequiredFields(layer);
            ValidateStorageOrSqlView(layer, logger);
            ValidateStyles(layer, styleIds);
            ValidateScales(layer);
        }

        return layerIds;
    }

    /// <summary>
    /// Validates that required fields are present on a layer.
    /// </summary>
    /// <param name="layer">The layer to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when required fields are missing.</exception>
    private static void ValidateRequiredFields(LayerDefinition layer)
    {
        if (layer.GeometryType.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryType.");
        }

        if (layer.IdField.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' is missing an idField.");
        }

        if (layer.GeometryField.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer '{layer.Id}' is missing a geometryField.");
        }
    }

    /// <summary>
    /// Validates that a layer has either Storage or SqlView defined, but not both.
    /// </summary>
    /// <param name="layer">The layer to validate.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <exception cref="InvalidDataException">Thrown when storage/SQL view validation fails.</exception>
    private static void ValidateStorageOrSqlView(LayerDefinition layer, ILogger? logger)
    {
        var hasStorage = layer.Storage?.Table.HasValue() == true;
        var hasSqlView = layer.SqlView?.Sql.HasValue() == true;

        if (!hasStorage && !hasSqlView)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' must have either Storage.Table or SqlView defined.");
        }

        if (hasStorage && hasSqlView)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' cannot have both Storage.Table and SqlView. Choose one or the other.");
        }

        // Validate SQL view if present
        if (hasSqlView)
        {
            SqlViewValidator.Validate(layer, logger);
        }
    }

    /// <summary>
    /// Validates style references for a layer.
    /// </summary>
    /// <param name="layer">The layer to validate.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <exception cref="InvalidDataException">Thrown when style validation fails.</exception>
    private static void ValidateStyles(LayerDefinition layer, HashSet<string> styleIds)
    {
        if (layer.DefaultStyleId.HasValue() && !styleIds.Contains(layer.DefaultStyleId))
        {
            throw new InvalidDataException($"Layer '{layer.Id}' references unknown default style '{layer.DefaultStyleId}'.");
        }

        foreach (var styleId in layer.StyleIds)
        {
            if (!styleIds.Contains(styleId))
            {
                throw new InvalidDataException($"Layer '{layer.Id}' references unknown style '{styleId}'.");
            }
        }
    }

    /// <summary>
    /// Validates scale ranges for a layer.
    /// </summary>
    /// <param name="layer">The layer to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when scale validation fails.</exception>
    private static void ValidateScales(LayerDefinition layer)
    {
        if (layer.MinScale is < 0)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' minScale cannot be negative.");
        }

        if (layer.MaxScale is < 0)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' maxScale cannot be negative.");
        }

        if (layer.MinScale is double minScale && minScale > 0 &&
            layer.MaxScale is double maxScale && maxScale > 0 && maxScale > minScale)
        {
            throw new InvalidDataException($"Layer '{layer.Id}' maxScale ({maxScale}) cannot be greater than minScale ({minScale}).");
        }
    }
}
