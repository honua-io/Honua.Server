// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Helper for resolving style definitions with fallback logic.
/// </summary>
public static class StyleResolutionHelper
{
    /// <summary>
    /// Resolves a style definition for a raster dataset with fallback logic.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to search.</param>
    /// <param name="dataset">The raster dataset definition.</param>
    /// <param name="requestedStyleId">The requested style ID (optional).</param>
    /// <returns>The resolved style definition, or null if none found.</returns>
    public static StyleDefinition? ResolveStyleForRaster(
        MetadataSnapshot snapshot,
        RasterDatasetDefinition dataset,
        string? requestedStyleId)
    {
        Guard.NotNull(snapshot);
        Guard.NotNull(dataset);

        // 1. Try requested style
        if (requestedStyleId.HasValue() &&
            snapshot.TryGetStyle(requestedStyleId, out var requestedStyle))
        {
            return requestedStyle;
        }

        // 2. Try dataset's default style
        if (dataset.Styles.DefaultStyleId.HasValue() &&
            snapshot.TryGetStyle(dataset.Styles.DefaultStyleId, out var defaultStyle))
        {
            return defaultStyle;
        }

        // 3. Try first available style from dataset
        foreach (var candidate in dataset.Styles.StyleIds)
        {
            if (snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a style definition for a layer with fallback logic.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to search.</param>
    /// <param name="layer">The layer definition.</param>
    /// <param name="requestedStyleId">The requested style ID (optional).</param>
    /// <returns>The resolved style definition, or null if none found.</returns>
    public static StyleDefinition? ResolveStyleForLayer(
        MetadataSnapshot snapshot,
        LayerDefinition layer,
        string? requestedStyleId)
    {
        Guard.NotNull(snapshot);
        Guard.NotNull(layer);

        if (requestedStyleId.HasValue())
        {
            if (snapshot.TryGetStyle(requestedStyleId, out var direct))
            {
                return direct;
            }

            if (string.Equals(requestedStyleId, "default", StringComparison.OrdinalIgnoreCase) &&
                layer.DefaultStyleId.HasValue() &&
                snapshot.TryGetStyle(layer.DefaultStyleId, out var defaultStyle))
            {
                return defaultStyle;
            }

            return null;
        }

        if (layer.DefaultStyleId.HasValue() &&
            snapshot.TryGetStyle(layer.DefaultStyleId, out var fallback))
        {
            return fallback;
        }

        foreach (var candidate in layer.StyleIds)
        {
            if (snapshot.TryGetStyle(candidate, out var style))
            {
                return style;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the default style ID for a layer, with fallback logic.
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <returns>The default style ID, or "default" if none configured.</returns>
    public static string GetDefaultStyleId(LayerDefinition layer)
    {
        Guard.NotNull(layer);

        if (layer.DefaultStyleId.HasValue())
        {
            return layer.DefaultStyleId;
        }

        if (layer.StyleIds.Count > 0)
        {
            return layer.StyleIds[0];
        }

        return "default";
    }

    /// <summary>
    /// Tries to resolve a raster style ID with validation.
    /// </summary>
    /// <param name="dataset">The raster dataset definition.</param>
    /// <param name="requestedStyleId">The requested style ID (optional).</param>
    /// <returns>A tuple indicating success, the resolved style ID, and any error message.</returns>
    public static (bool Success, string? StyleId, string? Error) TryResolveRasterStyleId(
        RasterDatasetDefinition dataset,
        string? requestedStyleId)
    {
        Guard.NotNull(dataset);

        var available = dataset.Styles.StyleIds ?? Array.Empty<string>();

        if (requestedStyleId.HasValue())
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            if ((dataset.Styles.DefaultStyleId.HasValue() &&
                 comparer.Equals(dataset.Styles.DefaultStyleId, requestedStyleId))
                || available.Any(id => comparer.Equals(id, requestedStyleId)))
            {
                return (true, requestedStyleId, null);
            }

            return (false, null, $"Style '{requestedStyleId}' is not available for dataset '{dataset.Id}'.");
        }

        if (dataset.Styles.DefaultStyleId.HasValue())
        {
            return (true, dataset.Styles.DefaultStyleId, null);
        }

        if (available.Count > 0)
        {
            return (true, available[0], null);
        }

        return (true, dataset.Id, null);
    }
}
