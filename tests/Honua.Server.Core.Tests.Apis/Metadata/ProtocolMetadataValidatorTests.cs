using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ProtocolMetadataValidatorTests
{
    [Fact]
    public void ValidateForOgcApiFeatures_WithValidMetadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            Description = "A test layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id" },
                new() { Name = "geom" }
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            },
            Crs = new List<string> { "http://www.opengis.net/def/crs/OGC/1.3/CRS84" }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForOgcApiFeatures(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForOgcApiFeatures_WithMissingExtent_HasError()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition> { new() { Name = "id" }, new() { Name = "geom" } }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForOgcApiFeatures(layer);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Contains("bbox"));
    }

    [Fact]
    public void ValidateForEsriRest_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "esriGeometryPoint",
            IdField = "OBJECTID",
            DisplayField = "Name",
            GeometryField = "SHAPE",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "OBJECTID", DataType = "esriFieldTypeOID" },
                new() { Name = "SHAPE", DataType = "esriFieldTypeGeometry" },
                new() { Name = "Name", DataType = "esriFieldTypeString" }
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            },
            Storage = new LayerStorageDefinition
            {
                Srid = 4326
            }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForEsriRest(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForWms_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Polygon",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>(),
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            },
            Crs = new List<string> { "EPSG:4326", "EPSG:3857" }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForWms(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForWfs_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "LineString",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "string" },
                new() { Name = "geom", DataType = "geometry" },
                new() { Name = "name", DataType = "string" }
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            },
            Crs = new List<string> { "urn:ogc:def:crs:EPSG::4326" }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForWfs(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForCsw_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            Description = "A comprehensive test layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>(),
            Keywords = new List<string> { "test", "geospatial", "catalog" },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForCsw(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForCsw_WithIso19115Metadata_IsValid()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            Description = "Test description",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>(),
            Iso19115 = new Iso19115Metadata
            {
                MetadataIdentifier = "test-metadata-001",
                MetadataStandard = new Iso19115MetadataStandard
                {
                    Name = "ISO 19115",
                    Version = "2003"
                },
                MetadataContact = new Iso19115Contact
                {
                    OrganisationName = "Test Organization",
                    Role = "pointOfContact"
                }
            }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForCsw(layer);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForWcs_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var raster = new RasterDatasetDefinition
        {
            Id = "test-raster",
            Title = "Test Raster",
            Description = "A test raster dataset",
            Source = new RasterSourceDefinition
            {
                Type = "geotiff",
                Uri = "s3://bucket/raster.tif"
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            },
            Crs = new List<string> { "EPSG:4326" }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForWcs(raster);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateForStac_WithCompleteMetadata_IsValid()
    {
        // Arrange
        var raster = new RasterDatasetDefinition
        {
            Id = "test-raster",
            Title = "Test Raster",
            Description = "A test raster dataset",
            Source = new RasterSourceDefinition
            {
                Type = "geotiff",
                Uri = "s3://bucket/raster.tif"
            },
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } },
                Temporal = new List<TemporalIntervalDefinition>
                {
                    new() { Start = DateTimeOffset.Parse("2024-01-01T00:00:00Z"), End = DateTimeOffset.Parse("2024-12-31T23:59:59Z") }
                }
            },
            Stac = new StacMetadata
            {
                Enabled = true,
                License = "CC-BY-4.0",
                Providers = new List<StacProvider>
                {
                    new() { Name = "Test Provider", Roles = new List<string> { "producer" } }
                }
            }
        };

        // Act
        var result = ProtocolMetadataValidator.ValidateForStac(raster);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateLayer_WithAllProtocols_ReturnsResults()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>(),
            Extent = new LayerExtentDefinition
            {
                Bbox = new List<double[]> { new[] { -180.0, -90.0, 180.0, 90.0 } }
            }
        };

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "FeatureServer",
            DataSourceId = "test-ds",
            Enabled = true
        };

        // Act
        var results = ProtocolMetadataValidator.ValidateLayer(layer, service, includeWarnings: true);

        // Assert
        Assert.NotEmpty(results);
        // Should have validation results for multiple protocols
        Assert.All(results, r => Assert.NotNull(r.Protocol));
    }

    [Fact]
    public void ValidateRasterDataset_WithAllProtocols_ReturnsAllResults()
    {
        // Arrange
        var raster = new RasterDatasetDefinition
        {
            Id = "test-raster",
            Title = "Test Raster",
            Source = new RasterSourceDefinition
            {
                Type = "geotiff",
                Uri = "s3://bucket/raster.tif"
            }
        };

        // Act
        var results = ProtocolMetadataValidator.ValidateRasterDataset(raster, includeWarnings: true);

        // Assert
        Assert.NotEmpty(results);
        // Should have results for: WCS, STAC (if enabled)
        Assert.True(results.Count >= 1);
    }

    [Fact]
    public void ValidateLayer_WithIncludeWarningsFalse_ExcludesWarnings()
    {
        // Arrange
        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>()
            // Missing description, etc. (warnings)
        };

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "FeatureServer",
            DataSourceId = "test-ds",
            Enabled = true
        };

        // Act
        var results = ProtocolMetadataValidator.ValidateLayer(layer, service, includeWarnings: false);

        // Assert
        // All results should be either valid or have only errors (no warnings)
        foreach (var result in results)
        {
            Assert.Empty(result.Warnings);
        }
    }
}
