// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

[Trait("Category", "Unit")]
public sealed class FileDataSourceNodeTests
{
    private readonly FileDataSourceNode _node;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public FileDataSourceNodeTests()
    {
        _node = new FileDataSourceNode(NullLogger<FileDataSourceNode>.Instance);
    }

    [Fact]
    public void NodeType_ReturnsCorrectType()
    {
        Assert.Equal("data_source.file", _node.NodeType);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.Equal("File Data Source", _node.DisplayName);
    }

    #region Validation Tests

    [Fact]
    public async Task ValidateAsync_WithContent_Succeeds()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.file",
            Parameters = new Dictionary<string, object>
            {
                ["content"] = "{\"type\": \"FeatureCollection\"}"
            }
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithUrl_Succeeds()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.file",
            Parameters = new Dictionary<string, object>
            {
                ["url"] = "https://example.com/data.geojson"
            }
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithoutContentOrUrl_Fails()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.file",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.False(result.IsValid);
        Assert.Contains("Either 'content' or 'url' parameter is required", result.Errors[0]);
    }

    #endregion

    #region Execution Tests

    [Fact]
    public async Task ExecuteAsync_ValidGeoJsonFeatureCollection_ParsesFeatures()
    {
        var geojson = """
        {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [0.0, 0.0]
                    },
                    "properties": {
                        "name": "Test Feature",
                        "value": 42
                    }
                },
                {
                    "type": "Feature",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [1.0, 1.0]
                    },
                    "properties": {
                        "name": "Another Feature",
                        "value": 100
                    }
                }
            ]
        }
        """;

        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = geojson,
            ["format"] = "geojson"
        });

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.True(result.Data.ContainsKey("features"));
        Assert.True(result.Data.ContainsKey("count"));
        Assert.Equal(2, result.Data["count"]);
        Assert.Equal(2L, result.FeaturesProcessed);

        var features = result.Data["features"] as List<Dictionary<string, object>>;
        Assert.NotNull(features);
        Assert.Equal(2, features.Count);
        Assert.Equal("Test Feature", features[0]["name"]);
        Assert.Equal(42L, features[0]["value"]);
    }

    [Fact]
    public async Task ExecuteAsync_SingleGeoJsonFeature_ParsesFeature()
    {
        var geojson = """
        {
            "type": "Feature",
            "geometry": {
                "type": "Point",
                "coordinates": [0.0, 0.0]
            },
            "properties": {
                "name": "Single Feature",
                "id": 123
            },
            "id": "feature-1"
        }
        """;

        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = geojson
        });

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data["count"]);
        Assert.Equal(1L, result.FeaturesProcessed);

        var features = result.Data["features"] as List<Dictionary<string, object>>;
        Assert.NotNull(features);
        Assert.Single(features);
        Assert.Equal("Single Feature", features[0]["name"]);
        Assert.Equal(123L, features[0]["id"]);
        Assert.Equal("feature-1", features[0]["id"]);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureWithId_PreservesId()
    {
        var geojson = """
        {
            "type": "Feature",
            "id": "my-feature-id",
            "geometry": {
                "type": "Point",
                "coordinates": [0.0, 0.0]
            },
            "properties": {
                "name": "Test"
            }
        }
        """;

        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = geojson
        });

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        var features = result.Data["features"] as List<Dictionary<string, object>>;
        Assert.NotNull(features);
        Assert.Single(features);
        Assert.True(features[0].ContainsKey("id"));
        Assert.Equal("my-feature-id", features[0]["id"]);
    }

    [Fact]
    public async Task ExecuteAsync_FeatureWithComplexProperties_ParsesCorrectly()
    {
        var geojson = """
        {
            "type": "Feature",
            "geometry": {
                "type": "Point",
                "coordinates": [0.0, 0.0]
            },
            "properties": {
                "string": "text",
                "number": 42,
                "float": 3.14,
                "boolean": true,
                "null": null,
                "array": [1, 2, 3],
                "object": { "nested": "value" }
            }
        }
        """;

        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = geojson
        });

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        var features = result.Data["features"] as List<Dictionary<string, object>>;
        Assert.NotNull(features);
        Assert.Single(features);

        var feature = features[0];
        Assert.Equal("text", feature["string"]);
        Assert.Equal(42L, feature["number"]);
        Assert.Equal(3.14, feature["float"]);
        Assert.Equal(true, feature["boolean"]);
        Assert.Null(feature["null"]);
        Assert.IsType<object[]>(feature["array"]);
        Assert.IsType<Dictionary<string, object>>(feature["object"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutContent_Fails()
    {
        var context = CreateContext(new Dictionary<string, object>
        {
            ["url"] = "https://example.com/data.geojson"
        });

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("Either 'content' or 'url' parameter is required", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedFormat_Fails()
    {
        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = "some content",
            ["format"] = "shapefile"
        });

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("not yet supported", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_Fails()
    {
        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = "{ invalid json }"
        });

        var result = await _node.ExecuteAsync(context);

        Assert.False(result.Success);
        Assert.Contains("parsing failed", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFeatureCollection_ReturnsEmptyList()
    {
        var geojson = """
        {
            "type": "FeatureCollection",
            "features": []
        }
        """;

        var context = CreateContext(new Dictionary<string, object>
        {
            ["content"] = geojson
        });

        var result = await _node.ExecuteAsync(context);

        Assert.True(result.Success);
        Assert.Equal(0, result.Data["count"]);
        Assert.Equal(0L, result.FeaturesProcessed);
    }

    #endregion

    #region Helper Methods

    private NodeExecutionContext CreateContext(Dictionary<string, object> parameters)
    {
        return new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_source.file",
                Parameters = parameters
            },
            Parameters = new Dictionary<string, object>(),
            Inputs = new Dictionary<string, NodeExecutionResult>(),
            TenantId = _tenantId,
            UserId = _userId
        };
    }

    #endregion
}

[Trait("Category", "Unit")]
public sealed class PostGisDataSourceNodeTests
{
    private readonly PostGisDataSourceNode _node;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public PostGisDataSourceNodeTests()
    {
        _node = new PostGisDataSourceNode(
            "Host=localhost;Database=test;Username=test;Password=test",
            NullLogger<PostGisDataSourceNode>.Instance);
    }

    [Fact]
    public void NodeType_ReturnsCorrectType()
    {
        Assert.Equal("data_source.postgis", _node.NodeType);
    }

    [Fact]
    public void DisplayName_ReturnsCorrectName()
    {
        Assert.Equal("PostGIS Data Source", _node.DisplayName);
    }

    #region Validation Tests

    [Fact]
    public async Task ValidateAsync_WithTable_Succeeds()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.postgis",
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
    public async Task ValidateAsync_WithQuery_Succeeds()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.postgis",
            Parameters = new Dictionary<string, object>
            {
                ["query"] = "SELECT * FROM my_table"
            }
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithoutTableOrQuery_Fails()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.postgis",
            Parameters = new Dictionary<string, object>()
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.False(result.IsValid);
        Assert.Contains("Either 'table' or 'query' parameter is required", result.Errors[0]);
    }

    [Fact]
    public async Task ValidateAsync_WithoutGeometryColumn_ProducesWarning()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.postgis",
            Parameters = new Dictionary<string, object>
            {
                ["table"] = "my_table"
            }
        };

        var result = await _node.ValidateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains("geometry_column", result.Warnings[0]);
    }

    #endregion

    #region Estimate Tests

    [Fact]
    public async Task EstimateAsync_ReturnsEstimate()
    {
        var nodeDefinition = new WorkflowNode
        {
            Id = "test",
            Type = "data_source.postgis",
            Parameters = new Dictionary<string, object>
            {
                ["table"] = "my_table"
            }
        };

        var estimate = await _node.EstimateAsync(nodeDefinition, new Dictionary<string, object>());

        Assert.NotNull(estimate);
        Assert.True(estimate.EstimatedDurationSeconds > 0);
        Assert.True(estimate.EstimatedMemoryMB > 0);
    }

    #endregion

    #region Helper Methods

    private NodeExecutionContext CreateContext(Dictionary<string, object> parameters)
    {
        return new NodeExecutionContext
        {
            WorkflowRunId = Guid.NewGuid(),
            NodeRunId = Guid.NewGuid(),
            NodeDefinition = new WorkflowNode
            {
                Id = "test-node",
                Type = "data_source.postgis",
                Parameters = parameters
            },
            Parameters = new Dictionary<string, object>(),
            Inputs = new Dictionary<string, NodeExecutionResult>(),
            TenantId = _tenantId,
            UserId = _userId
        };
    }

    #endregion
}
