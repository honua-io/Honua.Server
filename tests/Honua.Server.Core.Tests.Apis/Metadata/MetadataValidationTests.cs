using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

/// <summary>
/// Comprehensive metadata validation tests covering YAML/JSON parsing, schema validation,
/// field validation (bbox, CRS, temporal extents), layer configuration, and error reporting.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class MetadataValidationTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    #region Valid Metadata Tests

    [Fact]
    public void Parse_ValidJsonMetadata_ShouldSucceed()
    {
        // Arrange
        var json = GetValidMetadataJson();

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("test-catalog");
        snapshot.Folders.Should().HaveCount(1);
        snapshot.DataSources.Should().HaveCount(1);
        snapshot.Services.Should().HaveCount(1);
        snapshot.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_ValidYamlMetadata_ShouldSucceed()
    {
        // Arrange
        var yaml = GetValidMetadataYaml();

        // Act
        var snapshot = YamlMetadataProvider.Parse(yaml);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("test-catalog");
        snapshot.Folders.Should().HaveCount(1);
        snapshot.DataSources.Should().HaveCount(1);
        snapshot.Services.Should().HaveCount(1);
        snapshot.Layers.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_CompleteMetadataWithAllFields_ShouldSucceed()
    {
        // Arrange
        var json = GetCompleteMetadataJson();

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Title.Should().Be("Complete Test Catalog");
        snapshot.Catalog.Description.Should().Be("A comprehensive test metadata document");
        snapshot.Catalog.Contact.Should().NotBeNull();
        snapshot.Catalog.Contact!.Name.Should().Be("Test Admin");
        snapshot.Catalog.Contact.Email.Should().Be("admin@example.com");
        snapshot.Catalog.License.Should().NotBeNull();
        snapshot.Catalog.License!.Name.Should().Be("MIT");
        snapshot.Catalog.Extents.Should().NotBeNull();
        snapshot.Catalog.Extents!.Spatial.Should().NotBeNull();
        snapshot.Catalog.Extents.Temporal.Should().NotBeNull();
    }

    #endregion

    #region Invalid JSON/YAML Syntax Tests

    [Fact]
    public void Parse_InvalidJsonSyntax_ShouldThrowInvalidDataException()
    {
        // Arrange
        var invalidJson = "{ invalid json missing quotes }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(invalidJson));

        exception.Message.Should().Contain("invalid JSON");
    }

    [Fact]
    public void Parse_InvalidYamlSyntax_ShouldThrowInvalidDataException()
    {
        // Arrange
        var invalidYaml = @"
catalog:
  id: test
  - invalid yaml
    structure
";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            YamlMetadataProvider.Parse(invalidYaml));

        exception.Message.Should().Contain("invalid YAML");
    }

    [Fact]
    public void Parse_EmptyPayload_ShouldThrowInvalidDataException()
    {
        // Arrange
        var emptyJson = "";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(emptyJson));

        exception.Message.Should().Contain("empty");
    }

    [Fact]
    public void Parse_WhitespaceOnlyPayload_ShouldThrowInvalidDataException()
    {
        // Arrange
        var whitespaceJson = "   \n\t  ";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(whitespaceJson));

        exception.Message.Should().Contain("empty");
    }

    [Fact]
    public void Parse_NullDocument_ShouldThrowInvalidDataException()
    {
        // Arrange
        var nullJson = "null";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(nullJson));

        exception.Message.Should().Contain("empty or invalid");
    }

    #endregion

    #region Schema Validation Tests

    [Fact]
    public void Validate_MissingCatalogId_ShouldFail()
    {
        // Arrange
        var metadata = new
        {
            catalog = new { title = "Test" },
            folders = Array.Empty<object>(),
            dataSources = Array.Empty<object>(),
            services = Array.Empty<object>(),
            layers = Array.Empty<object>()
        };
        var json = JsonSerializer.Serialize(metadata, SerializerOptions);

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("catalog").And.Contain("id");
    }

    [Fact]
    public void Validate_MissingFolderId_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [ { ""title"": ""Folder without ID"" } ],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("Folders").And.Contain("id");
    }

    [Fact]
    public void Validate_DuplicateFolderId_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [
                { ""id"": ""folder1"", ""title"": ""Folder 1"" },
                { ""id"": ""folder1"", ""title"": ""Duplicate Folder"" }
            ],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("Duplicate").And.Contain("folder1");
    }

    [Fact]
    public void Validate_MissingDataSourceProvider_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [],
            ""dataSources"": [ { ""id"": ""db1"", ""connectionString"": ""test"" } ],
            ""services"": [],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("provider");
    }

    [Fact]
    public void Validate_MissingDataSourceConnectionString_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [],
            ""dataSources"": [ { ""id"": ""db1"", ""provider"": ""postgis"" } ],
            ""services"": [],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("connectionString");
    }

    [Fact]
    public void Validate_ServiceWithUnknownFolderId_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [ { ""id"": ""folder1"" } ],
            ""dataSources"": [ { ""id"": ""db1"", ""provider"": ""postgis"", ""connectionString"": ""test"" } ],
            ""services"": [
                {
                    ""id"": ""service1"",
                    ""folderId"": ""unknown-folder"",
                    ""dataSourceId"": ""db1""
                }
            ],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("unknown").And.Contain("folder");
    }

    [Fact]
    public void Validate_ServiceWithUnknownDataSourceId_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""folders"": [ { ""id"": ""folder1"" } ],
            ""dataSources"": [ { ""id"": ""db1"", ""provider"": ""postgis"", ""connectionString"": ""test"" } ],
            ""services"": [
                {
                    ""id"": ""service1"",
                    ""folderId"": ""folder1"",
                    ""dataSourceId"": ""unknown-db""
                }
            ],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("unknown").And.Contain("dataSource");
    }

    #endregion

    #region Layer Validation Tests

    [Fact]
    public void Validate_LayerMissingGeometryType_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            idField = "id",
            geometryField = "geom"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("geometryType");
    }

    [Fact]
    public void Validate_LayerMissingIdField_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            geometryField = "geom"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("idField");
    }

    [Fact]
    public void Validate_LayerMissingGeometryField_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("geometryField");
    }

    [Fact]
    public void Validate_LayerWithUnknownServiceId_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "unknown-service",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("unknown").And.Contain("service");
    }

    [Fact]
    public void Validate_LayerFieldWithoutName_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            fields = new[]
            {
                new { type = "string" }
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("field").And.Contain("name");
    }

    #endregion

    #region Bbox Validation Tests

    [Fact]
    public void Validate_InvalidBboxLessThan4Values_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            extent = new
            {
                bbox = new[] { new[] { -122.0, 45.0 } } // Only 2 values
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("bounding box").And.Contain("invalid");
    }

    [Fact]
    public void Validate_ValidBbox4Values_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            extent = new
            {
                bbox = new[] { new[] { -122.5, 45.0, -122.0, 45.5 } }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Layers[0].Extent.Should().NotBeNull();
        snapshot.Layers[0].Extent!.Bbox.Should().HaveCount(1);
        snapshot.Layers[0].Extent.Bbox[0].Should().HaveCount(4);
    }

    [Fact]
    public void Validate_ValidBbox6Values_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            extent = new
            {
                bbox = new[] { new[] { -122.5, 45.0, 0.0, -122.0, 45.5, 100.0 } }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Layers[0].Extent.Should().NotBeNull();
        snapshot.Layers[0].Extent!.Bbox[0].Should().HaveCount(6);
    }

    [Fact]
    public void Validate_MultipleBboxValues_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            extent = new
            {
                bbox = new[]
                {
                    new[] { -180.0, -90.0, 180.0, 90.0 },
                    new[] { -122.5, 45.0, -122.0, 45.5 }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Extent!.Bbox.Should().HaveCount(2);
    }

    #endregion

    #region CRS Validation Tests

    [Fact]
    public void Validate_ValidEpsgCrs_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            crs = new[] { "EPSG:4326", "EPSG:3857" },
            extent = new
            {
                crs = "EPSG:4326"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Crs.Should().Contain("EPSG:4326");
        snapshot.Layers[0].Crs.Should().Contain("EPSG:3857");
    }

    [Fact]
    public void Validate_ValidOgcCrs_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            crs = new[] { "http://www.opengis.net/def/crs/OGC/1.3/CRS84" }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Crs.Should().Contain("http://www.opengis.net/def/crs/OGC/1.3/CRS84");
    }

    #endregion

    #region Temporal Extent Validation Tests

    [Fact]
    public void Validate_ValidTemporalExtent_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
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
        snapshot.Layers[0].Extent.Should().NotBeNull();
        snapshot.Layers[0].Extent!.Temporal.Should().HaveCount(1);
        snapshot.Layers[0].Extent.Temporal[0].Start.Should().NotBeNull();
        snapshot.Layers[0].Extent.Temporal[0].End.Should().NotBeNull();
    }

    [Fact]
    public void Validate_OpenEndedTemporalExtent_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
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
                        new[] { "2024-01-01T00:00:00Z", ".." }
                    }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Extent!.Temporal[0].Start.Should().NotBeNull();
        snapshot.Layers[0].Extent.Temporal[0].End.Should().BeNull();
    }

    [Fact]
    public void Validate_InvalidTemporalFormat_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
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
                        new[] { "invalid-date", "2024-12-31T23:59:59Z" }
                    }
                }
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("temporal").And.Contain("invalid-date");
    }

    [Fact]
    public void Validate_TemporalExtentWithCustomTrs_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
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
                    },
                    trs = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Layers[0].Extent!.TemporalReferenceSystem.Should().Be("http://www.opengis.net/def/uom/ISO-8601/0/Gregorian");
    }

    #endregion

    #region Style Validation Tests

    [Fact]
    public void Validate_SimpleStyleRenderer_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithStyle(new
        {
            id = "style1",
            renderer = "simple",
            simple = new
            {
                fillColor = "#FF0000",
                strokeColor = "#000000",
                strokeWidth = 2.0
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Styles.Should().HaveCount(1);
        snapshot.Styles[0].Renderer.Should().Be("simple");
        snapshot.Styles[0].Simple.Should().NotBeNull();
    }

    [Fact]
    public void Validate_SimpleStyleWithoutSymbol_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithStyle(new
        {
            id = "style1",
            renderer = "simple"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("simple").And.Contain("symbol");
    }

    [Fact]
    public void Validate_UniqueValueStyleRenderer_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithStyle(new
        {
            id = "style1",
            renderer = "uniqueValue",
            uniqueValue = new
            {
                field = "category",
                classes = new[]
                {
                    new
                    {
                        value = "type1",
                        symbol = new { fillColor = "#FF0000" }
                    },
                    new
                    {
                        value = "type2",
                        symbol = new { fillColor = "#00FF00" }
                    }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Styles[0].Renderer.Should().Be("uniqueValue");
        snapshot.Styles[0].UniqueValue.Should().NotBeNull();
        snapshot.Styles[0].UniqueValue!.Classes.Should().HaveCount(2);
    }

    [Fact]
    public void Validate_UniqueValueStyleWithoutField_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithStyle(new
        {
            id = "style1",
            renderer = "uniqueValue",
            uniqueValue = new
            {
                classes = new[]
                {
                    new
                    {
                        value = "type1",
                        symbol = new { fillColor = "#FF0000" }
                    }
                }
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("field");
    }

    [Fact]
    public void Validate_UniqueValueStyleWithoutClasses_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithStyle(new
        {
            id = "style1",
            renderer = "uniqueValue",
            uniqueValue = new
            {
                field = "category"
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("class");
    }

    [Fact]
    public void Validate_LayerWithUnknownStyleId_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            styles = new
            {
                defaultStyleId = "unknown-style"
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("unknown").And.Contain("style");
    }

    #endregion

    #region Raster Dataset Validation Tests

    [Fact]
    public void Validate_RasterDatasetWithValidSource_ShouldSucceed()
    {
        // Arrange
        var json = GetMetadataWithRasterDataset(new
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
        snapshot.RasterDatasets.Should().HaveCount(1);
        snapshot.RasterDatasets[0].Source.Type.Should().Be("cog");
        snapshot.RasterDatasets[0].Source.Uri.Should().Be("s3://bucket/raster.tif");
    }

    [Fact]
    public void Validate_RasterDatasetWithoutSource_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithRasterDataset(new
        {
            id = "raster1"
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("source");
    }

    [Fact]
    public void Validate_RasterDatasetWithInvalidSourceType_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithRasterDataset(new
        {
            id = "raster1",
            source = new
            {
                type = "invalid-type",
                uri = "s3://bucket/raster.tif"
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("not supported");
    }

    [Fact]
    public void Validate_RasterDatasetWithoutUri_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithRasterDataset(new
        {
            id = "raster1",
            source = new
            {
                type = "cog"
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("uri");
    }

    #endregion

    #region Attachment Validation Tests

    [Fact]
    public void Validate_AttachmentsEnabledWithoutStorageProfile_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            attachments = new
            {
                enabled = true
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("attachments").And.Contain("storageProfileId");
    }

    [Fact]
    public void Validate_AttachmentsWithNegativeMaxSize_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            attachments = new
            {
                enabled = true,
                storageProfileId = "s3-profile",
                maxSizeMiB = -10
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("maxSizeMiB").And.Contain("greater than zero");
    }

    [Fact]
    public void Validate_AttachmentsOgcLinksWithoutEnabled_ShouldFail()
    {
        // Arrange
        var json = GetMetadataWithLayer(new
        {
            id = "layer1",
            serviceId = "service1",
            geometryType = "Point",
            idField = "id",
            geometryField = "geom",
            attachments = new
            {
                enabled = false,
                exposeOgcLinks = true
            }
        });

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("OGC").And.Contain("disabled");
    }

    #endregion

    #region CORS Validation Tests

    [Fact]
    public void Validate_CorsAllowCredentialsWithAllowAnyOrigin_ShouldFail()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""server"": {
                ""cors"": {
                    ""enabled"": true,
                    ""allowedOrigins"": [ ""*"" ],
                    ""allowCredentials"": true
                }
            },
            ""folders"": [],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": []
        }";

        // Act & Assert
        var exception = Assert.Throws<InvalidDataException>(() =>
            JsonMetadataProvider.Parse(json));

        exception.Message.Should().Contain("CORS").And.Contain("credentials").And.Contain("origins");
    }

    [Fact]
    public void Validate_ValidCorsConfiguration_ShouldSucceed()
    {
        // Arrange
        var json = @"{
            ""catalog"": { ""id"": ""test"" },
            ""server"": {
                ""cors"": {
                    ""enabled"": true,
                    ""allowedOrigins"": [ ""https://example.com"" ],
                    ""allowedMethods"": [ ""GET"", ""POST"" ],
                    ""allowCredentials"": true
                }
            },
            ""folders"": [],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": []
        }";

        // Act
        var snapshot = JsonMetadataProvider.Parse(json);

        // Assert
        snapshot.Server.Cors.Enabled.Should().BeTrue();
        snapshot.Server.Cors.AllowedOrigins.Should().Contain("https://example.com");
        snapshot.Server.Cors.AllowCredentials.Should().BeTrue();
    }

    #endregion

    #region JSON Comments and Formatting Tests

    [Fact]
    public void Parse_JsonWithComments_ShouldSucceed()
    {
        // Arrange
        var jsonWithComments = @"{
            // This is a comment
            ""catalog"": {
                ""id"": ""test"" /* inline comment */
            },
            ""folders"": [],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": []
        }";

        // Act
        var snapshot = JsonMetadataProvider.Parse(jsonWithComments);

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Catalog.Id.Should().Be("test");
    }

    [Fact]
    public void Parse_JsonWithTrailingCommas_ShouldSucceed()
    {
        // Arrange
        var jsonWithTrailingCommas = @"{
            ""catalog"": { ""id"": ""test"", },
            ""folders"": [],
            ""dataSources"": [],
            ""services"": [],
            ""layers"": [],
        }";

        // Act
        var snapshot = JsonMetadataProvider.Parse(jsonWithTrailingCommas);

        // Assert
        snapshot.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static string GetValidMetadataJson()
    {
        var metadata = new
        {
            catalog = new { id = "test-catalog", title = "Test Catalog" },
            folders = new[] { new { id = "folder1", title = "Folder 1" } },
            dataSources = new[] { new { id = "db1", provider = "postgis", connectionString = "Host=localhost" } },
            services = new[]
            {
                new { id = "service1", folderId = "folder1", dataSourceId = "db1" }
            },
            layers = new[]
            {
                new
                {
                    id = "layer1",
                    serviceId = "service1",
                    geometryType = "Point",
                    idField = "id",
                    geometryField = "geom"
                }
            }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string GetValidMetadataYaml()
    {
        return @"
catalog:
  id: test-catalog
  title: Test Catalog

folders:
  - id: folder1
    title: Folder 1

dataSources:
  - id: db1
    provider: postgis
    connectionString: Host=localhost

services:
  - id: service1
    folderId: folder1
    dataSourceId: db1

layers:
  - id: layer1
    serviceId: service1
    geometryType: Point
    idField: id
    geometryField: geom
";
    }

    private static string GetCompleteMetadataJson()
    {
        var metadata = new
        {
            catalog = new
            {
                id = "complete-catalog",
                title = "Complete Test Catalog",
                description = "A comprehensive test metadata document",
                version = "1.0.0",
                publisher = "Test Organization",
                keywords = new[] { "test", "geospatial", "ogc" },
                contact = new
                {
                    name = "Test Admin",
                    email = "admin@example.com",
                    organization = "Test Org",
                    role = "administrator"
                },
                license = new
                {
                    name = "MIT",
                    url = "https://opensource.org/licenses/MIT"
                },
                extents = new
                {
                    spatial = new
                    {
                        bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                        crs = "EPSG:4326"
                    },
                    temporal = new
                    {
                        interval = new[] { new[] { "2024-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } }
                    }
                }
            },
            folders = new[] { new { id = "folder1" } },
            dataSources = new[] { new { id = "db1", provider = "postgis", connectionString = "test" } },
            services = new[] { new { id = "service1", folderId = "folder1", dataSourceId = "db1" } },
            layers = new[]
            {
                new
                {
                    id = "layer1",
                    serviceId = "service1",
                    geometryType = "Point",
                    idField = "id",
                    geometryField = "geom"
                }
            }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string GetMetadataWithLayer(object layerDefinition)
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

    private static string GetMetadataWithStyle(object styleDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = new[] { new { id = "folder1" } },
            dataSources = new[] { new { id = "db1", provider = "postgis", connectionString = "test" } },
            services = new[] { new { id = "service1", folderId = "folder1", dataSourceId = "db1" } },
            layers = new[]
            {
                new
                {
                    id = "layer1",
                    serviceId = "service1",
                    geometryType = "Point",
                    idField = "id",
                    geometryField = "geom"
                }
            },
            styles = new[] { styleDefinition }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    private static string GetMetadataWithRasterDataset(object rasterDefinition)
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
