// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Honua.Server.Core.Models.Graph;

/// <summary>
/// Represents the result of a graph query operation.
/// </summary>
public sealed class GraphQueryResult
{
    /// <summary>
    /// Gets or sets the list of nodes returned by the query.
    /// </summary>
    [JsonPropertyName("nodes")]
    public List<GraphNode> Nodes { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of edges returned by the query.
    /// </summary>
    [JsonPropertyName("edges")]
    public List<GraphEdge> Edges { get; set; } = new();

    /// <summary>
    /// Gets or sets the total count of results (if different from returned items due to pagination).
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets whether there are more results available.
    /// </summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets the execution time of the query in milliseconds.
    /// </summary>
    [JsonPropertyName("executionTimeMs")]
    public long? ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the query result.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Represents a request to execute a Cypher query.
/// </summary>
public sealed class CypherQueryRequest
{
    /// <summary>
    /// Gets or sets the Cypher query string to execute.
    /// </summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameters for the query (optional).
    /// </summary>
    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the graph name to query (optional, uses default if not specified).
    /// </summary>
    [JsonPropertyName("graphName")]
    public string? GraphName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results to return (for pagination).
    /// </summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the number of results to skip (for pagination).
    /// </summary>
    [JsonPropertyName("skip")]
    public int? Skip { get; set; }
}

/// <summary>
/// Represents a request to create a relationship between nodes.
/// </summary>
public sealed class CreateRelationshipRequest
{
    /// <summary>
    /// Gets or sets the ID of the source node.
    /// </summary>
    [JsonPropertyName("sourceNodeId")]
    public long SourceNodeId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the target node.
    /// </summary>
    [JsonPropertyName("targetNodeId")]
    public long TargetNodeId { get; set; }

    /// <summary>
    /// Gets or sets the type of the relationship.
    /// </summary>
    [JsonPropertyName("relationshipType")]
    public string RelationshipType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the properties of the relationship (optional).
    /// </summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, object>? Properties { get; set; }

    /// <summary>
    /// Gets or sets the graph name (optional, uses default if not specified).
    /// </summary>
    [JsonPropertyName("graphName")]
    public string? GraphName { get; set; }
}

/// <summary>
/// Represents a graph traversal request.
/// </summary>
public sealed class GraphTraversalRequest
{
    /// <summary>
    /// Gets or sets the starting node ID.
    /// </summary>
    [JsonPropertyName("startNodeId")]
    public long StartNodeId { get; set; }

    /// <summary>
    /// Gets or sets the relationship types to traverse (empty means all types).
    /// </summary>
    [JsonPropertyName("relationshipTypes")]
    public List<string>? RelationshipTypes { get; set; }

    /// <summary>
    /// Gets or sets the direction of traversal.
    /// </summary>
    [JsonPropertyName("direction")]
    public TraversalDirection Direction { get; set; } = TraversalDirection.Outgoing;

    /// <summary>
    /// Gets or sets the maximum depth of traversal.
    /// </summary>
    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; set; } = 5;

    /// <summary>
    /// Gets or sets node filters (e.g., filter by node type or properties).
    /// </summary>
    [JsonPropertyName("nodeFilter")]
    public Dictionary<string, object>? NodeFilter { get; set; }

    /// <summary>
    /// Gets or sets the graph name (optional, uses default if not specified).
    /// </summary>
    [JsonPropertyName("graphName")]
    public string? GraphName { get; set; }
}

/// <summary>
/// Defines the direction of graph traversal.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TraversalDirection
{
    /// <summary>
    /// Follow outgoing edges from the start node.
    /// </summary>
    Outgoing,

    /// <summary>
    /// Follow incoming edges to the start node.
    /// </summary>
    Incoming,

    /// <summary>
    /// Follow edges in both directions.
    /// </summary>
    Both
}
