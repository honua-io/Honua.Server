// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Sources;

/// <summary>
/// Pure .NET raster source provider using native COG and Zarr readers.
/// No GDAL dependency for reading - only uses GDAL for ingestion/conversion.
/// </summary>
public sealed class NativeRasterSourceProvider : IRasterSourceProvider
{
    private readonly ILogger<NativeRasterSourceProvider> _logger;
    private readonly ICogReader _cogReader;
    private readonly IZarrReader? _zarrReader;

    public NativeRasterSourceProvider(
        ILogger<NativeRasterSourceProvider> logger,
        ICogReader cogReader,
        IZarrReader? zarrReader = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cogReader = cogReader ?? throw new ArgumentNullException(nameof(cogReader));
        _zarrReader = zarrReader;
    }

    public string ProviderKey => "native";

    public bool CanHandle(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        var extension = Path.GetExtension(uri).ToLowerInvariant();

        // Handle COG/GeoTIFF
        if (extension is ".tif" or ".tiff")
        {
            return true;
        }

        // Handle Zarr (directory-based)
        if (uri.EndsWith(".zarr", StringComparison.OrdinalIgnoreCase) || Directory.Exists(uri))
        {
            return _zarrReader != null;
        }

        // Handle HTTP COG/Zarr
        if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return uri.Contains(".tif") || uri.Contains(".zarr");
        }

        return false;
    }

    public async Task<Stream> OpenReadAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(uri))
        {
            throw new NotSupportedException($"Native provider cannot handle URI: {uri}");
        }

        // Detect format
        if (IsZarr(uri))
        {
            return await OpenZarrAsync(uri, cancellationToken);
        }
        else
        {
            return await OpenCogAsync(uri, cancellationToken);
        }
    }

    public async Task<Stream> OpenReadRangeAsync(
        string uri,
        long offset,
        long? length = null,
        CancellationToken cancellationToken = default)
    {
        // For COG, use HTTP range requests or seek to position
        if (IsCog(uri))
        {
            _logger.LogDebug("COG range request for {Uri} at offset {Offset}, length {Length}", uri, offset, length);

            // For HTTP COG, the COG reader should handle range requests internally
            // For local COG, we can use a seekable stream
            var stream = await OpenReadAsync(uri, cancellationToken);

            if (offset > 0 && stream.CanSeek)
            {
                stream.Seek(offset, SeekOrigin.Begin);
            }

            if (length.HasValue && stream.CanSeek)
            {
                // Return a limited stream wrapper that only reads up to the specified length
                return new LimitedLengthStream(stream, length.Value);
            }

            return stream;
        }

        // For Zarr, range requests map to chunk access
        if (IsZarr(uri) && _zarrReader != null)
        {
            _logger.LogDebug("Zarr range request for {Uri} at offset {Offset}, length {Length}", uri, offset, length);

            // Zarr chunk-based access doesn't map directly to byte offsets
            // For MVP, fall back to full read (proper implementation would need chunk coordinate mapping)
            _logger.LogWarning(
                "Zarr byte-level range requests not supported. " +
                "Use ZarrReader.ReadChunkAsync() for efficient chunk-based access.");

            return await OpenReadAsync(uri, cancellationToken);
        }

        throw new NotSupportedException($"Range requests not supported for: {uri}");
    }

    private async Task<Stream> OpenCogAsync(string uri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Opening COG via native reader: {Uri}", uri);

        var dataset = await _cogReader.OpenAsync(uri, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "COG opened: {Width}x{Height}, {Bands} bands, {Compression} compression, COG: {IsCog}",
            dataset.Metadata.Width,
            dataset.Metadata.Height,
            dataset.Metadata.BandCount,
            dataset.Metadata.Compression,
            dataset.Metadata.IsCog);

        // Return the underlying stream
        return dataset.Stream;
    }

    private async Task<Stream> OpenZarrAsync(string uri, CancellationToken cancellationToken)
    {
        if (_zarrReader == null)
        {
            throw new NotSupportedException("Zarr reader not configured");
        }

        _logger.LogInformation("Opening Zarr via native reader: {Uri}", uri);

        // Detect variable name from Zarr metadata
        var variableName = await DetectZarrVariableNameAsync(uri, cancellationToken);

        var array = await _zarrReader.OpenArrayAsync(uri, variableName, cancellationToken);

        _logger.LogInformation(
            "Zarr array opened: shape={Shape}, chunks={Chunks}, dtype={DType}, compressor={Compressor}",
            string.Join("x", array.Metadata.Shape),
            string.Join("x", array.Metadata.Chunks),
            array.Metadata.DType,
            array.Metadata.Compressor);

        // Create ZarrStream for lazy chunk-based reading
        var zarrStreamLogger = Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger<ZarrStream>(
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var zarrStream = new ZarrStream(_zarrReader, array, zarrStreamLogger);

        _logger.LogInformation(
            "Created ZarrStream: totalBytes={TotalBytes}, canRead={CanRead}, canSeek={CanSeek}",
            zarrStream.Length, zarrStream.CanRead, zarrStream.CanSeek);

        return zarrStream;
    }

    private async Task<string> DetectZarrVariableNameAsync(string uri, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        try
        {
            // For Zarr, check if there's a .zmetadata file (Zarr v2 consolidated metadata)
            var zmetadataPath = Path.Combine(uri, ".zmetadata");
            if (File.Exists(zmetadataPath))
            {
                var json = await File.ReadAllTextAsync(zmetadataPath, cancellationToken);
                // Parse .zmetadata to find array names
                // For simplicity, look for entries with .zarray suffix
                if (json.Contains("\"metadata\""))
                {
                    // Extract first array name from metadata structure
                    // This is a simplified parser - production would use proper JSON parsing
                    _logger.LogDebug("Found .zmetadata, attempting to parse variable names");
                }
            }

            // Check for common variable names in Zarr directory
            var commonNames = new[] { "data", "temperature", "precipitation", "variable", "values", "array" };
            foreach (var name in commonNames)
            {
                var zarrayPath = Path.Combine(uri, name, ".zarray");
                if (File.Exists(zarrayPath))
                {
                    _logger.LogDebug("Detected Zarr variable: {VariableName}", name);
                    return name;
                }
            }

            // Try to list subdirectories - each may be a variable
            if (Directory.Exists(uri))
            {
                var subdirs = Directory.GetDirectories(uri);
                foreach (var subdir in subdirs)
                {
                    var dirName = Path.GetFileName(subdir);
                    // Skip hidden directories and coordinate variables
                    if (!dirName.StartsWith('.') && !dirName.Equals("time", StringComparison.OrdinalIgnoreCase) &&
                        !dirName.Equals("lat", StringComparison.OrdinalIgnoreCase) &&
                        !dirName.Equals("lon", StringComparison.OrdinalIgnoreCase))
                    {
                        var zarrayPath = Path.Combine(subdir, ".zarray");
                        if (File.Exists(zarrayPath))
                        {
                            _logger.LogDebug("Detected Zarr variable from directory: {VariableName}", dirName);
                            return dirName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error detecting Zarr variable name for {Uri}, using default", uri);
        }

        // Fallback to default
        _logger.LogWarning("Could not detect Zarr variable name, using default 'data'");
        return "data";
    }

    private bool IsCog(string uri)
    {
        return uri.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
               uri.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsZarr(string uri)
    {
        return uri.EndsWith(".zarr", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("/zarr/") ||
               Directory.Exists(uri);
    }

    /// <summary>
    /// Stream wrapper that limits the number of bytes that can be read.
    /// </summary>
    private sealed class LimitedLengthStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _maxLength;
        private readonly long _startPosition;

        public LimitedLengthStream(Stream baseStream, long maxLength)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _maxLength = maxLength;
            _startPosition = _baseStream.CanSeek ? _baseStream.Position : 0;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length
        {
            get
            {
                if (!_baseStream.CanSeek)
                {
                    return _maxLength;
                }

                var remaining = _baseStream.Length - _startPosition;
                return Math.Min(_maxLength, remaining);
            }
        }
        public override long Position
        {
            get => _baseStream.CanSeek ? _baseStream.Position : 0;
            set
            {
                if (_baseStream.CanSeek)
                {
                    if (value < _startPosition || value > _startPosition + _maxLength)
                    {
                        throw new IOException("Cannot seek beyond the limited range");
                    }

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
            var consumed = _baseStream.CanSeek ? _baseStream.Position - _startPosition : 0;
            var remaining = _maxLength - consumed;
            if (remaining <= 0)
            {
                return 0;
            }

            var toRead = (int)Math.Min(count, remaining);
            var bytesRead = _baseStream.Read(buffer, offset, toRead);
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
                SeekOrigin.Begin => _startPosition + offset,
                SeekOrigin.Current => _baseStream.Position + offset,
                SeekOrigin.End => _startPosition + _maxLength + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < _startPosition || newPosition > _startPosition + _maxLength)
            {
                throw new IOException("Cannot seek beyond the limited length");
            }

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
