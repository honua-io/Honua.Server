// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Honua.Server.Enterprise.ETL.Engine;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.ETL.Nodes;
using Honua.Server.Enterprise.ETL.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Honua.Server.Enterprise.Tests.ETL.Performance;

/// <summary>
/// Performance benchmarks for GeoETL workflows
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class GeoEtlBenchmarks
{
    private WorkflowDefinition _simpleWorkflow = null!;
    private WorkflowDefinition _parallelWorkflow = null!;
    private WorkflowEngine _sequentialEngine = null!;
    private ParallelWorkflowEngine _parallelEngine = null!;
    private IWorkflowNodeRegistry _nodeRegistry = null!;
    private List<IFeature> _testFeatures = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Create test features
        _testFeatures = CreateTestFeatures(1000);

        // Create mock node registry
        _nodeRegistry = new MockNodeRegistry();

        // Create workflow store
        var workflowStore = new InMemoryWorkflowStore();

        // Create engines
        _sequentialEngine = new WorkflowEngine(
            workflowStore,
            _nodeRegistry,
            NullLogger<WorkflowEngine>.Instance);

        _parallelEngine = new ParallelWorkflowEngine(
            workflowStore,
            _nodeRegistry,
            NullLogger<ParallelWorkflowEngine>.Instance,
            new ParallelWorkflowEngineOptions { MaxParallelNodes = 4 });

        // Create test workflows
        _simpleWorkflow = CreateSimpleWorkflow();
        _parallelWorkflow = CreateParallelWorkflow();
    }

    [Benchmark(Baseline = true)]
    public async Task<WorkflowRun> SequentialExecution_SimpleWorkflow()
    {
        return await _sequentialEngine.ExecuteAsync(_simpleWorkflow, new WorkflowExecutionOptions
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        });
    }

    [Benchmark]
    public async Task<WorkflowRun> ParallelExecution_SimpleWorkflow()
    {
        return await _parallelEngine.ExecuteAsync(_simpleWorkflow, new WorkflowExecutionOptions
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        });
    }

    [Benchmark]
    public async Task<WorkflowRun> SequentialExecution_ParallelWorkflow()
    {
        return await _sequentialEngine.ExecuteAsync(_parallelWorkflow, new WorkflowExecutionOptions
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        });
    }

    [Benchmark]
    public async Task<WorkflowRun> ParallelExecution_ParallelWorkflow()
    {
        return await _parallelEngine.ExecuteAsync(_parallelWorkflow, new WorkflowExecutionOptions
        {
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        });
    }

    [Benchmark]
    public async Task<WorkflowValidationResult> ValidateWorkflow()
    {
        return await _parallelEngine.ValidateAsync(_parallelWorkflow);
    }

    [Benchmark]
    public async Task<WorkflowEstimate> EstimateWorkflow()
    {
        return await _parallelEngine.EstimateAsync(_parallelWorkflow);
    }

    private WorkflowDefinition CreateSimpleWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Metadata = new WorkflowMetadata { Name = "Simple Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "source", Type = "mock.source" },
                new() { Id = "transform", Type = "mock.transform" },
                new() { Id = "sink", Type = "mock.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "transform" },
                new() { From = "transform", To = "sink" }
            }
        };
    }

    private WorkflowDefinition CreateParallelWorkflow()
    {
        return new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Metadata = new WorkflowMetadata { Name = "Parallel Workflow" },
            Nodes = new List<WorkflowNode>
            {
                new() { Id = "source", Type = "mock.source" },
                new() { Id = "branch1", Type = "mock.transform" },
                new() { Id = "branch2", Type = "mock.transform" },
                new() { Id = "branch3", Type = "mock.transform" },
                new() { Id = "merge", Type = "mock.sink" }
            },
            Edges = new List<WorkflowEdge>
            {
                new() { From = "source", To = "branch1" },
                new() { From = "source", To = "branch2" },
                new() { From = "source", To = "branch3" },
                new() { From = "branch1", To = "merge" },
                new() { From = "branch2", To = "merge" },
                new() { From = "branch3", To = "merge" }
            }
        };
    }

    private List<IFeature> CreateTestFeatures(int count)
    {
        var features = new List<IFeature>();
        var factory = new GeometryFactory();

        for (int i = 0; i < count; i++)
        {
            var point = factory.CreatePoint(new Coordinate(i, i));
            var attributes = new AttributesTable
            {
                { "id", i },
                { "name", $"Feature {i}" },
                { "value", i * 1.5 }
            };
            features.Add(new Feature(point, attributes));
        }

        return features;
    }

    private class MockNodeRegistry : IWorkflowNodeRegistry
    {
        private readonly Dictionary<string, IWorkflowNode> _nodes = new()
        {
            { "mock.source", new MockNode() },
            { "mock.transform", new MockNode() },
            { "mock.sink", new MockNode() }
        };

        public IWorkflowNode? GetNode(string nodeType) => _nodes.TryGetValue(nodeType, out var node) ? node : null;
        public IEnumerable<IWorkflowNode> GetAllNodes() => _nodes.Values;
        public void RegisterNode(string nodeType, IWorkflowNode node) => _nodes[nodeType] = node;
        public IEnumerable<string> GetAllNodeTypes() => _nodes.Keys;
        public bool IsRegistered(string nodeType) => _nodes.ContainsKey(nodeType);
    }

    private class MockNode : IWorkflowNode
    {
        public string NodeType => "mock";
        public string DisplayName => "Mock Node";
        public string Description => "Mock node for testing";

        public Task<NodeValidationResult> ValidateAsync(
            WorkflowNode nodeDefinition,
            Dictionary<string, object> runtimeParameters,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(NodeValidationResult.Success());
        }

        public Task<NodeEstimate> EstimateAsync(
            WorkflowNode nodeDefinition,
            Dictionary<string, object> runtimeParameters,
            System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NodeEstimate
            {
                EstimatedDurationSeconds = 1,
                EstimatedMemoryMB = 10
            });
        }

        public async Task<NodeExecutionResult> ExecuteAsync(
            NodeExecutionContext context,
            System.Threading.CancellationToken cancellationToken = default)
        {
            // Simulate some work
            await Task.Delay(100, cancellationToken);

            return NodeExecutionResult.Succeed(new Dictionary<string, object>
            {
                { "features", new List<IFeature>() }
            });
        }
    }
}

/// <summary>
/// Benchmarks for database operations
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class DatabaseBenchmarks
{
    private List<IFeature> _features = null!;

    [GlobalSetup]
    public void Setup()
    {
        _features = CreateTestFeatures(10000);
    }

    [Benchmark]
    public void SerializeFeaturesToJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_features);
    }

    [Benchmark]
    public void DeserializeFeaturesFromJson()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(_features);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<IFeature>>(json);
    }

    private List<IFeature> CreateTestFeatures(int count)
    {
        var features = new List<IFeature>();
        var factory = new GeometryFactory();

        for (int i = 0; i < count; i++)
        {
            var point = factory.CreatePoint(new Coordinate(i, i));
            var attributes = new AttributesTable
            {
                { "id", i },
                { "name", $"Feature {i}" },
                { "value", i * 1.5 }
            };
            features.Add(new Feature(point, attributes));
        }

        return features;
    }
}
