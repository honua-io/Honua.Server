using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Kerchunk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Kerchunk;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class FilesystemKerchunkCacheProviderTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly FilesystemKerchunkCacheProvider _provider;

    public FilesystemKerchunkCacheProviderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"kerchunk-test-{Guid.NewGuid()}");
        _provider = new FilesystemKerchunkCacheProvider(
            _tempDirectory,
            NullLogger<FilesystemKerchunkCacheProvider>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_WithNullCacheDirectory_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => new FilesystemKerchunkCacheProvider(
            null,
            NullLogger<FilesystemKerchunkCacheProvider>.Instance);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cacheDirectory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new FilesystemKerchunkCacheProvider(_tempDirectory, null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_ShouldCreateCacheDirectory()
    {
        // Assert
        Directory.Exists(_tempDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ShouldReturnNull()
    {
        // Arrange
        var key = "nonexistent-key";

        // Act
        var result = await _provider.GetAsync(key);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ShouldReturnSameReferences()
    {
        // Arrange
        var key = "test-key";
        var refs = new KerchunkReferences
        {
            Version = "1.0",
            SourceUri = "s3://bucket/file.nc",
            Refs = new() { ["temperature/0.0"] = new object[] { "s3://bucket/file.nc", 0, 1024 } },
            Metadata = new() { ["temperature/.zarray"] = new { chunks = new[] { 256, 256 } } }
        };

        // Act
        await _provider.SetAsync(key, refs);
        var result = await _provider.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0");
        result.SourceUri.Should().Be("s3://bucket/file.nc");
        result.Refs.Should().ContainKey("temperature/0.0");
        result.Metadata.Should().ContainKey("temperature/.zarray");
    }

    [Fact]
    public async Task ExistsAsync_WithExistingKey_ShouldReturnTrue()
    {
        // Arrange
        var key = "existing-key";
        var refs = new KerchunkReferences { SourceUri = "s3://bucket/file.nc" };
        await _provider.SetAsync(key, refs);

        // Act
        var exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithNonExistentKey_ShouldReturnFalse()
    {
        // Arrange
        var key = "nonexistent-key";

        // Act
        var exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithExistingKey_ShouldRemoveFromCache()
    {
        // Arrange
        var key = "delete-key";
        var refs = new KerchunkReferences { SourceUri = "s3://bucket/file.nc" };
        await _provider.SetAsync(key, refs);

        // Act
        await _provider.DeleteAsync(key);
        var exists = await _provider.ExistsAsync(key);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithNonExistentKey_ShouldNotThrow()
    {
        // Arrange
        var key = "nonexistent-key";

        // Act
        Func<Task> act = async () => await _provider.DeleteAsync(key);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SetAsync_WithSpecialCharactersInKey_ShouldSanitizeKey()
    {
        // Arrange
        var key = "s3://bucket/path/to/file.nc";
        var refs = new KerchunkReferences { SourceUri = key };

        // Act
        await _provider.SetAsync(key, refs);
        var result = await _provider.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.SourceUri.Should().Be(key);
    }

    [Fact]
    public async Task SetAsync_ShouldOverwriteExisting()
    {
        // Arrange
        var key = "overwrite-key";
        var refs1 = new KerchunkReferences { SourceUri = "s3://bucket/file1.nc" };
        var refs2 = new KerchunkReferences { SourceUri = "s3://bucket/file2.nc" };

        // Act
        await _provider.SetAsync(key, refs1);
        await _provider.SetAsync(key, refs2);
        var result = await _provider.GetAsync(key);

        // Assert
        result.Should().NotBeNull();
        result!.SourceUri.Should().Be("s3://bucket/file2.nc");
    }
}
