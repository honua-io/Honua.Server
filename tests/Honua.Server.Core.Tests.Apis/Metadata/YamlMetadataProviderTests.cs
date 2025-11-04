using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class YamlMetadataProviderTests : IDisposable
{
    private readonly string _tempDirectory;

    public YamlMetadataProviderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"honua-yaml-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldLoadValidYamlMetadata()
    {
        // Arrange
        var yamlPath = Path.Combine(_tempDirectory, "metadata.yaml");
        await File.WriteAllTextAsync(yamlPath, GetSampleYaml());

        var provider = new YamlMetadataProvider(yamlPath);

        // Act
        var snapshot = await provider.LoadAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("test-catalog");
        snapshot.Catalog.Title.Should().Be("Test Catalog");
        snapshot.Folders.Should().HaveCount(1);
        snapshot.Folders[0].Id.Should().Be("test-folder");
        snapshot.DataSources.Should().HaveCount(1);
        snapshot.DataSources[0].Id.Should().Be("test-db");
        snapshot.Services.Should().HaveCount(1);
        snapshot.Services[0].Id.Should().Be("test-service");
        snapshot.Layers.Should().HaveCount(1);
        snapshot.Layers[0].Id.Should().Be("test-layer");
    }

    [Fact]
    public async Task LoadAsync_ShouldThrowWhenFileNotFound()
    {
        // Arrange
        var yamlPath = Path.Combine(_tempDirectory, "nonexistent.yaml");
        var provider = new YamlMetadataProvider(yamlPath);

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
        {
            await provider.LoadAsync();
        });
    }

    [Fact]
    public void SupportsChangeNotifications_ShouldReturnFalse()
    {
        // Arrange
        var yamlPath = Path.Combine(_tempDirectory, "metadata.yaml");
        var provider = new YamlMetadataProvider(yamlPath);

        // Act & Assert
        provider.SupportsChangeNotifications.Should().BeFalse();
    }

    private static string GetSampleYaml()
    {
        return """
catalog:
  id: test-catalog
  title: Test Catalog
  description: Test metadata for YAML provider
  version: 1.0.0

folders:
  - id: test-folder
    title: Test Folder
    order: 1

dataSources:
  - id: test-db
    provider: sqlite
    connectionString: "Data Source=:memory:"

services:
  - id: test-service
    title: Test Service
    folderId: test-folder
    serviceType: feature
    dataSourceId: test-db

layers:
  - id: test-layer
    serviceId: test-service
    title: Test Layer
    geometryType: Point
    idField: id
    geometryField: geom
    crs:
      - EPSG:4326
    storage:
      table: test_table
      geometryColumn: geom
      primaryKey: id
""";
    }
}
