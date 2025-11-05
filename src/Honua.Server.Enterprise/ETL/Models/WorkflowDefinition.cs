// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.ETL.Models;

/// <summary>
/// Defines a complete ETL workflow with nodes and connections
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// Unique workflow identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant ID (for multi-tenant isolation)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Workflow version number
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Workflow metadata
    /// </summary>
    public WorkflowMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Workflow parameters (user-definable inputs)
    /// </summary>
    public Dictionary<string, WorkflowParameter> Parameters { get; set; } = new();

    /// <summary>
    /// Nodes in the workflow
    /// </summary>
    public List<WorkflowNode> Nodes { get; set; } = new();

    /// <summary>
    /// Edges connecting nodes (defines DAG)
    /// </summary>
    public List<WorkflowEdge> Edges { get; set; } = new();

    /// <summary>
    /// Whether this workflow is published
    /// </summary>
    public bool IsPublished { get; set; } = false;

    /// <summary>
    /// Whether this workflow is deleted
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// When workflow was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When workflow was last updated
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// User who created the workflow
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// User who last updated the workflow
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Workflow metadata
/// </summary>
public class WorkflowMetadata
{
    /// <summary>
    /// Workflow display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Workflow description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Workflow author/owner
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Workflow category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Custom metadata
    /// </summary>
    public Dictionary<string, object>? Custom { get; set; }
}

/// <summary>
/// Workflow parameter definition
/// </summary>
public class WorkflowParameter
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type (string, number, boolean, geometry, etc.)
    /// </summary>
    public string Type { get; set; } = "string";

    /// <summary>
    /// Parameter description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Default value
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Whether parameter is required
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Minimum value (for numbers)
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Maximum value (for numbers)
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Allowed values (for enums)
    /// </summary>
    public List<object>? AllowedValues { get; set; }
}

/// <summary>
/// Individual node in the workflow
/// </summary>
public class WorkflowNode
{
    /// <summary>
    /// Node identifier (unique within workflow)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Node type (data_source, transformation, geoprocessing, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Node display name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Node description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Node parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Execution configuration
    /// </summary>
    public NodeExecutionConfig? Execution { get; set; }

    /// <summary>
    /// Node position in visual designer (x, y)
    /// </summary>
    public NodePosition? Position { get; set; }
}

/// <summary>
/// Node execution configuration
/// </summary>
public class NodeExecutionConfig
{
    /// <summary>
    /// Execution tier preference (nts, postgis, cloudbatch)
    /// </summary>
    public string? TierPreference { get; set; }

    /// <summary>
    /// Timeout in seconds
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// Maximum retries on failure
    /// </summary>
    public int? MaxRetries { get; set; }

    /// <summary>
    /// Whether to continue workflow on node failure
    /// </summary>
    public bool? ContinueOnError { get; set; }
}

/// <summary>
/// Node position for visual designer
/// </summary>
public class NodePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

/// <summary>
/// Edge connecting two nodes
/// </summary>
public class WorkflowEdge
{
    /// <summary>
    /// Source node ID
    /// </summary>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Target node ID
    /// </summary>
    public string To { get; set; } = string.Empty;

    /// <summary>
    /// Source port (optional, for nodes with multiple outputs)
    /// </summary>
    public string? FromPort { get; set; }

    /// <summary>
    /// Target port (optional, for nodes with multiple inputs)
    /// </summary>
    public string? ToPort { get; set; }

    /// <summary>
    /// Edge label/description
    /// </summary>
    public string? Label { get; set; }
}
