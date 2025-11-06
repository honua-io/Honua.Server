using System.IO.Compression;
using System.Text;

namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// Helper class for compressing and decompressing data using GZIP and Brotli.
/// </summary>
public static class CompressionHelper
{
    /// <summary>
    /// Compresses a string using GZIP compression.
    /// </summary>
    /// <param name="data">The string to compress.</param>
    /// <returns>Compressed byte array.</returns>
    public static byte[] CompressGzip(string data)
    {
        if (string.IsNullOrEmpty(data))
            return Array.Empty<byte>();

        var bytes = Encoding.UTF8.GetBytes(data);
        return CompressGzip(bytes);
    }

    /// <summary>
    /// Compresses a byte array using GZIP compression.
    /// </summary>
    /// <param name="data">The byte array to compress.</param>
    /// <returns>Compressed byte array.</returns>
    public static byte[] CompressGzip(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses GZIP-compressed data to a string.
    /// </summary>
    /// <param name="compressedData">The compressed byte array.</param>
    /// <returns>Decompressed string.</returns>
    public static string DecompressGzipToString(byte[] compressedData)
    {
        var decompressed = DecompressGzip(compressedData);
        return Encoding.UTF8.GetString(decompressed);
    }

    /// <summary>
    /// Decompresses GZIP-compressed data to a byte array.
    /// </summary>
    /// <param name="compressedData">The compressed byte array.</param>
    /// <returns>Decompressed byte array.</returns>
    public static byte[] DecompressGzip(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Compresses a string using Brotli compression.
    /// </summary>
    /// <param name="data">The string to compress.</param>
    /// <returns>Compressed byte array.</returns>
    public static byte[] CompressBrotli(string data)
    {
        if (string.IsNullOrEmpty(data))
            return Array.Empty<byte>();

        var bytes = Encoding.UTF8.GetBytes(data);
        return CompressBrotli(bytes);
    }

    /// <summary>
    /// Compresses a byte array using Brotli compression.
    /// </summary>
    /// <param name="data">The byte array to compress.</param>
    /// <returns>Compressed byte array.</returns>
    public static byte[] CompressBrotli(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal))
        {
            brotli.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    /// <summary>
    /// Decompresses Brotli-compressed data to a string.
    /// </summary>
    /// <param name="compressedData">The compressed byte array.</param>
    /// <returns>Decompressed string.</returns>
    public static string DecompressBrotliToString(byte[] compressedData)
    {
        var decompressed = DecompressBrotli(compressedData);
        return Encoding.UTF8.GetString(decompressed);
    }

    /// <summary>
    /// Decompresses Brotli-compressed data to a byte array.
    /// </summary>
    /// <param name="compressedData">The compressed byte array.</param>
    /// <returns>Decompressed byte array.</returns>
    public static byte[] DecompressBrotli(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Automatically decompresses data based on content encoding header.
    /// </summary>
    /// <param name="data">The compressed data.</param>
    /// <param name="contentEncoding">The content encoding (gzip, br, or null).</param>
    /// <returns>Decompressed string.</returns>
    public static string AutoDecompress(byte[] data, string? contentEncoding)
    {
        return contentEncoding?.ToLowerInvariant() switch
        {
            "gzip" => DecompressGzipToString(data),
            "br" => DecompressBrotliToString(data),
            _ => Encoding.UTF8.GetString(data)
        };
    }

    /// <summary>
    /// Calculates the compression ratio.
    /// </summary>
    /// <param name="originalSize">Original data size in bytes.</param>
    /// <param name="compressedSize">Compressed data size in bytes.</param>
    /// <returns>Compression ratio (e.g., 0.5 means 50% reduction).</returns>
    public static double GetCompressionRatio(long originalSize, long compressedSize)
    {
        if (originalSize == 0)
            return 0;

        return 1.0 - ((double)compressedSize / originalSize);
    }

    /// <summary>
    /// Determines if data should be compressed based on size and type.
    /// </summary>
    /// <param name="data">The data to check.</param>
    /// <param name="mimeType">The MIME type of the data.</param>
    /// <returns>True if compression is recommended; otherwise, false.</returns>
    public static bool ShouldCompress(byte[] data, string? mimeType = null)
    {
        // Don't compress small data (< 1KB)
        if (data.Length < 1024)
            return false;

        // Don't compress already compressed formats
        var compressedTypes = new[]
        {
            "image/jpeg", "image/png", "image/gif", "image/webp",
            "video/", "audio/",
            "application/zip", "application/gzip", "application/x-brotli"
        };

        if (mimeType != null && compressedTypes.Any(t => mimeType.StartsWith(t, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
