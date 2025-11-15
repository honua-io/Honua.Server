// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Features.Handlers.Export;

/// <summary>
/// Format handler for FlatGeobuf export format.
/// FlatGeobuf is a performant binary encoding for geographic data based on FlatBuffers
/// that supports streaming and random access.
/// </summary>
/// <remarks>
/// <para>
/// FlatGeobuf export streams features directly from the repository through the IFlatGeobufExporter
/// without buffering all features in memory, making it suitable for large datasets.
/// </para>
/// <para>
/// <strong>Features:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Efficient binary format with small file sizes</description></item>
/// <item><description>Supports spatial indexing for fast spatial queries</description></item>
/// <item><description>Streamable format suitable for web delivery</description></item>
/// <item><description>Full CRS support with EPSG code embedding</description></item>
/// </list>
/// <para>
/// <strong>Limitations:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Does not support resultType=hits (validation error returned)</description></item>
/// </list>
/// </remarks>
public sealed class FlatGeobufFormatHandler : ExportFormatHandlerBase
{
    private readonly IFlatGeobufExporter flatGeobufExporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlatGeobufFormatHandler"/> class.
    /// </summary>
    /// <param name="flatGeobufExporter">The FlatGeobuf exporter service.</param>
    /// <exception cref="ArgumentNullException">Thrown if flatGeobufExporter is null.</exception>
    public FlatGeobufFormatHandler(IFlatGeobufExporter flatGeobufExporter)
    {
        Guard.NotNull(flatGeobufExporter);
        this.flatGeobufExporter = flatGeobufExporter;
    }

    /// <inheritdoc/>
    public override OgcResponseFormat Format => OgcResponseFormat.FlatGeobuf;

    /// <inheritdoc/>
    protected override string GetFormatDisplayName() => "FlatGeobuf";

    /// <inheritdoc/>
    /// <remarks>
    /// Handles the FlatGeobuf export request by:
    /// <list type="number">
    /// <item><description>Resolving the effective CRS (defaults to CRS84 if not specified)</description></item>
    /// <item><description>Streaming features from the repository</description></item>
    /// <item><description>Exporting to FlatGeobuf binary format with spatial index</description></item>
    /// <item><description>Returning the .fgb file as a downloadable file</description></item>
    /// </list>
    /// This method extracts the exact logic from OgcFeaturesHandlers.Items.cs lines 226-243.
    /// </remarks>
    public override async Task<IResult> HandleAsync(
        FormatRequest request,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        // Line 233 from OgcFeaturesHandlers.Items.cs:
        // Determine effective CRS - use default CRS84 if not specified
        var effectiveCrs = string.IsNullOrWhiteSpace(request.ContentCrs) ? CrsHelper.DefaultCrsIdentifier : request.ContentCrs;

        // Lines 234-239 from OgcFeaturesHandlers.Items.cs:
        // Export FlatGeobuf using the exporter service
        var exportResult = await this.flatGeobufExporter.ExportAsync(
            request.Layer,
            request.Query,
            effectiveCrs,
            request.Dependencies.Repository.QueryAsync(request.Service.Id, request.Layer.Id, request.Query, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        // Lines 241-242 from OgcFeaturesHandlers.Items.cs:
        // Return file result with cache headers
        return Results.File(exportResult.Content, OgcSharedHandlers.GetMimeType(this.Format), exportResult.FileName)
            .WithFeatureCacheHeaders(request.Dependencies.CacheHeaderService);
    }
}
