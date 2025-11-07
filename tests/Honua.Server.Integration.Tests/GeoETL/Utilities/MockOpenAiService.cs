// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Enterprise.ETL.AI;
using Honua.Server.Enterprise.ETL.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Integration.Tests.GeoETL.Utilities;

/// <summary>
/// Mock implementation of IGeoEtlAiService for testing
/// </summary>
public class MockOpenAiService : IGeoEtlAiService
{
    public bool IsAvailable => true;

    public Task<GeoEtlAiResult> GenerateWorkflowAsync(
        string prompt,
        Guid tenantId,
        Guid userId,
        bool validateWorkflow = true,
        CancellationToken cancellationToken = default)
    {
        // Generate a simple workflow based on common patterns in the prompt
        var workflow = CreateWorkflowFromPrompt(prompt, tenantId, userId);

        var result = new GeoEtlAiResult
        {
            Success = true,
            Workflow = workflow,
            Explanation = $"Generated workflow for: {prompt}",
            Confidence = 0.85,
            Warnings = new System.Collections.Generic.List<string>()
        };

        return Task.FromResult(result);
    }

    public Task<GeoEtlAiResult> ExplainWorkflowAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        var result = new GeoEtlAiResult
        {
            Success = true,
            Explanation = $"This workflow '{workflow.Metadata.Name}' processes geospatial data through {workflow.Nodes.Count} nodes.",
            Confidence = 0.90
        };

        return Task.FromResult(result);
    }

    public Task<GeoEtlAiResult> SuggestImprovementsAsync(
        WorkflowDefinition workflow,
        CancellationToken cancellationToken = default)
    {
        var result = new GeoEtlAiResult
        {
            Success = true,
            Explanation = "Workflow appears well-structured. Consider adding error handling nodes.",
            Confidence = 0.75,
            Warnings = new System.Collections.Generic.List<string>
            {
                "Consider adding validation before geoprocessing",
                "Output nodes could benefit from format specification"
            }
        };

        return Task.FromResult(result);
    }

    private WorkflowDefinition CreateWorkflowFromPrompt(string prompt, Guid tenantId, Guid userId)
    {
        var promptLower = prompt.ToLowerInvariant();

        // Simple pattern matching
        if (promptLower.Contains("buffer"))
        {
            return CreateBufferWorkflow(prompt, tenantId, userId, promptLower);
        }
        else if (promptLower.Contains("intersect"))
        {
            return CreateIntersectionWorkflow(prompt, tenantId, userId);
        }
        else if (promptLower.Contains("union"))
        {
            return CreateUnionWorkflow(prompt, tenantId, userId);
        }
        else
        {
            return CreateDefaultWorkflow(prompt, tenantId, userId);
        }
    }

    private WorkflowDefinition CreateBufferWorkflow(string prompt, Guid tenantId, Guid userId, string promptLower)
    {
        // Extract buffer distance if mentioned
        double distance = 100; // default
        if (promptLower.Contains("50"))
        {
            distance = 50;
        }
        else if (promptLower.Contains("100"))
        {
            distance = 100;
        }

        // Determine output format
        string sinkType = "geojson";
        if (promptLower.Contains("geopackage") || promptLower.Contains("gpkg"))
        {
            sinkType = "geopackage";
        }
        else if (promptLower.Contains("shapefile"))
        {
            sinkType = "shapefile";
        }

        var workflow = new WorkflowDefinition
        {
            TenantId = tenantId,
            CreatedBy = userId,
            Metadata = new WorkflowMetadata
            {
                Name = $"Buffer Features {distance}m",
                Description = $"AI-generated workflow: {prompt}",
                Category = "Geoprocessing",
                Tags = new System.Collections.Generic.List<string> { "AI-generated", "buffer" }
            },
            Nodes = new System.Collections.Generic.List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = "source",
                    Type = "data_source.postgis",
                    Name = "Load Features",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["table"] = "features",
                        ["geometry_column"] = "geom"
                    }
                },
                new WorkflowNode
                {
                    Id = "buffer",
                    Type = "geoprocessing.buffer",
                    Name = $"Buffer {distance}m",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["distance"] = distance,
                        ["unit"] = "meters"
                    }
                },
                new WorkflowNode
                {
                    Id = "export",
                    Type = $"data_sink.{sinkType}",
                    Name = $"Export to {sinkType}",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["output_path"] = $"/output/buffered.{sinkType}"
                    }
                }
            },
            Edges = new System.Collections.Generic.List<WorkflowEdge>
            {
                new WorkflowEdge { From = "source", To = "buffer" },
                new WorkflowEdge { From = "buffer", To = "export" }
            }
        };

        return workflow;
    }

    private WorkflowDefinition CreateIntersectionWorkflow(string prompt, Guid tenantId, Guid userId)
    {
        return new WorkflowDefinition
        {
            TenantId = tenantId,
            CreatedBy = userId,
            Metadata = new WorkflowMetadata
            {
                Name = "Intersection Analysis",
                Description = $"AI-generated workflow: {prompt}",
                Category = "Geoprocessing",
                Tags = new System.Collections.Generic.List<string> { "AI-generated", "intersection" }
            },
            Nodes = new System.Collections.Generic.List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = "source1",
                    Type = "data_source.postgis",
                    Name = "Load Dataset 1",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["table"] = "dataset1",
                        ["geometry_column"] = "geom"
                    }
                },
                new WorkflowNode
                {
                    Id = "source2",
                    Type = "data_source.postgis",
                    Name = "Load Dataset 2",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["table"] = "dataset2",
                        ["geometry_column"] = "geom"
                    }
                },
                new WorkflowNode
                {
                    Id = "intersection",
                    Type = "geoprocessing.intersection",
                    Name = "Calculate Intersection"
                },
                new WorkflowNode
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Name = "Export Results"
                }
            },
            Edges = new System.Collections.Generic.List<WorkflowEdge>
            {
                new WorkflowEdge { From = "source1", To = "intersection" },
                new WorkflowEdge { From = "source2", To = "intersection" },
                new WorkflowEdge { From = "intersection", To = "export" }
            }
        };
    }

    private WorkflowDefinition CreateUnionWorkflow(string prompt, Guid tenantId, Guid userId)
    {
        return new WorkflowDefinition
        {
            TenantId = tenantId,
            CreatedBy = userId,
            Metadata = new WorkflowMetadata
            {
                Name = "Union Features",
                Description = $"AI-generated workflow: {prompt}",
                Category = "Geoprocessing",
                Tags = new System.Collections.Generic.List<string> { "AI-generated", "union" }
            },
            Nodes = new System.Collections.Generic.List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = "source",
                    Type = "data_source.postgis",
                    Name = "Load Features",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["table"] = "features",
                        ["geometry_column"] = "geom"
                    }
                },
                new WorkflowNode
                {
                    Id = "union",
                    Type = "geoprocessing.union",
                    Name = "Union Features"
                },
                new WorkflowNode
                {
                    Id = "export",
                    Type = "data_sink.geojson",
                    Name = "Export Results"
                }
            },
            Edges = new System.Collections.Generic.List<WorkflowEdge>
            {
                new WorkflowEdge { From = "source", To = "union" },
                new WorkflowEdge { From = "union", To = "export" }
            }
        };
    }

    private WorkflowDefinition CreateDefaultWorkflow(string prompt, Guid tenantId, Guid userId)
    {
        return new WorkflowDefinition
        {
            TenantId = tenantId,
            CreatedBy = userId,
            Metadata = new WorkflowMetadata
            {
                Name = "Data Processing",
                Description = $"AI-generated workflow: {prompt}",
                Category = "General",
                Tags = new System.Collections.Generic.List<string> { "AI-generated" }
            },
            Nodes = new System.Collections.Generic.List<WorkflowNode>
            {
                new WorkflowNode
                {
                    Id = "source",
                    Type = "data_source.file",
                    Name = "Load Data",
                    Parameters = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["geojson"] = @"{""type"":""FeatureCollection"",""features"":[]}"
                    }
                },
                new WorkflowNode
                {
                    Id = "output",
                    Type = "data_sink.output",
                    Name = "Store Results"
                }
            },
            Edges = new System.Collections.Generic.List<WorkflowEdge>
            {
                new WorkflowEdge { From = "source", To = "output" }
            }
        };
    }
}

/// <summary>
/// Result from AI workflow generation
/// </summary>
public class GeoEtlAiResult
{
    public bool Success { get; set; }
    public WorkflowDefinition? Workflow { get; set; }
    public string? Explanation { get; set; }
    public double Confidence { get; set; }
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
