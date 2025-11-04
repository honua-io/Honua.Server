// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
using ZstdSharp;

namespace Honua.Server.Core.Export;

public enum PmTilesCompression : byte
{
    Unknown = 0x00,
    None = 0x01,
    Gzip = 0x02,
    Brotli = 0x03,
    Zstd = 0x04
}

public sealed record PmTilesOptions
{
    public PmTilesCompression TileCompression { get; init; } = PmTilesCompression.None;
    public PmTilesCompression InternalCompression { get; init; } = PmTilesCompression.None;
    public Dictionary<string, object>? Metadata { get; init; }
    public byte TileType { get; init; } = 0x1;
}

public sealed record PmTileEntry(int Zoom, int X, int Y, byte[] Data);

public interface IPmTilesExporter
{
    byte[] CreateSingleTileArchive(
        int zoom,
        int x,
        int y,
        byte[] tileData,
        double[] tileBounds,
        string tileMatrixSetId,
        PmTilesOptions? options = null);

    byte[] CreateArchive(IEnumerable<PmTileEntry> tiles, PmTilesOptions? options = null);
}

public sealed class PmTilesExporter : IPmTilesExporter
{
    private const int HeaderSize = 127;
    private const double WebMercatorExtent = 20037508.3427892d;

    public byte[] CreateSingleTileArchive(
        int zoom,
        int x,
        int y,
        byte[] tileData,
        double[] tileBounds,
        string tileMatrixSetId,
        PmTilesOptions? options = null)
    {
        Guard.NotNull(tileData);
        Guard.NotNull(tileBounds);

        if (tileBounds.Length < 4)
        {
            throw new ArgumentException("Tile bounds must contain four values.", nameof(tileBounds));
        }

        options ??= new PmTilesOptions();

        // Compress tile data if requested
        var compressedTileData = CompressData(tileData, options.TileCompression);

        // Build metadata section
        var metadataBytes = BuildMetadata(options.Metadata);

        // Build directory
        var tileId = ZxyToTileId(zoom, x, y);
        var tileEntries = new List<TileEntryInfo>
        {
            new TileEntryInfo(tileId, 0, (ulong)compressedTileData.LongLength, 1)
        };
        var directoryBytes = EncodeDirectory(tileEntries);
        var compressedDirectoryBytes = CompressData(directoryBytes, options.InternalCompression);

        var bounds = ConvertBoundsToWgs84(tileBounds, tileMatrixSetId);
        var rootOffset = (ulong)HeaderSize;
        var rootLength = (ulong)compressedDirectoryBytes.LongLength;
        var metadataOffset = rootOffset + rootLength;
        var metadataLength = (ulong)metadataBytes.LongLength;
        var tileDataOffset = metadataOffset + metadataLength;
        var tileDataLength = (ulong)compressedTileData.LongLength;
        var headerBytes = BuildHeader(
            zoom,
            zoom,
            bounds,
            rootOffset,
            rootLength,
            metadataOffset,
            metadataLength,
            0,
            0,
            tileDataOffset,
            tileDataLength,
            addressedTiles: 1,
            tileEntries: 1,
            tileContents: 1,
            options.TileCompression,
            options.InternalCompression,
            options.TileType,
            clustered: true,
            centerLon: (bounds.MinLon + bounds.MaxLon) / 2d,
            centerLat: (bounds.MinLat + bounds.MaxLat) / 2d,
            centerZoom: zoom);

        var totalLength = HeaderSize + compressedDirectoryBytes.Length + metadataBytes.Length + compressedTileData.Length;
        var archive = new byte[totalLength];
        Buffer.BlockCopy(headerBytes, 0, archive, 0, HeaderSize);
        var cursor = HeaderSize;
        Buffer.BlockCopy(compressedDirectoryBytes, 0, archive, cursor, compressedDirectoryBytes.Length);
        cursor += compressedDirectoryBytes.Length;
        if (metadataBytes.Length > 0)
        {
            Buffer.BlockCopy(metadataBytes, 0, archive, cursor, metadataBytes.Length);
            cursor += metadataBytes.Length;
        }
        Buffer.BlockCopy(compressedTileData, 0, archive, cursor, compressedTileData.Length);

        return archive;
    }

    public byte[] CreateArchive(IEnumerable<PmTileEntry> tiles, PmTilesOptions? options = null)
    {
        if (tiles is null)
        {
            throw new ArgumentNullException(nameof(tiles));
        }

        var tileList = tiles.ToList();
        if (tileList.Count == 0)
        {
            throw new ArgumentException("At least one tile is required to build an archive.", nameof(tiles));
        }

        options ??= new PmTilesOptions();

        var descriptors = tileList
            .Select(t => new TileDescriptor(t, ZxyToTileId(t.Zoom, t.X, t.Y)))
            .OrderBy(t => t.TileId)
            .ToList();

        var tileDataBuffers = new List<byte[]>(descriptors.Count);
        var tileEntries = new List<TileEntryInfo>(descriptors.Count);

        ulong currentOffset = 0;
        foreach (var descriptor in descriptors)
        {
            var compressedTile = CompressData(descriptor.Tile.Data, options.TileCompression);
            tileDataBuffers.Add(compressedTile);
            tileEntries.Add(new TileEntryInfo(descriptor.TileId, currentOffset, (ulong)compressedTile.LongLength, 1));
            currentOffset += (ulong)compressedTile.LongLength;
        }

        var directoryBytes = EncodeDirectory(tileEntries);
        var compressedDirectoryBytes = CompressData(directoryBytes, options.InternalCompression);
        var metadataBytes = BuildMetadata(options.Metadata);

        var bounds = CalculateBoundsFromTiles(descriptors);
        var minZoom = descriptors.Min(t => t.Tile.Zoom);
        var maxZoom = descriptors.Max(t => t.Tile.Zoom);
        var centerZoom = (int)Math.Round((minZoom + maxZoom) / 2.0);
        var centerLon = (bounds.MinLon + bounds.MaxLon) / 2d;
        var centerLat = (bounds.MinLat + bounds.MaxLat) / 2d;

        var rootOffset = (ulong)HeaderSize;
        var rootLength = (ulong)compressedDirectoryBytes.LongLength;
        var metadataOffset = rootOffset + rootLength;
        var metadataLength = (ulong)metadataBytes.LongLength;
        var tileDataOffset = metadataOffset + metadataLength;
        ulong tileDataLength = 0;
        foreach (var buffer in tileDataBuffers)
        {
            tileDataLength += (ulong)buffer.LongLength;
        }

        ulong addressedTiles = 0;
        foreach (var entry in tileEntries)
        {
            addressedTiles += entry.RunLength;
        }
        var tileEntriesCount = (ulong)tileEntries.Count;
        var tileContents = (ulong)tileEntries.Count;

        var headerBytes = BuildHeader(
            minZoom,
            maxZoom,
            bounds,
            rootOffset,
            rootLength,
            metadataOffset,
            metadataLength,
            0,
            0,
            tileDataOffset,
            tileDataLength,
            addressedTiles,
            tileEntriesCount,
            tileContents,
            options.TileCompression,
            options.InternalCompression,
            options.TileType,
            clustered: true,
            centerLon,
            centerLat,
            centerZoom);

        var totalLength = checked(HeaderSize + compressedDirectoryBytes.Length + metadataBytes.Length + tileDataBuffers.Sum(b => b.Length));
        var archive = new byte[totalLength];
        var cursor = 0;
        Buffer.BlockCopy(headerBytes, 0, archive, cursor, HeaderSize);
        cursor += HeaderSize;
        Buffer.BlockCopy(compressedDirectoryBytes, 0, archive, cursor, compressedDirectoryBytes.Length);
        cursor += compressedDirectoryBytes.Length;
        if (metadataBytes.Length > 0)
        {
            Buffer.BlockCopy(metadataBytes, 0, archive, cursor, metadataBytes.Length);
            cursor += metadataBytes.Length;
        }
        foreach (var buffer in tileDataBuffers)
        {
            Buffer.BlockCopy(buffer, 0, archive, cursor, buffer.Length);
            cursor += buffer.Length;
        }

        return archive;
    }

    private static byte[] BuildHeader(
        int minZoom,
        int maxZoom,
        (double MinLon, double MinLat, double MaxLon, double MaxLat) bounds,
        ulong rootDirectoryOffset,
        ulong rootDirectoryLength,
        ulong metadataOffset,
        ulong metadataLength,
        ulong leafDirectoryOffset,
        ulong leafDirectoryLength,
        ulong tileDataOffset,
        ulong tileDataLength,
        ulong addressedTiles,
        ulong tileEntries,
        ulong tileContents,
        PmTilesCompression tileCompression,
        PmTilesCompression internalCompression,
        byte tileType,
        bool clustered,
        double centerLon,
        double centerLat,
        int centerZoom)
    {
        var header = new byte[HeaderSize];
        header[0] = (byte)'P';
        header[1] = (byte)'M';
        header[2] = (byte)'T';
        header[3] = (byte)'i';
        header[4] = (byte)'l';
        header[5] = (byte)'e';
        header[6] = (byte)'s';
        header[7] = 3; // spec version

        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8, 8), rootDirectoryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(16, 8), rootDirectoryLength);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(24, 8), metadataOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(32, 8), metadataLength);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(40, 8), leafDirectoryOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(48, 8), leafDirectoryLength);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(56, 8), tileDataOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(64, 8), tileDataLength);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(72, 8), addressedTiles);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(80, 8), tileEntries);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(88, 8), tileContents);

        header[96] = clustered ? (byte)0x1 : (byte)0x0;
        header[97] = (byte)internalCompression; // internal compression
        header[98] = (byte)tileCompression; // tile compression
        header[99] = tileType;
        header[100] = (byte)Math.Clamp(minZoom, 0, 255);
        header[101] = (byte)Math.Clamp(maxZoom, 0, 255);

        WriteCoordinate(header.AsSpan(102, 4), bounds.MinLon);
        WriteCoordinate(header.AsSpan(106, 4), bounds.MinLat);
        WriteCoordinate(header.AsSpan(110, 4), bounds.MaxLon);
        WriteCoordinate(header.AsSpan(114, 4), bounds.MaxLat);

        header[118] = (byte)Math.Clamp(centerZoom, 0, 255);
        WriteCoordinate(header.AsSpan(119, 4), centerLon);
        WriteCoordinate(header.AsSpan(123, 4), centerLat);

        return header;
    }

    private static byte[] CompressData(byte[] data, PmTilesCompression compression)
    {
        if (compression == PmTilesCompression.None || data.Length == 0)
        {
            return data;
        }

        using var inputStream = new MemoryStream(data);
        using var outputStream = new MemoryStream();

        switch (compression)
        {
            case PmTilesCompression.Gzip:
                using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    inputStream.CopyTo(gzipStream);
                }
                break;

            case PmTilesCompression.Brotli:
                using (var brotliStream = new BrotliStream(outputStream, CompressionLevel.Optimal, leaveOpen: true))
                {
                    inputStream.CopyTo(brotliStream);
                }
                break;

            case PmTilesCompression.Zstd:
                using (var zstdStream = new CompressionStream(outputStream, leaveOpen: true))
                {
                    inputStream.CopyTo(zstdStream);
                }
                break;

            default:
                return data;
        }

        return outputStream.ToArray();
    }

    private static byte[] BuildMetadata(Dictionary<string, object>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var json = JsonSerializer.Serialize(metadata, JsonSerializerOptionsRegistry.Web);

        return Encoding.UTF8.GetBytes(json);
    }

    private static byte[] EncodeDirectory(IReadOnlyList<TileEntryInfo> entries)
    {
        if (entries.Count == 0)
        {
            throw new ArgumentException("Directory must contain at least one entry.", nameof(entries));
        }

        using var stream = new MemoryStream();
        WriteVarint(stream, (ulong)entries.Count);

        ulong previousTileId = 0;
        foreach (var entry in entries)
        {
            WriteVarint(stream, entry.TileId - previousTileId);
            previousTileId = entry.TileId;
        }

        foreach (var entry in entries)
        {
            WriteVarint(stream, entry.RunLength);
        }

        foreach (var entry in entries)
        {
            WriteVarint(stream, entry.Length);
        }

        ulong previousOffset = 0;
        ulong previousLength = 0;
        var first = true;
        foreach (var entry in entries)
        {
            if (!first && entry.Offset == previousOffset + previousLength)
            {
                WriteVarint(stream, 0);
            }
            else
            {
                WriteVarint(stream, entry.Offset + 1);
            }

            previousOffset = entry.Offset;
            previousLength = entry.Length;
            first = false;
        }

        return stream.ToArray();
    }

    private static (double MinLon, double MinLat, double MaxLon, double MaxLat) ConvertBoundsToWgs84(double[] bbox, string tileMatrixSetId)
    {
        var minX = bbox[0];
        var minY = bbox[1];
        var maxX = bbox[2];
        var maxY = bbox[3];

        if (IsWebMercatorMatrix(tileMatrixSetId))
        {
            minX = WebMercatorToLongitude(minX);
            maxX = WebMercatorToLongitude(maxX);
            minY = WebMercatorToLatitude(minY);
            maxY = WebMercatorToLatitude(maxY);
        }

        minX = Math.Clamp(minX, -180d, 180d);
        maxX = Math.Clamp(maxX, -180d, 180d);
        minY = Math.Clamp(minY, -85.05112878d, 85.05112878d);
        maxY = Math.Clamp(maxY, -85.05112878d, 85.05112878d);

        return (minX, minY, maxX, maxY);
    }

    private static (double MinLon, double MinLat, double MaxLon, double MaxLat) CalculateBoundsFromTiles(IEnumerable<TileDescriptor> descriptors)
    {
        double minLon = double.PositiveInfinity;
        double minLat = double.PositiveInfinity;
        double maxLon = double.NegativeInfinity;
        double maxLat = double.NegativeInfinity;

        foreach (var descriptor in descriptors)
        {
            var tile = descriptor.Tile;
            var left = TileXToLon(tile.X, tile.Zoom);
            var right = TileXToLon(tile.X + 1, tile.Zoom);
            var top = TileYToLat(tile.Y, tile.Zoom);
            var bottom = TileYToLat(tile.Y + 1, tile.Zoom);

            minLon = Math.Min(minLon, Math.Min(left, right));
            maxLon = Math.Max(maxLon, Math.Max(left, right));
            minLat = Math.Min(minLat, Math.Min(top, bottom));
            maxLat = Math.Max(maxLat, Math.Max(top, bottom));
        }

        if (double.IsPositiveInfinity(minLon))
        {
            return (0d, 0d, 0d, 0d);
        }

        minLon = Math.Clamp(minLon, -180d, 180d);
        maxLon = Math.Clamp(maxLon, -180d, 180d);
        minLat = Math.Clamp(minLat, -85.05112878d, 85.05112878d);
        maxLat = Math.Clamp(maxLat, -85.05112878d, 85.05112878d);

        return (minLon, minLat, maxLon, maxLat);
    }

    private static double WebMercatorToLongitude(double meters)
        => (meters / WebMercatorExtent) * 180d;

    private static double WebMercatorToLatitude(double meters)
    {
        var lat = (meters / WebMercatorExtent) * 180d;
        lat = 180d / Math.PI * (2d * Math.Atan(Math.Exp(lat * Math.PI / 180d)) - Math.PI / 2d);
        return Math.Clamp(lat, -85.05112878d, 85.05112878d);
    }

    private static void WriteCoordinate(Span<byte> span, double value)
    {
        var scaled = (int)Math.Round(value * 10_000_000d, MidpointRounding.AwayFromZero);
        BinaryPrimitives.WriteInt32LittleEndian(span, scaled);
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        while (value >= 0x80)
        {
            stream.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        stream.WriteByte((byte)value);
    }

    private static ulong ZxyToTileId(int z, int x, int y)
    {
        if (z < 0)
        {
            return 0;
        }

        ulong acc = ((1UL << z) * (1UL << z) - 1) / 3;
        var a = z - 1;
        var tx = x;
        var ty = y;

        for (var s = 1 << a; s > 0; s >>= 1)
        {
            var rx = tx & s;
            var ry = ty & s;
            acc += (ulong)(((3 * rx) ^ ry) << a);
            (tx, ty) = Rotate(s, tx, ty, rx, ry);
            a--;
        }

        return acc;
    }

    private static (int, int) Rotate(int n, int x, int y, int rx, int ry)
    {
        if (ry == 0)
        {
            if (rx != 0)
            {
                x = n - 1 - x;
                y = n - 1 - y;
            }

            return (y, x);
        }

        return (x, y);
    }

    private static bool IsWebMercatorMatrix(string tileMatrixSetId)
    {
        if (string.IsNullOrWhiteSpace(tileMatrixSetId))
        {
            return false;
        }

        return string.Equals(tileMatrixSetId, "WorldWebMercatorQuad", StringComparison.OrdinalIgnoreCase)
            || string.Equals(tileMatrixSetId, "http://www.opengis.net/def/tms/OGC/1.0/WorldWebMercatorQuad", StringComparison.OrdinalIgnoreCase);
    }

    private static double TileXToLon(int x, int z)
    {
        var n = 1 << z;
        return x / (double)n * 360d - 180d;
    }

    private static double TileYToLat(int y, int z)
    {
        var n = Math.PI - 2d * Math.PI * y / (1 << z);
        return Math.Atan(Math.Sinh(n)) * 180d / Math.PI;
    }

    private readonly struct TileEntryInfo
    {
        public TileEntryInfo(ulong tileId, ulong offset, ulong length, ulong runLength)
        {
            TileId = tileId;
            Offset = offset;
            Length = length;
            RunLength = runLength;
        }

        public ulong TileId { get; }
        public ulong Offset { get; }
        public ulong Length { get; }
        public ulong RunLength { get; }
    }

    private readonly struct TileDescriptor
    {
        public TileDescriptor(PmTileEntry tile, ulong tileId)
        {
            Tile = tile;
            TileId = tileId;
        }

        public PmTileEntry Tile { get; }
        public ulong TileId { get; }
    }
}
