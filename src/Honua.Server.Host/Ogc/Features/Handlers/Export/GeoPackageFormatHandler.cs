// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Export;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Ogc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Export;

/// <summary>
/// Format handler for GeoPackage export format.
/// GeoPackage is an OGC standard for storing geospatial features in SQLite databases.
/// This handler streams features directly from the repository to a GeoPackage file
/// without buffering them in memory, making it suitable for large datasets.
/// </summary>
/// <remarks>
/// GeoPackage format characteristics:
/// - Output: SQLite database file (.gpkg)
/// - MIME type: application/geopackage+sqlite3
/// - Streaming: Yes (features are streamed directly to the file)
/// - Memory efficient: Yes (no buffering required)
/// - Supports: Multiple CRS, spatial indexing, metadata
/// - Limitations: Does not support resultType=hits
/// </remarks>
public sealed class GeoPackageFormatHandler : ExportFormatHandlerBase
{
    private readonly IGeoPackageExporter _geoPackageExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoPackageFormatHandler"/> class.
    /// </summary>
    /// <param name="geoPackageExporter">The GeoPackage exporter service for generating .gpkg files.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="geoPackageExporter"/> is null.</exception>
    public GeoPackageFormatHandler(IGeoPackageExporter geoPackageExporter)
    {
        _geoPackageExporter = Guard.NotNull(geoPackageExporter);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="OgcResponseFormat.GeoPackage"/>.
    /// </remarks>
    public override OgcResponseFormat Format => OgcResponseFormat.GeoPackage;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns "GeoPackage" for use in validation error messages.
    /// </remarks>
    protected override string GetFormatDisplayName() => "GeoPackage";

    /// <inheritdoc/>
    /// <remarks>
    /// Exports features to a GeoPackage (.gpkg) file and returns it as a downloadable file.
    /// The implementation:
    /// <list type="number">
    /// <item><description>Retrieves the feature stream from the request</description></item>
    /// <item><description>Delegates to <see cref="IGeoPackageExporter"/> to generate the .gpkg file</description></item>
    /// <item><description>Returns the file with appropriate MIME type and cache headers</description></item>
    /// </list>
    /// The exporter streams features directly to the SQLite database without buffering,
    /// making this suitable for large datasets.
    /// </remarks>
    /// <param name="request">The format request containing layer metadata, query parameters, and feature stream.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// A file result containing the GeoPackage database with:
    /// <list type="bullet">
    /// <item><description>MIME type: application/geopackage+sqlite3</description></item>
    /// <item><description>File name: {layer-id}.gpkg</description></item>
    /// <item><description>Cache headers for feature resources</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the request does not contain a feature stream, or when the export fails.
    /// </exception>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Ensure we have features to export
        if (request.Features is null)
        {
            throw new InvalidOperationException(
                "GeoPackage export requires a feature stream. The FormatRequest.Features property must not be null.");
        }

        // Delegate to the GeoPackage exporter to generate the .gpkg file
        // The exporter will stream features directly from the repository to the SQLite database
        var exportResult = await _geoPackageExporter.ExportAsync(
            request.Layer,
            request.Query,
            request.ContentCrs,
            request.Features,
            cancellationToken).ConfigureAwait(false);

        // Get the MIME type for GeoPackage format
        var mimeType = OgcSharedHandlers.GetMimeType(Format);

        // Return the file with cache headers
        // The file stream will be automatically disposed by ASP.NET Core after the response is sent
        return Results.File(exportResult.Content, mimeType, exportResult.FileName)
            .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);
    }
}
