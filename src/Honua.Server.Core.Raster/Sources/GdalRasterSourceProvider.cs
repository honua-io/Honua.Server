// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Cache;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// GDAL-based raster source provider that supports multiple scientific data formats
/// (NetCDF, HDF5, GRIB2, GeoTIFF, COG) with automatic routing to optimal storage (COG or Zarr).
/// </summary>
public sealed class GdalRasterSourceProvider : IRasterSourceProvider
{
    private readonly ILogger<GdalRasterSourceProvider> _logger;
    private readonly RasterStorageRouter? _storageRouter;

    public GdalRasterSourceProvider(
        ILogger<GdalRasterSourceProvider> logger,
        RasterStorageRouter? storageRouter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _storageRouter = storageRouter;
    }

    public string ProviderKey => "gdal";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        var extension = Path.GetExtension(uri).ToLowerInvariant();

        return extension switch
        {
            // GeoTIFF / COG
            ".tif" or ".tiff" => true,

            // NetCDF (Climate/Weather)
            ".nc" or ".nc4" or ".netcdf" => true,

            // HDF5 (NASA satellite data)
            ".h5" or ".hdf" or ".hdf5" or ".he5" => true,

            // GRIB2 (Weather forecasts)
            ".grib" or ".grib2" or ".grb" or ".grb2" => true,

            // Additional GDAL-supported formats
            ".asc" or ".bil" or ".img" or ".ecw" or ".jp2" => true,

            _ => false
        };
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(uri))
        {
            throw new NotSupportedException($"GDAL provider cannot handle URI: {uri}");
        }

        // If storage router is available, route to optimal format (COG or Zarr)
        if (_storageRouter != null && !IsGeoTiff(uri))
        {
            _logger.LogDebug("Routing {Uri} to optimal storage format", uri);

            // Create a temporary dataset definition for routing
            var tempDataset = new RasterDatasetDefinition
            {
                Id = Path.GetFileNameWithoutExtension(uri),
                Title = Path.GetFileName(uri),
                Source = new RasterSourceDefinition
                {
                    Type = "gdal",
                    Uri = uri
                }
            };

            var decision = await _storageRouter.RouteAndConvertAsync(tempDataset, cancellationToken);

            _logger.LogInformation(
                "Routed {Uri} to {Format}: {Reason}",
                uri, decision.StorageFormat, decision.Reason);

            // Return stream to optimized file
            return new FileStream(decision.OptimizedUri, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // For GeoTIFF or when router is disabled, open directly
        if (File.Exists(uri))
        {
            return new FileStream(uri, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        throw new FileNotFoundException($"Raster file not found: {uri}");
    }

    public async Task<Stream> OpenReadRangeAsync(string uri, long offset, long? length = null, CancellationToken cancellationToken = default)
    {
        // For HTTP/S3 COG access, GDAL handles range requests internally via /vsicurl/
        // For local files, we can seek to the offset and wrap in a limited stream
        var stream = await OpenReadAsync(uri, cancellationToken);

        if (offset > 0 && stream.CanSeek)
        {
            stream.Seek(offset, SeekOrigin.Begin);
        }

        if (length.HasValue)
        {
            // Return a limited stream wrapper that only reads up to the specified length
            return new LimitedLengthStream(stream, length.Value);
        }

        return stream;
    }

    private bool IsGeoTiff(string uri)
    {
        var extension = Path.GetExtension(uri).ToLowerInvariant();
        return extension is ".tif" or ".tiff";
    }

    /// <summary>
    /// Stream wrapper that limits the number of bytes that can be read.
    /// </summary>
    private sealed class LimitedLengthStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _maxLength;
        private long _position;

        public LimitedLengthStream(Stream baseStream, long maxLength)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _maxLength = maxLength;
            _position = 0;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => Math.Min(_baseStream.Length, _maxLength);
        public override long Position
        {
            get => _position;
            set
            {
                if (_baseStream.CanSeek)
                {
                    _position = value;
                    _baseStream.Position = value;
                }
                else
                {
                    throw new NotSupportedException("Stream does not support seeking");
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var remaining = _maxLength - _position;
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            var bytesRead = _baseStream.Read(buffer, offset, toRead);
            _position += bytesRead;
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
            {
                throw new NotSupportedException("Stream does not support seeking");
            }

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0 || newPosition > _maxLength)
            {
                throw new IOException("Cannot seek beyond the limited length");
            }

            _position = newPosition;
            return _baseStream.Seek(newPosition, SeekOrigin.Begin);
        }

        public override void Flush() => _baseStream.Flush();

        public override void SetLength(long value) =>
            throw new NotSupportedException("Cannot set length on limited stream");

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Stream is read-only");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
