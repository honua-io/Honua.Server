// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

/// <summary>
/// End-to-end integration tests for complete ETL workflows
/// </summary>
[Trait("Category", "Integration")]
public sealed class WorkflowIntegrationTests : IAsyncDisposable
{
    private readonly InMemoryWorkflowStore _store;
    private readonly WorkflowNodeRegistry _registry;
    private readonly WorkflowEngine _engine;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public WorkflowIntegrationTests()
    {
        _store = new InMemoryWorkflowStore();
        _registry = new WorkflowNodeRegistry();

        // Register real nodes
        _registry.RegisterNode("data_source.file", new FileDataSourceNode(NullLogger<FileDataSourceNode>.Instance));
        _registry.RegisterNode("data_sink.geojson", new GeoJsonExportNode(NullLogger<GeoJsonExportNode>.Instance));
        _registry.RegisterNode("data_sink.output", new OutputNode(NullLogger<OutputNode>.Instance));

        _engine = new WorkflowEngine(_store, _registry, NullLogger<WorkflowEngine>.Instance);
    }

    [Fact]
    public async Task EndToEnd_FileSourceToGeoJsonExport_Succeeds()
    {
        // Create a workflow: File Source -> GeoJSON Export -> Output
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata
            {
                Name = "File to GeoJSON Export",
                Description = "Reads GeoJSON file and exports it"
            },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source",
                    Type = "data_source.file",
                    Name = "Read GeoJSON",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Name = "Export GeoJSON",
                    Parameters = new Dictionary<string, object>
                    {
                        ["pretty"] = true
                    }
                },
                new()
                {
                    Id = "output",
                    Type = "data_sink.output",
                    Name = "Store Output",
                    Parameters = new Dictionary<string, object>
                    {
                        ["name"] = "result"
                    }
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "export" },
                new() { From = "export", To = "output" }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        // Execute workflow
        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId,
            TriggerType = WorkflowTriggerType.Manual
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        // Verify execution
        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Equal(3, run.NodeRuns.Count);
        Assert.All(run.NodeRuns, nr => Assert.Equal(NodeRunStatus.Completed, nr.Status));

        // Verify source node processed features
        var sourceRun = run.NodeRuns.First(nr => nr.NodeId == "source");
        Assert.Equal(3L, sourceRun.FeaturesProcessed);

        // Verify export node processed features
        var exportRun = run.NodeRuns.First(nr => nr.NodeId == "export");
        Assert.Equal(3L, exportRun.FeaturesProcessed);
        Assert.NotNull(exportRun.Output);
        Assert.True(exportRun.Output.ContainsKey("geojson"));

        // Verify output was stored in state
        Assert.True(run.State!.ContainsKey("output.result"));
        var outputData = run.State["output.result"] as Dictionary<string, object>;
        Assert.NotNull(outputData);
        Assert.True(outputData.ContainsKey("geojson"));

        // Verify exported GeoJSON is valid
        var geojson = outputData["geojson"] as string;
        Assert.NotNull(geojson);
        var doc = JsonDocument.Parse(geojson);
        Assert.Equal("FeatureCollection", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(3, doc.RootElement.GetProperty("features").GetArrayLength());
    }

    [Fact]
    public async Task EndToEnd_FileSourceToOutput_Succeeds()
    {
        // Simple workflow: File Source -> Output
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata
            {
                Name = "File to Output",
                Description = "Reads file and stores output"
            },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source",
                    Type = "data_source.file",
                    Name = "Read File",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "output",
                    Type = "data_sink.output",
                    Name = "Store Output",
                    Parameters = new Dictionary<string, object>
                    {
                        ["name"] = "features"
                    }
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "output" }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Equal(2, run.NodeRuns.Count);

        // Verify output contains features
        Assert.True(run.State!.ContainsKey("output.features"));
        var outputData = run.State["output.features"] as Dictionary<string, object>;
        Assert.NotNull(outputData);
        Assert.True(outputData.ContainsKey("features"));
        Assert.Equal(3, outputData["count"]);
    }

    [Fact]
    public async Task EndToEnd_MultipleDataSources_MergeToOutput()
    {
        // Complex workflow with multiple sources merging
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata
            {
                Name = "Multi-Source Merge",
                Description = "Multiple sources to single export"
            },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source1",
                    Type = "data_source.file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(1),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "source2",
                    Type = "data_source.file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(2),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "export1",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                },
                new()
                {
                    Id = "export2",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                },
                new()
                {
                    Id = "output1",
                    Type = "data_sink.output",
                    Parameters = new Dictionary<string, object>
                    {
                        ["name"] = "result1"
                    }
                },
                new()
                {
                    Id = "output2",
                    Type = "data_sink.output",
                    Parameters = new Dictionary<string, object>
                    {
                        ["name"] = "result2"
                    }
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source1", To = "export1" },
                new() { From = "source2", To = "export2" },
                new() { From = "export1", To = "output1" },
                new() { From = "export2", To = "output2" }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Equal(6, run.NodeRuns.Count);
        Assert.All(run.NodeRuns, nr => Assert.Equal(NodeRunStatus.Completed, nr.Status));

        // Verify both outputs exist
        Assert.True(run.State!.ContainsKey("output.result1"));
        Assert.True(run.State.ContainsKey("output.result2"));
    }

    [Fact]
    public async Task EndToEnd_ProgressReporting_WorksCorrectly()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata { Name = "Progress Test" },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source",
                    Type = "data_source.file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                },
                new()
                {
                    Id = "output",
                    Type = "data_sink.output",
                    Parameters = new Dictionary<string, object> { ["name"] = "result" }
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "export" },
                new() { From = "export", To = "output" }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        var progressReports = new List<WorkflowProgress>();
        var progress = new Progress<WorkflowProgress>(p => progressReports.Add(p));

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId,
            ProgressCallback = progress
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.NotEmpty(progressReports);

        // Should have progress for each node
        Assert.True(progressReports.Count >= 3);

        // Final progress should be 100%
        var finalProgress = progressReports.Last();
        Assert.Equal(100, finalProgress.ProgressPercent);
        Assert.Equal(3, finalProgress.NodesCompleted);
        Assert.Equal(3, finalProgress.TotalNodes);
    }

    [Fact]
    public async Task EndToEnd_ValidationBeforeExecution_CatchesErrors()
    {
        // Invalid workflow with missing input
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata { Name = "Invalid Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Failed, run.Status);
        // The export node will fail because it has no input
    }

    [Fact]
    public async Task EndToEnd_EstimateBeforeExecution_ProvidesAccurateEstimate()
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata { Name = "Estimate Test" },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source",
                    Type = "data_source.file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "export" }
            }
        };

        var estimate = await _engine.EstimateAsync(workflow);

        Assert.NotNull(estimate);
        Assert.True(estimate.TotalDurationSeconds > 0);
        Assert.True(estimate.PeakMemoryMB > 0);
        Assert.Equal(2, estimate.NodeEstimates.Count);
        Assert.NotNull(estimate.CriticalPath);
        Assert.Equal(new[] { "source", "export" }, estimate.CriticalPath);
    }

    [Fact]
    public async Task EndToEnd_ComplexDAG_ExecutesInCorrectOrder()
    {
        // Diamond DAG:
        //       source
        //      /      \
        //  export1  export2
        //      \      /
        //      output
        var workflow = new WorkflowDefinition
        {
            TenantId = _tenantId,
            CreatedBy = _userId,
            Metadata = new WorkflowMetadata { Name = "Diamond DAG" },
            Nodes = new List<WorkflowNode>
            {
                new()
                {
                    Id = "source",
                    Type = "data_source.file",
                    Parameters = new Dictionary<string, object>
                    {
                        ["content"] = CreateSampleGeoJson(),
                        ["format"] = "geojson"
                    }
                },
                new()
                {
                    Id = "export1",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                },
                new()
                {
                    Id = "export2",
                    Type = "data_sink.geojson",
                    Parameters = new Dictionary<string, object>()
                },
                new()
                {
                    Id = "output",
                    Type = "data_sink.output",
                    Parameters = new Dictionary<string, object> { ["name"] = "final" }
                }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "export1" },
                new() { From = "source", To = "export2" },
                new() { From = "export1", To = "output" },
                new() { From = "export2", To = "output" }
            }
        };

        await _store.CreateWorkflowAsync(workflow);

        var options = new WorkflowExecutionOptions
        {
            TenantId = _tenantId,
            UserId = _userId
        };

        var run = await _engine.ExecuteAsync(workflow, options);

        Assert.Equal(WorkflowRunStatus.Completed, run.Status);
        Assert.Equal(4, run.NodeRuns.Count);

        // Verify execution order
        var sourceRun = run.NodeRuns.First(nr => nr.NodeId == "source");
        var export1Run = run.NodeRuns.First(nr => nr.NodeId == "export1");
        var export2Run = run.NodeRuns.First(nr => nr.NodeId == "export2");
        var outputRun = run.NodeRuns.First(nr => nr.NodeId == "output");

        // Source must complete before exports
        Assert.True(sourceRun.CompletedAt < export1Run.StartedAt);
        Assert.True(sourceRun.CompletedAt < export2Run.StartedAt);

        // Both exports must complete before output
        Assert.True(export1Run.CompletedAt < outputRun.StartedAt);
        Assert.True(export2Run.CompletedAt < outputRun.StartedAt);
    }

    private string CreateSampleGeoJson(int seed = 0)
    {
        return $$"""
        {
            "type": "FeatureCollection",
            "features": [
                {
                    "type": "Feature",
                    "id": "feature-{{seed}}-1",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [{{seed}}.0, 0.0]
                    },
                    "properties": {
                        "name": "Feature {{seed}}-1",
                        "value": {{seed * 10 + 1}}
                    }
                },
                {
                    "type": "Feature",
                    "id": "feature-{{seed}}-2",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [{{seed}}.0, 1.0]
                    },
                    "properties": {
                        "name": "Feature {{seed}}-2",
                        "value": {{seed * 10 + 2}}
                    }
                },
                {
                    "type": "Feature",
                    "id": "feature-{{seed}}-3",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [{{seed}}.0, 2.0]
                    },
                    "properties": {
                        "name": "Feature {{seed}}-3",
                        "value": {{seed * 10 + 3}}
                    }
                }
            ]
        }
        """;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
