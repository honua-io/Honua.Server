// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates layer group metadata definitions including member validation and circular reference detection.
/// </summary>
internal static class LayerGroupValidator
{
    /// <summary>
    /// Validates layer group definitions and returns a set of layer group IDs.
    /// </summary>
    /// <param name="layerGroups">The layer groups to validate.</param>
    /// <param name="serviceIds">The set of valid service IDs.</param>
    /// <param name="layerIds">The set of valid layer IDs.</param>
    /// <param name="layers">The complete list of layers for cross-service validation.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <returns>A set of valid layer group IDs.</returns>
    /// <exception cref="InvalidDataException">Thrown when layer group validation fails.</exception>
    public static HashSet<string> ValidateAndGetIds(
        IReadOnlyList<LayerGroupDefinition> layerGroups,
        HashSet<string> serviceIds,
        HashSet<string> layerIds,
        IReadOnlyList<LayerDefinition> layers,
        HashSet<string> styleIds)
    {
        var layerGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: validate basic structure and collect IDs
        foreach (var layerGroup in layerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            if (layerGroup.Id.IsNullOrWhiteSpace())
            {
                throw new InvalidDataException("Layer groups must include an id.");
            }

            if (!layerGroupIds.Add(layerGroup.Id))
            {
                throw new InvalidDataException($"Duplicate layer group id '{layerGroup.Id}'.");
            }

            ValidateBasicProperties(layerGroup, serviceIds, styleIds);
            ValidateMembers(layerGroup, layers, styleIds);
        }

        // Second pass: validate group references and circular dependencies
        foreach (var layerGroup in layerGroups)
        {
            if (layerGroup is null)
            {
                continue;
            }

            ValidateGroupReferences(layerGroup, layerGroups, layerGroupIds);
            DetectCircularReferences(layerGroup, layerGroups);
        }

        return layerGroupIds;
    }

    /// <summary>
    /// Validates basic properties of a layer group.
    /// </summary>
    /// <param name="layerGroup">The layer group to validate.</param>
    /// <param name="serviceIds">The set of valid service IDs.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <exception cref="InvalidDataException">Thrown when validation fails.</exception>
    private static void ValidateBasicProperties(
        LayerGroupDefinition layerGroup,
        HashSet<string> serviceIds,
        HashSet<string> styleIds)
    {
        if (layerGroup.Title.IsNullOrWhiteSpace())
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' must have a title.");
        }

        if (layerGroup.ServiceId.IsNullOrWhiteSpace() || !serviceIds.Contains(layerGroup.ServiceId))
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown service '{layerGroup.ServiceId}'.");
        }

        if (layerGroup.DefaultStyleId.HasValue() && !styleIds.Contains(layerGroup.DefaultStyleId))
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown default style '{layerGroup.DefaultStyleId}'.");
        }

        foreach (var styleId in layerGroup.StyleIds)
        {
            if (!styleIds.Contains(styleId))
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown style '{styleId}'.");
            }
        }

        ValidateScales(layerGroup);
    }

    /// <summary>
    /// Validates scale ranges for a layer group.
    /// </summary>
    /// <param name="layerGroup">The layer group to validate.</param>
    /// <exception cref="InvalidDataException">Thrown when scale validation fails.</exception>
    private static void ValidateScales(LayerGroupDefinition layerGroup)
    {
        if (layerGroup.MinScale is < 0)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' minScale cannot be negative.");
        }

        if (layerGroup.MaxScale is < 0)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' maxScale cannot be negative.");
        }

        if (layerGroup.MinScale is double minScale && minScale > 0 &&
            layerGroup.MaxScale is double maxScale && maxScale > 0 && maxScale > minScale)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' maxScale ({maxScale}) cannot be greater than minScale ({minScale}).");
        }
    }

    /// <summary>
    /// Validates layer group members.
    /// </summary>
    /// <param name="layerGroup">The layer group to validate.</param>
    /// <param name="layers">The complete list of layers for validation.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <exception cref="InvalidDataException">Thrown when member validation fails.</exception>
    private static void ValidateMembers(
        LayerGroupDefinition layerGroup,
        IReadOnlyList<LayerDefinition> layers,
        HashSet<string> styleIds)
    {
        if (layerGroup.Members.Count == 0)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' must have at least one member.");
        }

        for (int i = 0; i < layerGroup.Members.Count; i++)
        {
            var member = layerGroup.Members[i];
            if (member is null)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' contains a null member at index {i}.");
            }

            ValidateMemberReferences(layerGroup, member, i, layers);
            ValidateMemberProperties(layerGroup, member, i, styleIds);
        }
    }

    /// <summary>
    /// Validates member references (layerId or groupId).
    /// </summary>
    /// <param name="layerGroup">The layer group being validated.</param>
    /// <param name="member">The member to validate.</param>
    /// <param name="index">The member index.</param>
    /// <param name="layers">The complete list of layers for validation.</param>
    /// <exception cref="InvalidDataException">Thrown when reference validation fails.</exception>
    private static void ValidateMemberReferences(
        LayerGroupDefinition layerGroup,
        LayerGroupMember member,
        int index,
        IReadOnlyList<LayerDefinition> layers)
    {
        var hasLayerId = member.LayerId.HasValue();
        var hasGroupId = member.GroupId.HasValue();

        if (!hasLayerId && !hasGroupId)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} must specify either layerId or groupId.");
        }

        if (hasLayerId && hasGroupId)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} cannot specify both layerId and groupId.");
        }

        // Validate type matches the ID provided
        if (member.Type == LayerGroupMemberType.Layer && !hasLayerId)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} has type 'Layer' but no layerId specified.");
        }

        if (member.Type == LayerGroupMemberType.Group && !hasGroupId)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} has type 'Group' but no groupId specified.");
        }

        // Validate referenced layer exists (only for layers in same service)
        if (hasLayerId)
        {
            var referencedLayerExists = layers.Any(l =>
                string.Equals(l.Id, member.LayerId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(l.ServiceId, layerGroup.ServiceId, StringComparison.OrdinalIgnoreCase));

            if (!referencedLayerExists)
            {
                throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} references unknown layer '{member.LayerId}' in service '{layerGroup.ServiceId}'.");
            }
        }
    }

    /// <summary>
    /// Validates member properties like opacity and style references.
    /// </summary>
    /// <param name="layerGroup">The layer group being validated.</param>
    /// <param name="member">The member to validate.</param>
    /// <param name="index">The member index.</param>
    /// <param name="styleIds">The set of valid style IDs.</param>
    /// <exception cref="InvalidDataException">Thrown when property validation fails.</exception>
    private static void ValidateMemberProperties(
        LayerGroupDefinition layerGroup,
        LayerGroupMember member,
        int index,
        HashSet<string> styleIds)
    {
        // Validate opacity range
        if (member.Opacity is < 0 or > 1)
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} opacity must be between 0 and 1.");
        }

        // Validate style reference if specified
        if (member.StyleId.HasValue() && !styleIds.Contains(member.StyleId))
        {
            throw new InvalidDataException($"Layer group '{layerGroup.Id}' member at index {index} references unknown style '{member.StyleId}'.");
        }
    }

    /// <summary>
    /// Validates group references in the second pass.
    /// </summary>
    /// <param name="layerGroup">The layer group to validate.</param>
    /// <param name="allGroups">All layer groups.</param>
    /// <param name="layerGroupIds">The set of valid layer group IDs.</param>
    /// <exception cref="InvalidDataException">Thrown when group reference validation fails.</exception>
    private static void ValidateGroupReferences(
        LayerGroupDefinition layerGroup,
        IReadOnlyList<LayerGroupDefinition> allGroups,
        HashSet<string> layerGroupIds)
    {
        foreach (var member in layerGroup.Members)
        {
            if (member.Type == LayerGroupMemberType.Group)
            {
                if (!layerGroupIds.Contains(member.GroupId!))
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' references unknown nested group '{member.GroupId}'.");
                }

                // Check that nested group is in the same service
                var nestedGroup = allGroups.FirstOrDefault(g =>
                    string.Equals(g.Id, member.GroupId, StringComparison.OrdinalIgnoreCase));

                if (nestedGroup != null && !string.Equals(nestedGroup.ServiceId, layerGroup.ServiceId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Layer group '{layerGroup.Id}' cannot reference group '{member.GroupId}' from a different service.");
                }
            }
        }
    }

    /// <summary>
    /// Detects circular references in layer group hierarchies.
    /// </summary>
    /// <param name="group">The layer group to check.</param>
    /// <param name="allGroups">All layer groups.</param>
    /// <exception cref="InvalidDataException">Thrown when a circular reference is detected.</exception>
    private static void DetectCircularReferences(
        LayerGroupDefinition group,
        IReadOnlyList<LayerGroupDefinition> allGroups)
    {
        DetectCircularReferencesRecursive(group, allGroups, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Recursively detects circular references in layer group hierarchies.
    /// </summary>
    /// <param name="group">The current layer group.</param>
    /// <param name="allGroups">All layer groups.</param>
    /// <param name="visitedGroups">Set of visited group IDs to detect cycles.</param>
    /// <exception cref="InvalidDataException">Thrown when a circular reference is detected.</exception>
    private static void DetectCircularReferencesRecursive(
        LayerGroupDefinition group,
        IReadOnlyList<LayerGroupDefinition> allGroups,
        HashSet<string> visitedGroups)
    {
        if (!visitedGroups.Add(group.Id))
        {
            throw new InvalidDataException($"Circular reference detected in layer group '{group.Id}'.");
        }

        foreach (var member in group.Members)
        {
            if (member.Type == LayerGroupMemberType.Group)
            {
                var nestedGroup = allGroups.FirstOrDefault(g =>
                    string.Equals(g.Id, member.GroupId, StringComparison.OrdinalIgnoreCase));

                if (nestedGroup != null)
                {
                    DetectCircularReferencesRecursive(nestedGroup, allGroups, new HashSet<string>(visitedGroups, StringComparer.OrdinalIgnoreCase));
                }
            }
        }
    }
}
