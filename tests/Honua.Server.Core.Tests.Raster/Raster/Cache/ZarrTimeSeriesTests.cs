using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache;

/// <summary>
/// Unit tests for Zarr time-series functionality.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ZarrTimeSeriesTests
{
    private readonly Mock<ILogger<ZarrTimeSeriesService>> _mockLogger;
    private readonly Mock<IZarrReader> _mockZarrReader;

    public ZarrTimeSeriesTests()
    {
        _mockLogger = new Mock<ILogger<ZarrTimeSeriesService>>();
        _mockZarrReader = new Mock<IZarrReader>();
    }

    [Fact]
    public async Task QueryTimeSliceAsync_ValidTimestamp_ReturnsCorrectSlice()
    {
        // Arrange
        var service = CreateService();
        var timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";

        var timeSteps = CreateMockTimeSteps(5);
        var metadata = CreateMockMetadata();
        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);
        SetupMockSliceRead(array, new byte[100]);

        // Act
        var result = await service.QueryTimeSliceAsync(zarrPath, variableName, timestamp);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(variableName, variableName);
        Assert.True(result.TimeIndex >= 0 && result.TimeIndex < timeSteps.Count);
        Assert.NotNull(result.Data);
        Assert.Equal(100, result.Data.Length);
    }

    [Fact]
    public async Task QueryTimeSliceAsync_WithSpatialExtent_SubsetsData()
    {
        // Arrange
        var service = CreateService();
        var timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var bbox = new BoundingBox(-10, -10, 10, 10);

        var timeSteps = CreateMockTimeSteps(5);
        var metadata = CreateMockMetadata();
        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);
        SetupMockSliceRead(array, new byte[50]);

        // Act
        var result = await service.QueryTimeSliceAsync(zarrPath, variableName, timestamp, bbox);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bbox.MinX, result.SpatialExtent.MinX);
        Assert.Equal(bbox.MaxX, result.SpatialExtent.MaxX);
    }

    [Fact]
    public async Task GetTimeStepsAsync_ReturnsAllTimesteps()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";

        var expectedTimeSteps = CreateMockTimeSteps(10);
        SetupMockTimeSteps(zarrPath, variableName, expectedTimeSteps);

        // Act
        var result = await service.GetTimeStepsAsync(zarrPath, variableName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedTimeSteps.Count, result.Count);
        for (int i = 0; i < expectedTimeSteps.Count; i++)
        {
            Assert.Equal(expectedTimeSteps[i], result[i]);
        }
    }

    [Fact]
    public async Task QueryTimeRangeAsync_ValidRange_ReturnsMultipleSlices()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero);

        var timeSteps = CreateMockTimeSteps(10);
        var metadata = CreateMockMetadata();
        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);
        SetupMockSliceRead(array, new byte[100]);

        // Act
        var result = await service.QueryTimeRangeAsync(zarrPath, variableName, startTime, endTime);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Timestamps.Count > 0);
        Assert.Equal(result.Timestamps.Count, result.DataSlices.Count);
        Assert.All(result.Timestamps, ts => Assert.True(ts >= startTime && ts <= endTime));
    }

    [Fact]
    public async Task QueryTimeRangeAsync_NoTimeStepsInRange_ThrowsException()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var startTime = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2025, 1, 5, 0, 0, 0, TimeSpan.Zero);

        var timeSteps = CreateMockTimeSteps(10); // All in 2024
        SetupMockTimeSteps(zarrPath, variableName, timeSteps);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.QueryTimeRangeAsync(zarrPath, variableName, startTime, endTime));
    }

    [Fact]
    public async Task QueryTimeSliceAsync_NoTimeSteps_ThrowsException()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var timestamp = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        SetupMockTimeSteps(zarrPath, variableName, new List<DateTimeOffset>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await service.QueryTimeSliceAsync(zarrPath, variableName, timestamp));
    }

    [Fact]
    public async Task QueryTimeSliceAsync_FindsClosestTimestamp()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";

        // Create timesteps at specific intervals
        var timeSteps = new List<DateTimeOffset>
        {
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 1, 1, 18, 0, 0, TimeSpan.Zero),
        };

        var metadata = CreateMockMetadata();
        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);
        SetupMockSliceRead(array, new byte[100]);

        // Request a timestamp between 6:00 and 12:00 (closer to 6:00)
        var requestedTimestamp = new DateTimeOffset(2024, 1, 1, 7, 0, 0, TimeSpan.Zero);

        // Act
        var result = await service.QueryTimeSliceAsync(zarrPath, variableName, requestedTimestamp);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TimeIndex); // Should be closest to 6:00 (index 1)
        Assert.Equal(timeSteps[1], result.Timestamp);
    }

    [Fact]
    public async Task QueryTimeRangeAsync_WithAggregation_ComputesMeanSlices()
    {
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var endTime = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var aggregationInterval = TimeSpan.FromHours(6);

        var timeSteps = new List<DateTimeOffset>
        {
            startTime,
            startTime.AddHours(3),
            startTime.AddHours(6),
            startTime.AddHours(9)
        };

        var metadata = new ZarrArrayMetadata
        {
            Shape = new[] { 4, 1, 4 },
            Chunks = new[] { 1, 1, 4 },
            DType = "<f4",
            Compressor = "zstd",
            ZarrFormat = 2,
            Order = "C",
            FillValue = null
        };

        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);

        var slices = new Queue<byte[]>(new[]
        {
            ToByteArray(new float[] { 1f, 2f, 3f, 4f }),
            ToByteArray(new float[] { 2f, 3f, 4f, 5f }),
            ToByteArray(new float[] { 3f, 4f, 5f, 6f }),
            ToByteArray(new float[] { 4f, 5f, 6f, 7f })
        });

        _mockZarrReader
            .Setup(r => r.ReadSliceAsync(
                It.Is<ZarrArray>(a => a.VariableName == variableName && a.VariableName != "time"),
                It.IsAny<int[]>(),
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => slices.Dequeue());

        // Act
        var result = await service.QueryTimeRangeAsync(
            zarrPath, variableName, startTime, endTime, null, aggregationInterval);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mean", result.AggregationMethod);
        Assert.Equal(2, result.DataSlices.Count);
        Assert.Equal(2, result.Timestamps.Count);
        Assert.Equal(timeSteps[0], result.Timestamps[0]);
        Assert.Equal(timeSteps[2], result.Timestamps[1]);

        var firstAggregate = ToFloatArray(result.DataSlices[0]);
        var secondAggregate = ToFloatArray(result.DataSlices[1]);

        Assert.Equal(new[] { 1.5f, 2.5f, 3.5f, 4.5f }, firstAggregate);
        Assert.Equal(new[] { 3.5f, 4.5f, 5.5f, 6.5f }, secondAggregate);
    }

    [Fact]
    public async Task ConvertBytesToFloat2D_CorrectlyConvertsData()
    {
        // This tests the internal conversion logic indirectly through the legacy method
        // Arrange
        var service = CreateService();
        var zarrPath = "/data/test.zarr";
        var variableName = "temperature";
        var timestamp = DateTime.UtcNow;

        var timeSteps = CreateMockTimeSteps(1);
        var metadata = CreateMockMetadata();
        var array = CreateMockZarrArray(zarrPath, variableName, metadata);

        // Create float data: 2x3 array
        var floatData = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
        var byteData = new byte[floatData.Length * 4];
        Buffer.BlockCopy(floatData, 0, byteData, 0, byteData.Length);

        SetupMockTimeSteps(zarrPath, variableName, timeSteps);
        SetupMockArray(zarrPath, variableName, array);
        SetupMockSliceRead(array, byteData);

        // Act
        var result = await service.QueryTimeSliceAsync(zarrPath, variableName, new DateTimeOffset(timestamp));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(byteData, result.Data);
    }

    // Helper methods

    private ZarrTimeSeriesService CreateService()
    {
        return new ZarrTimeSeriesService(
            _mockLogger.Object,
            _mockZarrReader.Object,
            pythonExecutable: null);
    }

    private List<DateTimeOffset> CreateMockTimeSteps(int count)
    {
        var baseTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return Enumerable.Range(0, count)
            .Select(i => baseTime.AddDays(i))
            .ToList();
    }

    private ZarrArrayMetadata CreateMockMetadata()
    {
        return new ZarrArrayMetadata
        {
            Shape = new[] { 10, 180, 360 }, // time, lat, lon
            Chunks = new[] { 1, 90, 180 },
            DType = "<f4", // little-endian float32
            Compressor = "zstd",
            ZarrFormat = 2,
            Order = "C",
            FillValue = null,
            DimensionNames = new[] { "time", "lat", "lon" }
        };
    }

    private ZarrArray CreateMockZarrArray(string uri, string variableName, ZarrArrayMetadata metadata)
    {
        return new ZarrArray
        {
            Uri = uri,
            VariableName = variableName,
            Metadata = metadata
        };
    }

    private void SetupMockTimeSteps(string zarrPath, string variableName, List<DateTimeOffset> timeSteps)
    {
        _mockZarrReader
            .Setup(r => r.GetMetadataAsync(zarrPath, variableName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMockMetadata());

        var timeMetadata = new ZarrArrayMetadata
        {
            Shape = new[] { timeSteps.Count },
            Chunks = new[] { timeSteps.Count },
            DType = "<f8", // float64 for time
            Compressor = "zstd",
            ZarrFormat = 2,
            Order = "C",
            FillValue = null
        };

        var timeArray = new ZarrArray
        {
            Uri = zarrPath,
            VariableName = "time",
            Metadata = timeMetadata
        };

        _mockZarrReader
            .Setup(r => r.OpenArrayAsync(zarrPath, "time", It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeArray);

        // Convert timestamps to byte array (seconds since epoch)
        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeBytes = new byte[timeSteps.Count * 8]; // 8 bytes per double
        for (int i = 0; i < timeSteps.Count; i++)
        {
            var seconds = (timeSteps[i] - epoch).TotalSeconds;
            var bytes = BitConverter.GetBytes(seconds);
            Buffer.BlockCopy(bytes, 0, timeBytes, i * 8, 8);
        }

        _mockZarrReader
            .Setup(r => r.ReadSliceAsync(
                It.Is<ZarrArray>(a => a.VariableName == "time"),
                It.IsAny<int[]>(),
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(timeBytes);
    }

    private void SetupMockArray(string zarrPath, string variableName, ZarrArray array)
    {
        _mockZarrReader
            .Setup(r => r.OpenArrayAsync(zarrPath, variableName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(array);

        _mockZarrReader
            .Setup(r => r.GetMetadataAsync(zarrPath, variableName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(array.Metadata);
    }

    private void SetupMockSliceRead(ZarrArray array, byte[] data)
    {
        _mockZarrReader
            .Setup(r => r.ReadSliceAsync(
                It.Is<ZarrArray>(a => a.VariableName == array.VariableName && a.VariableName != "time"),
                It.IsAny<int[]>(),
                It.IsAny<int[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(data);
    }

    private static byte[] ToByteArray(float[] values)
    {
        var buffer = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, buffer, 0, buffer.Length);
        return buffer;
    }

    private static float[] ToFloatArray(byte[] bytes)
        => MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}
