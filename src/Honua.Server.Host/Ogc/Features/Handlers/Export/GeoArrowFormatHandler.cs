// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Export;

/// <summary>
/// Format handler for GeoArrow export format.
/// GeoArrow is a specification for storing geospatial data in Apache Arrow columnar format,
/// enabling high-performance analytics and interoperability with data science tools.
/// </summary>
/// <remarks>
/// <para>
/// GeoArrow export streams features directly from the repository through the IGeoArrowExporter
/// without buffering all features in memory, making it suitable for large datasets.
/// </para>
/// <para>
/// <strong>Features:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Columnar format optimized for analytical queries</description></item>
/// <item><description>Zero-copy reads for maximum performance</description></item>
/// <item><description>Native integration with Apache Arrow ecosystem</description></item>
/// <item><description>Supports all Arrow data types</description></item>
/// <item><description>Full CRS support with metadata embedding</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Does not support resultType=hits (validation error returned)</description></item>
/// </list>
/// </remarks>
public sealed class GeoArrowFormatHandler : ExportFormatHandlerBase
{
    private readonly IGeoArrowExporter geoArrowExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoArrowFormatHandler"/> class.
    /// </summary>
    /// <param name="geoArrowExporter">The GeoArrow exporter service.</param>
    /// <exception cref="ArgumentNullException">Thrown if geoArrowExporter is null.</exception>
    public GeoArrowFormatHandler(IGeoArrowExporter geoArrowExporter)
    {
        Guard.NotNull(geoArrowExporter);
        this.geoArrowExporter = geoArrowExporter;
    }

    /// <inheritdoc/>
    public override OgcResponseFormat Format => OgcResponseFormat.GeoArrow;

    /// <inheritdoc/>
    protected override string GetFormatDisplayName() => "GeoArrow";

    /// <inheritdoc/>
    /// <remarks>
    /// Handles the GeoArrow export request by:
    /// <list type="number">
    /// <item><description>Resolving the effective CRS (defaults to CRS84 if not specified)</description></item>
    /// <item><description>Streaming features from the repository</description></item>
    /// <item><description>Exporting to Apache Arrow IPC format with GeoArrow geometry encoding</description></item>
    /// <item><description>Returning the .arrow file as a downloadable file</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 244-261.
    /// </remarks>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Line 251 from OgcFeaturesHandlers.Items.cs:
        // Determine effective CRS - use default CRS84 if not specified
        var effectiveCrs = string.IsNullOrWhiteSpace(request.ContentCrs) ? CrsHelper.DefaultCrsIdentifier : request.ContentCrs;

        // Lines 252-257 from OgcFeaturesHandlers.Items.cs:
        // Export GeoArrow using the exporter service
        var exportResult = await this.geoArrowExporter.ExportAsync(
            request.Layer,
            request.Query,
            effectiveCrs,
            request.Dependencies.Repository.QueryAsync(request.Service.Id, request.Layer.Id, request.Query, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Lines 259-260 from OgcFeaturesHandlers.Items.cs:
        // Return file result with cache headers
        return Results.File(exportResult.Content, OgcSharedHandlers.GetMimeType(this.Format), exportResult.FileName)
            .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);
    }
}
