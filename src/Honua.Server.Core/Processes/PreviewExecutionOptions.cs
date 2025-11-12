// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Core.Processes;

/// <summary>
/// Configuration options for preview execution of spatial operations.
/// </summary>
public sealed class PreviewExecutionOptions
{
    /// <summary>
    /// Gets or sets the maximum number of features to include in preview results.
    /// Default is 100.
    /// </summary>
    public int MaxPreviewFeatures { get; set; } = 100;

    /// <summary>
    /// Gets or sets the timeout for preview operations in milliseconds.
    /// Default is 5000 (5 seconds).
    /// </summary>
    public int PreviewTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets whether to use spatial sampling for large datasets.
    /// When enabled, features are sampled across the spatial extent rather than just taking the first N features.
    /// Default is true.
    /// </summary>
    public bool UseSpatialSampling { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable streaming preview responses.
    /// Default is true.
    /// </summary>
    public bool EnableStreamingPreview { get; set; } = true;

    /// <summary>
    /// Gets or sets the chunk size for streaming preview responses.
    /// Default is 10 features per chunk.
    /// </summary>
    public int StreamingChunkSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to simplify geometries in preview mode.
    /// Default is true to improve performance.
    /// </summary>
    public bool SimplifyGeometries { get; set; } = true;

    /// <summary>
    /// Gets or sets the simplification tolerance as a fraction of the extent.
    /// Default is 0.001 (0.1% of extent).
    /// </summary>
    public double SimplificationTolerance { get; set; } = 0.001;
}

/// <summary>
/// Represents a preview execution request.
/// </summary>
public sealed class PreviewExecutionRequest
{
    /// <summary>
    /// Gets or sets the process ID.
    /// </summary>
    public required string ProcessId { get; init; }

    /// <summary>
    /// Gets or sets the input parameters for the operation.
    /// </summary>
    public required Dictionary<string, object> Inputs { get; init; }

    /// <summary>
    /// Gets or sets preview-specific options.
    /// </summary>
    public PreviewExecutionOptions? Options { get; init; }

    /// <summary>
    /// Gets or sets whether to stream the response.
    /// </summary>
    public bool Stream { get; init; }
}

/// <summary>
/// Represents preview metadata included in responses.
/// </summary>
public sealed class PreviewMetadata
{
    /// <summary>
    /// Gets or sets whether this is a preview result.
    /// </summary>
    public required bool IsPreview { get; init; }

    /// <summary>
    /// Gets or sets the total number of features in the full dataset.
    /// </summary>
    public long? TotalFeatures { get; init; }

    /// <summary>
    /// Gets or sets the number of features included in the preview.
    /// </summary>
    public required int PreviewFeatures { get; init; }

    /// <summary>
    /// Gets or sets whether spatial sampling was used.
    /// </summary>
    public bool SpatialSampling { get; init; }

    /// <summary>
    /// Gets or sets whether geometries were simplified.
    /// </summary>
    public bool Simplified { get; init; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>
    /// Gets or sets a message describing the preview limitations.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets or sets validation errors or warnings.
    /// </summary>
    public List<string>? Warnings { get; init; }
}
