// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Kerchunk;

/// <summary>
/// Generates kerchunk reference JSON from NetCDF/HDF5/GRIB files.
/// </summary>
public interface IKerchunkGenerator
{
    /// <summary>
    /// Generates kerchunk references for the specified source file.
    /// </summary>
    /// <param name="sourceUri">URI to NetCDF/HDF5/GRIB file (s3://, file://, https://, etc.)</param>
    /// <param name="options">Generation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Kerchunk references mapping Zarr chunks to byte ranges</returns>
    Task<KerchunkReferences> GenerateAsync(
        string sourceUri,
        KerchunkGenerationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this generator can handle the specified file format.
    /// </summary>
    /// <param name="sourceUri">Source file URI</param>
    /// <returns>True if this generator supports the file format</returns>
    bool CanHandle(string sourceUri);
}
