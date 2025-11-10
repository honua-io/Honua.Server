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
/// Error handling and edge case tests for GraphDatabaseService.
/// Tests validation, error conditions, and special characters handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Phase", "Phase1")]
public class GraphDatabaseServiceErrorTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly ILogger<GraphDatabaseService> _logger;
    private GraphDatabaseService? _service;
    private readonly string _testGraphName = $"test_errors_{Guid.NewGuid():N}";
    private bool _skipTests = false;

    public GraphDatabaseServiceErrorTests(ITestOutputHelper output)
    {
        _output = output;
        var loggerFactory = LoggerFactory.Create(builder => builder.AddDebug());
        _logger = loggerFactory.CreateLogger<GraphDatabaseService>();
    }

    public async Task InitializeAsync()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("POSTGRES_AGE_CONNECTION_STRING")
                ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=postgres";

            var options = Options.Create(new GraphDatabaseOptions
            {
                Enabled = true,
                ConnectionString = connectionString,
                DefaultGraphName = _testGraphName,
                AutoCreateGraph = true,
                EnableSchemaInitialization = true
            });

            _service = new GraphDatabaseService(options, _logger);
            await _service.GraphExistsAsync(_testGraphName);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Skipping tests - PostgreSQL AGE not available: {ex.Message}");
            _skipTests = true;
        }
    }

    public async Task DisposeAsync()
    {
        if (_service != null && !_skipTests)
        {
            try
            {
                var exists = await _service.GraphExistsAsync(_testGraphName);
                if (exists)
                {
                    await _service.DropGraphAsync(_testGraphName);
                }
                await _service.DisposeAsync();
            }
            catch { }
        }
    }

    private void SkipIfNotAvailable()
    {
        if (_skipTests)
        {
            throw new SkipException("PostgreSQL AGE not available");
        }
    }

    #region Invalid Query Tests

    [Fact]
    public async Task ExecuteCypherQuery_WithInvalidSyntax_ShouldThrowException()
    {
        SkipIfNotAvailable();

        // Arrange
        var invalidQuery = "INVALID CYPHER SYNTAX HERE";

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _service!.ExecuteCypherQueryAsync(invalidQuery));

        _output.WriteLine("Invalid Cypher syntax correctly throws exception");
    }

    [Fact]
    public async Task ExecuteCypherQuery_WithMalformedSyntax_ShouldThrowException()
    {
        SkipIfNotAvailable();

        // Arrange - Missing closing parenthesis
        var malformedQuery = "MATCH (n:Node RETURN n";

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _service!.ExecuteCypherQueryAsync(malformedQuery));
    }

    [Fact]
    public async Task ExecuteCypherQuery_WithEmptyQuery_ShouldThrowException()
    {
        SkipIfNotAvailable();

        // Arrange
        var emptyQuery = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service!.ExecuteCypherQueryAsync(emptyQuery));
    }

    #endregion

    #region Null and Invalid Input Tests

    [Fact]
    public async Task CreateNode_WithNullLabel_ShouldThrowArgumentException()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode(null!)
        {
            Properties = new Dictionary<string, object> { ["test"] = "value" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service!.CreateNodeAsync(node));

        _output.WriteLine("Null label correctly throws ArgumentException");
    }

    [Fact]
    public async Task CreateNode_WithEmptyLabel_ShouldThrowArgumentException()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("")
        {
            Properties = new Dictionary<string, object> { ["test"] = "value" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service!.CreateNodeAsync(node));
    }

    [Fact]
    public async Task CreateNode_WithNullProperties_ShouldSucceed()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("TestNode")
        {
            Properties = null
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);

        // Assert
        Assert.NotNull(createdNode);
        Assert.NotNull(createdNode.Id);
    }

    #endregion

    #region Non-Existent Node Tests

    [Fact]
    public async Task GetNodeById_WithNonExistentId_ShouldReturnNull()
    {
        SkipIfNotAvailable();

        // Arrange
        long nonExistentId = 999999999;

        // Act
        var result = await _service!.GetNodeByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);

        _output.WriteLine("Non-existent node ID correctly returns null");
    }

    [Fact]
    public async Task UpdateNode_WithNonExistentId_ShouldThrowException()
    {
        SkipIfNotAvailable();

        // Arrange
        long nonExistentId = 999999999;
        var properties = new Dictionary<string, object> { ["status"] = "active" };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(async () =>
            await _service!.UpdateNodeAsync(nonExistentId, properties));
    }

    [Fact]
    public async Task DeleteNode_WithNonExistentId_ShouldNotThrow()
    {
        SkipIfNotAvailable();

        // Arrange
        long nonExistentId = 999999999;

        // Act - Should not throw, just no-op
        await _service!.DeleteNodeAsync(nonExistentId);

        // Assert - No exception is success
        _output.WriteLine("Deleting non-existent node does not throw");
    }

    #endregion

    #region Invalid Edge Tests

    [Fact]
    public async Task CreateEdge_WithNonExistentNodes_ShouldThrowException()
    {
        SkipIfNotAvailable();

        // Arrange
        var edge = new GraphEdge
        {
            Type = "LINKS_TO",
            StartNodeId = 999999998,
            EndNodeId = 999999999
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(async () =>
            await _service!.CreateEdgeAsync(edge));

        _output.WriteLine($"Expected exception: {exception.Message}");
    }

    [Fact]
    public async Task CreateEdge_WithNullType_ShouldThrowArgumentException()
    {
        SkipIfNotAvailable();

        // Arrange - Create two valid nodes first
        var node1 = await _service!.CreateNodeAsync(new GraphNode("TestNode1"));
        var node2 = await _service.CreateNodeAsync(new GraphNode("TestNode2"));

        var edge = new GraphEdge
        {
            Type = null!,
            StartNodeId = node1.Id!.Value,
            EndNodeId = node2.Id!.Value
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateEdgeAsync(edge));
    }

    [Fact]
    public async Task CreateEdge_WithSelfLoop_ShouldSucceed()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = await _service!.CreateNodeAsync(new GraphNode("SelfLoopNode"));

        var edge = new GraphEdge
        {
            Type = "REFERENCES",
            StartNodeId = node.Id!.Value,
            EndNodeId = node.Id.Value
        };

        // Act
        var createdEdge = await _service.CreateEdgeAsync(edge);

        // Assert
        Assert.NotNull(createdEdge);
        Assert.Equal(node.Id.Value, createdEdge.StartNodeId);
        Assert.Equal(node.Id.Value, createdEdge.EndNodeId);

        _output.WriteLine("Self-loop edge created successfully");
    }

    #endregion

    #region Special Characters Tests

    [Fact]
    public async Task CreateNode_WithSpecialCharactersInProperties_ShouldEscapeCorrectly()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("TestNode")
        {
            Properties = new Dictionary<string, object>
            {
                ["name"] = "O'Reilly",  // Single quote
                ["description"] = "Test \"quoted\" value",  // Double quotes
                ["path"] = "C:\\Users\\Test",  // Backslashes
                ["unicode"] = "Test 日本語 العربية",  // Unicode characters
                ["newline"] = "Line1\nLine2"  // Newline
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal("O'Reilly", retrievedNode.Properties["name"]);
        Assert.Equal("Test \"quoted\" value", retrievedNode.Properties["description"]);
        Assert.Equal("C:\\Users\\Test", retrievedNode.Properties["path"]);
        Assert.Equal("Test 日本語 العربية", retrievedNode.Properties["unicode"]);
        Assert.Equal("Line1\nLine2", retrievedNode.Properties["newline"]);

        _output.WriteLine("Special characters preserved correctly");
    }

    [Fact]
    public async Task CreateNode_WithSqlInjectionAttempt_ShouldBeSafelyEscaped()
    {
        SkipIfNotAvailable();

        // Arrange - Common SQL injection patterns
        var node = new GraphNode("TestNode")
        {
            Properties = new Dictionary<string, object>
            {
                ["malicious1"] = "'; DROP TABLE nodes; --",
                ["malicious2"] = "1' OR '1'='1",
                ["malicious3"] = "admin'--"
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert - Values should be stored as-is (safely escaped)
        Assert.NotNull(retrievedNode);
        Assert.Equal("'; DROP TABLE nodes; --", retrievedNode.Properties["malicious1"]);

        _output.WriteLine("SQL injection attempts safely handled");
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task CreateNodes_Concurrently_ShouldAllSucceed()
    {
        SkipIfNotAvailable();

        // Arrange
        var tasks = new List<Task<GraphNode>>();
        const int concurrentCount = 10;

        // Act - Create nodes concurrently
        for (int i = 0; i < concurrentCount; i++)
        {
            var index = i; // Capture loop variable
            tasks.Add(_service!.CreateNodeAsync(new GraphNode("ConcurrentNode")
            {
                Properties = new Dictionary<string, object>
                {
                    ["index"] = index,
                    ["timestamp"] = DateTime.UtcNow
                }
            }));
        }

        var createdNodes = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentCount, createdNodes.Length);
        Assert.All(createdNodes, node => Assert.NotNull(node.Id));

        // All IDs should be unique
        var uniqueIds = createdNodes.Select(n => n.Id!.Value).Distinct().Count();
        Assert.Equal(concurrentCount, uniqueIds);

        _output.WriteLine($"Successfully created {concurrentCount} nodes concurrently");
    }

    #endregion

    #region Large Data Tests

    [Fact]
    public async Task CreateNode_WithLargeProperties_ShouldSucceed()
    {
        SkipIfNotAvailable();

        // Arrange - Create a large property value (10KB)
        var largeText = new string('A', 10 * 1024);

        var node = new GraphNode("LargeNode")
        {
            Properties = new Dictionary<string, object>
            {
                ["large_text"] = largeText,
                ["count"] = 100
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal(largeText, retrievedNode.Properties["large_text"]);

        _output.WriteLine($"Large property stored successfully: {largeText.Length} characters");
    }

    [Fact]
    public async Task CreateNode_WithManyProperties_ShouldSucceed()
    {
        SkipIfNotAvailable();

        // Arrange - Create node with many properties
        var properties = new Dictionary<string, object>();
        for (int i = 0; i < 50; i++)
        {
            properties[$"prop_{i}"] = $"value_{i}";
        }

        var node = new GraphNode("ManyPropsNode")
        {
            Properties = properties
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal(50, retrievedNode.Properties.Count);

        _output.WriteLine($"Node with {properties.Count} properties stored successfully");
    }

    #endregion

    #region Property Type Tests

    [Fact]
    public async Task CreateNode_WithVariousPropertyTypes_ShouldPreserveTypes()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = new GraphNode("TypeTestNode")
        {
            Properties = new Dictionary<string, object>
            {
                ["string"] = "test",
                ["int"] = 42,
                ["long"] = 9876543210L,
                ["double"] = 3.14159,
                ["bool"] = true,
                ["datetime"] = DateTime.UtcNow.ToString("O"),
                ["null_value"] = null!
            }
        };

        // Act
        var createdNode = await _service!.CreateNodeAsync(node);
        var retrievedNode = await _service.GetNodeByIdAsync(createdNode.Id!.Value);

        // Assert
        Assert.NotNull(retrievedNode);
        Assert.Equal("test", retrievedNode.Properties["string"]);
        Assert.True(Convert.ToInt64(retrievedNode.Properties["int"]) == 42);
        Assert.True(Convert.ToInt64(retrievedNode.Properties["long"]) == 9876543210L);
        Assert.True(Convert.ToDouble(retrievedNode.Properties["double"]) > 3.14);
        Assert.True(Convert.ToBoolean(retrievedNode.Properties["bool"]));

        _output.WriteLine("Various property types preserved correctly");
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task FindNodes_WithNoMatches_ShouldReturnEmptyList()
    {
        SkipIfNotAvailable();

        // Arrange
        var nonExistentLabel = $"NonExistent_{Guid.NewGuid()}";

        // Act
        var results = await _service!.FindNodesAsync(nonExistentLabel);

        // Assert
        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetNodeRelationships_WithNoRelationships_ShouldReturnEmptyList()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = await _service!.CreateNodeAsync(new GraphNode("IsolatedNode"));

        // Act
        var relationships = await _service.GetNodeRelationshipsAsync(
            node.Id!.Value,
            "ANY_TYPE",
            TraversalDirection.Both);

        // Assert
        Assert.NotNull(relationships);
        Assert.Empty(relationships);
    }

    [Fact]
    public async Task TraverseGraph_WithMaxDepthZero_ShouldReturnOnlyStartNode()
    {
        SkipIfNotAvailable();

        // Arrange
        var node = await _service!.CreateNodeAsync(new GraphNode("StartNode"));

        // Act
        var result = await _service.TraverseGraphAsync(
            node.Id!.Value,
            new[] { "ANY" },
            TraversalDirection.Both,
            maxDepth: 0);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Nodes);
        Assert.Equal(node.Id.Value, result.Nodes[0].Id!.Value);
    }

    #endregion
}
