using System.Text.Json;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

/// <summary>
/// Tests for metadata crosswalk validation ensuring compatibility with STAC, ISO 19115, and OGC standards.
/// Validates that metadata can be properly transformed to different metadata standards.
/// </summary>
[Collection("UnitTests")]
public sealed class MetadataCrosswalkTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    #region STAC Crosswalk Tests

    [Fact]
    public void RasterDataset_WithStacRequiredFields_ShouldValidateForStac()
    {
        // Arrange
        var rasterDataset = CreateRasterDataset(new
        {
            id = "landsat-scene",
            title = "Landsat 8 Scene",
            description = "Landsat 8 OLI/TIRS imagery",
            keywords = new[] { "landsat", "satellite", "imagery" },
            source = new
            {
                type = "cog",
                uri = "s3://landsat-pds/c1/L8/139/045/LC08_L1TP_139045_20170304_20170316_01_T1/LC08_L1TP_139045_20170304_20170316_01_T1_B4.TIF"
            },
            extent = new
            {
                bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } },
                crs = "EPSG:4326",
                temporal = new
                {
                    interval = new[]
                    {
                        new[] { "2017-03-04T00:00:00Z", "2017-03-04T23:59:59Z" }
                    }
                }
            },
            catalog = new
            {
                thumbnail = "https://example.com/thumbnails/landsat.png",
                summary = "High-resolution multispectral imagery"
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(rasterDataset);

        // Assert - Verify STAC-compatible fields are present
        var raster = snapshot.RasterDatasets[0];
        raster.Id.Should().NotBeNullOrEmpty(); // Required for STAC item.id
        raster.Title.Should().NotBeNullOrEmpty(); // Required for STAC item.properties.title
        raster.Description.Should().NotBeNullOrEmpty(); // STAC item.properties.description
        raster.Keywords.Should().NotBeEmpty(); // STAC item.keywords
        raster.Extent.Should().NotBeNull();
        raster.Extent!.Bbox.Should().NotBeEmpty(); // Required for STAC item.bbox
        raster.Extent.Temporal.Should().NotBeNull();
        raster.Source.Uri.Should().NotBeNullOrEmpty(); // Required for STAC asset href
        raster.Catalog.Thumbnail.Should().NotBeNullOrEmpty(); // STAC preview asset

        // Validate against STAC requirements using ProtocolMetadataValidator
        var results = ProtocolMetadataValidator.ValidateForStac(raster);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    [Fact]
    public void RasterDataset_MissingStacRequiredFields_ShouldReportErrors()
    {
        // Arrange
        var rasterDataset = CreateRasterDataset(new
        {
            id = "incomplete-raster",
            source = new
            {
                type = "cog",
                uri = "s3://bucket/raster.tif"
            }
        });

        var snapshot = JsonMetadataProvider.Parse(rasterDataset);
        var raster = snapshot.RasterDatasets[0];

        // Act
        var results = ProtocolMetadataValidator.ValidateForStac(raster);

        // Assert
        results.IsValid.Should().BeFalse();
        results.Errors.Should().Contain(e => e.Contains("bbox", System.StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region OGC API Features Crosswalk Tests

    [Fact]
    public void Layer_WithOgcApiRequiredFields_ShouldValidateForOgc()
    {
        // Arrange
        var layer = CreateLayer(new
        {
            id = "buildings",
            serviceId = "service1",
            title = "Buildings",
            description = "Building footprints",
            geometryType = "Polygon",
            idField = "building_id",
            geometryField = "geom",
            keywords = new[] { "buildings", "structures" },
            crs = new[] { "http://www.opengis.net/def/crs/OGC/1.3/CRS84", "EPSG:3857" },
            extent = new
            {
                bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } },
                crs = "EPSG:4326"
            },
            fields = new[]
            {
                new { name = "building_id", type = "int" },
                new { name = "name", type = "string" },
                new { name = "height", type = "double" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(layer);
        var service = snapshot.Services[0];
        var layerDef = snapshot.Layers[0];

        // Assert - Verify OGC API Features compatible fields
        layerDef.Id.Should().NotBeNullOrEmpty(); // Required for collection id
        layerDef.Title.Should().NotBeNullOrEmpty(); // Required for collection title
        layerDef.Description.Should().NotBeNullOrEmpty(); // collection description
        layerDef.GeometryType.Should().NotBeNullOrEmpty(); // Required for schema
        layerDef.GeometryField.Should().NotBeNullOrEmpty(); // Required for geometry property
        layerDef.Extent.Should().NotBeNull();
        layerDef.Extent!.Bbox.Should().NotBeEmpty(); // Required for collection extent
        layerDef.Crs.Should().NotBeEmpty(); // CRS support
        layerDef.Fields.Should().NotBeEmpty(); // Schema properties

        var results = ProtocolMetadataValidator.ValidateForOgcApiFeatures(layerDef);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    #endregion

    #region WFS Crosswalk Tests

    [Fact]
    public void Layer_WithWfsRequiredFields_ShouldValidateForWfs()
    {
        // Arrange
        var layer = CreateLayer(new
        {
            id = "parcels",
            serviceId = "service1",
            title = "Property Parcels",
            description = "Cadastral property boundaries",
            geometryType = "Polygon",
            idField = "parcel_id",
            geometryField = "geom",
            crs = new[] { "EPSG:4326", "EPSG:3857" },
            extent = new
            {
                bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } }
            },
            fields = new object[]
            {
                new { name = "parcel_id", type = "string", nullable = false },
                new { name = "owner", type = "string" },
                new { name = "area_sqm", type = "double" },
                new { name = "zone_code", type = "string" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(layer);
        var layerDef = snapshot.Layers[0];
        var service = snapshot.Services[0];

        // Assert - Verify WFS 2.0 compatible fields
        layerDef.Fields.Should().NotBeEmpty(); // Required for XSD schema generation

        var results = ProtocolMetadataValidator.ValidateForWfs(layerDef);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    #endregion

    #region WMS Crosswalk Tests

    [Fact]
    public void Layer_WithWmsRequiredFields_ShouldValidateForWms()
    {
        // Arrange
        var layerJson = CreateLayer(new
        {
            id = "landcover",
            serviceId = "service1",
            title = "Land Cover",
            description = "Land cover classification",
            geometryType = "Polygon",
            idField = "id",
            geometryField = "geom",
            crs = new[] { "EPSG:4326", "EPSG:3857", "EPSG:3395" },
            extent = new
            {
                bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                crs = "EPSG:4326"
            },
            keywords = new[] { "landcover", "classification" },
            styles = new
            {
                defaultStyleId = "landcover-style",
                styleIds = new[] { "landcover-style" }
            },
            minScale = 0.0,
            maxScale = 1000000.0
        });

        // Also need to add the style
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
                    id = "landcover",
                    serviceId = "service1",
                    title = "Land Cover",
                    description = "Land cover classification",
                    geometryType = "Polygon",
                    idField = "id",
                    geometryField = "geom",
                    crs = new[] { "EPSG:4326", "EPSG:3857" },
                    extent = new
                    {
                        bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                        crs = "EPSG:4326"
                    },
                    keywords = new[] { "landcover" },
                    styles = new
                    {
                        defaultStyleId = "landcover-style"
                    },
                    minScale = 0.0,
                    maxScale = 1000000.0
                }
            },
            styles = new[]
            {
                new
                {
                    id = "landcover-style",
                    renderer = "simple",
                    simple = new { fillColor = "#00FF00" }
                }
            }
        };

        // Act
        var snapshot = JsonMetadataProvider.Parse(JsonSerializer.Serialize(metadata, SerializerOptions));
        var layerDef = snapshot.Layers[0];

        // Assert - Verify WMS 1.3 compatible fields
        layerDef.Crs.Should().NotBeEmpty(); // Required for WMS CRS list
        layerDef.DefaultStyleId.Should().NotBeNullOrEmpty(); // Required for default style

        var results = ProtocolMetadataValidator.ValidateForWms(layerDef);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    #endregion

    #region CSW Crosswalk Tests

    [Fact]
    public void Layer_WithCswMetadataFields_ShouldValidateForCsw()
    {
        // Arrange
        var layer = CreateLayer(new
        {
            id = "census-blocks",
            serviceId = "service1",
            title = "Census Blocks 2020",
            description = "US Census geographic blocks",
            geometryType = "Polygon",
            idField = "geoid",
            geometryField = "geom",
            keywords = new[] { "census", "demographics", "boundaries" },
            extent = new
            {
                bbox = new[] { new[] { -124.0, 32.0, -114.0, 42.0 } }
            },
            catalog = new
            {
                summary = "2020 Census Block boundaries for statistical analysis",
                keywords = new[] { "government", "statistics" },
                contacts = new[]
                {
                    new
                    {
                        name = "Census Bureau",
                        email = "geo@census.gov",
                        organization = "US Census Bureau",
                        role = "originator"
                    }
                },
                links = new[]
                {
                    new
                    {
                        href = "https://census.gov/",
                        rel = "about",
                        title = "About US Census"
                    }
                }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(layer);
        var layerDef = snapshot.Layers[0];

        // Assert - Verify CSW 2.0.2 compatible fields (Dublin Core + ISO)
        layerDef.Id.Should().NotBeNullOrEmpty(); // dc:identifier
        layerDef.Title.Should().NotBeNullOrEmpty(); // dc:title
        layerDef.Description.Should().NotBeNullOrEmpty(); // dct:abstract
        layerDef.Keywords.Should().NotBeEmpty(); // dc:subject
        layerDef.Extent!.Bbox.Should().NotBeEmpty(); // ows:BoundingBox
        layerDef.Catalog.Links.Should().NotBeEmpty(); // dct:references

        var results = ProtocolMetadataValidator.ValidateForCsw(layerDef);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    #endregion

    #region Esri REST API Crosswalk Tests

    [Fact]
    public void Layer_WithEsriRestRequiredFields_ShouldValidateForEsri()
    {
        // Arrange
        var layer = CreateLayer(new
        {
            id = "fire-hydrants",
            serviceId = "service1",
            title = "Fire Hydrants",
            geometryType = "Point",
            idField = "hydrant_id",
            displayField = "location",
            geometryField = "geom",
            extent = new
            {
                bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } }
            },
            storage = new
            {
                table = "fire_hydrants",
                geometryColumn = "geom",
                primaryKey = "hydrant_id",
                srid = 3857
            },
            fields = new object[]
            {
                new { name = "hydrant_id", type = "int", nullable = false },
                new { name = "location", type = "string" },
                new { name = "flow_gpm", type = "int" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(layer);
        var layerDef = snapshot.Layers[0];

        // Assert - Verify Esri REST API compatible fields
        layerDef.IdField.Should().NotBeNullOrEmpty(); // Required for objectIdField
        layerDef.DisplayField.Should().NotBeNullOrEmpty(); // displayField
        layerDef.Storage!.Srid.Should().NotBeNull(); // Required for spatialReference

        var results = ProtocolMetadataValidator.ValidateForEsriRest(layerDef);
        results.IsValid.Should().BeTrue(string.Join("; ", results.Errors));
    }

    #endregion

    #region Multi-Protocol Validation Tests

    [Fact]
    public void Layer_ValidForMultipleProtocols_ShouldPassAllValidations()
    {
        // Arrange - Create a layer with comprehensive metadata for all protocols
        var layer = CreateLayer(new
        {
            id = "roads",
            serviceId = "service1",
            title = "Road Network",
            description = "Complete road centerlines dataset",
            geometryType = "LineString",
            idField = "road_id",
            displayField = "road_name",
            geometryField = "geom",
            crs = new[] { "EPSG:4326", "EPSG:3857" },
            keywords = new[] { "roads", "transportation", "infrastructure" },
            extent = new
            {
                bbox = new[] { new[] { -122.5, 37.5, -122.0, 38.0 } },
                crs = "EPSG:4326"
            },
            storage = new
            {
                table = "roads",
                geometryColumn = "geom",
                primaryKey = "road_id",
                srid = 3857
            },
            fields = new object[]
            {
                new { name = "road_id", type = "int", nullable = false },
                new { name = "road_name", type = "string" },
                new { name = "road_type", type = "string" }
            }
        });

        // Act
        var snapshot = JsonMetadataProvider.Parse(layer);
        var layerDef = snapshot.Layers[0];
        var service = snapshot.Services[0];

        // Assert - Validate against all major protocols
        var ogcResults = ProtocolMetadataValidator.ValidateForOgcApiFeatures(layerDef);
        ogcResults.IsValid.Should().BeTrue("OGC API Features validation failed: " + string.Join("; ", ogcResults.Errors));

        var esriResults = ProtocolMetadataValidator.ValidateForEsriRest(layerDef);
        esriResults.IsValid.Should().BeTrue("Esri REST API validation failed: " + string.Join("; ", esriResults.Errors));

        var wfsResults = ProtocolMetadataValidator.ValidateForWfs(layerDef);
        wfsResults.IsValid.Should().BeTrue("WFS validation failed: " + string.Join("; ", wfsResults.Errors));

        var cswResults = ProtocolMetadataValidator.ValidateForCsw(layerDef);
        cswResults.IsValid.Should().BeTrue("CSW validation failed: " + string.Join("; ", cswResults.Errors));

        // Get all validation results at once
        var allResults = ProtocolMetadataValidator.ValidateLayer(layerDef, service, includeWarnings: false);
        allResults.Should().OnlyContain(r => r.IsValid, "All protocols should validate successfully");
    }

    #endregion

    #region Helper Methods

    private static string CreateLayer(object layerDefinition)
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

    private static string CreateRasterDataset(object rasterDefinition)
    {
        var metadata = new
        {
            catalog = new { id = "test" },
            folders = System.Array.Empty<object>(),
            dataSources = System.Array.Empty<object>(),
            services = System.Array.Empty<object>(),
            layers = System.Array.Empty<object>(),
            rasterDatasets = new[] { rasterDefinition }
        };

        return JsonSerializer.Serialize(metadata, SerializerOptions);
    }

    #endregion
}
