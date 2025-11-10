// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Graph;

/// <summary>
/// Represents a node (vertex) in the graph database.
/// </summary>
public sealed class GraphNode
{
    /// <summary>
    /// Gets or sets the unique identifier for the node in the graph database.
    /// This is the internal AGE vertex ID.
    /// </summary>
    [JsonPropertyName("id")]
    public long? Id { get; set; }

    /// <summary>
    /// Gets or sets the label (type) of the node.
    /// Examples: "Building", "Floor", "Room", "Equipment", "WaterMain", "Valve"
    /// </summary>
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the properties of the node as key-value pairs.
    /// Properties can include business identifiers, names, attributes, etc.
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Gets or sets the optional reference to a Honua feature ID.
    /// This links the graph node to a geospatial feature in the main database.
    /// </summary>
    [JsonPropertyName("featureId")]
    public Guid? FeatureId
    {
        get => Properties.TryGetValue("featureId", out var value) && value is string str && Guid.TryParse(str, out var guid)
            ? guid
            : null;
        set
        {
            if (value.HasValue)
            {
                Properties["featureId"] = value.Value.ToString();
            }
            else
            {
                Properties.Remove("featureId");
            }
        }
    }

    /// <summary>
    /// Gets or sets the timestamp when the node was created.
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
    /// Initializes a new instance of the <see cref="GraphNode"/> class.
    /// </summary>
    public GraphNode()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphNode"/> class with a label.
    /// </summary>
    /// <param name="label">The node label/type.</param>
    public GraphNode(string label)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphNode"/> class with a label and properties.
    /// </summary>
    /// <param name="label">The node label/type.</param>
    /// <param name="properties">The node properties.</param>
    public GraphNode(string label, Dictionary<string, object> properties)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
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
    /// Returns a string representation of the node.
    /// </summary>
    public override string ToString()
    {
        var id = Id.HasValue ? $"[{Id.Value}]" : "[new]";
        var name = GetProperty<string>("name") ?? GetProperty<string>("id") ?? "unnamed";
        return $"{id}:{Label} ({name})";
    }
}
