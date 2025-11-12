// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Enterprise.Geoprocessing;

/// <summary>
/// Represents a cloud-native geoprocessing job
/// </summary>
public class GeoprocessingJob
{
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Tenant ID (for multi-tenant isolation)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User who submitted the job
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User email/identifier
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Operation type (buffer, intersection, union, etc.)
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Job display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Job description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current job status
    /// </summary>
    public GeoprocessingJobStatus Status { get; set; } = GeoprocessingJobStatus.Pending;

    /// <summary>
    /// Input parameters (JSON)
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Input data sources
    /// </summary>
    public List<GeoprocessingInput> Inputs { get; set; } = new();

    /// <summary>
    /// Output configuration
    /// </summary>
    public GeoprocessingOutput? Output { get; set; }

    /// <summary>
    /// Job priority (1-10, higher = more important)
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Progress message
    /// </summary>
    public string? ProgressMessage { get; set; }

    /// <summary>
    /// Result data (GeoJSON, URLs, etc.)
    /// </summary>
    public Dictionary<string, object>? Result { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if failed
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Webhook URL for completion notifications
    /// </summary>
    public string? WebhookUrl { get; set; }

    /// <summary>
    /// Email notification on completion
    /// </summary>
    public bool NotifyEmail { get; set; } = false;

    /// <summary>
    /// When job was created
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// When job started processing
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// When job completed
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Processing duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Worker instance that processed this job
    /// </summary>
    public string? WorkerId { get; set; }

    /// <summary>
    /// Estimated completion time
    /// </summary>
    public DateTimeOffset? EstimatedCompletion { get; set; }

    /// <summary>
    /// Job tags for organization
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Job metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Geoprocessing job status
/// </summary>
public enum GeoprocessingJobStatus
{
    /// <summary>
    /// Job is queued and waiting for worker
    /// </summary>
    Pending,

    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Running,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Job failed with error
    /// </summary>
    Failed,

    /// <summary>
    /// Job was cancelled by user
    /// </summary>
    Cancelled,

    /// <summary>
    /// Job timed out
    /// </summary>
    Timeout
}

/// <summary>
/// Input data for geoprocessing operation
/// </summary>
public class GeoprocessingInput
{
    /// <summary>
    /// Input name/identifier
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Input type (collection, geojson, wkt, url, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Input source (collection ID, URL, inline GeoJSON, etc.)
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Optional CQL filter for collection inputs
    /// </summary>
    public string? Filter { get; set; }

    /// <summary>
    /// Optional additional parameters
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Output configuration for geoprocessing result
/// </summary>
public class GeoprocessingOutput
{
    /// <summary>
    /// Output type (collection, geojson, url, shapefile, geopackage, etc.)
    /// </summary>
    public string Type { get; set; } = "geojson";

    /// <summary>
    /// Output destination (collection name, blob URL, etc.)
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Output format options
    /// </summary>
    public Dictionary<string, object>? Options { get; set; }
}

/// <summary>
/// Geoprocessing operation types
/// </summary>
public static class GeoprocessingOperation
{
    // Vector operations
    public const string Buffer = "buffer";
    public const string Intersection = "intersection";
    public const string Union = "union";
    public const string Difference = "difference";
    public const string SymmetricDifference = "symmetric_difference";
    public const string Dissolve = "dissolve";
    public const string Clip = "clip";
    public const string Erase = "erase";
    public const string SpatialJoin = "spatial_join";
    public const string Centroid = "centroid";
    public const string ConvexHull = "convex_hull";
    public const string Envelope = "envelope";
    public const string Simplify = "simplify";
    public const string Smooth = "smooth";
    public const string Densify = "densify";

    // Geometric analysis
    public const string Area = "area";
    public const string Length = "length";
    public const string Distance = "distance";
    public const string Bearing = "bearing";
    public const string NearestFeature = "nearest_feature";
    public const string PointsAlongLine = "points_along_line";

    // Spatial relationships
    public const string Contains = "contains";
    public const string Within = "within";
    public const string Intersects = "intersects";
    public const string Touches = "touches";
    public const string Crosses = "crosses";
    public const string Overlaps = "overlaps";

    // Transformations
    public const string Reproject = "reproject";
    public const string Transform = "transform";
    public const string Rotate = "rotate";
    public const string Scale = "scale";
    public const string Translate = "translate";

    // Advanced analysis
    public const string Voronoi = "voronoi";
    public const string Delaunay = "delaunay";
    public const string Thiessen = "thiessen";
    public const string Heatmap = "heatmap";
    public const string Density = "density";
    public const string Cluster = "cluster";
    public const string H3Binning = "h3_binning";

    // Raster operations
    public const string RasterMosaic = "raster_mosaic";
    public const string RasterReproject = "raster_reproject";
    public const string RasterClip = "raster_clip";
    public const string RasterAlgebra = "raster_algebra";
    public const string Hillshade = "hillshade";
    public const string Slope = "slope";
    public const string Aspect = "aspect";
    public const string ZonalStatistics = "zonal_statistics";

    // Batch operations
    public const string BatchTransform = "batch_transform";
    public const string BatchReproject = "batch_reproject";
    public const string MergeCollections = "merge_collections";
}

/// <summary>
/// Query parameters for searching jobs
/// </summary>
public class GeoprocessingJobQuery
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string? Operation { get; set; }
    public GeoprocessingJobStatus? Status { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public List<string>? Tags { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string SortBy { get; set; } = "created_at";
    public string SortDirection { get; set; } = "desc";
}

/// <summary>
/// Paged result of geoprocessing jobs
/// </summary>
public class GeoprocessingJobResult
{
    public List<GeoprocessingJob> Jobs { get; set; } = new();
    public long TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Job statistics
/// </summary>
public class GeoprocessingStatistics
{
    public long TotalJobs { get; set; }
    public long PendingJobs { get; set; }
    public long RunningJobs { get; set; }
    public long CompletedJobs { get; set; }
    public long FailedJobs { get; set; }
    public long CancelledJobs { get; set; }
    public double AverageDurationSeconds { get; set; }
    public Dictionary<string, long> JobsByOperation { get; set; } = new();
    public Dictionary<string, long> JobsByStatus { get; set; } = new();
}
