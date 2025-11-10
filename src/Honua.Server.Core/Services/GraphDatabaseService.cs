// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApacheAGE;
using ApacheAGE.Types;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Models.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Services;

/// <summary>
/// Implementation of <see cref="IGraphDatabaseService"/> using Apache AGE.
/// </summary>
public sealed class GraphDatabaseService : IGraphDatabaseService, IAsyncDisposable
{
    private readonly GraphDatabaseOptions _options;
    private readonly ILogger<GraphDatabaseService> _logger;
    private readonly AgeClient _client;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public GraphDatabaseService(
        IOptions<GraphDatabaseOptions> options,
        ILogger<GraphDatabaseService> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Graph database is not enabled. Set GraphDatabase:Enabled to true in configuration.");
        }

        // Create AGE client
        var connectionString = _options.ConnectionString
            ?? throw new InvalidOperationException("Graph database connection string is required.");

        var clientBuilder = new AgeClientBuilder(connectionString);
        _client = clientBuilder.Build();
    }

    // ==================== Initialization ====================

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _client.OpenConnectionAsync().ConfigureAwait(false);
            _logger.LogInformation("Apache AGE connection established");

            if (_options.EnableSchemaInitialization)
            {
                await InitializeSchemaAsync(cancellationToken).ConfigureAwait(false);
            }

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if default graph exists
            var graphExists = await GraphExistsAsync(_options.DefaultGraphName, cancellationToken).ConfigureAwait(false);

            if (!graphExists && _options.AutoCreateGraph)
            {
                _logger.LogInformation("Creating default graph: {GraphName}", _options.DefaultGraphName);
                await CreateGraphAsync(_options.DefaultGraphName, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize graph database schema");
            throw;
        }
    }

    // ==================== Graph Management ====================

    public async Task CreateGraphAsync(string graphName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graphName);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _client.CreateGraphAsync(graphName).ConfigureAwait(false);
            _logger.LogInformation("Created graph: {GraphName}", graphName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create graph: {GraphName}", graphName);
            throw;
        }
    }

    public async Task DropGraphAsync(string graphName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graphName);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Apache AGE requires a specific SQL command to drop a graph
            var sql = $"SELECT drop_graph('{graphName}', true);";
            await using var reader = await _client.ExecuteQueryAsync(sql).ConfigureAwait(false);
            _logger.LogInformation("Dropped graph: {GraphName}", graphName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop graph: {GraphName}", graphName);
            throw;
        }
    }

    public async Task<bool> GraphExistsAsync(string graphName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graphName);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var sql = @"SELECT COUNT(*) FROM ag_catalog.ag_graph WHERE name = $1;";
            await using var reader = await _client.ExecuteQueryAsync(sql).ConfigureAwait(false);
            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var count = reader.GetInt64(0);
                return count > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check graph existence: {GraphName}", graphName);
            throw;
        }
    }

    // ==================== Node Operations ====================

    public async Task<GraphNode> CreateNodeAsync(GraphNode node, string? graphName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var properties = SerializeProperties(node.Properties);

        try
        {
            var cypherQuery = $"CREATE (n:{node.Label} {properties}) RETURN n";

            if (_options.LogQueries)
            {
                _logger.LogDebug("Executing Cypher: {Query}", cypherQuery);
            }

            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (node agtype);"
            ).ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var vertex = result.GetVertex();
                return ConvertVertexToNode(vertex);
            }

            throw new InvalidOperationException("Failed to create node - no result returned");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create node with label: {Label}", node.Label);
            throw;
        }
    }

    public async Task<GraphNode?> GetNodeByIdAsync(long nodeId, string? graphName = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;

        try
        {
            var cypherQuery = $"MATCH (n) WHERE id(n) = {nodeId} RETURN n";
            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (node agtype);"
            ).ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var vertex = result.GetVertex();
                return ConvertVertexToNode(vertex);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get node by ID: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GraphNode>> FindNodesAsync(
        string label,
        Dictionary<string, object>? propertyFilters = null,
        string? graphName = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(label);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var nodes = new List<GraphNode>();

        try
        {
            var whereClause = BuildWhereClause(propertyFilters);
            var cypherQuery = $"MATCH (n:{label}) {whereClause} RETURN n LIMIT {limit}";

            if (_options.LogQueries)
            {
                _logger.LogDebug("Executing Cypher: {Query}", cypherQuery);
            }

            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (node agtype);"
            ).ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var vertex = result.GetVertex();
                nodes.Add(ConvertVertexToNode(vertex));
            }

            return nodes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find nodes with label: {Label}", label);
            throw;
        }
    }

    public async Task UpdateNodeAsync(long nodeId, Dictionary<string, object> properties, string? graphName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(properties);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var setClause = BuildSetClause(properties);

        try
        {
            var cypherCommand = $"MATCH (n) WHERE id(n) = {nodeId} SET n += {setClause}";
            await _client.ExecuteCypherAsync(graph, cypherCommand).ConfigureAwait(false);
            _logger.LogDebug("Updated node {NodeId}", nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update node: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task DeleteNodeAsync(long nodeId, string? graphName = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;

        try
        {
            var cypherCommand = $"MATCH (n) WHERE id(n) = {nodeId} DETACH DELETE n";
            await _client.ExecuteCypherAsync(graph, cypherCommand).ConfigureAwait(false);
            _logger.LogDebug("Deleted node {NodeId}", nodeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete node: {NodeId}", nodeId);
            throw;
        }
    }

    // ==================== Edge Operations ====================

    public async Task<GraphEdge> CreateEdgeAsync(GraphEdge edge, string? graphName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edge);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var properties = SerializeProperties(edge.Properties);

        try
        {
            var cypherCommand = $@"
                MATCH (a), (b)
                WHERE id(a) = {edge.StartNodeId} AND id(b) = {edge.EndNodeId}
                CREATE (a)-[r:{edge.Type} {properties}]->(b)
                RETURN r";

            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherCommand} $$) AS (edge agtype);"
            ).ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var ageEdge = result.GetEdge();
                return ConvertEdgeToGraphEdge(ageEdge);
            }

            throw new InvalidOperationException("Failed to create edge - no result returned");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create edge: {EdgeType} from {StartId} to {EndId}",
                edge.Type, edge.StartNodeId, edge.EndNodeId);
            throw;
        }
    }

    public async Task<GraphEdge?> GetEdgeByIdAsync(long edgeId, string? graphName = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;

        try
        {
            var cypherQuery = $"MATCH ()-[r]->() WHERE id(r) = {edgeId} RETURN r";
            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (edge agtype);"
            ).ConfigureAwait(false);

            if (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var ageEdge = result.GetEdge();
                return ConvertEdgeToGraphEdge(ageEdge);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get edge by ID: {EdgeId}", edgeId);
            throw;
        }
    }

    public async Task<IReadOnlyList<GraphEdge>> GetNodeRelationshipsAsync(
        long nodeId,
        string? relationshipType = null,
        TraversalDirection direction = TraversalDirection.Both,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var edges = new List<GraphEdge>();

        try
        {
            var relationshipPattern = string.IsNullOrEmpty(relationshipType) ? "r" : $"r:{relationshipType}";
            string cypherQuery = direction switch
            {
                TraversalDirection.Outgoing => $"MATCH (n)-[{relationshipPattern}]->() WHERE id(n) = {nodeId} RETURN r",
                TraversalDirection.Incoming => $"MATCH ()-[{relationshipPattern}]->(n) WHERE id(n) = {nodeId} RETURN r",
                TraversalDirection.Both => $"MATCH (n)-[{relationshipPattern}]-() WHERE id(n) = {nodeId} RETURN r",
                _ => throw new ArgumentException($"Invalid traversal direction: {direction}")
            };

            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (edge agtype);"
            ).ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var result = reader.GetValue<Agtype>(0);
                var ageEdge = result.GetEdge();
                edges.Add(ConvertEdgeToGraphEdge(ageEdge));
            }

            return edges;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get relationships for node: {NodeId}", nodeId);
            throw;
        }
    }

    public async Task DeleteEdgeAsync(long edgeId, string? graphName = null, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;

        try
        {
            var cypherCommand = $"MATCH ()-[r]->() WHERE id(r) = {edgeId} DELETE r";
            await _client.ExecuteCypherAsync(graph, cypherCommand).ConfigureAwait(false);
            _logger.LogDebug("Deleted edge {EdgeId}", edgeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete edge: {EdgeId}", edgeId);
            throw;
        }
    }

    // ==================== Cypher Queries ====================

    public async Task<GraphQueryResult> ExecuteCypherQueryAsync(
        string cypherQuery,
        Dictionary<string, object>? parameters = null,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cypherQuery);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        var stopwatch = Stopwatch.StartNew();
        var result = new GraphQueryResult();

        try
        {
            if (_options.LogQueries)
            {
                _logger.LogDebug("Executing Cypher query: {Query}", cypherQuery);
            }

            // For now, we execute queries without parameterization support
            // TODO: Add parameter binding when ApacheAGE library supports it
            await using var reader = await _client.ExecuteQueryAsync(
                $"SELECT * FROM cypher('{graph}', $$ {cypherQuery} $$) AS (result agtype);"
            ).ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var agtype = reader.GetValue<Agtype>(0);

                // Try to parse as vertex
                try
                {
                    var vertex = agtype.GetVertex();
                    result.Nodes.Add(ConvertVertexToNode(vertex));
                }
                catch
                {
                    // Try to parse as edge
                    try
                    {
                        var edge = agtype.GetEdge();
                        result.Edges.Add(ConvertEdgeToGraphEdge(edge));
                    }
                    catch
                    {
                        // Not a vertex or edge, skip
                    }
                }
            }

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.TotalCount = result.Nodes.Count + result.Edges.Count;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Cypher query: {Query}", cypherQuery);
            throw;
        }
    }

    public async Task<int> ExecuteCypherCommandAsync(
        string cypherCommand,
        Dictionary<string, object>? parameters = null,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cypherCommand);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;

        try
        {
            if (_options.LogQueries)
            {
                _logger.LogDebug("Executing Cypher command: {Command}", cypherCommand);
            }

            await _client.ExecuteCypherAsync(graph, cypherCommand).ConfigureAwait(false);
            return 1; // Apache AGE doesn't return affected row count
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Cypher command: {Command}", cypherCommand);
            throw;
        }
    }

    // ==================== Graph Traversal ====================

    public async Task<GraphQueryResult> TraverseGraphAsync(
        long startNodeId,
        IEnumerable<string>? relationshipTypes = null,
        TraversalDirection direction = TraversalDirection.Outgoing,
        int maxDepth = 5,
        Dictionary<string, object>? nodeFilter = null,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        maxDepth = Math.Min(maxDepth, _options.MaxTraversalDepth);

        try
        {
            var relTypes = relationshipTypes?.Any() == true
                ? string.Join("|", relationshipTypes)
                : "";

            var relationshipPattern = string.IsNullOrEmpty(relTypes) ? "*1.." + maxDepth : $":{relTypes}*1..{maxDepth}";

            string pathPattern = direction switch
            {
                TraversalDirection.Outgoing => $"(start)-[{relationshipPattern}]->(end)",
                TraversalDirection.Incoming => $"(start)<-[{relationshipPattern}]-(end)",
                TraversalDirection.Both => $"(start)-[{relationshipPattern}]-(end)",
                _ => throw new ArgumentException($"Invalid traversal direction: {direction}")
            };

            var whereClause = BuildWhereClause(nodeFilter, "end");
            var cypherQuery = $"MATCH path = {pathPattern} WHERE id(start) = {startNodeId} {whereClause} RETURN start, end";

            return await ExecuteCypherQueryAsync(cypherQuery, null, graph, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to traverse graph from node: {NodeId}", startNodeId);
            throw;
        }
    }

    public async Task<GraphQueryResult> FindShortestPathAsync(
        long startNodeId,
        long endNodeId,
        IEnumerable<string>? relationshipTypes = null,
        int maxDepth = 10,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var graph = graphName ?? _options.DefaultGraphName;
        maxDepth = Math.Min(maxDepth, _options.MaxTraversalDepth);

        try
        {
            var relTypes = relationshipTypes?.Any() == true
                ? $":{string.Join("|", relationshipTypes)}"
                : "";

            var cypherQuery = $@"
                MATCH path = shortestPath((start)-[{relTypes}*1..{maxDepth}]-(end))
                WHERE id(start) = {startNodeId} AND id(end) = {endNodeId}
                RETURN nodes(path), relationships(path)";

            return await ExecuteCypherQueryAsync(cypherQuery, null, graph, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find shortest path between {StartId} and {EndId}", startNodeId, endNodeId);
            throw;
        }
    }

    // ==================== Bulk Operations ====================

    public async Task<IReadOnlyList<GraphNode>> CreateNodesAsync(
        IEnumerable<GraphNode> nodes,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var createdNodes = new List<GraphNode>();

        // Create nodes one by one for now
        // TODO: Optimize with batch operations when supported by ApacheAGE library
        foreach (var node in nodes)
        {
            var created = await CreateNodeAsync(node, graphName, cancellationToken).ConfigureAwait(false);
            createdNodes.Add(created);
        }

        return createdNodes;
    }

    public async Task<IReadOnlyList<GraphEdge>> CreateEdgesAsync(
        IEnumerable<GraphEdge> edges,
        string? graphName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(edges);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var createdEdges = new List<GraphEdge>();

        // Create edges one by one for now
        // TODO: Optimize with batch operations when supported by ApacheAGE library
        foreach (var edge in edges)
        {
            var created = await CreateEdgeAsync(edge, graphName, cancellationToken).ConfigureAwait(false);
            createdEdges.Add(created);
        }

        return createdEdges;
    }

    // ==================== Helper Methods ====================

    private GraphNode ConvertVertexToNode(Vertex vertex)
    {
        var node = new GraphNode
        {
            Id = vertex.Id,
            Label = vertex.Label,
            Properties = new Dictionary<string, object>()
        };

        if (vertex.Properties != null)
        {
            foreach (var prop in vertex.Properties)
            {
                node.Properties[prop.Key] = prop.Value;
            }
        }

        return node;
    }

    private GraphEdge ConvertEdgeToGraphEdge(Edge edge)
    {
        var graphEdge = new GraphEdge
        {
            Id = edge.Id,
            Type = edge.Label,
            StartNodeId = edge.StartId,
            EndNodeId = edge.EndId,
            Properties = new Dictionary<string, object>()
        };

        if (edge.Properties != null)
        {
            foreach (var prop in edge.Properties)
            {
                graphEdge.Properties[prop.Key] = prop.Value;
            }
        }

        return graphEdge;
    }

    private static string SerializeProperties(Dictionary<string, object> properties)
    {
        if (properties == null || properties.Count == 0)
        {
            return "{}";
        }

        var sb = new StringBuilder("{");
        var first = true;

        foreach (var kvp in properties)
        {
            if (!first) sb.Append(", ");
            first = false;

            sb.Append(kvp.Key).Append(": ");

            if (kvp.Value is string str)
            {
                sb.Append('\'').Append(str.Replace("'", "\\'")).Append('\'');
            }
            else if (kvp.Value is bool b)
            {
                sb.Append(b ? "true" : "false");
            }
            else
            {
                sb.Append(kvp.Value);
            }
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string BuildWhereClause(Dictionary<string, object>? filters, string nodeAlias = "n")
    {
        if (filters == null || filters.Count == 0)
        {
            return string.Empty;
        }

        var conditions = new List<string>();
        foreach (var kvp in filters)
        {
            if (kvp.Value is string str)
            {
                conditions.Add($"{nodeAlias}.{kvp.Key} = '{str.Replace("'", "\\'")}'");
            }
            else
            {
                conditions.Add($"{nodeAlias}.{kvp.Key} = {kvp.Value}");
            }
        }

        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
    }

    private static string BuildSetClause(Dictionary<string, object> properties)
    {
        return SerializeProperties(properties);
    }

    // ==================== Disposal ====================

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        _initLock?.Dispose();
    }
}
