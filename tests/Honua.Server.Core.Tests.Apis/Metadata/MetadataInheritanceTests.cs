using System;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

/// <summary>
/// Tests for metadata inheritance, defaults, and catalog entry overrides.
/// </summary>
[Collection("UnitTests")]
public sealed class MetadataInheritanceTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    [Fact]
    public void LayerDefaults_WhenNotSpecified_ShouldApplyDefaults()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom"
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var layer = snapshot.Layers[0];
        layer.Title.Should().Be("layer1"); // Default to ID when not specified
        layer.ItemType.Should().Be("feature"); // Default item type
        layer.Fields.Should().BeEmpty(); // Default empty collection
        layer.Editing.Capabilities.AllowAdd.Should().BeFalse(); // Default disabled
        layer.Editing.Capabilities.RequireAuthentication.Should().BeTrue(); // Default required
    }

    [Fact]
    public void ServiceDefaults_WhenNotSpecified_ShouldApplyDefaults()
    {
        // Arrange
        var json = CreateMetadataWithService(new
        {
            id = "service1",
            folderId = "folder1",
            dataSourceId = "db1"
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var service = snapshot.Services[0];
        service.Title.Should().Be("service1"); // Default to ID
        service.ServiceType.Should().Be("feature"); // Default service type
        service.Enabled.Should().BeTrue(); // Default enabled
        service.Ogc.CollectionsEnabled.Should().BeFalse(); // Default disabled
    }

    [Fact]
    public void CatalogEntry_InheritedFields_ShouldOverrideDefaults()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            title = "Layer One",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            keywords = new[] { "layer-keyword" },
            catalog = new
            {
                summary = "Custom catalog summary",
                keywords = new[] { "catalog-keyword" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var layer = snapshot.Layers[0];
        layer.Title.Should().Be("Layer One");
        layer.Keywords.Should().Contain("layer-keyword");
        layer.Catalog.Summary.Should().Be("Custom catalog summary");
        layer.Catalog.Keywords.Should().Contain("catalog-keyword");
    }

    [Fact]
    public void EditingConstraints_DefaultValues_ShouldBeApplied()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            editing = new
            {
                constraints = new
                {
                    defaultValues = new
                    {
                        status = "active",
                        category = "default"
                    }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var layer = snapshot.Layers[0];
        layer.Editing.Constraints.DefaultValues.Should().ContainKey("status");
        layer.Editing.Constraints.DefaultValues["status"].Should().Be("active");
        layer.Editing.Constraints.DefaultValues.Should().ContainKey("category");
        layer.Editing.Constraints.DefaultValues["category"].Should().Be("default");
    }

    [Fact]
    public void FieldDefaults_Nullable_ShouldDefaultToTrue()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            fields = new[]
            {
                new { name = "field1", type = "string" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var field = snapshot.Layers[0].Fields.First(f => f.Name == "field1");
        field.Nullable.Should().BeTrue();
        field.Editable.Should().BeTrue();
    }

    [Fact]
    public void FieldDefaults_Editable_ShouldDefaultToTrue()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            fields = new[]
            {
                new { name = "field1", type = "string", nullable = false }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var field = snapshot.Layers[0].Fields.First();
        field.Nullable.Should().BeFalse();
        field.Editable.Should().BeTrue();
    }

    [Fact]
    public void RasterCache_Defaults_ShouldApply()
    {
        // Arrange
        var json = CreateMetadataWithRasterDataset(new
        {
            id = "raster1",
            source = new
            {
                type = "cog",
                uri = "s3://bucket/raster.tif"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var raster = snapshot.RasterDatasets[0];
        raster.Cache.Enabled.Should().BeTrue(); // Default enabled
        raster.Cache.Preseed.Should().BeFalse(); // Default not pre-seeded
        raster.Cache.ZoomLevels.Should().BeEmpty(); // Default no zoom levels
    }

    [Fact]
    public void StyleRenderer_DefaultsToSimple()
    {
        // Arrange
        var json = CreateMetadataWithStyle(new
        {
            id = "style1",
            simple = new
            {
                fillColor = "#FF0000"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Styles[0].Renderer.Should().Be("simple");
    }

    [Fact]
    public void StyleFormat_DefaultsToLegacy()
    {
        // Arrange
        var json = CreateMetadataWithStyle(new
        {
            id = "style1",
            simple = new
            {
                fillColor = "#FF0000"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Styles[0].Format.Should().Be("legacy");
    }

    [Fact]
    public void StyleGeometryType_DefaultsToPolygon()
    {
        // Arrange
        var json = CreateMetadataWithStyle(new
        {
            id = "style1",
            simple = new
            {
                fillColor = "#FF0000"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Styles[0].GeometryType.Should().Be("polygon");
    }

    [Fact]
    public void TemporalReferenceSystem_DefaultsToGregorian()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            extent = new
            {
                temporal = new
                {
                    interval = new[]
                    {
                        new[] { "2024-01-01T00:00:00Z", "2024-12-31T23:59:59Z" }
                    }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Extent!.TemporalReferenceSystem
            .Should().Be("http://www.opengis.net/def/uom/ISO-8601/0/Gregorian");
    }

    [Fact]
    public void EmptyCollections_ShouldBeEmptyNotNull()
    {
        // Arrange
        var json = CreateMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom"
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        var layer = snapshot.Layers[0];
        layer.Fields.Should().NotBeNull().And.BeEmpty();
        layer.Keywords.Should().NotBeNull().And.BeEmpty();
        layer.Crs.Should().NotBeNull().And.BeEmpty();
        layer.Links.Should().NotBeNull().And.BeEmpty();
        layer.Relationships.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ServiceLayers_ShouldBeAttachedCorrectly()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [ { ""id"": ""folder1"" } ],
            ""dataSources"": [ { ""id"": ""db1"", ""provider"": ""postgis"", ""connectionString"": ""test"" } ],
            ""services"": [
                { ""id"": ""service1"", ""folderId"": ""folder1"", ""dataSourceId"": ""db1"" },
                { ""id"": ""service2"", ""folderId"": ""folder1"", ""dataSourceId"": ""db1"" }
            ],
            ""layers"": [
                { ""id"": ""layer1"", ""serviceId"": ""service1"", ""geometryType"": ""Point"", ""idField"": ""id"", ""geometryField"": ""geom"" },
                { ""id"": ""layer2"", ""serviceId"": ""service1"", ""geometryType"": ""Point"", ""idField"": ""id"", ""geometryField"": ""geom"" },
                { ""id"": ""layer3"", ""serviceId"": ""service2"", ""geometryType"": ""Point"", ""idField"": ""id"", ""geometryField"": ""geom"" }
            ]
        }";

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Services.Should().HaveCount(2);

        var service1 = snapshot.Services.First(s => s.Id == "service1");
        service1.Layers.Should().HaveCount(2);
        service1.Layers.Should().Contain(l => l.Id == "layer1");
        service1.Layers.Should().Contain(l => l.Id == "layer2");

        var service2 = snapshot.Services.First(s => s.Id == "service2");
        service2.Layers.Should().HaveCount(1);
        service2.Layers.Should().Contain(l => l.Id == "layer3");
    }

    #region Helper Methods

    private static string CreateMetadataWithLayer(object layerDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = new[] { new { id = "folder1" } },
            dataSources = new[] { new { id = "db1", provider = "postgis", connectionString = "test" } },
            services = new[] { new { id = "service1", folderId = "folder1", dataSourceId = "db1" } },
            layers = new[] { layerDefinition }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string CreateMetadataWithService(object serviceDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = new[] { new { id = "folder1" } },
            dataSources = new[] { new { id = "db1", provider = "postgis", connectionString = "test" } },
            services = new[] { serviceDefinition },
            layers = Array.Empty<object>()
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string CreateMetadataWithStyle(object styleDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = Array.Empty<object>(),
            dataSources = Array.Empty<object>(),
            services = Array.Empty<object>(),
            layers = Array.Empty<object>(),
            styles = new[] { styleDefinition }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string CreateMetadataWithRasterDataset(object rasterDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = Array.Empty<object>(),
            dataSources = Array.Empty<object>(),
            services = Array.Empty<object>(),
            layers = Array.Empty<object>(),
            rasterDatasets = new[] { rasterDefinition }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    #endregion
}
