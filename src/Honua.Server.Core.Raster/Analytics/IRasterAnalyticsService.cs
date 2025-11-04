// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Analytics;

/// <summary>
/// Service for performing analytical operations on raster data
/// </summary>
public interface IRasterAnalyticsService
{
    /// <summary>
    /// Calculate statistics for a raster dataset
    /// </summary>
    Task<RasterStatistics> CalculateStatisticsAsync(RasterStatisticsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform algebra operations on raster datasets
    /// </summary>
    Task<RasterAlgebraResult> CalculateAlgebraAsync(RasterAlgebraRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract values at specific points
    /// </summary>
    Task<RasterValueExtractionResult> ExtractValuesAsync(RasterValueExtractionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate histogram for a raster dataset
    /// </summary>
    Task<RasterHistogram> CalculateHistogramAsync(RasterHistogramRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate zonal statistics for polygons
    /// </summary>
    Task<ZonalStatisticsResult> CalculateZonalStatisticsAsync(ZonalStatisticsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate terrain analysis (hillshade, slope, aspect)
    /// </summary>
    Task<TerrainAnalysisResult> CalculateTerrainAsync(TerrainAnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get analytics capabilities
    /// </summary>
    RasterAnalyticsCapabilities GetCapabilities();
}

public sealed record RasterStatisticsRequest(
    RasterDatasetDefinition Dataset,
    double[]? BoundingBox = null,
    int? BandIndex = null);

public sealed record RasterStatistics(
    string DatasetId,
    int BandCount,
    IReadOnlyList<BandStatistics> Bands,
    double[]? BoundingBox);

public sealed record BandStatistics(
    int BandIndex,
    double Min,
    double Max,
    double Mean,
    double StdDev,
    double Median,
    long ValidPixelCount,
    long NoDataPixelCount,
    double? NoDataValue);

public sealed record RasterAlgebraRequest(
    IReadOnlyList<RasterDatasetDefinition> Datasets,
    string Expression,
    double[] BoundingBox,
    int Width,
    int Height,
    string Format);

public sealed record RasterAlgebraResult(
    byte[] Data,
    string ContentType,
    int Width,
    int Height,
    RasterStatistics Statistics);

public sealed record RasterValueExtractionRequest(
    RasterDatasetDefinition Dataset,
    IReadOnlyList<Point> Points,
    int? BandIndex = null);

public sealed record Point(double X, double Y);

public sealed record RasterValueExtractionResult(
    string DatasetId,
    IReadOnlyList<PointValue> Values);

public sealed record PointValue(
    double X,
    double Y,
    double? Value,
    int BandIndex);

public sealed record RasterHistogramRequest(
    RasterDatasetDefinition Dataset,
    int BinCount = 256,
    double[]? BoundingBox = null,
    int? BandIndex = null);

public sealed record RasterHistogram(
    string DatasetId,
    int BandIndex,
    IReadOnlyList<HistogramBin> Bins,
    double Min,
    double Max);

public sealed record HistogramBin(
    double RangeStart,
    double RangeEnd,
    long Count);

public sealed record ZonalStatisticsRequest(
    RasterDatasetDefinition Dataset,
    IReadOnlyList<Polygon> Zones,
    int? BandIndex = null,
    IReadOnlyList<string>? Statistics = null); // e.g., ["mean", "min", "max", "sum", "count", "stddev"]

public sealed record Polygon(
    IReadOnlyList<Point> Coordinates,
    string? ZoneId = null,
    Dictionary<string, object>? Properties = null);

public sealed record ZonalStatisticsResult(
    string DatasetId,
    IReadOnlyList<ZoneStatistics> Zones);

public sealed record ZoneStatistics(
    string? ZoneId,
    int BandIndex,
    double Mean,
    double Min,
    double Max,
    double Sum,
    double StdDev,
    long PixelCount,
    double? Median = null,
    Dictionary<string, object>? Properties = null);

public sealed record TerrainAnalysisRequest(
    RasterDatasetDefinition ElevationDataset,
    TerrainAnalysisType AnalysisType,
    double[]? BoundingBox = null,
    int Width = 512,
    int Height = 512,
    string Format = "png",
    double ZFactor = 1.0,  // Vertical exaggeration
    double Azimuth = 315.0,  // Light source angle for hillshade (degrees)
    double Altitude = 45.0);  // Light source altitude for hillshade (degrees)

public enum TerrainAnalysisType
{
    /// <summary>Hillshade (relief shading)</summary>
    Hillshade,

    /// <summary>Slope in degrees</summary>
    Slope,

    /// <summary>Aspect in degrees (0-360)</summary>
    Aspect,

    /// <summary>Curvature (rate of slope change)</summary>
    Curvature,

    /// <summary>Roughness (terrain variation)</summary>
    Roughness
}

public sealed record TerrainAnalysisResult(
    byte[] Data,
    string ContentType,
    int Width,
    int Height,
    TerrainAnalysisType AnalysisType,
    TerrainAnalysisStatistics Statistics);

public sealed record TerrainAnalysisStatistics(
    double MinValue,
    double MaxValue,
    double MeanValue,
    double StdDevValue,
    string Unit);

public sealed record RasterAnalyticsCapabilities(
    IReadOnlyList<string> SupportedAlgebraOperators,
    IReadOnlyList<string> SupportedAlgebraFunctions,
    IReadOnlyList<string> SupportedTerrainAnalyses,
    int MaxAlgebraDatasets,
    int MaxExtractionPoints,
    int MaxHistogramBins,
    int MaxZonalPolygons = 1000);
