// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

#nullable enable

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Represents kerchunk reference mapping for virtual Zarr access to NetCDF/HDF5/GRIB files.
/// Maps Zarr chunk coordinates to byte ranges in the source file.
/// </summary>
public sealed record KerchunkReferences
{
    /// <summary>
    /// Kerchunk format version (typically "1.0").
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Chunk reference mappings: key = "variable/chunk.coords", value = [uri, offset, length] or inline data.
    /// Example: "temperature/0.1.2" => ["s3://bucket/file.nc", 12345, 67890]
    /// </summary>
    public Dictionary<string, object> Refs { get; init; } = new();

    /// <summary>
    /// Zarr metadata: .zarray, .zattrs, .zgroup, etc.
    /// Example: "temperature/.zarray" =&gt; { chunks: [256, 256], dtype: "&lt;f4", ... }
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Timestamp when these references were generated.
    /// </summary>
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Source file URI these references point to.
    /// </summary>
    public string? SourceUri { get; init; }
}

/// <summary>
/// Options for controlling kerchunk reference generation.
/// </summary>
public sealed record KerchunkGenerationOptions
{
    /// <summary>
    /// Specific variables to include (null/empty = all variables).
    /// Example: ["temperature", "salinity"]
    /// </summary>
    public string[]? Variables { get; init; }

    /// <summary>
    /// Whether to inline small chunks directly in the JSON (vs byte range reference).
    /// </summary>
    public bool InlineThreshold { get; init; } = true;

    /// <summary>
    /// Maximum size in bytes for inlining chunks (default 1KB).
    /// Chunks larger than this use byte range references.
    /// </summary>
    public int MaxInlineSize { get; init; } = 1024;

    /// <summary>
    /// Whether to include coordinate variables (lat, lon, time).
    /// </summary>
    public bool IncludeCoordinates { get; init; } = true;

    /// <summary>
    /// Whether to consolidate metadata into .zmetadata (faster access).
    /// </summary>
    public bool ConsolidateMetadata { get; init; } = true;
}
