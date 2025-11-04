// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Process registry - Catalog of available geoprocessing operations
/// Supports declarative YAML-based process definitions
/// </summary>
public interface IProcessRegistry
{
    /// <summary>
    /// Gets a process definition by ID
    /// </summary>
    Task<ProcessDefinition?> GetProcessAsync(string processId, CancellationToken ct = default);

    /// <summary>
    /// Lists all available processes
    /// </summary>
    Task<IReadOnlyList<ProcessDefinition>> ListProcessesAsync(CancellationToken ct = default);

    /// <summary>
    /// Registers a new process definition
    /// </summary>
    Task RegisterProcessAsync(ProcessDefinition process, CancellationToken ct = default);

    /// <summary>
    /// Unregisters a process
    /// </summary>
    Task UnregisterProcessAsync(string processId, CancellationToken ct = default);

    /// <summary>
    /// Reloads process definitions from YAML files (auto-discovery)
    /// </summary>
    Task ReloadAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a process is available
    /// </summary>
    Task<bool> IsAvailableAsync(string processId, CancellationToken ct = default);
}

/// <summary>
/// Process definition (declarative metadata)
/// </summary>
public class ProcessDefinition
{
    /// <summary>Unique process identifier (e.g., "buffer", "intersection")</summary>
    public required string Id { get; init; }

    /// <summary>Display title</summary>
    public required string Title { get; init; }

    /// <summary>Description</summary>
    public string? Description { get; init; }

    /// <summary>Version</summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>Keywords/tags</summary>
    public List<string> Keywords { get; init; } = new();

    /// <summary>Process category (vector, raster, analysis, conversion, etc.)</summary>
    public string Category { get; init; } = "vector";

    /// <summary>Input parameters</summary>
    public required List<ProcessParameter> Inputs { get; init; }

    /// <summary>Output definition</summary>
    public ProcessOutput? Output { get; init; }

    /// <summary>Supported output formats</summary>
    public List<string> OutputFormats { get; init; } = new() { "geojson" };

    /// <summary>Execution configuration</summary>
    public ProcessExecutionConfig ExecutionConfig { get; init; } = new();

    /// <summary>Links (documentation, examples, etc.)</summary>
    public List<ProcessLink> Links { get; init; } = new();

    /// <summary>When this process was registered</summary>
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this process is enabled</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Implementation class (if using code-based operations)</summary>
    public string? ImplementationClass { get; init; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Process parameter definition
/// </summary>
public class ProcessParameter
{
    /// <summary>Parameter name</summary>
    public required string Name { get; init; }

    /// <summary>Display title</summary>
    public string? Title { get; init; }

    /// <summary>Description</summary>
    public string? Description { get; init; }

    /// <summary>Data type (geometry, number, string, boolean, array, object)</summary>
    public required string Type { get; init; }

    /// <summary>Whether parameter is required</summary>
    public bool Required { get; init; } = true;

    /// <summary>Default value (if optional)</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Minimum value (for numbers)</summary>
    public double? MinValue { get; init; }

    /// <summary>Maximum value (for numbers)</summary>
    public double? MaxValue { get; init; }

    /// <summary>Allowed values (enum)</summary>
    public List<object>? AllowedValues { get; init; }

    /// <summary>Geometry types allowed (Point, LineString, Polygon, etc.)</summary>
    public List<string>? GeometryTypes { get; init; }

    /// <summary>CRS/SRID constraints</summary>
    public List<int>? AllowedSrids { get; init; }

    /// <summary>Array item type (if type is array)</summary>
    public string? ItemType { get; init; }

    /// <summary>Min array length</summary>
    public int? MinItems { get; init; }

    /// <summary>Max array length</summary>
    public int? MaxItems { get; init; }

    /// <summary>Additional constraints/validation</summary>
    public Dictionary<string, object>? Constraints { get; init; }
}

/// <summary>
/// Process output definition
/// </summary>
public class ProcessOutput
{
    /// <summary>Output type (geometry, featurecollection, url, etc.)</summary>
    public required string Type { get; init; }

    /// <summary>Description</summary>
    public string? Description { get; init; }

    /// <summary>Output geometry type (if applicable)</summary>
    public string? GeometryType { get; init; }

    /// <summary>Output CRS/SRID</summary>
    public int? Srid { get; init; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Process execution configuration
/// </summary>
public class ProcessExecutionConfig
{
    /// <summary>Supported execution tiers</summary>
    public List<ProcessExecutionTier> SupportedTiers { get; init; } = new()
    {
        ProcessExecutionTier.NTS,
        ProcessExecutionTier.PostGIS,
        ProcessExecutionTier.CloudBatch
    };

    /// <summary>Default tier to try first</summary>
    public ProcessExecutionTier DefaultTier { get; init; } = ProcessExecutionTier.NTS;

    /// <summary>Default timeout in seconds</summary>
    public int DefaultTimeoutSeconds { get; init; } = 300;

    /// <summary>Max timeout in seconds</summary>
    public int MaxTimeoutSeconds { get; init; } = 1800;

    /// <summary>Max input size in MB</summary>
    public int MaxInputSizeMB { get; init; } = 100;

    /// <summary>Max features that can be processed</summary>
    public long? MaxFeatures { get; init; }

    /// <summary>Whether this process supports streaming/chunking</summary>
    public bool SupportsStreaming { get; init; } = false;

    /// <summary>Whether this process can run synchronously</summary>
    public bool SupportsSyncExecution { get; init; } = true;

    /// <summary>Estimated duration in seconds (for planning)</summary>
    public int? EstimatedDurationSeconds { get; init; }

    /// <summary>Estimated memory usage in MB</summary>
    public int? EstimatedMemoryMB { get; init; }

    /// <summary>Tier selection thresholds</summary>
    public TierThresholds? Thresholds { get; init; }
}

/// <summary>
/// Thresholds for automatic tier selection
/// </summary>
public class TierThresholds
{
    /// <summary>Max features for NTS tier</summary>
    public long? NtsMaxFeatures { get; init; } = 1000;

    /// <summary>Max features for PostGIS tier</summary>
    public long? PostGisMaxFeatures { get; init; } = 100000;

    /// <summary>Max input size for NTS (MB)</summary>
    public int? NtsMaxInputMB { get; init; } = 10;

    /// <summary>Max input size for PostGIS (MB)</summary>
    public int? PostGisMaxInputMB { get; init; } = 500;

    /// <summary>Max expected duration for NTS (seconds)</summary>
    public int? NtsMaxDurationSeconds { get; init; } = 1;

    /// <summary>Max expected duration for PostGIS (seconds)</summary>
    public int? PostGisMaxDurationSeconds { get; init; } = 30;
}

/// <summary>
/// Process link (documentation, examples, etc.)
/// </summary>
public class ProcessLink
{
    /// <summary>Link relation type (documentation, example, source, etc.)</summary>
    public required string Rel { get; init; }

    /// <summary>URL</summary>
    public required string Href { get; init; }

    /// <summary>Title</summary>
    public string? Title { get; init; }

    /// <summary>Media type</summary>
    public string? Type { get; init; }
}

/// <summary>
/// Constants for standard process operations
/// </summary>
public static class StandardProcesses
{
    // Vector operations
    public const string Buffer = "buffer";
    public const string Intersection = "intersection";
    public const string Union = "union";
    public const string Difference = "difference";
    public const string Dissolve = "dissolve";
    public const string Clip = "clip";
    public const string Simplify = "simplify";
    public const string ConvexHull = "convex-hull";
    public const string Centroid = "centroid";
    public const string VoronoiDiagram = "voronoi";

    // Analysis operations
    public const string SpatialJoin = "spatial-join";
    public const string NearestNeighbor = "nearest-neighbor";
    public const string Clustering = "clustering";
    public const string Heatmap = "heatmap";
    public const string Density = "density";

    // Raster operations
    public const string RasterCalculator = "raster-calculator";
    public const string Slope = "slope";
    public const string Aspect = "aspect";
    public const string Hillshade = "hillshade";
    public const string Contour = "contour";

    // Conversion operations
    public const string Reproject = "reproject";
    public const string ToRaster = "to-raster";
    public const string ToVector = "to-vector";
    public const string FormatConversion = "format-conversion";
}
