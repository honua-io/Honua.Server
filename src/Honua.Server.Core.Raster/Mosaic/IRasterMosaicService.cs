// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Mosaic;

/// <summary>
/// Service for creating raster mosaics from multiple datasets
/// </summary>
public interface IRasterMosaicService
{
    /// <summary>
    /// Create a mosaic from multiple raster datasets
    /// </summary>
    Task<RasterMosaicResult> CreateMosaicAsync(RasterMosaicRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata about available mosaic operations
    /// </summary>
    RasterMosaicCapabilities GetCapabilities();
}

public sealed record RasterMosaicRequest(
    IReadOnlyList<RasterDatasetDefinition> Datasets,
    double[] BoundingBox,
    int Width,
    int Height,
    string SourceCrs,
    string TargetCrs,
    string Format,
    bool Transparent,
    RasterMosaicMethod Method = RasterMosaicMethod.First,
    RasterResamplingMethod Resampling = RasterResamplingMethod.Bilinear,
    string? StyleId = null);

public sealed record RasterMosaicResult(
    byte[] Data,
    string ContentType,
    int Width,
    int Height,
    RasterMosaicMetadata Metadata);

public sealed record RasterMosaicMetadata(
    int DatasetCount,
    IReadOnlyList<string> DatasetIds,
    double[] BoundingBox,
    string Method,
    string Resampling);

public sealed record RasterMosaicCapabilities(
    IReadOnlyList<string> SupportedMethods,
    IReadOnlyList<string> SupportedResamplingMethods,
    IReadOnlyList<string> SupportedFormats,
    int MaxDatasets,
    int MaxWidth,
    int MaxHeight);

public enum RasterMosaicMethod
{
    /// <summary>First non-nodata value</summary>
    First,

    /// <summary>Last non-nodata value</summary>
    Last,

    /// <summary>Minimum value across datasets</summary>
    Min,

    /// <summary>Maximum value across datasets</summary>
    Max,

    /// <summary>Mean (average) value</summary>
    Mean,

    /// <summary>Median value</summary>
    Median,

    /// <summary>Blend with alpha transparency</summary>
    Blend
}

public enum RasterResamplingMethod
{
    /// <summary>Nearest neighbor (fastest)</summary>
    NearestNeighbor,

    /// <summary>Bilinear interpolation</summary>
    Bilinear,

    /// <summary>Cubic convolution</summary>
    Cubic,

    /// <summary>Lanczos windowed sinc</summary>
    Lanczos
}
