using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Honua.Server.Core.Raster.Cache;
using Honua.Server.Core.Raster.Readers;

namespace Honua.Server.Core.Tests.Raster.Raster.Cache;

/// <summary>
/// Security tests for ZarrTimeSeriesService to verify Python injection vulnerability is fixed.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ZarrTimeSeriesServiceSecurityTests
{
    private static IZarrReader CreateMockZarrReader()
    {
        var mock = new Mock<IZarrReader>();
        return mock.Object;
    }
    [Theory]
    [InlineData("http://example.com/'; import os; os.system('rm -rf /'); #")]
    [InlineData("http://example.com/\"; exec('malicious code'); #")]
    [InlineData("test'; DROP TABLE users; --")]
    [InlineData("path/with\nimport sys\nsys.exit(1)")]
    [InlineData("data.nc'; __import__('os').system('whoami'); '")]
    [InlineData("test'; exec(open('evil.py').read()); '")]
    [InlineData("path'; subprocess.call(['rm', '-rf', '/']); '")]
    public async Task ConvertToZarrAsync_RejectsInjectionAttemptsInSourceUri(string maliciousUri)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ConvertToZarrAsync(maliciousUri, "/tmp/output.zarr", options));

        Assert.Contains("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://example.com/'; import os; os.system('rm -rf /'); #")]
    [InlineData("http://example.com/\"; exec('malicious code'); #")]
    [InlineData("test'; DROP TABLE users; --")]
    [InlineData("path/with\nimport sys\nsys.exit(1)")]
    public async Task ConvertToZarrAsync_RejectsInjectionAttemptsInZarrUri(string maliciousUri)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ConvertToZarrAsync("/tmp/source.nc", maliciousUri, options));

        Assert.Contains("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("temp'; import os; os.system('whoami'); #")]
    [InlineData("var'; exec('evil'); '")]
    [InlineData("data\nimport sys")]
    [InlineData("test'; subprocess.call(['ls']); '")]
    public async Task ConvertToZarrAsync_RejectsInjectionAttemptsInVariableName(string maliciousVariable)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = maliciousVariable
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ConvertToZarrAsync("/tmp/source.nc", "/tmp/output.zarr", options));

        Assert.Contains("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("zstd'; import os; #")]
    [InlineData("gzip\nexec('code')")]
    [InlineData("blosc'; subprocess.run(['ls']); '")]
    public async Task ConvertToZarrAsync_RejectsInjectionAttemptsInCompression(string maliciousCompression)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature",
            Compression = maliciousCompression
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ConvertToZarrAsync("/tmp/source.nc", "/tmp/output.zarr", options));

        Assert.Contains("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ConvertToZarrAsync_RejectsNullOrWhitespaceSourceUri(string? invalidUri)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.ConvertToZarrAsync(invalidUri!, "/tmp/output.zarr", options));
    }

    [Fact]
    public async Task ConvertToZarrAsync_AcceptsLegitimateHttpUri()
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature",
            Compression = "zstd"
        };

        // Act & Assert - Should not throw on validation (will fail later on Python execution, but that's expected)
        // We're just verifying that legitimate URIs pass the security validation
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.ConvertToZarrAsync(
                "http://example.com/data/climate.nc",
                "/tmp/output.zarr",
                options));

        // Should fail with Python execution error (not security validation error)
        Assert.DoesNotContain("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToZarrAsync_AcceptsLegitimateS3Uri()
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature",
            Compression = "gzip"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.ConvertToZarrAsync(
                "s3://bucket/path/to/data.nc",
                "s3://bucket/path/to/output.zarr",
                options));

        // Should fail with Python execution error (not security validation error)
        Assert.DoesNotContain("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConvertToZarrAsync_AcceptsLegitimateFileSystemPath()
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "precipitation",
            Compression = "lz4"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.ConvertToZarrAsync(
                "/mnt/data/climate/ERA5_2020.nc",
                "/mnt/output/zarr/ERA5_2020.zarr",
                options));

        // Should fail with Python execution error (not security validation error)
        Assert.DoesNotContain("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("http://example.com/data.nc?query=value&param=123")]
    [InlineData("https://storage.googleapis.com/bucket/path/file.nc")]
    [InlineData("/home/user/my data/file with spaces.nc")]
    [InlineData("C:\\Users\\Data\\climate.nc")]
    public async Task ConvertToZarrAsync_AcceptsComplexButLegitimateUris(string legitimateUri)
    {
        // Arrange
        var service = new ZarrTimeSeriesService(NullLogger<ZarrTimeSeriesService>.Instance, CreateMockZarrReader());
        var options = new ZarrConversionOptions
        {
            VariableName = "temperature",
            Compression = "zstd"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await service.ConvertToZarrAsync(legitimateUri, "/tmp/output.zarr", options));

        // Should NOT throw security validation error (may throw other errors)
        Assert.DoesNotContain("security", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
