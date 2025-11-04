using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Null implementation of IGeoPackageExporter that throws NotSupportedException.
/// Use when testing code that doesn't actually need to export GeoPackage files.
/// </summary>
public sealed class NullGeoPackageExporter : IGeoPackageExporter
{
    /// <summary>
    /// Throws NotSupportedException.
    /// </summary>
    public Task<GeoPackageExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> source,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("NullGeoPackageExporter does not support actual exports. Use a real implementation or configure tests to avoid export operations.");
    }
}

/// <summary>
/// Null implementation of IShapefileExporter that throws NotSupportedException.
/// Use when testing code that doesn't actually need to export Shapefile files.
/// </summary>
public sealed class NullShapefileExporter : IShapefileExporter
{
    /// <summary>
    /// Throws NotSupportedException.
    /// </summary>
    public Task<ShapefileExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> source,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("NullShapefileExporter does not support actual exports. Use a real implementation or configure tests to avoid export operations.");
    }
}

/// <summary>
/// Null implementation of ICsvExporter that throws NotSupportedException.
/// Use when testing code that doesn't actually need to export CSV files.
/// </summary>
public sealed class NullCsvExporter : ICsvExporter
{
    /// <summary>
    /// Throws NotSupportedException.
    /// </summary>
    public Task<CsvExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("NullCsvExporter does not support actual exports. Use a real implementation or configure tests to avoid export operations.");
    }
}
