// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

[Trait("Category", "Unit")]
public sealed class GeoJsonExportNodeTests
{
    private readonly GeoJsonExportNode _node;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public GeoJsonExportNodeTests()
    {
        _node = new GeoJsonExportNode(NullLogger<GeoJsonExportNode>.Instance);
    }

    [Fact]
    public void NodeType_ReturnsCorrectType()
    {
        Assert.Equal("data_sink.geojson", _node.NodeType);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.Equal("GeoJSON Export", _node.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_WithFeatures_ExportsGeoJson()
    {
        var inputFeatures = new List<Dictionary<string, object>>
        {
            new()
            {
                ["id"] = "feature-1",
                ["geometry"] = "{\"type\":\"Point\",\"coordinates\":[0.0,0.0]}",
                ["name"] = "Feature 1",
                ["value"] = 42
            },
            new()
            {
                ["id"] = "feature-2",
                ["geometry"] = "{\"type\":\"Point\",\"coordinates\":[1.0,1.0]}",
                ["name"] = "Feature 2",
                ["value"] = 100
            }
        };

        var context = CreateContext(
            new Dictionary<string, object>(),
            inputFeatures);

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.True(result.Data.ContainsKey("geojson"));
        Assert.True(result.Data.ContainsKey("count"));
        Assert.True(result.Data.ContainsKey("size_bytes"));
        Assert.Equal(2, result.Data["count"]);
        Assert.Equal(2L, result.FeaturesProcessed);

        var geojson = result.Data["geojson"] as string;
        Assert.NotNull(geojson);

        var doc = JsonDocument.Parse(geojson);
        Assert.Equal("FeatureCollection", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_PrettyPrintFalse_ProducesCompactJson()
    {
        var inputFeatures = new List<Dictionary<string, object>>
        {
            new()
            {
                ["id"] = "feature-1",
                ["geometry"] = "{\"type\":\"Point\",\"coordinates\":[0.0,0.0]}",
                ["name"] = "Test"
            }
        };

        var context = CreateContext(
            new Dictionary<string, object> { ["pretty"] = false },
            inputFeatures);

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        var geojson = result.Data["geojson"] as string;
        Assert.NotNull(geojson);
        Assert.DoesNotContain("\n", geojson); // No newlines in compact JSON
    }

    [Fact]
    public async Task ExecuteAsync_WithoutInput_Fails()
    {
        var context = new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_sink.geojson",
                Parameters = new Dictionary<string, object>()
            },
            Parameters = new Dictionary<string, object>(),
            Inputs = new Dictionary<string, NodeExecutionResult>(), // No inputs
            TenantId = _tenantId,
            UserId = _userId
        };

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("No input node connected", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InputWithoutFeatures_Fails()
    {
        var context = CreateContext(
            new Dictionary<string, object>(),
            null); // No features in input

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("does not contain 'features' data", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFeatures_ExportsEmptyCollection()
    {
        var context = CreateContext(
            new Dictionary<string, object>(),
            new List<Dictionary<string, object>>());

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data["count"]);
        Assert.Equal(0L, result.FeaturesProcessed);

        var geojson = result.Data["geojson"] as string;
        Assert.NotNull(geojson);

        var doc = JsonDocument.Parse(geojson);
        Assert.Equal(0, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task ExecuteAsync_FeaturesWithProperties_PreservesProperties()
    {
        var inputFeatures = new List<Dictionary<string, object>>
        {
            new()
            {
                ["id"] = "feature-1",
                ["geometry"] = "{\"type\":\"Point\",\"coordinates\":[0.0,0.0]}",
                ["name"] = "Test",
                ["value"] = 42,
                ["enabled"] = true
            }
        };

        var context = CreateContext(
            new Dictionary<string, object>(),
            inputFeatures);

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        var geojson = result.Data["geojson"] as string;
        var doc = JsonDocument.Parse(geojson!);

        var feature = doc.RootElement.GetProperty("features")[0];
        var properties = feature.GetProperty("properties");

        Assert.Equal("Test", properties.GetProperty("name").GetString());
        Assert.Equal(42, properties.GetProperty("value").GetInt32());
        Assert.True(properties.GetProperty("enabled").GetBoolean());
    }

    private NodeExecutionContext CreateContext(
        Dictionary<string, object> parameters,
        List<Dictionary<string, object>>? inputFeatures)
    {
        var context = new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_sink.geojson",
                Parameters = parameters
            },
            Parameters = new Dictionary<string, object>(),
            TenantId = _tenantId,
            UserId = _userId
        };

        if (inputFeatures != null)
        {
            context.Inputs = new Dictionary<string, NodeExecutionResult>
            {
                ["upstream"] = new NodeExecutionResult
                {
                    Success = true,
                    Data = new Dictionary<string, object>
                    {
                        ["features"] = inputFeatures
                    }
                }
            };
        }
        else
        {
            context.Inputs = new Dictionary<string, NodeExecutionResult>
            {
                ["upstream"] = new NodeExecutionResult
                {
                    Success = true,
                    Data = new Dictionary<string, object>()
                }
            };
        }

        return context;
    }
}

[Trait("Category", "Unit")]
public sealed class OutputNodeTests
{
    private readonly OutputNode _node;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public OutputNodeTests()
    {
        _node = new OutputNode(NullLogger<OutputNode>.Instance);
    }

    [Fact]
    public void NodeType_ReturnsCorrectType()
    {
        Assert.Equal("data_sink.output", _node.NodeType);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.Equal("Output", _node.DisplayName);
    }

    [Fact]
    public async Task ExecuteAsync_WithInput_StoresInState()
    {
        var inputData = new Dictionary<string, object>
        {
            ["features"] = new List<object>(),
            ["count"] = 10
        };

        var state = new Dictionary<string, object>();
        var context = CreateContext(
            new Dictionary<string, object>(),
            inputData,
            state);

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.True(result.Data.ContainsKey("output_name"));
        Assert.True(result.Data.ContainsKey("stored"));
        Assert.Equal("output", result.Data["output_name"]);
        Assert.Equal(true, result.Data["stored"]);

        // Verify data was stored in state
        Assert.True(state.ContainsKey("output.output"));
        Assert.Same(inputData, state["output.output"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomName_UsesCustomName()
    {
        var inputData = new Dictionary<string, object>
        {
            ["result"] = "test"
        };

        var state = new Dictionary<string, object>();
        var context = CreateContext(
            new Dictionary<string, object> { ["name"] = "myOutput" },
            inputData,
            state);

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal("myOutput", result.Data["output_name"]);
        Assert.True(state.ContainsKey("output.myOutput"));
        Assert.Same(inputData, state["output.myOutput"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutInput_Fails()
    {
        var context = new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_sink.output",
                Parameters = new Dictionary<string, object>()
            },
            Parameters = new Dictionary<string, object>(),
            Inputs = new Dictionary<string, NodeExecutionResult>(), // No inputs
            State = new Dictionary<string, object>(),
            TenantId = _tenantId,
            UserId = _userId
        };

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("No input node connected", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleOutputs_StoresIndependently()
    {
        var inputData1 = new Dictionary<string, object> { ["result"] = "data1" };
        var inputData2 = new Dictionary<string, object> { ["result"] = "data2" };

        var state = new Dictionary<string, object>();

        var context1 = CreateContext(
            new Dictionary<string, object> { ["name"] = "output1" },
            inputData1,
            state);

        var result1 = await _node.ExecuteAsync(context1);
        Assert.True(result1.Success);

        var context2 = CreateContext(
            new Dictionary<string, object> { ["name"] = "output2" },
            inputData2,
            state);

        var result2 = await _node.ExecuteAsync(context2);
        Assert.True(result2.Success);

        Assert.True(state.ContainsKey("output.output1"));
        Assert.True(state.ContainsKey("output.output2"));
        Assert.Same(inputData1, state["output.output1"]);
        Assert.Same(inputData2, state["output.output2"]);
    }

    private NodeExecutionContext CreateContext(
        Dictionary<string, object> parameters,
        Dictionary<string, object> inputData,
        Dictionary<string, object> state)
    {
        return new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_sink.output",
                Parameters = parameters
            },
            Parameters = new Dictionary<string, object>(),
            Inputs = new Dictionary<string, NodeExecutionResult>
            {
                ["upstream"] = new NodeExecutionResult
                {
                    Success = true,
                    Data = inputData
                }
            },
            State = state,
            TenantId = _tenantId,
            UserId = _userId
        };
    }
}

[Trait("Category", "Unit")]
public sealed class PostGisDataSinkNodeTests
{
    private readonly PostGisDataSinkNode _node;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PostGisDataSinkNodeTests()
    {
        _node = new PostGisDataSinkNode(
            "Host=localhost;Database=test;Username=test;Password=test",
            NullLogger<PostGisDataSinkNode>.Instance);
    }

    [Fact]
    public void NodeType_ReturnsCorrectType()
    {
        Assert.Equal("data_sink.postgis", _node.NodeType);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.Equal("PostGIS Data Sink", _node.DisplayName);
    }

    #region Validation Tests

    [Fact]
    public async Task ValidateAsync_WithTable_Succeeds()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_sink.postgis",
            Parameters = new Dictionary<string, object>
            {
                ["table"] = "my_table"
            }
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithoutTable_Fails()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_sink.postgis",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.False(result.IsValid);
        Assert.Contains("'table' parameter is required", result.Errors[0]);
    }

    #endregion

    // Note: Execution tests for PostGisDataSinkNode would require a real database connection
    // or advanced mocking of Npgsql. For comprehensive coverage, integration tests with a test
    // database would be more appropriate.
}
