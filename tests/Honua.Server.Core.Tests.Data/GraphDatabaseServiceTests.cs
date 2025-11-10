// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Models.Graph;
using Honua.Server.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data;

/// <summary>
/// Unit tests for GraphDatabaseService.
/// These tests require a PostgreSQL instance with Apache AGE extension installed.
/// Use the skip attribute if the environment is not available.
/// </summary>
public class GraphDatabaseServiceTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GraphDatabaseService> _logger;
    private GraphDatabaseService? _service;
    private readonly string _testGraphName = $"test_graph_{Guid.NewGuid():N}";
    private bool _skipTests = false;

    public GraphDatabaseServiceTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<GraphDatabaseService>();
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Check if PostgreSQL with AGE is available
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_AGE_CONNECTION_STRING")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

            var options = Options.Create(new GraphDatabaseOptions
            {
                Enabled = true,
                ConnectionString = connectionString,
                DefaultGraphName = _testGraphName,
                AutoCreateGraph = true,
                EnableSchemaInitialization = true,
                LogQueries = true
            });

            _service = new GraphDatabaseService(options, _logger);

            // Test connection by checking if graph exists
            await _service.GraphExistsAsync(_testGraphName);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Skipping tests - PostgreSQL with AGE not available: {ex.Message}");
            _skipTests = true;
        }
    }

    public async Task DisposeAsync()
    {
        if (_service != null && !_skipTests)
        {
            try
            {
                // Clean up test graph
                var exists = await _service.GraphExistsAsync(_testGraphName);
                if (exists)
                {
                    await _service.DropGraphAsync(_testGraphName);
                }

                await _service.DisposeAsync();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private void SkipIfNotAvailable()
    {
        if (_skipTests)
        {
            throw new SkipException("PostgreSQL with Apache AGE is not available");
        }
    }

    [Fact]
    public async Task CreateGraph_ShouldSucceed()
    {
        SkipIfNotAvailable();

        // Arrange
        var graphName = $"test_create_graph_{Guid.NewGuid():N}";

        try
        {
            // Act
            await _service!.CreateGraphAsync(graphName);
            var exists = await _service.GraphExistsAsync(graphName);

            // Assert
            Assert.True(exists);
        }
        finally
        {
            // Cleanup
            try
            {
                await _service!.DropGraphAsync(graphName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task CreateNode_ShouldReturnNodeWithId()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("Building")
        {
            Properties = new Dictionary<string, object>
            {
                ["id"] = "bldg-001",
                ["name"] = "Test Building",
                ["floors"] = 10
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);

        // Assert
        Assert.NotNull(createdNode);
        Assert.NotNull(createdNode.Id);
        Assert.True(createdNode.Id > 0);
        Assert.Equal("Building", createdNode.Label);
        Assert.Equal("Test Building", createdNode.Properties["name"]);
    }

    [Fact]
    public async Task GetNodeById_ShouldReturnCorrectNode()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("Floor")
        {
            Properties = new Dictionary<string, object>
            {
                ["level"] = 1,
                ["area_sqm"] = 1200
            }
        };
        var createdNode = await _service!.CreateNodeAsync(node);

        // Act
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal(createdNode.Id, retrievedNode.Id);
        Assert.Equal("Floor", retrievedNode.Label);
        Assert.Equal(1L, Convert.ToInt64(retrievedNode.Properties["level"]));
    }

    [Fact]
    public async Task FindNodes_ShouldReturnMatchingNodes()
    {
        SkipIfNotAvailable();

        // Arrange
        await _service!.CreateNodeAsync(new GraphNode("Room") { Properties = new() { ["number"] = "101" } });
        await _service.CreateNodeAsync(new GraphNode("Room") { Properties = new() { ["number"] = "102" } });
        await _service.CreateNodeAsync(new GraphNode("Equipment") { Properties = new() { ["type"] = "hvac" } });

        // Act
        var rooms = await _service.FindNodesAsync("Room");

        // Assert
        Assert.NotNull(rooms);
        Assert.True(rooms.Count >= 2);
        Assert.All(rooms, r => Assert.Equal("Room", r.Label));
    }

    [Fact]
    public async Task UpdateNode_ShouldModifyProperties()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("Equipment") { Properties = new() { ["type"] = "sensor", ["status"] = "active" } };
        var createdNode = await _service!.CreateNodeAsync(node);

        // Act
        await _service.UpdateNodeAsync(createdNode.Id!.Value, new Dictionary<string, object>
        {
            ["status"] = "inactive",
            ["last_updated"] = DateTime.UtcNow.ToString("O")
        });

        var updatedNode = await _service.GetNodeByIdAsync(createdNode.Id.Value);

        // Assert
        Assert.NotNull(updatedNode);
        Assert.Equal("inactive", updatedNode.Properties["status"]);
    }

    [Fact]
    public async Task CreateEdge_ShouldCreateRelationship()
    {
        SkipIfNotAvailable();

        // Arrange
        var building = await _service!.CreateNodeAsync(new GraphNode("Building") { Properties = new() { ["name"] = "Tower A" } });
        var floor = await _service.CreateNodeAsync(new GraphNode("Floor") { Properties = new() { ["level"] = 1 } });

        var edge = new GraphEdge
        {
            Type = "CONTAINS",
            StartNodeId = building.Id!.Value,
            EndNodeId = floor.Id!.Value,
            Properties = new Dictionary<string, object>
            {
                ["sequence"] = 1
            }
        };

        // Act
        var createdEdge = await _service.CreateEdgeAsync(edge);

        // Assert
        Assert.NotNull(createdEdge);
        Assert.NotNull(createdEdge.Id);
        Assert.Equal("CONTAINS", createdEdge.Type);
        Assert.Equal(building.Id.Value, createdEdge.StartNodeId);
        Assert.Equal(floor.Id.Value, createdEdge.EndNodeId);
    }

    [Fact]
    public async Task GetNodeRelationships_ShouldReturnEdges()
    {
        SkipIfNotAvailable();

        // Arrange
        var building = await _service!.CreateNodeAsync(new GraphNode("Building") { Properties = new() { ["name"] = "Building X" } });
        var floor1 = await _service.CreateNodeAsync(new GraphNode("Floor") { Properties = new() { ["level"] = 1 } });
        var floor2 = await _service.CreateNodeAsync(new GraphNode("Floor") { Properties = new() { ["level"] = 2 } });

        await _service.CreateEdgeAsync(new GraphEdge("CONTAINS", building.Id!.Value, floor1.Id!.Value));
        await _service.CreateEdgeAsync(new GraphEdge("CONTAINS", building.Id.Value, floor2.Id!.Value));

        // Act
        var relationships = await _service.GetNodeRelationshipsAsync(building.Id.Value, "CONTAINS", TraversalDirection.Outgoing);

        // Assert
        Assert.NotNull(relationships);
        Assert.True(relationships.Count >= 2);
        Assert.All(relationships, r => Assert.Equal("CONTAINS", r.Type));
    }

    [Fact]
    public async Task DeleteNode_ShouldRemoveNodeAndRelationships()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = await _service!.CreateNodeAsync(new GraphNode("TempNode") { Properties = new() { ["temp"] = true } });

        // Act
        await _service.DeleteNodeAsync(node.Id!.Value);
        var deletedNode = await _service.GetNodeByIdAsync(node.Id.Value);

        // Assert
        Assert.Null(deletedNode);
    }

    [Fact]
    public async Task ExecuteCypherQuery_ShouldReturnResults()
    {
        SkipIfNotAvailable();

        // Arrange
        await _service!.CreateNodeAsync(new GraphNode("Person") { Properties = new() { ["name"] = "Alice", ["age"] = 30 } });
        await _service.CreateNodeAsync(new GraphNode("Person") { Properties = new() { ["name"] = "Bob", ["age"] = 25 } });

        // Act
        var query = "MATCH (p:Person) RETURN p";
        var result = await _service.ExecuteCypherQueryAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Nodes.Count >= 2);
        Assert.NotNull(result.ExecutionTimeMs);
    }

    [Fact]
    public async Task CreateNodesBatch_ShouldCreateMultipleNodes()
    {
        SkipIfNotAvailable();

        // Arrange
        var nodes = new List<GraphNode>
        {
            new("Sensor") { Properties = new() { ["id"] = "s1", ["type"] = "temperature" } },
            new("Sensor") { Properties = new() { ["id"] = "s2", ["type"] = "humidity" } },
            new("Sensor") { Properties = new() { ["id"] = "s3", ["type"] = "pressure" } }
        };

        // Act
        var createdNodes = await _service!.CreateNodesAsync(nodes);

        // Assert
        Assert.Equal(3, createdNodes.Count);
        Assert.All(createdNodes, n =>
        {
            Assert.NotNull(n.Id);
            Assert.Equal("Sensor", n.Label);
        });
    }

    [Fact]
    public async Task TraverseGraph_ShouldFindConnectedNodes()
    {
        SkipIfNotAvailable();

        // Arrange
        var root = await _service!.CreateNodeAsync(new GraphNode("Root") { Properties = new() { ["id"] = "root" } });
        var child1 = await _service.CreateNodeAsync(new GraphNode("Child") { Properties = new() { ["id"] = "c1" } });
        var child2 = await _service.CreateNodeAsync(new GraphNode("Child") { Properties = new() { ["id"] = "c2" } });

        await _service.CreateEdgeAsync(new GraphEdge("LINKS_TO", root.Id!.Value, child1.Id!.Value));
        await _service.CreateEdgeAsync(new GraphEdge("LINKS_TO", root.Id.Value, child2.Id!.Value));

        // Act
        var result = await _service.TraverseGraphAsync(
            root.Id.Value,
            new[] { "LINKS_TO" },
            TraversalDirection.Outgoing,
            maxDepth: 2);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Nodes.Count >= 2); // Should find child nodes
    }
}

/// <summary>
/// Custom exception to skip tests when prerequisites are not available.
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
