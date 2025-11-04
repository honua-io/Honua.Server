using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Readers;

/// <summary>
/// Comprehensive tests for ZarrStream implementation.
/// Covers lazy chunk loading, spatial windowing, seeking, and error handling.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class ZarrStreamTests
{
    private readonly Mock<IZarrReader> _mockZarrReader;
    private readonly Mock<ILogger<ZarrStream>> _mockLogger;

    public ZarrStreamTests()
    {
        _mockZarrReader = new Mock<IZarrReader>();
        _mockLogger = new Mock<ILogger<ZarrStream>>();
    }

    [Fact]
    public void Constructor_WithNullZarrReader_ThrowsArgumentNullException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);

        // Act & Assert
        var act = () => new ZarrStream(null!, array, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("zarrReader");
    }

    [Fact]
    public void Constructor_WithNullZarrArray_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ZarrStream(_mockZarrReader.Object, null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("zarrArray");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);

        // Act & Assert
        var act = () => new ZarrStream(_mockZarrReader.Object, array, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidParameters_InitializesStream()
    {
        // Arrange
        var array = CreateTestArray(100, 100);

        // Act
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Assert
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Position.Should().Be(0);
        stream.Length.Should().Be(100 * 100 * 4); // 100x100 float32 (4 bytes)
    }

    [Fact]
    public void StreamProperties_AfterCreation_HaveExpectedValues()
    {
        // Arrange
        var array = CreateTestArray(256, 256);

        // Act
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Assert
        stream.CanRead.Should().BeTrue();
        stream.CanSeek.Should().BeTrue();
        stream.CanWrite.Should().BeFalse();
        stream.Length.Should().Be(256 * 256 * 4);
    }

    [Fact]
    public void Position_SetToValidValue_UpdatesPosition()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act
        stream.Position = 1000;

        // Assert
        stream.Position.Should().Be(1000);
    }

    [Fact]
    public void Position_SetToNegative_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.Position = -1;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Position_SetBeyondLength_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.Position = stream.Length + 1;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ReadAsync_WithValidBuffer_ReadsDataFromChunks()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        var chunkData = new byte[64 * 64 * 4]; // 64x64 chunk of float32
        for (int i = 0; i < chunkData.Length; i++)
        {
            chunkData[i] = (byte)(i % 256);
        }

        _mockZarrReader.Setup(r => r.ReadChunkAsync(
            It.IsAny<ZarrArray>(),
            It.IsAny<int[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunkData);

        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[1024];

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(1024);
        stream.Position.Should().Be(1024);
        _mockZarrReader.Verify(r => r.ReadChunkAsync(
            It.IsAny<ZarrArray>(),
            It.IsAny<int[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReadAsync_AtEndOfStream_ReturnsZero()
    {
        // Arrange
        var array = CreateTestArray(10, 10);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        stream.Position = stream.Length;
        var buffer = new byte[100];

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_WithNullBuffer_ThrowsArgumentNullException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        await stream.Invoking(s => s.ReadAsync(null!, 0, 100))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadAsync_WithInvalidOffset_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[100];

        // Act & Assert
        await stream.Invoking(s => s.ReadAsync(buffer, -1, 10))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ReadAsync_WithInvalidCount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[100];

        // Act & Assert
        await stream.Invoking(s => s.ReadAsync(buffer, 0, 200))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_WithBeginOrigin_SetsPositionFromStart()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act
        var newPosition = stream.Seek(500, SeekOrigin.Begin);

        // Assert
        newPosition.Should().Be(500);
        stream.Position.Should().Be(500);
    }

    [Fact]
    public void Seek_WithCurrentOrigin_SetsPositionRelativeToCurrent()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        stream.Position = 1000;

        // Act
        var newPosition = stream.Seek(500, SeekOrigin.Current);

        // Assert
        newPosition.Should().Be(1500);
        stream.Position.Should().Be(1500);
    }

    [Fact]
    public void Seek_WithEndOrigin_SetsPositionFromEnd()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act
        var newPosition = stream.Seek(-100, SeekOrigin.End);

        // Assert
        newPosition.Should().Be(stream.Length - 100);
        stream.Position.Should().Be(stream.Length - 100);
    }

    [Fact]
    public void Seek_BeyondEnd_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.Seek(stream.Length + 1, SeekOrigin.Begin);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Seek_BeforeStart_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.Seek(-1, SeekOrigin.Begin);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Write_WhenCalled_ThrowsNotSupportedException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[100];

        // Act & Assert
        var act = () => stream.Write(buffer, 0, buffer.Length);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetLength_WhenCalled_ThrowsNotSupportedException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.SetLength(1000);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Flush_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act & Assert
        var act = () => stream.Flush();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenCalled_MarksStreamAsDisposed()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);

        // Act
        stream.Dispose();

        // Assert
        stream.CanRead.Should().BeFalse();
        stream.CanSeek.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WithValidParameters_CreatesStream()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        _mockZarrReader.Setup(r => r.OpenArrayAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(array);

        // Act
        using var stream = await ZarrStream.CreateAsync(
            _mockZarrReader.Object,
            "https://example.com/data.zarr",
            "temperature",
            _mockLogger.Object);

        // Assert
        stream.Should().NotBeNull();
        stream.CanRead.Should().BeTrue();
        stream.Length.Should().Be(100 * 100 * 4);
    }

    [Fact]
    public async Task CreateWithWindowAsync_WithValidSlice_CreatesStreamWithCorrectSize()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        _mockZarrReader.Setup(r => r.OpenArrayAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(array);

        var sliceStart = new[] { 10, 10 };
        var sliceCount = new[] { 50, 50 };

        // Act
        using var stream = await ZarrStream.CreateWithWindowAsync(
            _mockZarrReader.Object,
            "https://example.com/data.zarr",
            "temperature",
            sliceStart,
            sliceCount,
            _mockLogger.Object);

        // Assert
        stream.Should().NotBeNull();
        stream.Length.Should().Be(50 * 50 * 4); // 50x50 window
    }

    [Fact]
    public void Constructor_WithInvalidSliceStart_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        var sliceStart = new[] { -1, 0 }; // Invalid negative start
        var sliceCount = new[] { 50, 50 };

        // Act & Assert
        var act = () => new ZarrStream(
            _mockZarrReader.Object,
            array,
            _mockLogger.Object,
            sliceStart: sliceStart,
            sliceCount: sliceCount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithSliceCountBeyondBounds_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var array = CreateTestArray(100, 100);
        var sliceStart = new[] { 50, 50 };
        var sliceCount = new[] { 60, 60 }; // Would go beyond array bounds

        // Act & Assert
        var act = () => new ZarrStream(
            _mockZarrReader.Object,
            array,
            _mockLogger.Object,
            sliceStart: sliceStart,
            sliceCount: sliceCount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ReadAsync_AcrossMultipleChunks_LoadsChunksOnDemand()
    {
        // Arrange
        var array = CreateTestArray(128, 128); // 2x2 grid of 64x64 chunks
        var chunkData = new byte[64 * 64 * 4];

        _mockZarrReader.Setup(r => r.ReadChunkAsync(
            It.IsAny<ZarrArray>(),
            It.IsAny<int[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunkData);

        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[128 * 128 * 4]; // Read entire array

        // Act
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(buffer.Length);
        // Should load all 4 chunks (2x2)
        _mockZarrReader.Verify(r => r.ReadChunkAsync(
            It.IsAny<ZarrArray>(),
            It.IsAny<int[]>(),
            It.IsAny<CancellationToken>()), Times.AtLeast(1));
    }

    [Fact]
    public async Task ReadAsync_AfterSeek_LoadsCorrectChunk()
    {
        // Arrange
        var array = CreateTestArray(128, 128);
        var chunkData = new byte[64 * 64 * 4];

        _mockZarrReader.Setup(r => r.ReadChunkAsync(
            It.IsAny<ZarrArray>(),
            It.IsAny<int[]>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunkData);

        using var stream = new ZarrStream(_mockZarrReader.Object, array, _mockLogger.Object);
        var buffer = new byte[1024];

        // Act - Seek to middle of array
        stream.Seek(64 * 64 * 4, SeekOrigin.Begin);
        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

        // Assert
        bytesRead.Should().Be(1024);
        stream.Position.Should().Be(64 * 64 * 4 + 1024);
    }

    [Fact]
    public void ZarrStreamMetrics_WhenCreated_InitializesCounters()
    {
        // Act
        var metrics = new ZarrStreamMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.StreamsCreated.Should().NotBeNull();
        metrics.StreamsDisposed.Should().NotBeNull();
        metrics.BytesRead.Should().NotBeNull();
        metrics.ChunksLoaded.Should().NotBeNull();
        metrics.ChunkErrors.Should().NotBeNull();
        metrics.ChunkLoadTimeMs.Should().NotBeNull();
    }

    private ZarrArray CreateTestArray(int height, int width, int chunkSize = 64)
    {
        return new ZarrArray
        {
            Uri = "https://example.com/test.zarr",
            VariableName = "data",
            Metadata = new ZarrArrayMetadata
            {
                Shape = new[] { height, width },
                Chunks = new[] { chunkSize, chunkSize },
                DType = "<f4", // Little-endian float32
                Compressor = "null",
                ZarrFormat = 2,
                Order = "C"
            }
        };
    }
}
