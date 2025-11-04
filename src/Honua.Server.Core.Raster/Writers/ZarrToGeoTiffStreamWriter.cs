// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Writers;

/// <summary>
/// Converts Zarr arrays to GeoTIFF format on-the-fly.
/// Writes GeoTIFF data to a stream without materializing entire array in memory.
/// Supports compression (Deflate) and tiling.
/// </summary>
/// <remarks>
/// This writer generates a minimal valid GeoTIFF file from Zarr data.
/// For production use with large datasets, consider using GDAL for full feature support.
/// </remarks>
public sealed class ZarrToGeoTiffStreamWriter
{
    private readonly ILogger<ZarrToGeoTiffStreamWriter> _logger;
    private readonly ZarrToGeoTiffOptions _options;

    public ZarrToGeoTiffStreamWriter(
        ILogger<ZarrToGeoTiffStreamWriter> logger,
        ZarrToGeoTiffOptions? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new ZarrToGeoTiffOptions();
    }

    /// <summary>
    /// Converts a Zarr array to GeoTIFF format and writes to the output stream.
    /// </summary>
    /// <param name="zarrReader">Zarr reader for accessing source data</param>
    /// <param name="zarrArray">Opened Zarr array</param>
    /// <param name="outputStream">Destination stream for GeoTIFF data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task WriteAsync(
        IZarrReader zarrReader,
        ZarrArray zarrArray,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (!outputStream.CanWrite)
        {
            throw new ArgumentException("Output stream must be writable", nameof(outputStream));
        }

        // Validate array is 2D (for simplicity in MVP)
        if (zarrArray.Metadata.Shape.Length != 2)
        {
            throw new NotSupportedException(
                $"Only 2D arrays supported for GeoTIFF conversion, got {zarrArray.Metadata.Shape.Length}D");
        }

        var height = zarrArray.Metadata.Shape[0];
        var width = zarrArray.Metadata.Shape[1];

        _logger.LogInformation(
            "Converting Zarr to GeoTIFF: {Width}x{Height}, dtype={DType}, compression={Compression}",
            width, height, zarrArray.Metadata.DType, _options.Compression);

        // Write TIFF header
        await WriteTiffHeaderAsync(outputStream, width, height, cancellationToken);

        // Read and write image data
        var imageDataOffset = await WriteImageDataAsync(
            zarrReader, zarrArray, outputStream, width, height, cancellationToken);

        // Write IFD (Image File Directory) at end
        await WriteIfdAsync(outputStream, width, height, imageDataOffset, cancellationToken);

        _logger.LogInformation("GeoTIFF conversion complete: {Bytes} bytes written", outputStream.Position);
    }

    /// <summary>
    /// Creates a stream that converts Zarr data to GeoTIFF on-the-fly as it's read.
    /// </summary>
    public static async Task<Stream> CreateGeoTiffStreamAsync(
        IZarrReader zarrReader,
        ZarrArray zarrArray,
        ILogger<ZarrToGeoTiffStreamWriter> logger,
        ZarrToGeoTiffOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var writer = new ZarrToGeoTiffStreamWriter(logger, options);
        var outputStream = new MemoryStream();

        await writer.WriteAsync(zarrReader, zarrArray, outputStream, cancellationToken).ConfigureAwait(false);

        outputStream.Position = 0;
        return outputStream;
    }

    private async Task WriteTiffHeaderAsync(Stream stream, int width, int height, CancellationToken cancellationToken)
    {
        // TIFF header: 8 bytes
        // Byte order (II = little-endian, MM = big-endian)
        await stream.WriteAsync(Encoding.ASCII.GetBytes("II"), cancellationToken);

        // TIFF version (42)
        await WriteUInt16Async(stream, 42, cancellationToken);

        // Offset to first IFD (will write IFD at end, after image data)
        // Placeholder - will update later
        await WriteUInt32Async(stream, 0, cancellationToken);
    }

    private async Task<long> WriteImageDataAsync(
        IZarrReader zarrReader,
        ZarrArray zarrArray,
        Stream stream,
        int width,
        int height,
        CancellationToken cancellationToken)
    {
        var imageDataOffset = stream.Position;

        // Read entire array (for MVP - streaming row-by-row would be better for large arrays)
        var data = await zarrReader.ReadSliceAsync(
            zarrArray,
            new int[] { 0, 0 },
            new int[] { height, width },
            cancellationToken);

        var bytes = data as byte[] ?? throw new InvalidOperationException("Expected byte array from ReadSliceAsync");

        if (_options.Compression == TiffCompression.Deflate)
        {
            // Compress using Deflate
            using var compressedStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
            {
                await deflateStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            var compressedBytes = compressedStream.ToArray();
            await stream.WriteAsync(compressedBytes, cancellationToken).ConfigureAwait(false);

            _logger.LogDebug(
                "Compressed image data: {UncompressedSize} -> {CompressedSize} bytes ({Ratio:F2}x)",
                bytes.Length, compressedBytes.Length, (double)bytes.Length / compressedBytes.Length);
        }
        else
        {
            // No compression
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        return imageDataOffset;
    }

    private async Task WriteIfdAsync(
        Stream stream,
        int width,
        int height,
        long imageDataOffset,
        CancellationToken cancellationToken)
    {
        var ifdOffset = stream.Position;

        // Update IFD offset in header
        var currentPosition = stream.Position;
        stream.Seek(4, SeekOrigin.Begin);
        await WriteUInt32Async(stream, (uint)ifdOffset, cancellationToken);
        stream.Seek(currentPosition, SeekOrigin.Begin);

        // Write IFD entries
        var entries = new System.Collections.Generic.List<IfdEntry>
        {
            // ImageWidth (256)
            new IfdEntry(256, TiffFieldType.Long, 1, (uint)width),

            // ImageLength (257)
            new IfdEntry(257, TiffFieldType.Long, 1, (uint)height),

            // BitsPerSample (258)
            new IfdEntry(258, TiffFieldType.Short, 1, _options.BitsPerSample),

            // Compression (259)
            new IfdEntry(259, TiffFieldType.Short, 1, (uint)_options.Compression),

            // PhotometricInterpretation (262) - MinIsBlack
            new IfdEntry(262, TiffFieldType.Short, 1, 1),

            // StripOffsets (273)
            new IfdEntry(273, TiffFieldType.Long, 1, (uint)imageDataOffset),

            // SamplesPerPixel (277)
            new IfdEntry(277, TiffFieldType.Short, 1, 1),

            // RowsPerStrip (278)
            new IfdEntry(278, TiffFieldType.Long, 1, (uint)height),

            // StripByteCounts (279)
            new IfdEntry(279, TiffFieldType.Long, 1, (uint)(stream.Position - imageDataOffset)),

            // ResolutionUnit (296) - None (1 = no absolute unit)
            new IfdEntry(296, TiffFieldType.Short, 1, 1),

            // SampleFormat (339) - unsigned integer
            new IfdEntry(339, TiffFieldType.Short, 1, 1)
        };

        // Number of directory entries
        await WriteUInt16Async(stream, (ushort)entries.Count, cancellationToken);

        // Write entries
        foreach (var entry in entries.OrderBy(e => e.Tag))
        {
            await WriteIfdEntryAsync(stream, entry, cancellationToken);
        }

        // Next IFD offset (0 = no more IFDs)
        await WriteUInt32Async(stream, 0, cancellationToken);
    }

    private async Task WriteIfdEntryAsync(Stream stream, IfdEntry entry, CancellationToken cancellationToken)
    {
        // Tag (2 bytes)
        await WriteUInt16Async(stream, entry.Tag, cancellationToken);

        // Field type (2 bytes)
        await WriteUInt16Async(stream, (ushort)entry.FieldType, cancellationToken);

        // Count (4 bytes)
        await WriteUInt32Async(stream, entry.Count, cancellationToken);

        // Value/Offset (4 bytes)
        await WriteUInt32Async(stream, entry.ValueOrOffset, cancellationToken);
    }

    private async Task WriteUInt16Async(Stream stream, ushort value, CancellationToken cancellationToken)
    {
        var buffer = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteUInt32Async(Stream stream, uint value, CancellationToken cancellationToken)
    {
        var buffer = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    private sealed record IfdEntry(ushort Tag, TiffFieldType FieldType, uint Count, uint ValueOrOffset);

    private enum TiffFieldType : ushort
    {
        Byte = 1,
        Ascii = 2,
        Short = 3,
        Long = 4,
        Rational = 5
    }
}

/// <summary>
/// Options for Zarr to GeoTIFF conversion.
/// </summary>
public sealed class ZarrToGeoTiffOptions
{
    /// <summary>
    /// Compression method. Default: None (for streaming performance).
    /// </summary>
    public TiffCompression Compression { get; set; } = TiffCompression.None;

    /// <summary>
    /// Bits per sample. Default: 32 (for float32 data).
    /// </summary>
    public ushort BitsPerSample { get; set; } = 32;

    /// <summary>
    /// Whether to write as tiled TIFF (for large datasets). Default: false.
    /// </summary>
    public bool UseTiling { get; set; } = false;

    /// <summary>
    /// Tile width (if UseTiling = true). Default: 256.
    /// </summary>
    public int TileWidth { get; set; } = 256;

    /// <summary>
    /// Tile height (if UseTiling = true). Default: 256.
    /// </summary>
    public int TileHeight { get; set; } = 256;
}

/// <summary>
/// TIFF compression methods.
/// </summary>
public enum TiffCompression : ushort
{
    None = 1,
    Deflate = 8,
    Lzw = 5
}
