// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Readers;

/// <summary>
/// Pure .NET COG reader using BitMiracle.LibTiff.NET.
/// Supports local files and HTTP range requests for remote COGs.
/// </summary>
public sealed class LibTiffCogReader : ICogReader
{
    private readonly ILogger<LibTiffCogReader> _logger;
    private readonly HttpClient? _httpClient;
    private readonly GeoTiffTagParser _geoTagParser;

    public LibTiffCogReader(ILogger<LibTiffCogReader> logger, HttpClient? httpClient = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient;
        if (_httpClient != null)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Raster operations
        }
        _geoTagParser = new GeoTiffTagParser(logger as ILogger<GeoTiffTagParser>
            ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<GeoTiffTagParser>());
    }

    public async Task<CogDataset> OpenAsync(string uri, CancellationToken cancellationToken = default)
    {
        Stream stream;
        bool isRemote = uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                       uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isRemote)
        {
            if (_httpClient == null)
            {
                throw new InvalidOperationException("HttpClient required for remote COG access");
            }

            // Use HTTP range requests for efficient COG access
            _logger.LogInformation("Opening remote COG with range requests: {Uri}", uri);
            stream = await HttpRangeStream.CreateAsync(
                _httpClient,
                uri,
                _logger as ILogger<HttpRangeStream>
                    ?? new Microsoft.Extensions.Logging.Abstractions.NullLogger<HttpRangeStream>(),
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (!File.Exists(uri))
            {
                throw new FileNotFoundException($"COG file not found: {uri}");
            }

            stream = new FileStream(uri, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        // Open TIFF using BitMiracle
        Tiff? tiff = null;
        try
        {
            tiff = Tiff.ClientOpen(uri, "r", stream, new TiffStream());
            if (tiff == null)
            {
                stream.Dispose();
                throw new InvalidOperationException($"Failed to open TIFF file: {uri}");
            }

            var metadata = ExtractMetadata(tiff);

            return new CogDataset
            {
                Uri = uri,
                Metadata = metadata,
                Stream = stream,
                TiffHandle = tiff
            };
        }
        catch
        {
            // Ensure resources are cleaned up on any failure
            tiff?.Dispose();
            stream.Dispose();
            throw;
        }
    }

    public async Task<byte[]> ReadTileAsync(
        CogDataset dataset,
        int tileX,
        int tileY,
        int level = 0,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (dataset.TiffHandle is not Tiff tiff)
        {
            throw new InvalidOperationException("Invalid TIFF handle");
        }

        var metadata = dataset.Metadata;
        if (!metadata.IsTiled)
        {
            throw new NotSupportedException("TIFF is not tiled, use ReadWindowAsync instead");
        }

        // Calculate tile size
        var tileSize = metadata.TileWidth * metadata.TileHeight * (metadata.BitsPerSample / 8) * metadata.BandCount;

        // Use ArrayPool for tiles larger than 85KB to avoid LOH allocations
        var usePool = tileSize > 85000;
        var buffer = usePool ? ArrayPool<byte>.Shared.Rent(tileSize) : new byte[tileSize];

        try
        {
            // Read tile
            var tileNumber = tileY * ((metadata.Width + metadata.TileWidth - 1) / metadata.TileWidth) + tileX;
            var bytesRead = tiff.ReadEncodedTile(tileNumber, buffer, 0, tileSize);

            if (bytesRead == -1)
            {
                throw new InvalidOperationException($"Failed to read tile {tileX},{tileY}");
            }

            // Return only the used portion
            if (usePool)
            {
                var result = new byte[tileSize];
                Array.Copy(buffer, 0, result, 0, tileSize);
                return result;
            }

            return buffer;
        }
        finally
        {
            if (usePool)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }

    public async Task<byte[]> ReadWindowAsync(
        CogDataset dataset,
        int x,
        int y,
        int width,
        int height,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (dataset.TiffHandle is not Tiff tiff)
        {
            throw new InvalidOperationException("Invalid TIFF handle");
        }

        var metadata = dataset.Metadata;
        var bytesPerPixel = (metadata.BitsPerSample / 8) * metadata.BandCount;
        var bufferSize = width * height * bytesPerPixel;

        // Use ArrayPool for large buffers to avoid LOH allocations
        var usePool = bufferSize > 85000;
        var buffer = usePool ? ArrayPool<byte>.Shared.Rent(bufferSize) : new byte[bufferSize];

        // Read scanlines
        var scanlineSize = metadata.Width * bytesPerPixel;
        var useScanlinePool = scanlineSize > 85000;
        var scanline = useScanlinePool ? ArrayPool<byte>.Shared.Rent(scanlineSize) : new byte[scanlineSize];

        try
        {
            for (int row = y; row < y + height && row < metadata.Height; row++)
            {
                if (!tiff.ReadScanline(scanline, row))
                {
                    throw new InvalidOperationException($"Failed to read scanline {row}");
                }

                // Copy requested window from scanline
                var srcOffset = x * bytesPerPixel;
                var dstOffset = (row - y) * width * bytesPerPixel;
                var copyWidth = Math.Min(width, metadata.Width - x) * bytesPerPixel;

                Array.Copy(scanline, srcOffset, buffer, dstOffset, copyWidth);
            }

            // Return only the used portion if using pool
            if (usePool)
            {
                var result = new byte[bufferSize];
                Array.Copy(buffer, 0, result, 0, bufferSize);
                return result;
            }

            return buffer;
        }
        finally
        {
            if (usePool)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
            if (useScanlinePool)
            {
                ArrayPool<byte>.Shared.Return(scanline);
            }
        }
    }

    public async Task<CogMetadata> GetMetadataAsync(string uri, CancellationToken cancellationToken = default)
    {
        using var dataset = await OpenAsync(uri, cancellationToken).ConfigureAwait(false);
        return dataset.Metadata;
    }

    private CogMetadata ExtractMetadata(Tiff tiff)
    {
        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)?[0].ToInt() ?? 8;
        var samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)?[0].ToInt() ?? 1;
        var compression = tiff.GetField(TiffTag.COMPRESSION)?[0].ToInt() ?? 1;
        var planarConfig = tiff.GetField(TiffTag.PLANARCONFIG)?[0].ToInt() ?? 1;

        var tileWidth = tiff.GetField(TiffTag.TILEWIDTH)?[0].ToInt() ?? 0;
        var tileHeight = tiff.GetField(TiffTag.TILELENGTH)?[0].ToInt() ?? 0;
        var isTiled = tileWidth > 0 && tileHeight > 0;

        // Check if it's a proper COG (has overviews, tiled, etc.)
        var overviewCount = CountOverviews(tiff);
        var isCog = isTiled && overviewCount > 0;

        // Extract geospatial metadata using GeoTIFF tag parser
        var (geoTransform, projectionWkt) = _geoTagParser.ParseGeoTags(tiff);

        return new CogMetadata
        {
            Width = width,
            Height = height,
            BandCount = samplesPerPixel,
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            BitsPerSample = bitsPerSample,
            Compression = GetCompressionName(compression),
            IsTiled = isTiled,
            IsCog = isCog,
            OverviewCount = overviewCount,
            GeoTransform = geoTransform,
            ProjectionWkt = projectionWkt
        };
    }

    private int CountOverviews(Tiff tiff)
    {
        int count = 0;
        var currentDir = tiff.CurrentDirectory();

        try
        {
            // Count subdirectories (overviews)
            while (tiff.ReadDirectory())
            {
                count++;
            }

            return count;
        }
        finally
        {
            // Always restore original directory, even on exception
            tiff.SetDirectory(currentDir);
        }
    }

    private string GetCompressionName(int compression)
    {
        return compression switch
        {
            1 => "None",
            5 => "LZW",
            7 => "JPEG",
            8 => "Deflate",
            32946 => "Deflate",
            32773 => "PackBits",
            50001 => "WebP",
            50000 => "ZSTD",
            _ => $"Unknown({compression})"
        };
    }
}
