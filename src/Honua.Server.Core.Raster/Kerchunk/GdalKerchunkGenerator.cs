// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Interop;
using Microsoft.Extensions.Logging;
using OSGeo.GDAL;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// GDAL-based kerchunk reference generator for NetCDF/HDF5/GRIB files.
/// Extracts metadata and structure using GDAL, generates Zarr-compatible references.
/// </summary>
/// <remarks>
/// Current limitations:
/// - GDAL doesn't directly expose HDF5/NetCDF chunk byte offsets
/// - Phase 1 implementation extracts metadata only
/// - Future: Use HDF5-DotNet or Python kerchunk for complete byte offset mapping
/// </remarks>
public sealed class GdalKerchunkGenerator : IKerchunkGenerator
{
    private static readonly string[] SupportedExtensions =
    {
        ".nc", ".nc4", ".netcdf",
        ".h5", ".hdf5", ".hdf", ".he5",
        ".grib", ".grib2", ".grb", ".grb2"
    };

    private readonly ILogger<GdalKerchunkGenerator> _logger;

    public GdalKerchunkGenerator(ILogger<GdalKerchunkGenerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        GdalInitializer.EnsureInitialized();
    }

    public bool CanHandle(string sourceUri)
    {
        if (sourceUri.IsNullOrEmpty())
            return false;

        var extension = Path.GetExtension(sourceUri).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    public async Task<KerchunkReferences> GenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!CanHandle(sourceUri))
        {
            throw new NotSupportedException(
                $"Unsupported file format: {sourceUri}. " +
                $"Supported extensions: {string.Join(", ", SupportedExtensions)}");
        }

        _logger.LogInformation("Generating kerchunk references for {SourceUri}", sourceUri);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var refs = new KerchunkReferences
            {
                Version = "1.0",
                SourceUri = sourceUri,
                GeneratedAt = DateTimeOffset.UtcNow,
                Refs = new Dictionary<string, object>(),
                Metadata = new Dictionary<string, object>()
            };

            try
            {
                // Open the dataset
                using var dataset = Gdal.Open(sourceUri, Access.GA_ReadOnly);
                if (dataset == null)
                {
                    throw new InvalidOperationException($"Failed to open dataset: {sourceUri}");
                }

                // Check for subdatasets (NetCDF variables, HDF5 groups)
                var subdatasets = dataset.GetMetadata("SUBDATASETS");
                if (subdatasets != null && subdatasets.Length > 0)
                {
                    ProcessSubdatasets(subdatasets, sourceUri, options, refs, cancellationToken);
                }
                else
                {
                    // Single raster dataset (GRIB or simple HDF5)
                    ProcessRasterDataset(dataset, "data", sourceUri, options, refs);
                }

                // Add root .zgroup metadata
                refs.Metadata[".zgroup"] = new { zarr_format = 2 };

                // Add consolidated metadata if requested
                if (options.ConsolidateMetadata)
                {
                    refs.Metadata[".zmetadata"] = new
                    {
                        metadata = refs.Metadata,
                        zarr_consolidated_format = 1
                    };
                }

                _logger.LogInformation(
                    "Generated kerchunk references for {SourceUri}: {VarCount} variables, {ChunkCount} chunks",
                    sourceUri,
                    refs.Metadata.Count(kv => kv.Key.EndsWith("/.zarray")),
                    refs.Refs.Count);

                // IMPORTANT: Log warning about byte offset limitation
                if (refs.Refs.Count == 0)
                {
                    _logger.LogWarning(
                        "Kerchunk generation for {SourceUri} succeeded but no chunk byte offsets were generated. " +
                        "GDAL doesn't expose HDF5/NetCDF chunk byte offsets directly. " +
                        "To enable full kerchunk support, future implementation will use HDF5-DotNet or Python kerchunk fallback. " +
                        "For now, this file will fall back to Zarr conversion.",
                        sourceUri);
                }

                return refs;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate kerchunk references for {SourceUri}", sourceUri);
                throw;
            }
        }, cancellationToken);
    }

    private void ProcessSubdatasets(
        string[] subdatasets,
        string sourceUri,
        KerchunkGenerationOptions options,
        KerchunkReferences refs,
        CancellationToken cancellationToken)
    {
        // GDAL returns subdatasets as key-value pairs:
        // SUBDATASET_1_NAME=NETCDF:"file.nc":temperature
        // SUBDATASET_1_DESC=temperature variable
        var subdatasetPaths = new Dictionary<string, string>();

        for (int i = 0; i < subdatasets.Length; i += 2)
        {
            if (subdatasets[i].StartsWith("SUBDATASET_") && subdatasets[i].EndsWith("_NAME"))
            {
                var path = subdatasets[i + 1];
                var parts = path.Split(':');
                var varName = parts.Length > 2 ? parts[^1] : $"var_{i / 2}";
                subdatasetPaths[varName] = path;
            }
        }

        _logger.LogDebug("Found {Count} subdatasets in {SourceUri}", subdatasetPaths.Count, sourceUri);

        foreach (var (varName, subdatasetPath) in subdatasetPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Filter by requested variables
            if (options.Variables != null && options.Variables.Length > 0 &&
                !options.Variables.Contains(varName))
            {
                _logger.LogDebug("Skipping variable {VarName} (not in requested list)", varName);
                continue;
            }

            using var subDataset = Gdal.Open(subdatasetPath, Access.GA_ReadOnly);
            if (subDataset == null)
            {
                _logger.LogWarning("Failed to open subdataset: {Path}", subdatasetPath);
                continue;
            }

            ProcessRasterDataset(subDataset, varName, sourceUri, options, refs);
        }
    }

    private void ProcessRasterDataset(
        Dataset dataset,
        string variableName,
        string sourceUri,
        KerchunkGenerationOptions options,
        KerchunkReferences refs)
    {
        if (dataset.RasterCount == 0)
        {
            _logger.LogWarning("Dataset {VarName} has no raster bands", variableName);
            return;
        }

        var band = dataset.GetRasterBand(1);
        if (band == null)
        {
            _logger.LogWarning("Failed to get raster band for {VarName}", variableName);
            return;
        }

        // Get chunk/block dimensions
        band.GetBlockSize(out int blockX, out int blockY);

        // Get data type
        var dtype = MapGdalTypeToZarr(band.DataType);

        // Get dimensions
        var shape = new List<int>();
        if (dataset.RasterCount > 1)
        {
            shape.Add(dataset.RasterCount); // Time/band dimension
        }
        shape.Add(dataset.RasterYSize); // Y dimension
        shape.Add(dataset.RasterXSize); // X dimension

        // Build chunks array matching shape
        var chunks = new List<int>();
        if (dataset.RasterCount > 1)
        {
            chunks.Add(1); // One time step per chunk
        }
        chunks.Add(blockY);
        chunks.Add(blockX);

        // Get compression info
        var compression = GetCompressionInfo(band);

        // Build .zarray metadata
        var zarrayMetadata = new Dictionary<string, object>
        {
            ["chunks"] = chunks.ToArray(),
            ["compressor"] = compression,
            ["dtype"] = dtype,
            ["fill_value"] = GetFillValue(band),
            ["filters"] = null,
            ["order"] = "C",
            ["shape"] = shape.ToArray(),
            ["zarr_format"] = 2
        };

        refs.Metadata[$"{variableName}/.zarray"] = zarrayMetadata;

        // Build .zattrs metadata (attributes)
        var attributes = ExtractAttributes(dataset, band);
        if (attributes.Count > 0)
        {
            refs.Metadata[$"{variableName}/.zattrs"] = attributes;
        }

        // BUG FIX #40: Document kerchunk byte offset limitation with clear guidance
        // TODO: Generate chunk byte offset references
        // LIMITATION: GDAL doesn't expose HDF5/NetCDF chunk byte offsets directly via its API.
        // Current behavior: Generates metadata-only kerchunk references without byte offset mappings.
        // Impact: Files will fall back to full Zarr conversion for cloud-optimized workflows.
        //
        // Future implementation options:
        // 1. Use HDF5-DotNet library to read HDF5 chunk B-tree and extract byte offsets directly
        // 2. Integrate Python kerchunk library via PythonNet for full-featured reference generation
        // 3. Use GDAL VSI hooks to intercept and log read operations to derive byte positions
        // 4. Parse HDF5/NetCDF file structure directly using binary readers
        //
        // Without chunk references, the system will fall back to Zarr conversion.
        // This limitation is logged as a warning in the GenerateAsync method above (line 122-129).
    }

    private static string MapGdalTypeToZarr(DataType gdalType)
    {
        return gdalType switch
        {
            DataType.GDT_Byte => "|u1",
            DataType.GDT_UInt16 => "<u2",
            DataType.GDT_Int16 => "<i2",
            DataType.GDT_UInt32 => "<u4",
            DataType.GDT_Int32 => "<i4",
            DataType.GDT_Float32 => "<f4",
            DataType.GDT_Float64 => "<f8",
            DataType.GDT_CInt16 => "<c4",
            DataType.GDT_CInt32 => "<c8",
            DataType.GDT_CFloat32 => "<c8",
            DataType.GDT_CFloat64 => "<c16",
            _ => "<f4" // Default to float32
        };
    }

    private static object? GetCompressionInfo(Band band)
    {
        // Try to get compression metadata from GDAL
        var compression = band.GetMetadata("IMAGE_STRUCTURE")?
            .FirstOrDefault(m => m.StartsWith("COMPRESSION="))?
            .Split('=')[1];

        if (compression.IsNullOrEmpty())
        {
            return null;
        }

        // Map GDAL compression names to Zarr codec names
        return compression.ToUpperInvariant() switch
        {
            "DEFLATE" or "ZIP" => new
            {
                id = "gzip",
                level = 5
            },
            "LZW" => new
            {
                id = "lzw"
            },
            "ZSTD" => new
            {
                id = "zstd",
                level = 3
            },
            "LZ4" => new
            {
                id = "lz4",
                acceleration = 1
            },
            _ => null
        };
    }

    private static object? GetFillValue(Band band)
    {
        band.GetNoDataValue(out double noDataValue, out int hasNoData);
        if (hasNoData != 0)
        {
            return noDataValue;
        }

        // Default fill values by data type
        return band.DataType switch
        {
            DataType.GDT_Float32 or DataType.GDT_Float64 => float.NaN,
            _ => 0
        };
    }

    private static Dictionary<string, object> ExtractAttributes(Dataset dataset, Band band)
    {
        var attributes = new Dictionary<string, object>();

        // Extract dataset-level metadata
        var metadata = dataset.GetMetadata("");
        if (metadata != null)
        {
            foreach (var item in metadata)
            {
                var parts = item.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    attributes[parts[0]] = parts[1];
                }
            }
        }

        // Extract band-level metadata
        var bandMetadata = band.GetMetadata("");
        if (bandMetadata != null)
        {
            foreach (var item in bandMetadata)
            {
                var parts = item.Split(new[] { '=' }, 2);
                if (parts.Length == 2 && !attributes.ContainsKey(parts[0]))
                {
                    attributes[parts[0]] = parts[1];
                }
            }
        }

        // Add geotransform if available
        try
        {
            var geotransform = new double[6];
            dataset.GetGeoTransform(geotransform);
            // Check if geotransform is not the default identity matrix
            if (geotransform[1] != 0 || geotransform[5] != 0)
            {
                attributes["geotransform"] = geotransform;
            }
        }
        catch
        {
            // Geotransform not available
        }

        // Add projection
        var projection = dataset.GetProjection();
        if (!projection.IsNullOrEmpty())
        {
            attributes["crs"] = projection;
        }

        return attributes;
    }
}
