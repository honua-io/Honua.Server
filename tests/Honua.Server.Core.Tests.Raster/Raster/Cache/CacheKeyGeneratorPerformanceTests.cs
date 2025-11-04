using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Honua.Server.Core.Raster.Cache;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache;

/// <summary>
/// Performance tests for CacheKeyGenerator to ensure hash generation overhead is minimal.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
[Trait("Speed", "Slow")]
public sealed class CacheKeyGeneratorPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public CacheKeyGeneratorPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void HashGeneration_Performance_SinglePath()
    {
        // Arrange
        var path = "/data/weather/noaa/gfs/2023/01/15/temperature_2m_above_ground.nc";
        var iterations = 100000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.GeneratePathHash(path);
        }
        sw.Stop();

        // Assert
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Average hash generation time: {avgMicroseconds:F3} microseconds");
        _output.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");

        // Hash generation should be very fast (< 10 microseconds on average)
        avgMicroseconds.Should().BeLessThan(10, "Hash generation should be very fast");
    }

    [Fact]
    public void CacheKeyGeneration_Performance_WithAllParameters()
    {
        // Arrange
        var path = "/data/weather/noaa/gfs/2023/01/15/temperature_2m_above_ground.nc";
        var variable = "temperature_2m";
        var timeIndex = 42;
        var iterations = 100000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.GenerateCacheKey(path, variable, timeIndex);
        }
        sw.Stop();

        // Assert
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Average cache key generation time: {avgMicroseconds:F3} microseconds");
        _output.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");

        // Full cache key generation should still be very fast (< 15 microseconds)
        avgMicroseconds.Should().BeLessThan(15, "Cache key generation should be fast");
    }

    [Fact]
    public void HashGeneration_Performance_VaryingPathLengths()
    {
        // Arrange
        var paths = new[]
        {
            "/data/file.nc",
            "/data/weather/temperature.nc",
            "/data/weather/noaa/gfs/2023/temperature.nc",
            "/data/weather/noaa/gfs/2023/01/15/12/temperature_2m_above_ground_forecast_hour_24.nc"
        };
        var iterations = 10000;

        // Act & Assert
        foreach (var path in paths)
        {
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = CacheKeyGenerator.GeneratePathHash(path);
            }
            sw.Stop();

            var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
            _output.WriteLine($"Path length {path.Length}: {avgMicroseconds:F3} microseconds");

            avgMicroseconds.Should().BeLessThan(20, "Hash generation should be fast regardless of path length");
        }
    }

    [Fact]
    public void CacheKeyValidation_Performance()
    {
        // Arrange
        var validKey = CacheKeyGenerator.GenerateCacheKey("/data/test.nc");
        var iterations = 100000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.ValidateCacheKey(validKey);
        }
        sw.Stop();

        // Assert
        var avgNanoseconds = (sw.Elapsed.TotalMilliseconds * 1000000) / iterations;
        _output.WriteLine($"Average validation time: {avgNanoseconds:F1} nanoseconds");
        _output.WriteLine($"Total time for {iterations} iterations: {sw.ElapsedMilliseconds} ms");

        // Validation should be extremely fast (< 1 microsecond)
        avgNanoseconds.Should().BeLessThan(1000, "Validation should be extremely fast");
    }

    [Fact]
    public void CollisionDetection_Performance()
    {
        // Arrange
        var key1 = CacheKeyGenerator.GenerateCacheKey("/data/dir1/test.nc");
        var key2 = CacheKeyGenerator.GenerateCacheKey("/data/dir2/test.nc");
        var iterations = 100000;

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.DetectCollision(key1, key2);
        }
        sw.Stop();

        // Assert
        var avgNanoseconds = (sw.Elapsed.TotalMilliseconds * 1000000) / iterations;
        _output.WriteLine($"Average collision detection time: {avgNanoseconds:F1} nanoseconds");

        // Collision detection is just string comparison - should be extremely fast
        avgNanoseconds.Should().BeLessThan(500, "Collision detection should be extremely fast");
    }

    [Fact]
    public void BulkCacheKeyGeneration_Performance_1000Paths()
    {
        // Arrange - Generate 1000 unique paths
        var paths = Enumerable.Range(0, 1000)
            .Select(i => $"/data/weather/station{i}/2023/01/{i % 31 + 1}/temperature.nc")
            .ToList();

        // Act
        var sw = Stopwatch.StartNew();
        var keys = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            keys.Add(CacheKeyGenerator.GenerateCacheKey(path));
        }
        sw.Stop();

        // Assert
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / paths.Count;
        _output.WriteLine($"Average time per key (1000 paths): {avgMicroseconds:F3} microseconds");
        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Throughput: {paths.Count / sw.Elapsed.TotalSeconds:F0} keys/second");

        keys.Should().HaveCount(1000);
        keys.Should().OnlyHaveUniqueItems("All generated keys should be unique");
        avgMicroseconds.Should().BeLessThan(15, "Bulk generation should maintain good performance");
    }

    [Fact]
    public void MemoryAllocation_HashGeneration()
    {
        // Arrange
        var path = "/data/weather/noaa/gfs/2023/01/15/temperature.nc";
        var iterations = 1000;

        // Warm up
        for (int i = 0; i < 100; i++)
        {
            _ = CacheKeyGenerator.GeneratePathHash(path);
        }

        // Act - Measure memory before and after
        var gcBefore = GC.CollectionCount(0);
        var memoryBefore = GC.GetTotalMemory(true);

        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.GeneratePathHash(path);
        }

        var memoryAfter = GC.GetTotalMemory(false);
        var gcAfter = GC.CollectionCount(0);

        // Assert
        var memoryDelta = memoryAfter - memoryBefore;
        var avgBytesPerCall = memoryDelta / iterations;

        _output.WriteLine($"Memory delta: {memoryDelta:N0} bytes for {iterations} iterations");
        _output.WriteLine($"Average allocation per call: {avgBytesPerCall} bytes");
        _output.WriteLine($"GC collections (Gen 0): {gcAfter - gcBefore}");

        // Should have minimal allocations (< 1KB per call on average)
        avgBytesPerCall.Should().BeLessThan(1024, "Hash generation should have minimal memory allocations");
    }

    [Fact]
    public void CacheKeyGeneration_Comparison_WithVsWithoutHash()
    {
        // Arrange
        var path = "/data/weather/temperature.nc";
        var iterations = 100000;

        // Act - Measure simple filename-only approach
        var sw1 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = System.IO.Path.GetFileNameWithoutExtension(path);
        }
        sw1.Stop();
        var simpleAvg = (sw1.Elapsed.TotalMilliseconds * 1000) / iterations;

        // Act - Measure hash-based approach
        var sw2 = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = CacheKeyGenerator.GenerateCacheKey(path);
        }
        sw2.Stop();
        var hashAvg = (sw2.Elapsed.TotalMilliseconds * 1000) / iterations;

        // Report
        _output.WriteLine($"Simple filename approach: {simpleAvg:F3} microseconds");
        _output.WriteLine($"Hash-based approach: {hashAvg:F3} microseconds");
        _output.WriteLine($"Overhead: {hashAvg - simpleAvg:F3} microseconds ({((hashAvg - simpleAvg) / simpleAvg * 100):F1}%)");

        // Assert - Overhead should be acceptable (< 100x the simple approach)
        // Note: Hash computation is inherently more expensive than simple string manipulation
        var overhead = hashAvg - simpleAvg;
        overhead.Should().BeLessThan(simpleAvg * 100, "Hash overhead should be reasonable");
        hashAvg.Should().BeLessThan(50, "Total time should still be very fast");
    }

    [Fact]
    public void ConcurrentHashGeneration_Performance()
    {
        // Arrange
        var paths = Enumerable.Range(0, 1000)
            .Select(i => $"/data/station{i}/temperature.nc")
            .ToArray();

        // Act - Sequential
        var sw1 = Stopwatch.StartNew();
        var keys1 = new List<string>();
        foreach (var path in paths)
        {
            keys1.Add(CacheKeyGenerator.GeneratePathHash(path));
        }
        sw1.Stop();

        // Act - Parallel
        var sw2 = Stopwatch.StartNew();
        var keys2 = paths.AsParallel()
            .Select(CacheKeyGenerator.GeneratePathHash)
            .ToList();
        sw2.Stop();

        // Report
        _output.WriteLine($"Sequential: {sw1.ElapsedMilliseconds} ms");
        _output.WriteLine($"Parallel: {sw2.ElapsedMilliseconds} ms");
        _output.WriteLine($"Speedup: {sw1.Elapsed.TotalMilliseconds / sw2.Elapsed.TotalMilliseconds:F2}x");

        // Assert
        keys1.Should().HaveCount(1000);
        keys2.Should().HaveCount(1000);
        keys1.Should().BeEquivalentTo(keys2, "Parallel execution should produce same results");
    }

    [Fact]
    public void RealWorldScenario_Performance_TimeSeries()
    {
        // Arrange - Simulate a time series dataset with 365 time steps
        var basePath = "/data/weather/noaa/gfs/2023/temperature_2m.nc";
        var iterations = 365;

        // Act
        var sw = Stopwatch.StartNew();
        var keys = new List<string>();
        for (int i = 0; i < iterations; i++)
        {
            var key = CacheKeyGenerator.GenerateCacheKey(basePath, "temperature_2m", i);
            keys.Add(key);
        }
        sw.Stop();

        // Assert
        var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / iterations;
        _output.WriteLine($"Average time per time step: {avgMicroseconds:F3} microseconds");
        _output.WriteLine($"Total time for 365 time steps: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Estimated time for 1 year hourly data (8760 steps): {(avgMicroseconds * 8760 / 1000):F1} ms");

        keys.Should().HaveCount(365);
        keys.Should().OnlyHaveUniqueItems();
        avgMicroseconds.Should().BeLessThan(100, "Should handle time series efficiently");
    }
}
