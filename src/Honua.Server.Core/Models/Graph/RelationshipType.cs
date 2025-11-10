// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Graph;

/// <summary>
/// Defines a relationship type schema for graph database relationships.
/// </summary>
public sealed class RelationshipType
{
    /// <summary>
    /// Gets or sets the unique name of the relationship type.
    /// Examples: "CONTAINS", "FEEDS", "SUPPORTS", "CONNECTS_TO"
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the relationship type.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the allowed source node type(s) for this relationship.
    /// If null or empty, any node type can be a source.
    /// </summary>
    [JsonPropertyName("sourceNodeTypes")]
    public List<string>? SourceNodeTypes { get; set; }

    /// <summary>
    /// Gets or sets the allowed target node type(s) for this relationship.
    /// If null or empty, any node type can be a target.
    /// </summary>
    [JsonPropertyName("targetNodeTypes")]
    public List<string>? TargetNodeTypes { get; set; }

    /// <summary>
    /// Gets or sets the cardinality constraint for the relationship.
    /// </summary>
    [JsonPropertyName("cardinality")]
    public RelationshipCardinality Cardinality { get; set; } = RelationshipCardinality.ManyToMany;

    /// <summary>
    /// Gets or sets the required properties for this relationship type.
    /// </summary>
    [JsonPropertyName("requiredProperties")]
    public List<string>? RequiredProperties { get; set; }

    /// <summary>
    /// Gets or sets the optional properties for this relationship type.
    /// </summary>
    [JsonPropertyName("optionalProperties")]
    public List<string>? OptionalProperties { get; set; }

    /// <summary>
    /// Gets or sets whether this relationship type allows cyclic relationships (a node connecting to itself).
    /// </summary>
    [JsonPropertyName("allowSelfReference")]
    public bool AllowSelfReference { get; set; } = false;

    /// <summary>
    /// Gets or sets metadata about this relationship type.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Validates whether a relationship is valid according to this type definition.
    /// </summary>
    /// <param name="sourceNodeLabel">The label of the source node.</param>
    /// <param name="targetNodeLabel">The label of the target node.</param>
    /// <param name="properties">The properties of the relationship.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValid(string sourceNodeLabel, string targetNodeLabel, Dictionary<string, object>? properties = null)
    {
        // Check source node type
        if (SourceNodeTypes != null && SourceNodeTypes.Count > 0 && !SourceNodeTypes.Contains(sourceNodeLabel))
        {
            return false;
        }

        // Check target node type
        if (TargetNodeTypes != null && TargetNodeTypes.Count > 0 && !TargetNodeTypes.Contains(targetNodeLabel))
        {
            return false;
        }

        // Check required properties
        if (RequiredProperties != null && properties != null)
        {
            foreach (var requiredProp in RequiredProperties)
            {
                if (!properties.ContainsKey(requiredProp))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

/// <summary>
/// Defines the cardinality constraint for a relationship.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RelationshipCardinality
{
    /// <summary>
    /// One source node can connect to one target node.
    /// </summary>
    OneToOne,

    /// <summary>
    /// One source node can connect to many target nodes.
    /// </summary>
    OneToMany,

    /// <summary>
    /// Many source nodes can connect to one target node.
    /// </summary>
    ManyToOne,

    /// <summary>
    /// Many source nodes can connect to many target nodes (default).
    /// </summary>
    ManyToMany
}
