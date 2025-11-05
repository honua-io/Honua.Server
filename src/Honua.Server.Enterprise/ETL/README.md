# Honua GeoETL - Phase 1 Implementation

**Version:** 1.0
**Status:** Phase 1 Complete - Foundation Ready
**Last Updated:** 2025-11-05

## Overview

This directory contains the Phase 1 implementation of the Honua GeoETL capability - an AI-powered, cloud-native geospatial data pipeline platform. The implementation provides a solid foundation for building FME-competitive ETL workflows with seamless integration to existing geoprocessing capabilities.

## Architecture

```
ETL/
├── Models/              # Workflow and execution models
│   ├── WorkflowDefinition.cs  # JSON-based workflow with nodes/edges
│   └── WorkflowRun.cs         # Execution tracking with metrics
├── Engine/              # Core workflow execution
│   ├── IWorkflowEngine.cs     # Engine interface
│   └── WorkflowEngine.cs      # DAG validation and execution
├── Nodes/               # Workflow node implementations
│   ├── IWorkflowNode.cs       # Node interface
│   ├── WorkflowNodeBase.cs    # Base class
│   ├── GeoprocessingNode.cs   # Geoprocessing integration (3 ops)
│   ├── DataSourceNodes.cs     # PostGIS, File sources
│   └── DataSinkNodes.cs       # PostGIS, GeoJSON, Output sinks
├── Stores/              # Data persistence
│   ├── IWorkflowStore.cs      # Store interface
│   ├── PostgresWorkflowStore.cs  # PostgreSQL implementation
│   └── InMemoryWorkflowStore.cs  # Testing implementation
└── ServiceCollectionExtensions.cs  # DI setup
```

## Key Features

### ✅ Implemented (Phase 1)

- **DAG-Based Workflow Engine**
  - Topological sort for correct execution order
  - Cycle detection and validation
  - Per-node validation and estimation
  - Progress tracking and cancellation support

- **Geoprocessing Integration**
  - Wraps existing `IGeoprocessingService`
  - Inherits tiered execution (NTS → PostGIS → Cloud Batch)
  - 3 operations: Buffer, Intersection, Union
  - Ready for Phase 1.5 expansion

- **Data Source Nodes**
  - PostGIS: Read from tables or custom queries
  - File: Parse GeoJSON features (inline or URL)

- **Data Sink Nodes**
  - PostGIS: Write features to database tables
  - GeoJSON: Export to GeoJSON format
  - Output: Store in workflow state for retrieval

- **PostgreSQL Storage**
  - Workflow definitions with JSONB
  - Execution history with full metrics
  - Per-node execution details
  - Row-level security for multi-tenancy

- **Observable & Measurable**
  - Progress callbacks
  - Metrics: features processed, bytes, CPU time
  - Cost tracking: compute and storage costs
  - Data lineage: input/output datasets

## Usage

### 1. Register Services

```csharp
// In Startup.cs or Program.cs
services.AddGeoEtl(configuration.GetConnectionString("Postgres"));
```

### 2. Create a Workflow

```csharp
var workflow = new WorkflowDefinition
{
    TenantId = tenantId,
    Metadata = new WorkflowMetadata
    {
        Name = "Buffer Buildings",
        Description = "Creates 50m buffers around all buildings"
    },
    Nodes = new List<WorkflowNode>
    {
        new WorkflowNode
        {
            Id = "load_buildings",
            Type = "data_source.postgis",
            Parameters = new Dictionary<string, object>
            {
                ["table"] = "buildings",
                ["geometry_column"] = "geom"
            }
        },
        new WorkflowNode
        {
            Id = "buffer_50m",
            Type = "geoprocessing.buffer",
            Parameters = new Dictionary<string, object>
            {
                ["distance"] = 50,
                ["unit"] = "meters"
            }
        },
        new WorkflowNode
        {
            Id = "export_geojson",
            Type = "data_sink.geojson"
        }
    },
    Edges = new List<WorkflowEdge>
    {
        new WorkflowEdge { From = "load_buildings", To = "buffer_50m" },
        new WorkflowEdge { From = "buffer_50m", To = "export_geojson" }
    }
};

// Save workflow
await workflowStore.CreateWorkflowAsync(workflow);
```

### 3. Execute Workflow

```csharp
var workflowEngine = serviceProvider.GetRequiredService<IWorkflowEngine>();

// Validate workflow
var validation = await workflowEngine.ValidateAsync(workflow);
if (!validation.IsValid)
{
    Console.WriteLine($"Validation errors: {string.Join(", ", validation.Errors)}");
    return;
}

// Execute workflow
var options = new WorkflowExecutionOptions
{
    TenantId = tenantId,
    UserId = userId,
    ProgressCallback = new Progress<WorkflowProgress>(p =>
    {
        Console.WriteLine($"Progress: {p.ProgressPercent}% - {p.Message}");
    })
};

var run = await workflowEngine.ExecuteAsync(workflow, options);
Console.WriteLine($"Workflow completed with status: {run.Status}");
Console.WriteLine($"Features processed: {run.FeaturesProcessed}");
Console.WriteLine($"Duration: {(run.CompletedAt - run.StartedAt)?.TotalSeconds}s");
```

### 4. Query Workflow Runs

```csharp
// Get run status
var run = await workflowStore.GetRunAsync(runId);
Console.WriteLine($"Status: {run.Status}");
Console.WriteLine($"Nodes completed: {run.NodeRuns.Count(n => n.Status == NodeRunStatus.Completed)}");

// List runs for a tenant
var runs = await workflowStore.ListRunsByTenantAsync(tenantId, limit: 10);
foreach (var r in runs)
{
    Console.WriteLine($"{r.CreatedAt}: {r.Status} - {r.FeaturesProcessed} features");
}
```

## Available Node Types

### Data Sources
- `data_source.postgis` - Read from PostGIS table or query
- `data_source.file` - Parse GeoJSON file (inline or URL)

### Geoprocessing (Phase 1)
- `geoprocessing.buffer` - Create buffer polygons
- `geoprocessing.intersection` - Find geometric intersection
- `geoprocessing.union` - Merge geometries

### Data Sinks
- `data_sink.postgis` - Write to PostGIS table
- `data_sink.geojson` - Export to GeoJSON format
- `data_sink.output` - Store in workflow state

## Database Migration

The implementation includes migration `017_ETL.sql` which creates:

- `geoetl_workflows` - Workflow definitions
- `geoetl_workflow_runs` - Execution history
- `geoetl_node_runs` - Per-node execution details
- Helper functions for statistics and summaries
- Indexes optimized for common queries
- Row-level security policies

Run the migration:
```bash
psql -d honua -f src/Honua.Server.Core/Data/Migrations/017_ETL.sql
```

## Testing

### Unit Tests (TODO - Phase 1 Continuation)

```bash
dotnet test tests/Honua.Server.Enterprise.Tests/ETL/
```

### Integration Test Example

```csharp
[Fact]
public async Task CanExecuteSimpleWorkflow()
{
    // Arrange
    var workflow = CreateSimpleWorkflow();
    var engine = new WorkflowEngine(store, registry, logger);

    // Act
    var run = await engine.ExecuteAsync(workflow, options);

    // Assert
    Assert.Equal(WorkflowRunStatus.Completed, run.Status);
    Assert.True(run.FeaturesProcessed > 0);
}
```

## Phase 1 Completion Status

| Component | Status | Notes |
|-----------|--------|-------|
| Core Models | ✅ Complete | WorkflowDefinition, WorkflowRun |
| Workflow Engine | ✅ Complete | DAG validation, execution |
| Geoprocessing Integration | ✅ Complete | 3 operations (Buffer, Intersection, Union) |
| Data Source Nodes | ✅ Complete | PostGIS, File |
| Data Sink Nodes | ✅ Complete | PostGIS, GeoJSON, Output |
| PostgreSQL Store | ✅ Complete | Full CRUD with metrics |
| Database Migration | ✅ Complete | 017_ETL.sql |
| Service Registration | ✅ Complete | DI extensions |
| Unit Tests | ⏳ Pending | Next phase |
| Web UI | ⏳ Pending | Phase 2 |

## Next Steps

### Phase 1.5 (Months 4-6)
- Add remaining geoprocessing operations (Difference, Simplify, ConvexHull, Dissolve)
- Build custom parameter UIs for complex operations
- Implement long-running job progress tracking
- Add retry logic and advanced error handling

### Phase 2 (Months 4-6, Parallel)
- AI-powered workflow generation (research phase)
- Template library with 25+ pre-built workflows
- Rule-based data quality validation

### Phase 3 (Months 7-9)
- GeoParquet, GeoArrow, PMTiles support
- Cloud storage connectors (S3, Azure Blob, GCS)
- Performance optimization and parallel processing

## Design Documentation

Full design document: `/docs/design/geoetl-capability-design.md`

Key design principles:
- **Integration over replacement**: Extends existing geoprocessing
- **DAG execution**: Topological sort ensures correct order
- **Tiered execution**: Leverages NTS → PostGIS → Cloud Batch
- **Multi-tenant**: Tenant isolation with RLS
- **Observable**: Built-in metrics and cost tracking
- **Extensible**: Easy to add new node types

## API Reference

### IWorkflowEngine

```csharp
Task<WorkflowValidationResult> ValidateAsync(
    WorkflowDefinition workflow,
    Dictionary<string, object>? parameterValues = null,
    CancellationToken cancellationToken = default);

Task<WorkflowEstimate> EstimateAsync(
    WorkflowDefinition workflow,
    Dictionary<string, object>? parameterValues = null,
    CancellationToken cancellationToken = default);

Task<WorkflowRun> ExecuteAsync(
    WorkflowDefinition workflow,
    WorkflowExecutionOptions options,
    CancellationToken cancellationToken = default);
```

### IWorkflowStore

```csharp
Task<WorkflowDefinition> CreateWorkflowAsync(WorkflowDefinition workflow, ...);
Task<WorkflowDefinition?> GetWorkflowAsync(Guid workflowId, ...);
Task<WorkflowDefinition> UpdateWorkflowAsync(WorkflowDefinition workflow, ...);
Task<List<WorkflowDefinition>> ListWorkflowsAsync(Guid tenantId, ...);

Task<WorkflowRun> CreateRunAsync(WorkflowRun run, ...);
Task<WorkflowRun?> GetRunAsync(Guid runId, ...);
Task<WorkflowRun> UpdateRunAsync(WorkflowRun run, ...);
Task<List<WorkflowRun>> ListRunsAsync(Guid workflowId, ...);
```

### IWorkflowNode

```csharp
Task<NodeValidationResult> ValidateAsync(
    WorkflowNode nodeDefinition,
    Dictionary<string, object> runtimeParameters,
    CancellationToken cancellationToken = default);

Task<NodeEstimate> EstimateAsync(
    WorkflowNode nodeDefinition,
    Dictionary<string, object> runtimeParameters,
    CancellationToken cancellationToken = default);

Task<NodeExecutionResult> ExecuteAsync(
    NodeExecutionContext context,
    CancellationToken cancellationToken = default);
```

## Contributing

When adding new node types:

1. Create a class that inherits from `WorkflowNodeBase`
2. Implement required properties (`NodeType`, `DisplayName`, `Description`)
3. Override `ExecuteAsync` with your node logic
4. Optionally override `ValidateAsync` and `EstimateAsync`
5. Register in `ServiceCollectionExtensions.cs`

Example:
```csharp
public class CustomNode : WorkflowNodeBase
{
    public CustomNode(ILogger<CustomNode> logger) : base(logger) {}

    public override string NodeType => "custom.my_operation";
    public override string DisplayName => "My Custom Operation";
    public override string Description => "Does something custom";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Your logic here
        return NodeExecutionResult.Succeed(new Dictionary<string, object>
        {
            ["result"] = "success"
        });
    }
}
```

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
