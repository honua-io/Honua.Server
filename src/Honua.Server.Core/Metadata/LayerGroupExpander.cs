// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Expands layer groups into their component layers, handling nested groups recursively.
/// </summary>
public static class LayerGroupExpander
{
    /// <summary>
    /// Expands a layer group into an ordered list of layers with their styling and opacity.
    /// </summary>
    /// <param name="layerGroup">The layer group to expand.</param>
    /// <param name="snapshot">The metadata snapshot containing all layers and groups.</param>
    /// <returns>An ordered list of expanded layer members.</returns>
    public static IReadOnlyList<ExpandedLayerMember> ExpandLayerGroup(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot)
    {
        if (layerGroup is null)
        {
            throw new ArgumentNullException(nameof(layerGroup));
        }

        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var expandedMembers = new List<ExpandedLayerMember>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ExpandGroup(layerGroup, snapshot, expandedMembers, visited, opacity: 1.0);

        return expandedMembers;
    }

    /// <summary>
    /// Recursively expands a layer group, handling nested groups.
    /// </summary>
    private static void ExpandGroup(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot,
        List<ExpandedLayerMember> expandedMembers,
        HashSet<string> visited,
        double opacity)
    {
        // Prevent circular references (should already be validated, but check anyway)
        if (!visited.Add(layerGroup.Id))
        {
            throw new InvalidOperationException($"Circular reference detected in layer group '{layerGroup.Id}'.");
        }

        // Process members in order (lower order values render first/bottom)
        var orderedMembers = layerGroup.Members
            .Where(m => m.Enabled)
            .OrderBy(m => m.Order)
            .ToList();

        foreach (var member in orderedMembers)
        {
            // Calculate cumulative opacity
            var memberOpacity = opacity * member.Opacity;

            if (member.Type == LayerGroupMemberType.Layer)
            {
                // Get the layer definition
                if (!snapshot.TryGetLayer(layerGroup.ServiceId, member.LayerId!, out var layer))
                {
                    // Layer not found - skip it (validation should have caught this)
                    continue;
                }

                expandedMembers.Add(new ExpandedLayerMember
                {
                    Layer = layer,
                    Opacity = memberOpacity,
                    StyleId = member.StyleId ?? layer.DefaultStyleId,
                    Order = member.Order
                });
            }
            else if (member.Type == LayerGroupMemberType.Group)
            {
                // Get the nested group definition
                if (!snapshot.TryGetLayerGroup(layerGroup.ServiceId, member.GroupId!, out var nestedGroup))
                {
                    // Group not found - skip it (validation should have caught this)
                    continue;
                }

                // Recursively expand the nested group
                // Create a new visited set for the nested expansion to allow the same group
                // to appear in different branches of the group tree
                var nestedVisited = new HashSet<string>(visited, StringComparer.OrdinalIgnoreCase);
                ExpandGroup(nestedGroup, snapshot, expandedMembers, nestedVisited, memberOpacity);
            }
        }

        // Remove from visited set when backtracking
        visited.Remove(layerGroup.Id);
    }

    /// <summary>
    /// Gets all unique layers referenced by a layer group (including nested groups).
    /// </summary>
    /// <param name="layerGroup">The layer group to analyze.</param>
    /// <param name="snapshot">The metadata snapshot.</param>
    /// <returns>A list of unique layer IDs referenced by the group.</returns>
    public static IReadOnlyList<string> GetReferencedLayerIds(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot)
    {
        var expanded = ExpandLayerGroup(layerGroup, snapshot);
        return expanded.Select(m => m.Layer.Id).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Calculates the combined bounding box for all layers in a group.
    /// </summary>
    /// <param name="layerGroup">The layer group.</param>
    /// <param name="snapshot">The metadata snapshot.</param>
    /// <returns>The combined bounding box, or null if no layers have extents.</returns>
    public static LayerExtentDefinition? CalculateGroupExtent(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot)
    {
        // If the group explicitly defines an extent, use it
        if (layerGroup.Extent != null)
        {
            return layerGroup.Extent;
        }

        // Otherwise, calculate from component layers
        var expanded = ExpandLayerGroup(layerGroup, snapshot);
        var layersWithExtents = expanded
            .Where(m => m.Layer.Extent?.Bbox != null && m.Layer.Extent.Bbox.Count > 0)
            .ToList();

        if (layersWithExtents.Count == 0)
        {
            return null;
        }

        // Combine all bounding boxes
        double? minX = null, minY = null, maxX = null, maxY = null;

        foreach (var member in layersWithExtents)
        {
            foreach (var bbox in member.Layer.Extent!.Bbox)
            {
                if (bbox.Length >= 4)
                {
                    minX = minX.HasValue ? Math.Min(minX.Value, bbox[0]) : bbox[0];
                    minY = minY.HasValue ? Math.Min(minY.Value, bbox[1]) : bbox[1];
                    maxX = maxX.HasValue ? Math.Max(maxX.Value, bbox[2]) : bbox[2];
                    maxY = maxY.HasValue ? Math.Max(maxY.Value, bbox[3]) : bbox[3];
                }
            }
        }

        if (!minX.HasValue || !minY.HasValue || !maxX.HasValue || !maxY.HasValue)
        {
            return null;
        }

        return new LayerExtentDefinition
        {
            Bbox = new[] { new[] { minX.Value, minY.Value, maxX.Value, maxY.Value } },
            Crs = layersWithExtents.FirstOrDefault()?.Layer.Extent?.Crs
        };
    }

    /// <summary>
    /// Gets all coordinate reference systems supported by layers in the group.
    /// </summary>
    /// <param name="layerGroup">The layer group.</param>
    /// <param name="snapshot">The metadata snapshot.</param>
    /// <returns>A list of unique CRS identifiers.</returns>
    public static IReadOnlyList<string> GetSupportedCrs(
        LayerGroupDefinition layerGroup,
        MetadataSnapshot snapshot)
    {
        // If the group explicitly defines CRS, use them
        if (layerGroup.Crs != null && layerGroup.Crs.Count > 0)
        {
            return layerGroup.Crs;
        }

        // Otherwise, get common CRS from all layers
        var expanded = ExpandLayerGroup(layerGroup, snapshot);

        if (expanded.Count == 0)
        {
            return Array.Empty<string>();
        }

        // Start with CRS from the first layer
        var commonCrs = expanded[0].Layer.Crs.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Intersect with CRS from all other layers to find common ones
        foreach (var member in expanded.Skip(1))
        {
            var layerCrs = new HashSet<string>(member.Layer.Crs, StringComparer.OrdinalIgnoreCase);
            commonCrs.IntersectWith(layerCrs);
        }

        return commonCrs.ToList();
    }
}

/// <summary>
/// Represents a layer that has been expanded from a layer group,
/// including its effective opacity and style.
/// </summary>
public sealed record ExpandedLayerMember
{
    /// <summary>
    /// The layer definition.
    /// </summary>
    public required LayerDefinition Layer { get; init; }

    /// <summary>
    /// The cumulative opacity (0.0 - 1.0) from all parent groups.
    /// </summary>
    public double Opacity { get; init; } = 1.0;

    /// <summary>
    /// The style ID to use for rendering (may be overridden from the layer's default).
    /// </summary>
    public string? StyleId { get; init; }

    /// <summary>
    /// The order value from the group member.
    /// </summary>
    public int Order { get; init; }
}
