// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

#nullable enable

namespace Honua.Server.Core.Serialization;

/// <summary>
/// Streaming Well-Known Binary (WKB) writer for feature collections.
/// Outputs geometries in ISO 19125-1 WKB format with length-prefixed framing.
///
/// Binary Format Structure:
/// - Header: [4-byte feature count (int32, little-endian)]
/// - For each feature:
///   - [4-byte WKB length (int32, little-endian)]
///   - [WKB geometry bytes (variable length)]
///
/// This format enables streaming parsing where consumers can read the count,
/// then read each geometry by first reading its length prefix.
///
/// Byte Order: Little-endian (default WKB byte order)
/// Coordinate Dimension: Determined by geometry (2D/3D/4D)
///
/// Note: Only geometries are written - feature properties are not included.
/// For full feature data with properties, use GeoJSON or other formats.
/// </summary>
public sealed class WkbStreamingWriter : StreamingFeatureCollectionWriterBase
{
    private long _actualFeatureCount;

    protected override string ContentType => "application/wkb";
    protected override string FormatName => "WKB";

    /// <summary>
    /// Creates a new WKB streaming writer.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostics</param>
    public WkbStreamingWriter(ILogger<WkbStreamingWriter> logger)
        : base(logger)
    {
    }

    /// <summary>
    /// Writes the WKB header containing the feature count.
    /// Note: We write a placeholder count (0) that will be updated in the footer.
    /// This is necessary because we don't know the final count until streaming completes.
    /// </summary>
    protected override async Task WriteHeaderAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(layer);

        _actualFeatureCount = 0;

        // Write placeholder feature count (4 bytes, little-endian)
        // This will be updated in WriteFooterAsync if the stream is seekable
        var countBytes = BitConverter.GetBytes(0);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(countBytes);
        }

        await outputStream.WriteAsync(countBytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// WKB format has no separators between features.
    /// Each feature is length-prefixed, providing natural boundaries.
    /// </summary>
    protected override Task WriteFeatureSeparatorAsync(
        Stream outputStream,
        bool isFirst,
        CancellationToken cancellationToken)
    {
        // No separator needed - length-prefixed format
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a single feature's geometry as length-prefixed WKB.
    /// Format: [4-byte length][WKB bytes]
    ///
    /// If the feature has no geometry or geometry extraction fails,
    /// the feature is skipped (no output is written).
    /// </summary>
    protected override async Task WriteFeatureAsync(
        Stream outputStream,
        FeatureRecord feature,
        LayerDefinition layer,
        StreamingWriterContext context,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);
        Guard.NotNull(feature);
        Guard.NotNull(layer);

        // Only write geometry if requested
        if (!context.ReturnGeometry)
        {
            return;
        }

        // Extract geometry from feature
        var geometry = ExtractGeometry(feature, layer.GeometryField);
        if (geometry is null || geometry.IsEmpty)
        {
            return;
        }

        try
        {
            // THREAD-SAFETY FIX: Create per-request WKBWriter instead of static shared instance
            var wkbWriter = new WKBWriter(ByteOrder.LittleEndian);

            // Convert geometry to WKB bytes
            var wkbBytes = wkbWriter.Write(geometry);

            // Write length prefix (4 bytes, little-endian)
            var lengthBytes = BitConverter.GetBytes(wkbBytes.Length);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            await outputStream.WriteAsync(lengthBytes, cancellationToken).ConfigureAwait(false);

            // Write WKB geometry bytes
            await outputStream.WriteAsync(wkbBytes, cancellationToken).ConfigureAwait(false);

            _actualFeatureCount++;
        }
        catch (Exception ex)
        {
            var idValue = feature.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
            throw new InvalidOperationException(
                $"Failed to write geometry as WKB for feature {idValue}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Finalizes the WKB stream by updating the feature count in the header.
    /// If the stream is seekable, we seek back to the beginning and write the actual count.
    /// If not seekable (e.g., network stream), the header retains the placeholder value.
    /// </summary>
    protected override async Task WriteFooterAsync(
        Stream outputStream,
        LayerDefinition layer,
        StreamingWriterContext context,
        long featuresWritten,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(outputStream);

        // Update feature count in header if stream is seekable
        if (outputStream.CanSeek)
        {
            var currentPosition = outputStream.Position;

            // Seek to beginning and write actual count
            outputStream.Position = 0;
            var countBytes = BitConverter.GetBytes((int)_actualFeatureCount);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(countBytes);
            }
            await outputStream.WriteAsync(countBytes, cancellationToken).ConfigureAwait(false);

            // Restore position to end of stream
            outputStream.Position = currentPosition;
        }

        await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts a NetTopologySuite Geometry from a feature record.
    /// Supports multiple input formats:
    /// - Direct Geometry objects
    /// - GeoJSON strings
    /// - GeoJSON JsonNode/JsonElement objects
    /// </summary>
    /// <param name="record">Feature record containing geometry</param>
    /// <param name="geometryField">Name of the geometry field</param>
    /// <returns>Extracted geometry or null if not found/invalid</returns>
    private static Geometry? ExtractGeometry(FeatureRecord record, string geometryField)
    {
        if (!record.Attributes.TryGetValue(geometryField, out var raw) || raw is null)
        {
            return null;
        }

        try
        {
            // THREAD-SAFETY FIX: Create per-request GeoJsonReader instead of static shared instance
            var geoJsonReader = new GeoJsonReader();

            return raw switch
            {
                Geometry g => g,
                JsonNode node => geoJsonReader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String =>
                    geoJsonReader.Read<Geometry>(element.GetString() ?? string.Empty),
                JsonElement element => geoJsonReader.Read<Geometry>(element.GetRawText()),
                string text => geoJsonReader.Read<Geometry>(text),
                _ => geoJsonReader.Read<Geometry>(raw.ToString() ?? string.Empty)
            };
        }
        catch
        {
            return null;
        }
    }
}
