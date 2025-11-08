using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Raster.Kerchunk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Kerchunk;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class GdalKerchunkGeneratorTests
{
    private readonly GdalKerchunkGenerator _generator;

    public GdalKerchunkGeneratorTests()
    {
        _generator = new GdalKerchunkGenerator(NullLogger<GdalKerchunkGenerator>.Instance);
    }

    [Theory]
    [InlineData(".nc")]
    [InlineData(".nc4")]
    [InlineData(".netcdf")]
    [InlineData(".h5")]
    [InlineData(".hdf5")]
    [InlineData(".hdf")]
    [InlineData(".he5")]
    [InlineData(".grib")]
    [InlineData(".grib2")]
    [InlineData(".grb")]
    [InlineData(".grb2")]
    public void CanHandle_WithSupportedExtension_ShouldReturnTrue(string extension)
    {
        // Arrange
        var uri = $"s3://bucket/file{extension}";

        // Act
        var canHandle = _generator.CanHandle(uri);

        // Assert
        canHandle.Should().BeTrue();
    }

    [Theory]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    [InlineData(".zarr")]
    [InlineData(".jpg")]
    [InlineData("")]
    public void CanHandle_WithUnsupportedExtension_ShouldReturnFalse(string extension)
    {
        // Arrange
        var uri = $"s3://bucket/file{extension}";

        // Act
        var canHandle = _generator.CanHandle(uri);

        // Assert
        canHandle.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WithNullUri_ShouldReturnFalse()
    {
        // Act
        var canHandle = _generator.CanHandle(null);

        // Assert
        canHandle.Should().BeFalse();
    }

    [Fact]
    public void CanHandle_WithEmptyUri_ShouldReturnFalse()
    {
        // Act
        var canHandle = _generator.CanHandle("");

        // Assert
        canHandle.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_WithUnsupportedFormat_ShouldThrowNotSupportedException()
    {
        // Arrange
        var uri = "s3://bucket/file.tif";
        var options = new KerchunkGenerationOptions();

        // Act
        Func<Task> act = async () => await _generator.GenerateAsync(uri, options, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Unsupported file format*");
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidFile_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var uri = "/nonexistent/file.nc";
        var options = new KerchunkGenerationOptions();

        // Act
        Func<Task> act = async () => await _generator.GenerateAsync(uri, options, CancellationToken.None);

        // Assert - GDAL throws ApplicationException for file not found
        await act.Should().ThrowAsync<ApplicationException>()
            .WithMessage("*No such file or directory*");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new GdalKerchunkGenerator(null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }
}
