// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging;
using OSGeo.GDAL;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// Routes raster datasets to optimal storage format (COG vs Zarr).
/// Decision based on dataset characteristics (temporal, spatial, size).
/// </summary>
public sealed class RasterStorageRouter
{
    private readonly ILogger<RasterStorageRouter> _logger;
    private readonly IRasterCacheService _cogCache;
    private readonly IZarrTimeSeriesService _zarrService;
    private readonly RasterStorageRoutingOptions _options;

    public RasterStorageRouter(
        ILogger<RasterStorageRouter> logger,
        IRasterCacheService cogCache,
        IZarrTimeSeriesService zarrService,
        RasterStorageRoutingOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cogCache = cogCache ?? throw new ArgumentNullException(nameof(cogCache));
        _zarrService = zarrService ?? throw new ArgumentNullException(nameof(zarrService));
        _options = options ?? RasterStorageRoutingOptions.Default;
    }

    /// <summary>
    /// Analyze dataset and route to optimal storage (COG or Zarr).
    /// Returns URI to optimized dataset.
    /// </summary>
    public async Task<RasterStorageDecision> RouteAndConvertAsync(
        RasterDatasetDefinition dataset,
        CancellationToken cancellationToken = default)
    {
        var sourceUri = dataset.Source.Uri;
        var analysis = await AnalyzeDatasetAsync(sourceUri, cancellationToken);

        _logger.LogInformation(
            "Dataset {DatasetId} analysis: Format={Format}, HasTime={HasTime}, TimeSteps={TimeSteps}, Dimensions={Dimensions}",
            dataset.Id, analysis.Format, analysis.HasTimeDimension, analysis.TimeSteps, analysis.Dimensions);

        // Decision logic: COG vs Zarr
        var useZarr = ShouldUseZarr(analysis);

        if (useZarr && _options.ZarrEnabled)
        {
            _logger.LogInformation("Routing {DatasetId} to Zarr (time-series optimized)", dataset.Id);

            var zarrUri = GetZarrOutputPath(dataset.Id);
            var variableName = await DetectVariableNameAsync(sourceUri, cancellationToken);

            var zarrOptions = new ZarrConversionOptions
            {
                VariableName = variableName,
                TimeChunkSize = _options.ZarrTimeChunkSize,
                LatitudeChunkSize = _options.ZarrSpatialChunkSize,
                LongitudeChunkSize = _options.ZarrSpatialChunkSize,
                Compression = _options.ZarrCompression
            };

            await _zarrService.ConvertToZarrAsync(sourceUri, zarrUri, zarrOptions, cancellationToken);

            return new RasterStorageDecision
            {
                DatasetId = dataset.Id,
                OriginalUri = sourceUri,
                OptimizedUri = zarrUri,
                StorageFormat = RasterStorageFormat.Zarr,
                Reason = $"Multi-temporal dataset with {analysis.TimeSteps} time steps"
            };
        }
        else
        {
            _logger.LogInformation("Routing {DatasetId} to COG (spatial optimized)", dataset.Id);

            var cogOptions = new CogConversionOptions
            {
                Compression = _options.CogCompression,
                BlockSize = _options.CogBlockSize
            };

            var cogUri = await _cogCache.ConvertToCogAsync(sourceUri, cogOptions, cancellationToken);

            return new RasterStorageDecision
            {
                DatasetId = dataset.Id,
                OriginalUri = sourceUri,
                OptimizedUri = cogUri,
                StorageFormat = RasterStorageFormat.COG,
                Reason = useZarr
                    ? "Zarr disabled, using COG fallback"
                    : "Single-time or static raster, COG optimal"
            };
        }
    }

    private bool ShouldUseZarr(DatasetAnalysis analysis)
    {
        // Use Zarr if:
        // 1. Has time dimension with multiple time steps
        // 2. Time steps > threshold (default: 3+)
        // 3. Is multi-dimensional (3D or 4D)

        if (!analysis.HasTimeDimension)
        {
            return false;
        }

        if (analysis.TimeSteps >= _options.ZarrTimeStepThreshold)
        {
            return true;
        }

        // Even with few time steps, use Zarr for 4D data (time + level + lat + lon)
        if (analysis.Dimensions >= 4)
        {
            return true;
        }

        return false;
    }

    private async Task<DatasetAnalysis> AnalyzeDatasetAsync(string sourceUri, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(sourceUri).ToLowerInvariant();

        // For NetCDF/HDF5, analyze dimensions
        if (extension is ".nc" or ".nc4" or ".netcdf" or ".h5" or ".hdf" or ".hdf5")
        {
            return await AnalyzeNetCdfHdf5Async(sourceUri, cancellationToken);
        }

        // For GRIB2, check for multiple messages
        if (extension is ".grib" or ".grib2" or ".grb" or ".grb2")
        {
            return await AnalyzeGrib2Async(sourceUri, cancellationToken);
        }

        // For GeoTIFF, always use COG
        return new DatasetAnalysis
        {
            Format = "GeoTIFF",
            HasTimeDimension = false,
            TimeSteps = 1,
            Dimensions = 2
        };
    }

    private async Task<DatasetAnalysis> AnalyzeNetCdfHdf5Async(string sourceUri, CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Async for consistency

        try
        {
            using var dataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
            if (dataset == null)
            {
                throw new InvalidOperationException($"Failed to open dataset: {sourceUri}");
            }

            // Check subdatasets (NetCDF/HDF5 often have multiple variables)
            var metadata = dataset.GetMetadata("SUBDATASETS");
            var hasSubdatasets = metadata != null && metadata.Length > 0;

            // Try to detect time dimension from metadata
            var globalMetadata = dataset.GetMetadata("");
            var hasTime = globalMetadata != null &&
                          (globalMetadata.Any(k => k.Contains("time", StringComparison.OrdinalIgnoreCase)) ||
                           globalMetadata.Any(k => k.Contains("DIMENSION_LIST", StringComparison.OrdinalIgnoreCase)));

            return new DatasetAnalysis
            {
                Format = Path.GetExtension(sourceUri).Contains(".nc") ? "NetCDF" : "HDF5",
                HasTimeDimension = hasTime,
                TimeSteps = hasTime ? EstimateTimeSteps(dataset) : 1,
                Dimensions = hasTime ? 3 : 2
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze dataset {SourceUri}, assuming single-time raster", sourceUri);
            return new DatasetAnalysis
            {
                Format = "Unknown",
                HasTimeDimension = false,
                TimeSteps = 1,
                Dimensions = 2
            };
        }
    }

    private async Task<DatasetAnalysis> AnalyzeGrib2Async(string sourceUri, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        try
        {
            using var dataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
            if (dataset == null)
            {
                return new DatasetAnalysis { Format = "GRIB2", HasTimeDimension = false, TimeSteps = 1, Dimensions = 2 };
            }

            // GRIB2 files can contain multiple messages/bands
            var bandCount = dataset.RasterCount;

            return new DatasetAnalysis
            {
                Format = "GRIB2",
                HasTimeDimension = bandCount > 1,
                TimeSteps = bandCount,
                Dimensions = bandCount > 1 ? 3 : 2
            };
        }
        catch
        {
            return new DatasetAnalysis { Format = "GRIB2", HasTimeDimension = false, TimeSteps = 1, Dimensions = 2 };
        }
    }

    private int EstimateTimeSteps(Dataset dataset)
    {
        // Try to extract time dimension size from metadata
        var metadata = dataset.GetMetadata("");
        if (metadata != null)
        {
            foreach (string metadataItem in metadata)
            {
                if (metadataItem.Contains("time", StringComparison.OrdinalIgnoreCase) &&
                    metadataItem.Contains("size", StringComparison.OrdinalIgnoreCase))
                {
                    // Parse metadata item format: "key=value"
                    var parts = metadataItem.Split('=');
                    if (parts.Length == 2 && int.TryParse(parts[1], out var size))
                    {
                        return size;
                    }
                }
            }
        }

        // Fallback: assume multiple time steps if has time dimension
        return 10; // Conservative estimate
    }

    private async Task<string> DetectVariableNameAsync(string sourceUri, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        try
        {
            using var dataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
            if (dataset == null)
            {
                _logger.LogWarning("Failed to open dataset {SourceUri} for variable detection", sourceUri);
                return "data";
            }

            // Check for subdatasets (NetCDF/HDF5 often have multiple variables)
            var subdatasets = dataset.GetMetadata("SUBDATASETS");
            if (subdatasets != null && subdatasets.Length > 0)
            {
                // Parse first subdataset to extract variable name
                // Format: SUBDATASET_1_NAME=NETCDF:"file.nc":variable_name
                foreach (string item in subdatasets)
                {
                    if (item.Contains("SUBDATASET_1_NAME=", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = item.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var subdatasetPath = parts[1];
                            // Extract variable name from subdataset path
                            var variableName = ExtractVariableFromSubdatasetPath(subdatasetPath);
                            if (!variableName.IsNullOrEmpty())
                            {
                                _logger.LogDebug("Detected variable name: {VariableName} from subdataset", variableName);
                                return variableName;
                            }
                        }
                    }
                }
            }

            // Try to get variable name from global metadata
            var globalMetadata = dataset.GetMetadata("");
            if (globalMetadata != null)
            {
                foreach (string item in globalMetadata)
                {
                    // Look for common variable metadata patterns
                    if (item.Contains("NC_GLOBAL#", StringComparison.OrdinalIgnoreCase) ||
                        item.Contains("variables", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = item.Split('=', 2);
                        if (parts.Length == 2 && parts[1].HasValue())
                        {
                            // Extract first variable name from comma-separated list
                            var variables = parts[1].Split(',');
                            if (variables.Length > 0)
                            {
                                var varName = variables[0].Trim();
                                if (!varName.IsNullOrEmpty())
                                {
                                    _logger.LogDebug("Detected variable name from metadata: {VariableName}", varName);
                                    return varName;
                                }
                            }
                        }
                    }
                }
            }

            // Fallback: use extension-based defaults
            var extension = Path.GetExtension(sourceUri).ToLowerInvariant();
            if (extension.Contains(".nc"))
            {
                // Common NetCDF variable names as fallback
                return "temperature";
            }

            if (extension.Contains(".h5"))
            {
                // Common HDF5 dataset paths as fallback
                return "data";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting variable name for {SourceUri}, using default", sourceUri);
        }

        return "data";
    }

    private string? ExtractVariableFromSubdatasetPath(string subdatasetPath)
    {
        // Parse subdataset path formats:
        // NETCDF:"file.nc":variable_name
        // HDF5:"file.h5"://path/to/variable
        // GRIB:"file.grib":band_number:variable

        if (subdatasetPath.Contains("NETCDF:", StringComparison.OrdinalIgnoreCase))
        {
            var lastColon = subdatasetPath.LastIndexOf(':');
            if (lastColon > 0)
            {
                return subdatasetPath.Substring(lastColon + 1).Trim('"', ' ');
            }
        }
        else if (subdatasetPath.Contains("HDF5:", StringComparison.OrdinalIgnoreCase))
        {
            var lastDoubleSlash = subdatasetPath.LastIndexOf("//");
            if (lastDoubleSlash > 0)
            {
                var path = subdatasetPath.Substring(lastDoubleSlash + 2).Trim('"', ' ');
                // Extract last component of path as variable name
                var lastSlash = path.LastIndexOf('/');
                return lastSlash > 0 ? path.Substring(lastSlash + 1) : path;
            }
        }

        return null;
    }

    private string GetZarrOutputPath(string datasetId)
    {
        var baseDir = _options.ZarrCacheDirectory ?? Path.Combine("data", "raster-zarr");
        Directory.CreateDirectory(baseDir);
        return Path.Combine(baseDir, $"{datasetId}.zarr");
    }
}

public sealed record DatasetAnalysis
{
    public required string Format { get; init; }
    public required bool HasTimeDimension { get; init; }
    public required int TimeSteps { get; init; }
    public required int Dimensions { get; init; }
}

public sealed record RasterStorageDecision
{
    public required string DatasetId { get; init; }
    public required string OriginalUri { get; init; }
    public required string OptimizedUri { get; init; }
    public required RasterStorageFormat StorageFormat { get; init; }
    public required string Reason { get; init; }
}

public enum RasterStorageFormat
{
    COG,
    Zarr
}

public sealed record RasterStorageRoutingOptions
{
    public static RasterStorageRoutingOptions Default => new();

    public bool ZarrEnabled { get; init; } = true;
    public int ZarrTimeStepThreshold { get; init; } = 3;
    public int ZarrTimeChunkSize { get; init; } = 1;
    public int ZarrSpatialChunkSize { get; init; } = 128;
    public string ZarrCompression { get; init; } = "zstd";
    public string? ZarrCacheDirectory { get; init; }
    public string CogCompression { get; init; } = "DEFLATE";
    public int CogBlockSize { get; init; } = 512;
}
