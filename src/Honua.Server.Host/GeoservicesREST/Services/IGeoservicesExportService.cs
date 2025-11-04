// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer export operations (Shapefile, KML, CSV).
/// </summary>
public interface IGeoservicesExportService
{
    /// <summary>
    /// Exports features to Shapefile format.
    /// </summary>
    Task<IActionResult> ExportShapefileAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exports features to KML or KMZ format.
    /// </summary>
    Task<IActionResult> ExportKmlAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        bool kmz,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exports features to CSV format.
    /// </summary>
    Task<IActionResult> ExportCsvAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exports an empty Shapefile (for scale-suppressed or no-result queries).
    /// </summary>
    Task<IActionResult> ExportEmptyShapefileAsync(
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exports an empty KML file (for scale-suppressed or no-result queries).
    /// </summary>
    Task<IActionResult> ExportEmptyKmlAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context);

    /// <summary>
    /// Exports an empty CSV file (for scale-suppressed or no-result queries).
    /// </summary>
    Task<IActionResult> ExportEmptyCsvAsync(
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);
}
