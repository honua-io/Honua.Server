// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using OSGeo.GDAL;

namespace Honua.Server.Core.Raster.Interop;

/// <summary>
/// GDAL configuration utilities for cloud-optimized operations.
/// Extends GdalInitializer with additional configuration options.
/// </summary>
public static class GdalConfiguration
{
    private static readonly object _lock = new();
    private static bool _configuredForCog;

    /// <summary>
    /// Configure GDAL for cloud-optimized operations (COG, NetCDF, HDF5, etc.).
    /// Thread-safe, idempotent.
    /// </summary>
    public static void ConfigureForCloudOptimizedOperations()
    {
        lock (_lock)
        {
            if (_configuredForCog)
            {
                return;
            }

            // Ensure GDAL is initialized first
            GdalInitializer.EnsureInitialized();

            try
            {
                // Configure GDAL for cloud-optimized operations
                Gdal.SetConfigOption("GDAL_DISABLE_READDIR_ON_OPEN", "EMPTY_DIR");
                Gdal.SetConfigOption("CPL_VSIL_CURL_ALLOWED_EXTENSIONS", ".tif,.tiff,.nc,.nc4,.h5,.hdf,.hdf5,.grib,.grib2");
                Gdal.SetConfigOption("GDAL_HTTP_MERGE_CONSECUTIVE_RANGES", "YES");
                Gdal.SetConfigOption("GDAL_HTTP_MULTIPLEX", "YES");
                Gdal.SetConfigOption("GDAL_HTTP_VERSION", "2");

                // Enable COG driver optimizations
                Gdal.SetConfigOption("GDAL_TIFF_INTERNAL_MASK", "YES");
                Gdal.SetConfigOption("GDAL_TIFF_OVR_BLOCKSIZE", "512");

                // NetCDF-specific optimizations
                Gdal.SetConfigOption("GDAL_NETCDF_BOTTOMUP", "NO");

                // HDF5-specific optimizations
                Gdal.SetConfigOption("GDAL_HDF5_OPEN_OPTIONS", "OVERVIEW_LEVEL=AUTO");

                // Set number of threads for operations
                var processorCount = Environment.ProcessorCount;
                Gdal.SetConfigOption("GDAL_NUM_THREADS", processorCount.ToString());

                _configuredForCog = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to configure GDAL for cloud-optimized operations", ex);
            }
        }
    }

    /// <summary>
    /// Get GDAL version information.
    /// </summary>
    public static string GetVersion()
    {
        GdalInitializer.EnsureInitialized();
        return Gdal.VersionInfo("--version");
    }
}
