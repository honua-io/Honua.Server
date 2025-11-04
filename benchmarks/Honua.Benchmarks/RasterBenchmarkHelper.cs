using System;
using System.IO;
using System.IO.Compression;

namespace Honua.Benchmarks;

/// <summary>
/// Helper utilities for raster benchmarks.
/// </summary>
public static class RasterBenchmarkHelper
{
    /// <summary>
    /// Generate test raster data for benchmarking.
    /// </summary>
    public static byte[] GenerateTestRasterData(int width, int height, int bands = 1)
    {
        var bytesPerPixel = bands;
        var data = new byte[width * height * bytesPerPixel];

        // Generate simple gradient pattern
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int b = 0; b < bands; b++)
                {
                    var offset = (y * width + x) * bytesPerPixel + b;
                    data[offset] = (byte)((x + y) % 256);
                }
            }
        }

        return data;
    }

    /// <summary>
    /// Generate compressed test data for decompression benchmarks.
    /// </summary>
    public static byte[] GenerateCompressedTestData(string codec, int uncompressedSize)
    {
        var testData = GenerateTestRasterData(
            (int)Math.Sqrt(uncompressedSize),
            (int)Math.Sqrt(uncompressedSize),
            1);

        return codec.ToLowerInvariant() switch
        {
            "gzip" => CompressGzip(testData),
            "zstd" => CompressZstd(testData),
            _ => testData
        };
    }

    private static byte[] CompressGzip(byte[] data)
    {
        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionLevel.Optimal))
        {
            gzipStream.Write(data, 0, data.Length);
        }
        return outputStream.ToArray();
    }

    private static byte[] CompressZstd(byte[] data)
    {
        using var compressor = new ZstdSharp.Compressor();
        return compressor.Wrap(data).ToArray();
    }

    /// <summary>
    /// Ensure benchmark test data directory exists.
    /// </summary>
    public static void EnsureTestDataDirectory()
    {
        var testDataDir = Path.Combine(
            Directory.GetCurrentDirectory(),
            "test-data");

        if (!Directory.Exists(testDataDir))
        {
            Directory.CreateDirectory(testDataDir);
        }
    }

    /// <summary>
    /// Print benchmark environment information.
    /// </summary>
    public static void PrintEnvironmentInfo()
    {
        Console.WriteLine("Raster Benchmark Environment:");
        Console.WriteLine($"  OS: {Environment.OSVersion}");
        Console.WriteLine($"  .NET: {Environment.Version}");
        Console.WriteLine($"  Processors: {Environment.ProcessorCount}");
        Console.WriteLine($"  Memory: {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024 * 1024)} GB");
        Console.WriteLine();
    }
}
