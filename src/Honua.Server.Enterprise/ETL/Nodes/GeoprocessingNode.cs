// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Models;
using Honua.Server.Enterprise.Geoprocessing;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Nodes;

/// <summary>
/// Workflow node that executes a geoprocessing operation
/// Integrates with IGeoprocessingService to leverage existing tiered execution
/// </summary>
public class GeoprocessingNode : WorkflowNodeBase
{
    private readonly IGeoprocessingService _geoprocessingService;
    private readonly string _operation;

    public GeoprocessingNode(
        IGeoprocessingService geoprocessingService,
        string operation,
        ILogger<GeoprocessingNode> logger) : base(logger)
    {
        _geoprocessingService = geoprocessingService ?? throw new ArgumentNullException(nameof(geoprocessingService));
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    public override string NodeType => $"geoprocessing.{_operation}";

    public override string DisplayName => _operation switch
    {
        // Phase 1 operations
        GeoprocessingOperation.Buffer => "Buffer",
        GeoprocessingOperation.Intersection => "Intersection",
        GeoprocessingOperation.Union => "Union",
        // Phase 1.5 operations
        GeoprocessingOperation.Difference => "Difference",
        GeoprocessingOperation.Simplify => "Simplify",
        GeoprocessingOperation.ConvexHull => "Convex Hull",
        GeoprocessingOperation.Dissolve => "Dissolve",
        _ => _operation
    };

    public override string Description => _operation switch
    {
        // Phase 1 operations
        GeoprocessingOperation.Buffer => "Creates buffer polygons around geometries at a specified distance",
        GeoprocessingOperation.Intersection => "Finds the geometric intersection of two datasets",
        GeoprocessingOperation.Union => "Merges geometries from two datasets",
        // Phase 1.5 operations
        GeoprocessingOperation.Difference => "Subtracts geometries of one dataset from another",
        GeoprocessingOperation.Simplify => "Simplifies geometries by reducing vertex count while preserving shape",
        GeoprocessingOperation.ConvexHull => "Creates the smallest convex polygon that encloses all input geometries",
        GeoprocessingOperation.Dissolve => "Merges adjacent or overlapping geometries based on attribute values",
        _ => $"Executes {_operation} geoprocessing operation"
    };

    public override async Task<NodeValidationResult> ValidateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        var baseResult = await base.ValidateAsync(nodeDefinition, runtimeParameters, cancellationToken);
        if (!baseResult.IsValid)
        {
            return baseResult;
        }

        try
        {
            // Create geoprocessing job from node parameters
            var job = CreateGeoprocessingJob(nodeDefinition, runtimeParameters);

            // Validate using existing geoprocessing service
            var validationResult = await _geoprocessingService.ValidateJobAsync(job, cancellationToken);

            return new NodeValidationResult
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors,
                Warnings = validationResult.Warnings
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating geoprocessing node {NodeId}", nodeDefinition.Id);
            return NodeValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }

    public override async Task<NodeEstimate> EstimateAsync(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create geoprocessing job from node parameters
            var job = CreateGeoprocessingJob(nodeDefinition, runtimeParameters);

            // Estimate using existing geoprocessing service
            var estimate = await _geoprocessingService.EstimateJobAsync(job, cancellationToken);

            return new NodeEstimate
            {
                EstimatedDurationSeconds = estimate.EstimatedDurationSeconds,
                EstimatedMemoryMB = estimate.EstimatedMemoryMB,
                EstimatedCpuCores = estimate.EstimatedCpuCores
            };
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error estimating geoprocessing node {NodeId}, using defaults", nodeDefinition.Id);
            return await base.EstimateAsync(nodeDefinition, runtimeParameters, cancellationToken);
        }
    }

    public override async Task<NodeExecutionResult> ExecuteAsync(
        NodeExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            Logger.LogInformation(
                "Executing geoprocessing node {NodeId} ({Operation}) in workflow run {WorkflowRunId}",
                context.NodeDefinition.Id,
                _operation,
                context.WorkflowRunId);

            ReportProgress(context, 0, $"Starting {DisplayName} operation");

            // Create geoprocessing job from node parameters
            var job = CreateGeoprocessingJob(
                context.NodeDefinition,
                context.Parameters,
                context.TenantId,
                context.UserId);

            // Create progress callback that forwards to node progress
            var geoProgress = new Progress<GeoprocessingProgress>(progress =>
            {
                ReportProgress(
                    context,
                    progress.ProgressPercent,
                    progress.Message,
                    progress.FeaturesProcessed,
                    progress.TotalFeatures);
            });

            // Execute using existing geoprocessing service (leverages tiered execution)
            var result = await _geoprocessingService.ExecuteJobAsync(job, geoProgress, cancellationToken);

            stopwatch.Stop();

            Logger.LogInformation(
                "Geoprocessing node {NodeId} ({Operation}) completed in {DurationMs}ms",
                context.NodeDefinition.Id,
                _operation,
                stopwatch.ElapsedMilliseconds);

            ReportProgress(context, 100, $"{DisplayName} completed successfully");

            // Return result for downstream nodes
            return new NodeExecutionResult
            {
                Success = true,
                Data = result,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning(
                "Geoprocessing node {NodeId} ({Operation}) was cancelled",
                context.NodeDefinition.Id,
                _operation);

            return NodeExecutionResult.Fail("Operation was cancelled");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            Logger.LogError(
                ex,
                "Geoprocessing node {NodeId} ({Operation}) failed after {DurationMs}ms",
                context.NodeDefinition.Id,
                _operation,
                stopwatch.ElapsedMilliseconds);

            return NodeExecutionResult.Fail(
                $"{DisplayName} failed: {ex.Message}",
                ex.StackTrace);
        }
    }

    /// <summary>
    /// Creates a GeoprocessingJob from workflow node definition
    /// </summary>
    private GeoprocessingJob CreateGeoprocessingJob(
        WorkflowNode nodeDefinition,
        Dictionary<string, object> runtimeParameters,
        Guid? tenantId = null,
        Guid? userId = null)
    {
        var job = new GeoprocessingJob
        {
            Operation = _operation,
            Name = nodeDefinition.Name ?? $"{DisplayName} - {nodeDefinition.Id}",
            Description = nodeDefinition.Description,
            TenantId = tenantId ?? Guid.Empty,
            UserId = userId ?? Guid.Empty
        };

        // Map node parameters to geoprocessing parameters
        foreach (var (key, value) in nodeDefinition.Parameters)
        {
            // Resolve template expressions if needed
            var resolvedValue = value is string strValue && strValue.StartsWith("{{")
                ? runtimeParameters.TryGetValue(key, out var runtimeValue) ? runtimeValue : value
                : value;

            job.Parameters[key] = resolvedValue;
        }

        // Handle inputs from upstream nodes
        // For now, assume inputs are provided in parameters as GeoprocessingInput objects
        // This will be enhanced when we add data source nodes

        return job;
    }
}

/// <summary>
/// Factory for creating geoprocessing nodes
/// </summary>
public class GeoprocessingNodeFactory
{
    private readonly IGeoprocessingService _geoprocessingService;
    private readonly ILoggerFactory _loggerFactory;

    public GeoprocessingNodeFactory(
        IGeoprocessingService geoprocessingService,
        ILoggerFactory loggerFactory)
    {
        _geoprocessingService = geoprocessingService ?? throw new ArgumentNullException(nameof(geoprocessingService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Creates a geoprocessing node for the specified operation
    /// </summary>
    public GeoprocessingNode CreateNode(string operation)
    {
        var logger = _loggerFactory.CreateLogger<GeoprocessingNode>();
        return new GeoprocessingNode(_geoprocessingService, operation, logger);
    }

    /// <summary>
    /// Gets all available geoprocessing node types (Phase 1 + Phase 1.5: 7 operations)
    /// </summary>
    public static List<string> GetAvailableOperations()
    {
        return new List<string>
        {
            // Phase 1 operations (Tier 1 - Simple)
            GeoprocessingOperation.Buffer,
            GeoprocessingOperation.Intersection,
            GeoprocessingOperation.Union,

            // Phase 1.5 operations (Tier 2 - Moderate)
            GeoprocessingOperation.Difference,
            GeoprocessingOperation.Simplify,
            GeoprocessingOperation.ConvexHull,
            GeoprocessingOperation.Dissolve

            // Future operations will include:
            // - Tier 3 (Complex) operations with custom UIs
            // - Spatial analysis (Clip, Erase, SpatialJoin)
            // - Transformations (Reproject, Transform, Rotate)
            // - Advanced analysis (Voronoi, Delaunay, Heatmap)
        };
    }
}
