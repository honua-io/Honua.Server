// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Models.Graph;

namespace Honua.Server.Core.Services;

/// <summary>
/// Service interface for Apache AGE graph database operations.
/// Provides graph database capabilities including node/edge management, Cypher queries, and graph traversals.
/// </summary>
public interface IGraphDatabaseService
{
    // ==================== Graph Management ====================

    /// <summary>
    /// Creates a new graph with the specified name.
    /// </summary>
    /// <param name="graphName">The name of the graph to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateGraphAsync(string graphName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops an existing graph and all its data.
    /// </summary>
    /// <param name="graphName">The name of the graph to drop.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DropGraphAsync(string graphName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a graph exists.
    /// </summary>
    /// <param name="graphName">The name of the graph to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the graph exists, false otherwise.</returns>
    Task<bool> GraphExistsAsync(string graphName, CancellationToken cancellationToken = default);

    // ==================== Node Operations ====================

    /// <summary>
    /// Creates a new node in the graph.
    /// </summary>
    /// <param name="node">The node to create.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created node with its assigned ID.</returns>
    Task<GraphNode> CreateNodeAsync(GraphNode node, string? graphName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The node if found, null otherwise.</returns>
    Task<GraphNode?> GetNodeByIdAsync(long nodeId, string? graphName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds nodes by label and optional property filters.
    /// </summary>
    /// <param name="label">The node label to search for.</param>
    /// <param name="propertyFilters">Optional property filters (key-value pairs).</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching nodes.</returns>
    Task<IReadOnlyList<GraphNode>> FindNodesAsync(
        string label,
        Dictionary<string, object>? propertyFilters = null,
        string? graphName = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a node's properties.
    /// </summary>
    /// <param name="nodeId">The node ID to update.</param>
    /// <param name="properties">The properties to update.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateNodeAsync(long nodeId, Dictionary<string, object> properties, string? graphName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a node and all its relationships.
    /// </summary>
    /// <param name="nodeId">The node ID to delete.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteNodeAsync(long nodeId, string? graphName = null, CancellationToken cancellationToken = default);

    // ==================== Edge Operations ====================

    /// <summary>
    /// Creates a new relationship (edge) between two nodes.
    /// </summary>
    /// <param name="edge">The edge to create.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created edge with its assigned ID.</returns>
    Task<GraphEdge> CreateEdgeAsync(GraphEdge edge, string? graphName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an edge by its ID.
    /// </summary>
    /// <param name="edgeId">The edge ID.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The edge if found, null otherwise.</returns>
    Task<GraphEdge?> GetEdgeByIdAsync(long edgeId, string? graphName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all relationships for a node.
    /// </summary>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="relationshipType">Optional filter by relationship type.</param>
    /// <param name="direction">The direction of relationships to retrieve.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of edges.</returns>
    Task<IReadOnlyList<GraphEdge>> GetNodeRelationshipsAsync(
        long nodeId,
        string? relationshipType = null,
        TraversalDirection direction = TraversalDirection.Both,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an edge.
    /// </summary>
    /// <param name="edgeId">The edge ID to delete.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteEdgeAsync(long edgeId, string? graphName = null, CancellationToken cancellationToken = default);

    // ==================== Cypher Queries ====================

    /// <summary>
    /// Executes a Cypher query and returns the results.
    /// </summary>
    /// <param name="cypherQuery">The Cypher query string.</param>
    /// <param name="parameters">Optional query parameters.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query results containing nodes and edges.</returns>
    Task<GraphQueryResult> ExecuteCypherQueryAsync(
        string cypherQuery,
        Dictionary<string, object>? parameters = null,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a raw Cypher command (for CREATE, UPDATE, DELETE operations).
    /// </summary>
    /// <param name="cypherCommand">The Cypher command string.</param>
    /// <param name="parameters">Optional command parameters.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of affected rows/nodes.</returns>
    Task<int> ExecuteCypherCommandAsync(
        string cypherCommand,
        Dictionary<string, object>? parameters = null,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    // ==================== Graph Traversal ====================

    /// <summary>
    /// Traverses the graph starting from a node.
    /// </summary>
    /// <param name="startNodeId">The starting node ID.</param>
    /// <param name="relationshipTypes">Optional list of relationship types to follow.</param>
    /// <param name="direction">The direction of traversal.</param>
    /// <param name="maxDepth">Maximum depth of traversal.</param>
    /// <param name="nodeFilter">Optional filter for nodes (key-value property filters).</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The traversal results containing all discovered nodes and edges.</returns>
    Task<GraphQueryResult> TraverseGraphAsync(
        long startNodeId,
        IEnumerable<string>? relationshipTypes = null,
        TraversalDirection direction = TraversalDirection.Outgoing,
        int maxDepth = 5,
        Dictionary<string, object>? nodeFilter = null,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the shortest path between two nodes.
    /// </summary>
    /// <param name="startNodeId">The starting node ID.</param>
    /// <param name="endNodeId">The ending node ID.</param>
    /// <param name="relationshipTypes">Optional list of relationship types to follow.</param>
    /// <param name="maxDepth">Maximum path length to search.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The shortest path as nodes and edges, or empty result if no path exists.</returns>
    Task<GraphQueryResult> FindShortestPathAsync(
        long startNodeId,
        long endNodeId,
        IEnumerable<string>? relationshipTypes = null,
        int maxDepth = 10,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    // ==================== Bulk Operations ====================

    /// <summary>
    /// Creates multiple nodes in a single transaction.
    /// </summary>
    /// <param name="nodes">The nodes to create.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created nodes with their assigned IDs.</returns>
    Task<IReadOnlyList<GraphNode>> CreateNodesAsync(
        IEnumerable<GraphNode> nodes,
        string? graphName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates multiple edges in a single transaction.
    /// </summary>
    /// <param name="edges">The edges to create.</param>
    /// <param name="graphName">The graph name (uses default if null).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created edges with their assigned IDs.</returns>
    Task<IReadOnlyList<GraphEdge>> CreateEdgesAsync(
        IEnumerable<GraphEdge> edges,
        string? graphName = null,
        CancellationToken cancellationToken = default);
}
