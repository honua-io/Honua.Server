// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Export;

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Aggregates all format exporters for feature collections.
/// Supports multiple output formats in a single handler.
/// </summary>
/// <remarks>
/// Feature collections can be exported in various geospatial formats based on client needs:
/// - GeoPackage (.gpkg) - OGC standard for mobile/offline use
/// - Shapefile (.shp) - Industry standard for GIS interoperability
/// - FlatGeobuf (.fgb) - Cloud-optimized format for streaming
/// - GeoArrow (.arrow) - Columnar format for analytics
/// - CSV - Tabular format with optional geometry
///
/// This parameter object groups all format exporters together since they serve a common purpose
/// and are typically injected as a cohesive set of capabilities.
/// </remarks>
public sealed record OgcFeatureExportServices
{
    /// <summary>
    /// Exports features to GeoPackage format (.gpkg).
    /// GeoPackage is an OGC standard built on SQLite, ideal for mobile and offline scenarios.
    /// </summary>
    public required IGeoPackageExporter GeoPackage { get; init; }

    /// <summary>
    /// Exports features to Shapefile format (.shp).
    /// Shapefile is the de facto industry standard despite technical limitations.
    /// </summary>
    public required IShapefileExporter Shapefile { get; init; }

    /// <summary>
    /// Exports features to FlatGeobuf format (.fgb).
    /// FlatGeobuf is a cloud-optimized format supporting HTTP range requests for streaming.
    /// </summary>
    public required IFlatGeobufExporter FlatGeobuf { get; init; }

    /// <summary>
    /// Exports features to GeoArrow format (.arrow).
    /// GeoArrow uses columnar storage for efficient analytics and data science workflows.
    /// </summary>
    public required IGeoArrowExporter GeoArrow { get; init; }

    /// <summary>
    /// Exports features to CSV format with optional geometry.
    /// CSV provides simple tabular output for non-GIS tools, with WKT geometry if requested.
    /// </summary>
    public required ICsvExporter Csv { get; init; }
}
