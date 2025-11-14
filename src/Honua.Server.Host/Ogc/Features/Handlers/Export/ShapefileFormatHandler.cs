// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Export;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Export;

/// <summary>
/// Format handler for Shapefile export format.
/// Shapefiles are a popular GIS vector data format consisting of multiple files (.shp, .shx, .dbf, .prj)
/// packaged into a ZIP archive for convenient distribution.
/// </summary>
/// <remarks>
/// <para>
/// Shapefile export streams features directly from the repository through the IShapefileExporter
/// without buffering all features in memory, making it suitable for large datasets.
/// </para>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Does not support resultType=hits (validation error returned)</description></item>
/// <item><description>Field names limited to 10 characters (truncated automatically)</description></item>
/// <item><description>String fields limited to 254 characters</description></item>
/// <item><description>Limited data type support compared to modern formats</description></item>
/// </list>
/// </remarks>
public sealed class ShapefileFormatHandler : ExportFormatHandlerBase
{
    private readonly IShapefileExporter shapefileExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShapefileFormatHandler"/> class.
    /// </summary>
    /// <param name="shapefileExporter">The shapefile exporter service.</param>
    /// <exception cref="ArgumentNullException">Thrown if shapefileExporter is null.</exception>
    public ShapefileFormatHandler(IShapefileExporter shapefileExporter)
    {
        Guard.NotNull(shapefileExporter);
        this.shapefileExporter = shapefileExporter;
    }

    /// <inheritdoc/>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.Shapefile;

    /// <inheritdoc/>
    protected override string GetFormatDisplayName() => "Shapefile";

    /// <inheritdoc/>
    /// <remarks>
    /// Handles the shapefile export request by:
    /// <list type="number">
    /// <item><description>Streaming features from the repository</description></item>
    /// <item><description>Exporting to shapefile format (multiple files in ZIP archive)</description></item>
    /// <item><description>Returning the ZIP archive as a downloadable file</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 209-225.
    /// </remarks>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Lines 216-221 from OgcFeaturesHandlers.Items.cs:
        // Export shapefile using the exporter service
        var shapefileResult = await this.shapefileExporter.ExportAsync(
            request.Layer,
            request.Query,
            request.ContentCrs,
            request.Dependencies.Repository.QueryAsync(request.Service.Id, request.Layer.Id, request.Query, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Lines 223-224 from OgcFeaturesHandlers.Items.cs:
        // Return file result with cache headers
        return Results.File(shapefileResult.Content, OgcSharedHandlers.GetMimeType(this.Format), shapefileResult.FileName)
            .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);
    }
}
