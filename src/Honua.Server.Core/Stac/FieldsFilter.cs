// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Stac;

/// <summary>
/// Filters STAC Item JSON objects based on the Fields Extension specification.
/// </summary>
/// <remarks>
/// Implements field filtering logic according to the STAC API Fields Extension.
/// Supports nested field paths (e.g., "properties.datetime") and respects include/exclude semantics.
///
/// Filtering Rules:
/// 1. Empty specification: return all fields (no filtering)
/// 2. Include only: return only specified fields
/// 3. Exclude only: return all fields except specified ones
/// 4. Mixed include/exclude: most specific path wins, include preferred over exclude
///
/// Reference: https://github.com/stac-api-extensions/fields
/// </remarks>
public static class FieldsFilter
{
    /// <summary>
    /// Applies field filtering to a STAC Item JSON object.
    /// </summary>
    /// <param name="itemJson">The JSON object representing the STAC Item.</param>
    /// <param name="fields">The fields specification to apply.</param>
    /// <returns>A filtered JSON object with only the requested fields.</returns>
    public static JsonObject ApplyFieldsFilter(JsonObject itemJson, FieldsSpecification? fields)
    {
        if (fields is null || fields.IsEmpty)
        {
            // No filtering requested, return as-is
            return itemJson;
        }

        // Create a new filtered object
        var filtered = new JsonObject();

        if (fields.IsIncludeMode)
        {
            // Include mode: only return specified fields
            ApplyIncludeFilter(itemJson, filtered, fields.Include!);
        }
        else if (fields.IsExcludeMode)
        {
            // Exclude mode: return all fields except specified ones
            ApplyExcludeFilter(itemJson, filtered, fields.Exclude!);
        }
        else if (fields.Include is not null && fields.Exclude is not null)
        {
            // Mixed mode: apply both include and exclude with include taking precedence
            ApplyMixedFilter(itemJson, filtered, fields.Include, fields.Exclude);
        }

        return filtered;
    }

    /// <summary>
    /// Applies include-only filtering: only specified fields are returned.
    /// </summary>
    private static void ApplyIncludeFilter(JsonObject source, JsonObject target, IReadOnlySet<string> includes)
    {
        // Build a tree of included paths for efficient lookup
        var pathTree = BuildPathTree(includes);

        // Recursively copy included fields
        CopyIncludedFields(source, target, pathTree, string.Empty);
    }

    /// <summary>
    /// Applies exclude-only filtering: all fields except specified ones are returned.
    /// </summary>
    private static void ApplyExcludeFilter(JsonObject source, JsonObject target, IReadOnlySet<string> excludes)
    {
        // Build a tree of excluded paths for efficient lookup
        var pathTree = BuildPathTree(excludes);

        // Recursively copy all non-excluded fields
        CopyExcludedFields(source, target, pathTree, string.Empty);
    }

    /// <summary>
    /// Applies mixed include/exclude filtering with include taking precedence.
    /// </summary>
    private static void ApplyMixedFilter(JsonObject source, JsonObject target, IReadOnlySet<string> includes, IReadOnlySet<string> excludes)
    {
        // For mixed mode, we apply include filter first, then respect excludes within included paths
        var includeTree = BuildPathTree(includes);
        var excludeTree = BuildPathTree(excludes);

        // Copy included fields while checking for exclusions
        CopyMixedFields(source, target, includeTree, excludeTree, string.Empty);
    }

    /// <summary>
    /// Recursively copies fields that are included in the path tree.
    /// </summary>
    private static void CopyIncludedFields(JsonObject source, JsonObject target, PathTreeNode tree, string currentPath)
    {
        foreach (var property in source)
        {
            var fieldName = property.Key;
            var fieldPath = currentPath.IsNullOrEmpty() ? fieldName : $"{currentPath}.{fieldName}";

            // Check if this field or its parent is in the include set
            if (tree.IsIncluded(fieldName) || tree.IsIncluded(fieldPath))
            {
                // Include this entire field
                target[fieldName] = property.Value?.DeepClone();
            }
            else if (tree.HasChildren(fieldName) && property.Value is JsonObject childObject)
            {
                // This field has children that might be included, recurse
                var childTarget = new JsonObject();
                var childTree = tree.GetChild(fieldName);
                if (childTree is not null)
                {
                    CopyIncludedFields(childObject, childTarget, childTree, fieldPath);
                    if (childTarget.Count > 0)
                    {
                        target[fieldName] = childTarget;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Recursively copies fields that are NOT excluded in the path tree.
    /// </summary>
    private static void CopyExcludedFields(JsonObject source, JsonObject target, PathTreeNode tree, string currentPath)
    {
        foreach (var property in source)
        {
            var fieldName = property.Key;
            var fieldPath = currentPath.IsNullOrEmpty() ? fieldName : $"{currentPath}.{fieldName}";

            // Check if this field or its parent is in the exclude set
            if (tree.IsIncluded(fieldName) || tree.IsIncluded(fieldPath))
            {
                // This field is excluded, skip it
                continue;
            }

            if (tree.HasChildren(fieldName) && property.Value is JsonObject childObject)
            {
                // This field has children that might be excluded, recurse
                var childTarget = new JsonObject();
                var childTree = tree.GetChild(fieldName);
                if (childTree is not null)
                {
                    CopyExcludedFields(childObject, childTarget, childTree, fieldPath);
                }
                else
                {
                    // No exclusions in children, copy entire object
                    childTarget = childObject.DeepClone() as JsonObject ?? new JsonObject();
                }

                if (childTarget.Count > 0)
                {
                    target[fieldName] = childTarget;
                }
            }
            else
            {
                // No exclusion for this field, copy it
                target[fieldName] = property.Value?.DeepClone();
            }
        }
    }

    /// <summary>
    /// Recursively copies fields with mixed include/exclude logic.
    /// </summary>
    private static void CopyMixedFields(JsonObject source, JsonObject target, PathTreeNode includeTree, PathTreeNode excludeTree, string currentPath)
    {
        foreach (var property in source)
        {
            var fieldName = property.Key;
            var fieldPath = currentPath.IsNullOrEmpty() ? fieldName : $"{currentPath}.{fieldName}";

            // Check include first (takes precedence)
            var isIncluded = includeTree.IsIncluded(fieldName) || includeTree.IsIncluded(fieldPath);
            var isExcluded = excludeTree.IsIncluded(fieldName) || excludeTree.IsIncluded(fieldPath);

            if (isIncluded && !isExcluded)
            {
                // Explicitly included and not excluded
                target[fieldName] = property.Value?.DeepClone();
            }
            else if (isIncluded && isExcluded)
            {
                // Conflict: include takes precedence
                target[fieldName] = property.Value?.DeepClone();
            }
            else if (includeTree.HasChildren(fieldName) && property.Value is JsonObject childObject)
            {
                // This field has children that might be included, recurse
                var childTarget = new JsonObject();
                var includeChild = includeTree.GetChild(fieldName);
                var excludeChild = excludeTree.GetChild(fieldName);

                if (includeChild is not null)
                {
                    CopyMixedFields(childObject, childTarget, includeChild, excludeChild ?? PathTreeNode.Empty, fieldPath);
                    if (childTarget.Count > 0)
                    {
                        target[fieldName] = childTarget;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds a hierarchical tree structure from field paths for efficient filtering.
    /// </summary>
    /// <param name="paths">Field paths like "properties.datetime", "assets", etc.</param>
    /// <returns>A tree structure representing the paths.</returns>
    private static PathTreeNode BuildPathTree(IEnumerable<string> paths)
    {
        var root = new PathTreeNode();

        foreach (var path in paths)
        {
            if (path.IsNullOrWhiteSpace())
            {
                continue;
            }

            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var current = root;

            foreach (var part in parts)
            {
                current = current.GetOrAddChild(part);
            }

            // Mark the final node as a terminal (complete path)
            current.IsTerminal = true;
        }

        return root;
    }

    /// <summary>
    /// Represents a node in the field path tree for efficient filtering.
    /// </summary>
    private sealed class PathTreeNode
    {
        private Dictionary<string, PathTreeNode>? _children;

        public bool IsTerminal { get; set; }

        public static PathTreeNode Empty { get; } = new PathTreeNode();

        public PathTreeNode GetOrAddChild(string name)
        {
            _children ??= new Dictionary<string, PathTreeNode>(StringComparer.Ordinal);

            if (!_children.TryGetValue(name, out var child))
            {
                child = new PathTreeNode();
                _children[name] = child;
            }

            return child;
        }

        public PathTreeNode? GetChild(string name)
        {
            return _children?.TryGetValue(name, out var child) == true ? child : null;
        }

        public bool HasChildren(string name)
        {
            return _children?.ContainsKey(name) == true;
        }

        public bool IsIncluded(string name)
        {
            return IsTerminal || (_children?.ContainsKey(name) == true && _children[name].IsTerminal);
        }
    }
}
