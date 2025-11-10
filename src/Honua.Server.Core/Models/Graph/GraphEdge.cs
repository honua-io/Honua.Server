// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Graph;

/// <summary>
/// Represents an edge (relationship) in the graph database.
/// </summary>
public sealed class GraphEdge
{
    /// <summary>
    /// Gets or sets the unique identifier for the edge in the graph database.
    /// This is the internal AGE edge ID.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Gets or sets the type/label of the relationship.
    /// Examples: "CONTAINS", "FEEDS", "SUPPORTS", "CONNECTS_TO", "HAS"
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source node ID (start vertex).
    /// </summary>
    [JsonPropertyName("startNodeId")]
    public long StartNodeId { get; set; }

    /// <summary>
    /// Gets or sets the target node ID (end vertex).
    /// </summary>
    [JsonPropertyName("endNodeId")]
    public long EndNodeId { get; set; }

    /// <summary>
    /// Gets or sets the properties of the edge as key-value pairs.
    /// Properties can include relationship metadata like weights, timestamps, etc.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional reference to the start node object.
    /// This is populated when the edge is retrieved with node details.
    /// </summary>
    [JsonPropertyName("startNode")]
    public GraphNode? StartNode { get; set; }

    /// <summary>
    /// Gets or sets the optional reference to the end node object.
    /// This is populated when the edge is retrieved with node details.
    /// </summary>
    [JsonPropertyName("endNode")]
    public GraphNode? EndNode { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the edge was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt
    {
        get => Properties.TryGetValue("createdAt", out var value) && value is string str && DateTimeOffset.TryParse(str, out var dt)
            ? dt
            : null;
        set
        {
            if (value.HasValue)
            {
                Properties["createdAt"] = value.Value.ToString("O");
            }
            else
            {
                Properties.Remove("createdAt");
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEdge"/> class.
    /// </summary>
    public GraphEdge()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEdge"/> class with a type.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    public GraphEdge(string type)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphEdge"/> class with full details.
    /// </summary>
    /// <param name="type">The relationship type.</param>
    /// <param name="startNodeId">The source node ID.</param>
    /// <param name="endNodeId">The target node ID.</param>
    /// <param name="properties">The edge properties.</param>
    public GraphEdge(string type, long startNodeId, long endNodeId, Dictionary<string, object>? properties = null)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
        Properties = properties ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Gets a property value by key with a default fallback.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The property key.</param>
    /// <param name="defaultValue">The default value if the property doesn't exist.</param>
    /// <returns>The property value or default.</returns>
    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (Properties.TryGetValue(key, out var value))
        {
            try
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }

                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Sets a property value.
    /// </summary>
    /// <param name="key">The property key.</param>
    /// <param name="value">The property value.</param>
    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
    }

    /// <summary>
    /// Returns a string representation of the edge.
    /// </summary>
    public override string ToString()
    {
        var id = Id.HasValue ? $"[{Id.Value}]" : "[new]";
        return $"{id} ({StartNodeId})-[{Type}]->({EndNodeId})";
    }
}
