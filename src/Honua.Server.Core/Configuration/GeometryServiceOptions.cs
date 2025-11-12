// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Configuration options for the Geometry Service.
/// </summary>
public sealed class GeometryServiceOptions
{
    public const string SectionName = "Honua:Services:Geometry";

    /// <summary>
    /// Whether the geometry service is enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum number of geometries allowed per operation.
    /// </summary>
    public int MaxGeometries { get; init; } = 1000;

    /// <summary>
    /// Maximum total coordinate count across all geometries in an operation.
    /// </summary>
    public int MaxCoordinateCount { get; init; } = 100000;

    /// <summary>
    /// List of allowed spatial reference IDs (SRIDs/WKIDs). Empty list means all are allowed.
    /// </summary>
    public List<int>? AllowedSrids { get; init; }
}
