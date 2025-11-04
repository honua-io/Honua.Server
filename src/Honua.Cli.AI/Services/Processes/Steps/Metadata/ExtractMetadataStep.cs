// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MetadataState = Honua.Cli.AI.Services.Processes.State.MetadataState;
using OSGeo.GDAL;
using OSGeo.OSR;
using System.Globalization;
using MaxRev.Gdal.Core;
using OSGeo.OGR;

namespace Honua.Cli.AI.Services.Processes.Steps.Metadata;

/// <summary>
/// Extracts geospatial metadata from raster files (COG, Zarr, NetCDF).
/// </summary>
public class ExtractMetadataStep : KernelProcessStep<MetadataState>
{
    private readonly ILogger<ExtractMetadataStep> _logger;
    private MetadataState _state = new();
    private static readonly object GdalInitLock = new();
    private static bool _gdalInitialized;

    public ExtractMetadataStep(ILogger<ExtractMetadataStep> logger)
    {
        _logger = logger;
    }

    private static void EnsureGdalInitialized()
    {
        if (_gdalInitialized)
        {
            return;
        }

        lock (GdalInitLock)
        {
            if (_gdalInitialized)
            {
                return;
            }

            try
            {
                GdalBase.ConfigureAll();
            }
            catch
            {
                // MaxRev bootstrap not available; continue with direct registration.
            }

            Gdal.AllRegister();
            Ogr.RegisterAll();
            _gdalInitialized = true;
        }
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<MetadataState> state)
    {
        _state = state.State ?? new MetadataState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("ExtractMetadata")]
    public async Task ExtractMetadataAsync(
        KernelProcessStepContext context,
        MetadataRequest request)
    {
        _logger.LogInformation("Extracting metadata from {DatasetPath}", request.DatasetPath);

        _state.MetadataId = Guid.NewGuid().ToString();
        _state.DatasetPath = request.DatasetPath;
        _state.DatasetName = request.DatasetName;
        _state.StartTime = DateTime.UtcNow;
        _state.Status = "ExtractingMetadata";

        try
        {
            // Extract geospatial metadata
            await ExtractSpatialMetadata();
            await ExtractTemporalMetadata();
            await ExtractBands();

            _state.MetadataExtracted = true;

            _logger.LogInformation("Metadata extracted for {DatasetName}: {BandCount} bands, bbox: {BBox}",
                _state.DatasetName, _state.Bands.Count, _state.BoundingBox);

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "MetadataExtracted",
                Data = _state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract metadata from {DatasetPath}", request.DatasetPath);
            _state.Status = "ExtractionFailed";
            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "ExtractionFailed",
                Data = new { request.DatasetPath, Error = ex.Message }
            });
        }
    }

    private async Task ExtractSpatialMetadata()
    {
        _logger.LogInformation("Extracting spatial metadata from {DatasetPath}", _state.DatasetPath);

        EnsureGdalInitialized();

        // Check if GDAL is available and can be used
        if (Gdal.GetDriverCount() == 0)
        {
            throw new InvalidOperationException("GDAL drivers not available. Ensure GDAL is properly installed and initialized.");
        }

        await Task.Run(() =>
        {
            Dataset? dataset = null;
            try
            {
                // Open the dataset
                dataset = Gdal.Open(_state.DatasetPath, Access.GA_ReadOnly);
                if (dataset == null)
                {
                    throw new InvalidOperationException($"Failed to open dataset: {_state.DatasetPath}");
                }

                // Extract geotransform to calculate bounding box
                var geoTransform = new double[6];
                bool hasGeoTransform = true;
                try
                {
                    dataset.GetGeoTransform(geoTransform);
                }
                catch
                {
                    hasGeoTransform = false;
                }

                if (hasGeoTransform && !(Math.Abs(geoTransform[1]) < double.Epsilon && Math.Abs(geoTransform[5]) < double.Epsilon))
                {
                    var width = dataset.RasterXSize;
                    var height = dataset.RasterYSize;

                    // GeoTransform[0]: top left x
                    // GeoTransform[1]: w-e pixel resolution
                    // GeoTransform[2]: rotation, 0 if north up
                    // GeoTransform[3]: top left y
                    // GeoTransform[4]: rotation, 0 if north up
                    // GeoTransform[5]: n-s pixel resolution (negative value)

                    var minX = geoTransform[0];
                    var maxY = geoTransform[3];
                    var maxX = minX + (width * geoTransform[1]);
                    var minY = maxY + (height * geoTransform[5]);

                    _state.BoundingBox = $"{minX},{minY},{maxX},{maxY}";
                    _state.Resolution = Math.Abs(geoTransform[1]).ToString("F6", CultureInfo.InvariantCulture);

                    _logger.LogInformation(
                        "Extracted bounding box: {BBox}, resolution: {Resolution}",
                        _state.BoundingBox, _state.Resolution);
                }
                else
                {
                    _logger.LogWarning("No geotransform found, using default world extent");
                    _state.BoundingBox = "-180,-90,180,90";
                    _state.Resolution = "unknown";
                }

                // Extract CRS
                var projection = dataset.GetProjection();
                if (!string.IsNullOrWhiteSpace(projection))
                {
                    using var spatialRef = new SpatialReference(projection);
                    if (spatialRef != null)
                    {
                        // Try to get EPSG code
                        var epsgCode = spatialRef.GetAuthorityCode(null);
                        if (!string.IsNullOrWhiteSpace(epsgCode))
                        {
                            _state.CRS = $"EPSG:{epsgCode}";
                        }
                        else
                        {
                            _state.CRS = "UNKNOWN";
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No projection found, assuming EPSG:4326");
                    _state.CRS = "EPSG:4326";
                }

                // Store additional metadata
                _state.ExtractedMetadata["Width"] = dataset.RasterXSize;
                _state.ExtractedMetadata["Height"] = dataset.RasterYSize;
                _state.ExtractedMetadata["BandCount"] = dataset.RasterCount;

                _logger.LogInformation("Extracted CRS: {CRS}", _state.CRS);
            }
            finally
            {
                dataset?.Dispose();
            }
        });
    }

    private async Task ExtractTemporalMetadata()
    {
        _logger.LogInformation("Extracting temporal metadata from {DatasetPath}", _state.DatasetPath);

        await Task.Run(() =>
        {
            Dataset? dataset = null;
            try
            {
                dataset = Gdal.Open(_state.DatasetPath, Access.GA_ReadOnly);
                if (dataset == null)
                {
                    _logger.LogWarning("Failed to open dataset for temporal metadata");
                    return;
                }

                // Try to extract temporal information from metadata
                var metadata = dataset.GetMetadata("");
                if (metadata != null && metadata.Length > 0)
                {
                    string? startDate = null;
                    string? endDate = null;

                    foreach (var entry in metadata)
                    {
                        var lower = entry.ToLowerInvariant();
                        if (lower.Contains("start_date") || lower.Contains("start_time") || lower.Contains("begin_date"))
                        {
                            var parts = entry.Split('=');
                            if (parts.Length > 1)
                            {
                                startDate = parts[1].Trim();
                            }
                        }
                        else if (lower.Contains("end_date") || lower.Contains("end_time") || lower.Contains("stop_date"))
                        {
                            var parts = entry.Split('=');
                            if (parts.Length > 1)
                            {
                                endDate = parts[1].Trim();
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate))
                    {
                        _state.TemporalExtent = $"{startDate}/{endDate}";
                        _logger.LogInformation("Extracted temporal extent: {Extent}", _state.TemporalExtent);
                    }
                    else if (!string.IsNullOrWhiteSpace(startDate))
                    {
                        _state.TemporalExtent = startDate;
                        _logger.LogInformation("Extracted single date: {Date}", _state.TemporalExtent);
                    }
                    else
                    {
                        // Use file modification time as fallback
                        if (File.Exists(_state.DatasetPath))
                        {
                            var fileInfo = new FileInfo(_state.DatasetPath);
                            _state.TemporalExtent = fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd");
                            _logger.LogInformation("Using file modification time: {Date}", _state.TemporalExtent);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No temporal metadata found in dataset");
                }
            }
            finally
            {
                dataset?.Dispose();
            }
        });
    }

    private async Task ExtractBands()
    {
        _logger.LogInformation("Extracting band information from {DatasetPath}", _state.DatasetPath);

        await Task.Run(() =>
        {
            Dataset? dataset = null;
            try
            {
                dataset = Gdal.Open(_state.DatasetPath, Access.GA_ReadOnly);
                if (dataset == null)
                {
                    _logger.LogWarning("Failed to open dataset for band extraction");
                    return;
                }

                _state.Bands.Clear();
                var bandCount = dataset.RasterCount;

                for (int i = 1; i <= bandCount; i++)
                {
                    using var band = dataset.GetRasterBand(i);
                    if (band != null)
                    {
                        // Try to get band description
                        var description = band.GetDescription();
                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            _state.Bands.Add(description);
                        }
                        else
                        {
                            // Try to get color interpretation
                            var colorInterp = band.GetColorInterpretation();
                            var bandName = colorInterp switch
                            {
                                ColorInterp.GCI_RedBand => "Red",
                                ColorInterp.GCI_GreenBand => "Green",
                                ColorInterp.GCI_BlueBand => "Blue",
                                ColorInterp.GCI_AlphaBand => "Alpha",
                                ColorInterp.GCI_GrayIndex => "Gray",
                                _ => $"Band{i}"
                            };
                            _state.Bands.Add(bandName);
                        }

                        // Store additional band metadata
                        var bandMetadata = new Dictionary<string, object>
                        {
                            ["Index"] = i,
                            ["DataType"] = band.DataType.ToString(),
                            ["ColorInterpretation"] = band.GetColorInterpretation().ToString()
                        };

                        // Get NoData value if present
                        double noDataValue;
                        int hasNoData;
                        band.GetNoDataValue(out noDataValue, out hasNoData);
                        if (hasNoData != 0)
                        {
                            bandMetadata["NoDataValue"] = noDataValue;
                        }

                        // Get band statistics if available
                        double min, max, mean, stddev;
                        if (band.GetStatistics(0, 1, out min, out max, out mean, out stddev) == CPLErr.CE_None)
                        {
                            bandMetadata["Min"] = min;
                            bandMetadata["Max"] = max;
                            bandMetadata["Mean"] = mean;
                            bandMetadata["StdDev"] = stddev;
                        }

                        _state.ExtractedMetadata[$"Band{i}"] = bandMetadata;
                    }
                }

                _logger.LogInformation("Extracted {Count} bands: {Bands}", _state.Bands.Count, string.Join(", ", _state.Bands));
            }
            finally
            {
                dataset?.Dispose();
            }
        });
    }
}

/// <summary>
/// Request object for metadata extraction.
/// </summary>
public record MetadataRequest(
    string DatasetPath,
    string DatasetName);
