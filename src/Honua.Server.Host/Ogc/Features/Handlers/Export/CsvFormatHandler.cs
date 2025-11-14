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
/// Format handler for CSV (Comma-Separated Values) export format.
/// CSV is a simple text-based format widely supported by spreadsheet applications,
/// databases, and data processing tools.
/// </summary>
/// <remarks>
/// <para>
/// CSV export streams features directly from the repository through the ICsvExporter
/// without buffering all features in memory, making it suitable for large datasets.
/// </para>
/// <para>
/// <strong>Features:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Human-readable text format</description></item>
/// <item><description>Universal compatibility with spreadsheet and database tools</description></item>
/// <item><description>Geometry exported as WKT (Well-Known Text) or GeoJSON</description></item>
/// <item><description>Configurable delimiters and encoding</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Does not support resultType=hits (validation error returned)</description></item>
/// <item><description>No native CRS support (geometry format determines coordinate interpretation)</description></item>
/// <item><description>Limited data type preservation (all values become text)</description></item>
/// <item><description>Larger file sizes compared to binary formats</description></item>
/// </list>
/// </remarks>
public sealed class CsvFormatHandler : ExportFormatHandlerBase
{
    private readonly ICsvExporter csvExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvFormatHandler"/> class.
    /// </summary>
    /// <param name="csvExporter">The CSV exporter service.</param>
    /// <exception cref="ArgumentNullException">Thrown if csvExporter is null.</exception>
    public CsvFormatHandler(ICsvExporter csvExporter)
    {
        Guard.NotNull(csvExporter);
        this.csvExporter = csvExporter;
    }

    /// <inheritdoc/>
    public override OgcSharedHandlers.OgcResponseFormat Format => OgcSharedHandlers.OgcResponseFormat.Csv;

    /// <inheritdoc/>
    protected override string GetFormatDisplayName() => "CSV";

    /// <inheritdoc/>
    /// <remarks>
    /// Handles the CSV export request by:
    /// <list type="number">
    /// <item><description>Streaming features from the repository</description></item>
    /// <item><description>Exporting to CSV format with WKT or GeoJSON geometry encoding</description></item>
    /// <item><description>Returning the .csv file as a downloadable file</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 262-277.
    /// Note that CSV export does not use the contentCrs parameter - geometry is exported
    /// in the query's CRS as WKT or GeoJSON text without additional transformation.
    /// </remarks>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Lines 269-273 from OgcFeaturesHandlers.Items.cs:
        // Export CSV using the exporter service
        // Note: CSV exporter does not take contentCrs parameter
        var csvResult = await this.csvExporter.ExportAsync(
            request.Layer,
            request.Query,
            request.Dependencies.Repository.QueryAsync(request.Service.Id, request.Layer.Id, request.Query, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Lines 275-276 from OgcFeaturesHandlers.Items.cs:
        // Return file result with cache headers
        return Results.File(csvResult.Content, OgcSharedHandlers.GetMimeType(this.Format), csvResult.FileName)
            .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);
    }
}
