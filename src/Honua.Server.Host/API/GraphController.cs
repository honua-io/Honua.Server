// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Services;
using Honua.Server.Core.Models.Graph;
using System.ComponentModel.DataAnnotations;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for graph database operations using Apache AGE.
/// Provides endpoints for managing nodes, edges, and executing Cypher queries.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize(Policy = "RequireEditor")]
[Route("api/v{version:apiVersion}/graph")]
[Produces("application/json")]
public class GraphController : ControllerBase
{
    private readonly ILogger<GraphController> _logger;
    private readonly IGraphDatabaseService _graphService;

    public GraphController(
        ILogger<GraphController> logger,
        IGraphDatabaseService graphService)
    {
        _logger = logger;
        _graphService = graphService;
    }

    // ==================== Graph Management ====================

    /// <summary>
    /// Creates a new graph.
    /// </summary>
    /// <param name="graphName">The name of the graph to create</param>
    /// <response code="201">Graph created successfully</response>
    /// <response code="400">Invalid graph name</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("graphs/{graphName}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateGraph([FromRoute] string graphName)
    {
        await _graphService.CreateGraphAsync(graphName);
        return Created($"/api/graph/graphs/{graphName}", new { graphName, message = "Graph created successfully" });
    }

    /// <summary>
    /// Checks if a graph exists.
    /// </summary>
    /// <param name="graphName">The name of the graph to check</param>
    /// <response code="200">Graph existence status</response>
    [HttpGet("graphs/{graphName}/exists")]
    [ProducesResponseType(typeof(GraphExistsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphExistsResponse>> GraphExists([FromRoute] string graphName)
    {
        var exists = await _graphService.GraphExistsAsync(graphName);
        return Ok(new GraphExistsResponse { GraphName = graphName, Exists = exists });
    }

    /// <summary>
    /// Deletes a graph and all its data.
    /// </summary>
    /// <param name="graphName">The name of the graph to delete</param>
    /// <response code="204">Graph deleted successfully</response>
    /// <response code="404">Graph not found</response>
    [HttpDelete("graphs/{graphName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGraph([FromRoute] string graphName)
    {
        var exists = await _graphService.GraphExistsAsync(graphName);
        if (!exists)
        {
            return NotFound(new { message = $"Graph '{graphName}' not found" });
        }

        await _graphService.DropGraphAsync(graphName);
        return NoContent();
    }

    // ==================== Node Operations ====================

    /// <summary>
    /// Creates a new node in the graph.
    /// </summary>
    /// <param name="node">The node to create</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="201">Node created successfully</response>
    /// <response code="400">Invalid node data</response>
    [HttpPost("nodes")]
    [ProducesResponseType(typeof(GraphNode), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GraphNode>> CreateNode(
        [FromBody] GraphNode node,
        [FromQuery] string? graphName = null)
    {
        if (string.IsNullOrEmpty(node.Label))
        {
            return BadRequest(new { message = "Node label is required" });
        }

        var createdNode = await _graphService.CreateNodeAsync(node, graphName);
        return CreatedAtAction(nameof(GetNode), new { id = createdNode.Id, graphName }, createdNode);
    }

    /// <summary>
    /// Gets a node by its ID.
    /// </summary>
    /// <param name="id">The node ID</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="200">Node found</response>
    /// <response code="404">Node not found</response>
    [HttpGet("nodes/{id}")]
    [ProducesResponseType(typeof(GraphNode), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GraphNode>> GetNode(
        [FromRoute] long id,
        [FromQuery] string? graphName = null)
    {
        var node = await _graphService.GetNodeByIdAsync(id, graphName);
        if (node == null)
        {
            return NotFound(new { message = $"Node with ID {id} not found" });
        }

        return Ok(node);
    }

    /// <summary>
    /// Finds nodes by label and optional property filters.
    /// </summary>
    /// <param name="label">The node label to search for</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <param name="limit">Maximum number of results (default: 100, max: 1000)</param>
    /// <response code="200">Nodes found</response>
    [HttpGet("nodes")]
    [ProducesResponseType(typeof(IReadOnlyList<GraphNode>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GraphNode>>> FindNodes(
        [FromQuery, Required] string label,
        [FromQuery] string? graphName = null,
        [FromQuery] int limit = 100)
    {
        limit = Math.Min(limit, 1000);
        var nodes = await _graphService.FindNodesAsync(label, null, graphName, limit);
        return Ok(nodes);
    }

    /// <summary>
    /// Updates a node's properties.
    /// </summary>
    /// <param name="id">The node ID to update</param>
    /// <param name="properties">The properties to update</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="204">Node updated successfully</response>
    /// <response code="400">Invalid properties</response>
    [HttpPut("nodes/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateNode(
        [FromRoute] long id,
        [FromBody] Dictionary<string, object> properties,
        [FromQuery] string? graphName = null)
    {
        await _graphService.UpdateNodeAsync(id, properties, graphName);
        return NoContent();
    }

    /// <summary>
    /// Deletes a node and all its relationships.
    /// </summary>
    /// <param name="id">The node ID to delete</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="204">Node deleted successfully</response>
    [HttpDelete("nodes/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteNode(
        [FromRoute] long id,
        [FromQuery] string? graphName = null)
    {
        await _graphService.DeleteNodeAsync(id, graphName);
        return NoContent();
    }

    /// <summary>
    /// Creates multiple nodes in a single transaction.
    /// </summary>
    /// <param name="nodes">The nodes to create</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="201">Nodes created successfully</response>
    [HttpPost("nodes/batch")]
    [ProducesResponseType(typeof(IReadOnlyList<GraphNode>), StatusCodes.Status201Created)]
    public async Task<ActionResult<IReadOnlyList<GraphNode>>> CreateNodesBatch(
        [FromBody] List<GraphNode> nodes,
        [FromQuery] string? graphName = null)
    {
        var createdNodes = await _graphService.CreateNodesAsync(nodes, graphName);
        return CreatedAtAction(nameof(CreateNodesBatch), new { graphName }, createdNodes);
    }

    // ==================== Edge Operations ====================

    /// <summary>
    /// Creates a new relationship (edge) between two nodes.
    /// </summary>
    /// <param name="request">The relationship creation request</param>
    /// <response code="201">Relationship created successfully</response>
    /// <response code="400">Invalid relationship data</response>
    [HttpPost("relationships")]
    [ProducesResponseType(typeof(GraphEdge), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GraphEdge>> CreateRelationship([FromBody] CreateRelationshipRequest request)
    {
        if (string.IsNullOrEmpty(request.RelationshipType))
        {
            return BadRequest(new { message = "Relationship type is required" });
        }

        var edge = new GraphEdge
        {
            Type = request.RelationshipType,
            StartNodeId = request.SourceNodeId,
            EndNodeId = request.TargetNodeId,
            Properties = request.Properties ?? new Dictionary<string, object>()
        };

        var createdEdge = await _graphService.CreateEdgeAsync(edge, request.GraphName);
        return CreatedAtAction(nameof(GetRelationship), new { id = createdEdge.Id, graphName = request.GraphName }, createdEdge);
    }

    /// <summary>
    /// Gets a relationship by its ID.
    /// </summary>
    /// <param name="id">The relationship ID</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="200">Relationship found</response>
    /// <response code="404">Relationship not found</response>
    [HttpGet("relationships/{id}")]
    [ProducesResponseType(typeof(GraphEdge), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GraphEdge>> GetRelationship(
        [FromRoute] long id,
        [FromQuery] string? graphName = null)
    {
        var edge = await _graphService.GetEdgeByIdAsync(id, graphName);
        if (edge == null)
        {
            return NotFound(new { message = $"Relationship with ID {id} not found" });
        }

        return Ok(edge);
    }

    /// <summary>
    /// Gets all relationships for a node.
    /// </summary>
    /// <param name="nodeId">The node ID</param>
    /// <param name="relationshipType">Optional filter by relationship type</param>
    /// <param name="direction">The direction of relationships (outgoing, incoming, both)</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="200">Relationships found</response>
    [HttpGet("nodes/{nodeId}/relationships")]
    [ProducesResponseType(typeof(IReadOnlyList<GraphEdge>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GraphEdge>>> GetNodeRelationships(
        [FromRoute] long nodeId,
        [FromQuery] string? relationshipType = null,
        [FromQuery] TraversalDirection direction = TraversalDirection.Both,
        [FromQuery] string? graphName = null)
    {
        var edges = await _graphService.GetNodeRelationshipsAsync(nodeId, relationshipType, direction, graphName);
        return Ok(edges);
    }

    /// <summary>
    /// Deletes a relationship.
    /// </summary>
    /// <param name="id">The relationship ID to delete</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="204">Relationship deleted successfully</response>
    [HttpDelete("relationships/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteRelationship(
        [FromRoute] long id,
        [FromQuery] string? graphName = null)
    {
        await _graphService.DeleteEdgeAsync(id, graphName);
        return NoContent();
    }

    // ==================== Cypher Queries ====================

    /// <summary>
    /// Executes a Cypher query against the graph database.
    /// </summary>
    /// <param name="request">The Cypher query request</param>
    /// <response code="200">Query executed successfully</response>
    /// <response code="400">Invalid query</response>
    [HttpPost("query")]
    [ProducesResponseType(typeof(GraphQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GraphQueryResult>> ExecuteCypherQuery([FromBody] CypherQueryRequest request)
    {
        if (string.IsNullOrEmpty(request.Query))
        {
            return BadRequest(new { message = "Query is required" });
        }

        var result = await _graphService.ExecuteCypherQueryAsync(
            request.Query,
            request.Parameters,
            request.GraphName);

        return Ok(result);
    }

    // ==================== Graph Traversal ====================

    /// <summary>
    /// Traverses the graph starting from a node.
    /// </summary>
    /// <param name="request">The traversal request</param>
    /// <response code="200">Traversal completed successfully</response>
    /// <response code="400">Invalid traversal request</response>
    [HttpPost("traverse")]
    [ProducesResponseType(typeof(GraphQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GraphQueryResult>> TraverseGraph([FromBody] GraphTraversalRequest request)
    {
        var result = await _graphService.TraverseGraphAsync(
            request.StartNodeId,
            request.RelationshipTypes,
            request.Direction,
            request.MaxDepth,
            request.NodeFilter,
            request.GraphName);

        return Ok(result);
    }

    /// <summary>
    /// Finds the shortest path between two nodes.
    /// </summary>
    /// <param name="startNodeId">The starting node ID</param>
    /// <param name="endNodeId">The ending node ID</param>
    /// <param name="relationshipTypes">Optional list of relationship types to follow</param>
    /// <param name="maxDepth">Maximum path length to search (default: 10)</param>
    /// <param name="graphName">Optional graph name (uses default if not specified)</param>
    /// <response code="200">Shortest path found (or empty if no path exists)</response>
    [HttpGet("shortest-path")]
    [ProducesResponseType(typeof(GraphQueryResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GraphQueryResult>> FindShortestPath(
        [FromQuery, Required] long startNodeId,
        [FromQuery, Required] long endNodeId,
        [FromQuery] string[]? relationshipTypes = null,
        [FromQuery] int maxDepth = 10,
        [FromQuery] string? graphName = null)
    {
        var result = await _graphService.FindShortestPathAsync(
            startNodeId,
            endNodeId,
            relationshipTypes,
            maxDepth,
            graphName);

        return Ok(result);
    }
}

/// <summary>
/// Response model for graph existence check.
/// </summary>
public sealed class GraphExistsResponse
{
    public string GraphName { get; set; } = string.Empty;
    public bool Exists { get; set; }
}
