// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Export;
using Honua.Server.Core.Raster.Export;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Aggregates all format exporters for feature collections.
/// Supports multiple output formats (GeoPackage, Shapefile, FlatGeobuf, GeoArrow, CSV)
/// in a single handler without polluting method signatures.
/// </summary>
public sealed record OgcFeatureExportServices
{
    /// <summary>
    /// Exports features to GeoPackage format (.gpkg).
    /// Supports complex geometries and multiple tables.
    /// </summary>
    public required IGeoPackageExporter GeoPackage { get; init; }

    /// <summary>
    /// Exports features to Shapefile format (.shp).
    /// Legacy format with limited data type support.
    /// </summary>
    public required IShapefileExporter Shapefile { get; init; }

    /// <summary>
    /// Exports features to FlatGeobuf format (.fgb).
    /// Cloud-optimized format supporting streaming and spatial indexing.
    /// </summary>
    public required IFlatGeobufExporter FlatGeobuf { get; init; }

    /// <summary>
    /// Exports features to GeoArrow format (.arrow).
    /// Columnar format optimized for analytical workloads.
    /// </summary>
    public required IGeoArrowExporter GeoArrow { get; init; }

    /// <summary>
    /// Exports features to CSV format with optional geometry.
    /// Simple tabular format for basic data exchange.
    /// </summary>
    public required ICsvExporter Csv { get; init; }
}
