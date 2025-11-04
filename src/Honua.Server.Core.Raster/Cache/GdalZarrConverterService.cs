// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Raster.Interop;
using Microsoft.Extensions.Logging;
using OSGeo.GDAL;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// GDAL-based Zarr conversion service (GDAL 3.4+ Zarr driver).
/// Pure .NET implementation without Python dependency.
/// Falls back to Python interop if GDAL Zarr driver unavailable.
/// </summary>
public sealed class GdalZarrConverterService : IZarrTimeSeriesService
{
    private readonly ILogger<GdalZarrConverterService> _logger;
    private readonly IZarrTimeSeriesService? _pythonFallback;
    private readonly bool _gdalZarrAvailable;

    public GdalZarrConverterService(
        ILogger<GdalZarrConverterService> logger,
        IZarrTimeSeriesService? pythonFallback = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pythonFallback = pythonFallback;

        GdalInitializer.EnsureInitialized();
        _gdalZarrAvailable = CheckGdalZarrDriver();

        if (_gdalZarrAvailable)
        {
            _logger.LogInformation("GDAL Zarr driver available, using native .NET implementation");
        }
        else
        {
            _logger.LogWarning("GDAL Zarr driver not available, will use Python fallback if configured");
        }
    }

    public async Task ConvertToZarrAsync(
        string sourceUri,
        string zarrUri,
        ZarrConversionOptions options,
        CancellationToken cancellationToken = default)
    {
        if (_gdalZarrAvailable)
        {
            await ConvertViaGdalAsync(sourceUri, zarrUri, options, cancellationToken);
        }
        else if (_pythonFallback != null)
        {
            _logger.LogInformation("Using Python fallback for Zarr conversion");
            await _pythonFallback.ConvertToZarrAsync(sourceUri, zarrUri, options, cancellationToken);
        }
        else
        {
            throw new NotSupportedException(
                "GDAL Zarr driver not available and no Python fallback configured. " +
                "Upgrade to GDAL 3.4+ or install Python with xarray/zarr packages.");
        }
    }

    public Task<float[,,]> QueryTimeRangeAsync(
        string zarrUri,
        string variableName,
        DateTime startTime,
        DateTime endTime,
        double[]? bbox = null,
        CancellationToken cancellationToken = default)
    {
        if (_pythonFallback != null)
        {
            return _pythonFallback.QueryTimeRangeAsync(zarrUri, variableName, startTime, endTime, bbox, cancellationToken);
        }

        throw new NotSupportedException(
            "Zarr querying via GDAL is not supported without Python fallback. " +
            "Configure Python with xarray/zarr packages or access data as individual COG slices.");
    }

    public Task<float[,]> QueryTimeSliceAsync(
        string zarrUri,
        string variableName,
        DateTime time,
        double[]? bbox = null,
        CancellationToken cancellationToken = default)
    {
        if (_pythonFallback != null)
        {
            return _pythonFallback.QueryTimeSliceAsync(zarrUri, variableName, time, bbox, cancellationToken);
        }

        throw new NotSupportedException(
            "Zarr querying via GDAL is not supported without Python fallback. " +
            "Configure Python with xarray/zarr packages or access data as individual COG slices.");
    }

    public async Task<ZarrMetadata> GetMetadataAsync(string zarrUri, CancellationToken cancellationToken = default)
    {
        // Try reading .zarray/.zmetadata files
        var zarrayPath = Path.Combine(zarrUri, ".zarray");
        var zmetadataPath = Path.Combine(zarrUri, ".zmetadata");

        if (File.Exists(zarrayPath) || File.Exists(zmetadataPath))
        {
            // Basic metadata from filesystem
            return new ZarrMetadata
            {
                Uri = zarrUri,
                Dimensions = Array.Empty<ZarrDimensionInfo>(),
                Variables = Array.Empty<string>(),
                Attributes = Array.Empty<ZarrAttributeInfo>()
            };
        }

        if (_pythonFallback != null)
        {
            return await _pythonFallback.GetMetadataAsync(zarrUri, cancellationToken);
        }

        throw new FileNotFoundException($"Zarr metadata not found at: {zarrUri}");
    }

    public Task<bool> ExistsAsync(string zarrUri, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(zarrUri))
        {
            var zarrayPath = Path.Combine(zarrUri, ".zarray");
            var zmetadataPath = Path.Combine(zarrUri, ".zmetadata");
            return Task.FromResult(File.Exists(zarrayPath) || File.Exists(zmetadataPath));
        }

        return Task.FromResult(false);
    }

    public Task<ZarrTimeSlice> QueryTimeSliceAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset timestamp,
        BoundingBox? spatialExtent = null,
        CancellationToken cancellationToken = default)
    {
        if (_pythonFallback != null)
        {
            return _pythonFallback.QueryTimeSliceAsync(zarrPath, variableName, timestamp, spatialExtent, cancellationToken);
        }

        throw new NotSupportedException(
            "Zarr time-slice querying via GDAL is not supported without Python fallback. " +
            "Configure Python with xarray/zarr packages or access data as individual COG slices.");
    }

    public Task<IReadOnlyList<DateTimeOffset>> GetTimeStepsAsync(
        string zarrPath,
        string variableName,
        CancellationToken cancellationToken = default)
    {
        if (_pythonFallback != null)
        {
            return _pythonFallback.GetTimeStepsAsync(zarrPath, variableName, cancellationToken);
        }

        throw new NotSupportedException(
            "Zarr timestep reading via GDAL is not supported without Python fallback. " +
            "Configure Python with xarray/zarr packages.");
    }

    public Task<ZarrTimeSeriesData> QueryTimeRangeAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        BoundingBox? spatialExtent = null,
        TimeSpan? aggregationInterval = null,
        CancellationToken cancellationToken = default)
    {
        if (_pythonFallback != null)
        {
            return _pythonFallback.QueryTimeRangeAsync(zarrPath, variableName, startTime, endTime, spatialExtent, aggregationInterval, cancellationToken);
        }

        throw new NotSupportedException(
            "Zarr time-range querying via GDAL is not supported without Python fallback. " +
            "Configure Python with xarray/zarr packages or access data as individual COG slices.");
    }

    private async Task ConvertViaGdalAsync(
        string sourceUri,
        string zarrUri,
        ZarrConversionOptions options,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            _logger.LogInformation("Converting {SourceUri} to Zarr via GDAL at {ZarrUri}", sourceUri, zarrUri);

            // Open source dataset
            using var sourceDataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
            if (sourceDataset == null)
            {
                throw new InvalidOperationException($"Failed to open source dataset: {sourceUri}");
            }

            // Get Zarr driver
            using var zarrDriver = Gdal.GetDriverByName("Zarr");
            if (zarrDriver == null)
            {
                throw new InvalidOperationException("GDAL Zarr driver not available");
            }

            // Build creation options
            var creationOptions = new[]
            {
                $"COMPRESS={options.Compression.ToUpperInvariant()}",
                $"CHUNK_SIZE={options.TimeChunkSize},{options.LatitudeChunkSize},{options.LongitudeChunkSize}",
                "ARRAY_NAME=" + (options.VariableName ?? "data")
            };

            // Create Zarr dataset
            using var zarrDataset = zarrDriver.CreateCopy(zarrUri, sourceDataset, 0, creationOptions, null, null);
            if (zarrDataset == null)
            {
                throw new InvalidOperationException($"Failed to convert {sourceUri} to Zarr");
            }

            zarrDataset.FlushCache();

            _logger.LogInformation("Successfully converted {SourceUri} to Zarr", sourceUri);
        }, cancellationToken);

        await ZarrMetadataConsolidator.ConsolidateAsync(zarrUri, cancellationToken).ConfigureAwait(false);
    }

    private bool CheckGdalZarrDriver()
    {
        try
        {
            using var driver = Gdal.GetDriverByName("Zarr");
            return driver != null;
        }
        catch
        {
            return false;
        }
    }
}
