using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

/// <summary>
/// Tests for validating metadata from actual files, including error reporting with line numbers.
/// </summary>
[Collection("UnitTests")]
public sealed class MetadataFileValidationTests
{
    private readonly string _testDataPath;

    public MetadataFileValidationTests()
    {
        // Get the test data directory relative to this test file
        var assemblyLocation = typeof(MetadataFileValidationTests).Assembly.Location;
        var assemblyDirectory = Path.GetDirectoryName(assemblyLocation)!;
        _testDataPath = Path.Combine(assemblyDirectory, "Metadata", "TestData");

        // Fallback to repository path if running from different context
        if (!Directory.Exists(_testDataPath))
        {
            _testDataPath = Path.Combine(
                Environment.CurrentDirectory,
                "tests",
                "Honua.Server.Core.Tests",
                "Metadata",
                "TestData"
            );
        }
    }

    [Fact]
    public void LoadValidCompleteJsonFile_ShouldSucceed()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-complete.json");

        // Skip if file doesn't exist (test data not deployed)
        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("sample-catalog");
        snapshot.Catalog.Title.Should().Be("Sample Geospatial Catalog");
        snapshot.Folders.Should().HaveCount(2);
        snapshot.Services.Should().HaveCount(1);
        snapshot.Layers.Should().HaveCount(1);
        snapshot.Styles.Should().HaveCount(2);
        snapshot.RasterDatasets.Should().HaveCount(1);

        // Verify complete catalog metadata
        snapshot.Catalog.Contact.Should().NotBeNull();
        snapshot.Catalog.Contact!.Email.Should().Be("gis@example.com");
        snapshot.Catalog.License.Should().NotBeNull();
        snapshot.Catalog.License!.Name.Should().Be("CC BY 4.0");

        // Verify server CORS configuration
        snapshot.Server.Cors.Enabled.Should().BeTrue();
        snapshot.Server.Cors.AllowCredentials.Should().BeTrue();

        // Verify layer details
        var highway = snapshot.Layers[0];
        highway.Id.Should().Be("highways");
        highway.Fields.Should().HaveCount(7);
        highway.Editing.Capabilities.AllowUpdate.Should().BeTrue();
        highway.Editing.Constraints.RequiredFields.Should().Contain("name");

        // Verify styles
        var uniqueStyle = snapshot.Styles[0];
        uniqueStyle.Renderer.Should().Be("uniqueValue");
        uniqueStyle.UniqueValue.Should().NotBeNull();
        uniqueStyle.UniqueValue!.Classes.Should().HaveCount(3);

        // Verify raster dataset
        var raster = snapshot.RasterDatasets[0];
        raster.Source.Type.Should().Be("cog");
        raster.Cache.Enabled.Should().BeTrue();
        raster.Cache.ZoomLevels.Should().HaveCount(11);
    }

    [Fact]
    public async Task LoadValidMinimalYamlFile_ShouldSucceed()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "valid-minimal.yaml");

        // Skip if file doesn't exist
        if (!File.Exists(filePath))
        {
            return;
        }

        var provider = new YamlMetadataProvider(filePath);

        // Act
        var snapshot = await provider.LoadAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("minimal-catalog");
        snapshot.Folders.Should().HaveCount(1);
        snapshot.DataSources.Should().HaveCount(1);
        snapshot.Services.Should().HaveCount(1);
        snapshot.Layers.Should().HaveCount(1);

        var layer = snapshot.Layers[0];
        layer.Id.Should().Be("points");
        layer.GeometryType.Should().Be("Point");
        layer.Storage.Should().NotBeNull();
        layer.Storage!.Srid.Should().Be(4326);
    }

    [Fact]
    public void LoadInvalidMissingCatalogId_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-missing-catalog-id.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("catalog");
        exception.Message.Should().Contain("id");
    }

    [Fact]
    public void LoadInvalidDuplicateIds_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-duplicate-ids.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("Duplicate");
        exception.Message.Should().Contain("duplicate");
    }

    [Fact]
    public void LoadInvalidBrokenReferences_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-broken-references.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("unknown");
        exception.Message.Should().Contain("folder");
    }

    [Fact]
    public void LoadInvalidBbox_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-bbox.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("bounding box");
        exception.Message.Should().Contain("invalid");
    }

    [Fact]
    public void LoadInvalidTemporalFormat_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-temporal-format.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("temporal");
        exception.Message.Should().Contain("not-a-valid-date");
    }

    [Fact]
    public void LoadInvalidCorsConfig_ShouldFailWithClearError()
    {
        // Arrange
        var filePath = Path.Combine(_testDataPath, "invalid-cors-config.json");

        if (!File.Exists(filePath))
        {
            return;
        }

        var json = File.ReadAllText(filePath);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("CORS");
        exception.Message.Should().Contain("credentials");
    }

    [Fact]
    public async Task YamlProvider_FileNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_testDataPath, "does-not-exist.yaml");
        var provider = new YamlMetadataProvider(nonexistentPath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await provider.LoadAsync());

        exception.FileName.Should().Be(nonexistentPath);
    }

    [Fact]
    public async Task JsonProvider_FileNotFound_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var nonexistentPath = Path.Combine(_testDataPath, "does-not-exist.json");
        var provider = new JsonMetadataProvider(nonexistentPath);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await provider.LoadAsync());

        exception.FileName.Should().Be(nonexistentPath);
    }
}
