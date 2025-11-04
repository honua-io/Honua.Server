// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Discovery;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Discovery;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Discovery;

/// <summary>
/// Unit tests for DynamicOgcCollectionProvider.
/// </summary>
public sealed class DynamicOgcCollectionProviderTests
{
    [Fact]
    public void Constructor_WithNullDiscoveryService_ThrowsArgumentNullException()
    {
        // Arrange
        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DynamicOgcCollectionProvider(
                null!,
                metadataRegistry,
                options,
                NullLogger<DynamicOgcCollectionProvider>.Instance));
    }

    [Fact]
    public async Task GetCollectionsAsync_WhenDisabled_ReturnsEmpty()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        Assert.Empty(collections);
    }

    [Fact]
    public async Task GetCollectionsAsync_WhenOgcDiscoveryDisabled_ReturnsEmpty()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = false
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        Assert.Empty(collections);
    }

    [Fact]
    public async Task GetCollectionsAsync_CreatesCollectionForEachTable()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "cities", "Point"),
            CreateTestTable("public", "roads", "LineString"),
            CreateTestTable("geo", "parcels", "Polygon")
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        var collectionList = collections.ToList();
        Assert.Equal(3, collectionList.Count);

        Assert.Contains(collectionList, c => c.Id == "public_cities");
        Assert.Contains(collectionList, c => c.Id == "public_roads");
        Assert.Contains(collectionList, c => c.Id == "geo_parcels");
    }

    [Fact]
    public async Task GetCollectionsAsync_CreatesFriendlyTitles()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "user_locations", "Point")
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true,
            UseFriendlyNames = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        var collection = Assert.Single(collections);
        Assert.Equal("User Locations", collection.Title);
    }

    [Fact]
    public async Task GetCollectionsAsync_IncludesExtent()
    {
        // Arrange
        var testTable = new DiscoveredTable
        {
            Schema = "public",
            TableName = "cities",
            GeometryColumn = "geom",
            SRID = 4326,
            GeometryType = "Point",
            PrimaryKeyColumn = "id",
            Columns = new Dictionary<string, ColumnInfo>(),
            HasSpatialIndex = true,
            EstimatedRowCount = 1000,
            Extent = new Envelope
            {
                MinX = -122.5,
                MinY = 37.7,
                MaxX = -122.4,
                MaxY = 37.8
            }
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testTable });

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        var collection = Assert.Single(collections);
        Assert.NotNull(collection.Extent);
        Assert.NotNull(collection.Extent.Spatial);
        Assert.Single(collection.Extent.Spatial.Bbox);

        var bbox = collection.Extent.Spatial.Bbox[0];
        Assert.Equal(-122.5, bbox[0]);
        Assert.Equal(37.7, bbox[1]);
        Assert.Equal(-122.4, bbox[2]);
        Assert.Equal(37.8, bbox[3]);
    }

    [Fact]
    public async Task GetCollectionsAsync_IncludesCrs()
    {
        // Arrange
        var testTable = new DiscoveredTable
        {
            Schema = "public",
            TableName = "cities",
            GeometryColumn = "geom",
            SRID = 3857,
            GeometryType = "Point",
            PrimaryKeyColumn = "id",
            Columns = new Dictionary<string, ColumnInfo>(),
            HasSpatialIndex = true,
            EstimatedRowCount = 1000,
            Extent = null
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testTable });

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        var collection = Assert.Single(collections);
        Assert.Contains("http://www.opengis.net/def/crs/EPSG/0/3857", collection.Crs);
        Assert.Contains("http://www.opengis.net/def/crs/OGC/1.3/CRS84", collection.Crs);
    }

    [Fact]
    public async Task GetCollectionsAsync_IncludesLinks()
    {
        // Arrange
        var testTable = CreateTestTable("public", "cities", "Point");

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testTable });

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true,
            GenerateOpenApiDocs = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collections = await provider.GetCollectionsAsync("test-datasource", httpContext);

        // Assert
        var collection = Assert.Single(collections);
        Assert.NotEmpty(collection.Links);

        // Should have self, items, schema, and queryables links
        Assert.Contains(collection.Links, l => l.Rel == "self");
        Assert.Contains(collection.Links, l => l.Rel == "items");
        Assert.Contains(collection.Links, l => l.Rel == "describedby");
        Assert.Contains(collection.Links, l => l.Rel == "http://www.opengis.net/def/rel/ogc/1.0/queryables");
    }

    [Fact]
    public async Task GetCollectionAsync_FindsSpecificCollection()
    {
        // Arrange
        var testTable = CreateTestTable("public", "cities", "Point");

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTableAsync("test-datasource", "public.cities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTable);

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collection = await provider.GetCollectionAsync("test-datasource", "public_cities", httpContext);

        // Assert
        Assert.NotNull(collection);
        Assert.Equal("public_cities", collection.Id);
        Assert.Equal("Cities", collection.Title);
    }

    [Fact]
    public async Task GetCollectionAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        var httpContext = CreateHttpContext();

        // Act
        var collection = await provider.GetCollectionAsync("test-datasource", "invalid_id_format", httpContext);

        // Assert
        Assert.Null(collection);
    }

    [Fact]
    public async Task GetQueryablesAsync_GeneratesJsonSchema()
    {
        // Arrange
        var testTable = CreateTestTable("public", "cities", "Point");

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTableAsync("test-datasource", "public.cities", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTable);

        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsOgcCollections = true
        });

        var provider = new DynamicOgcCollectionProvider(
            discoveryService.Object,
            metadataRegistry,
            options,
            NullLogger<DynamicOgcCollectionProvider>.Instance);

        // Act
        var queryables = await provider.GetQueryablesAsync("test-datasource", "public_cities");

        // Assert
        Assert.NotEmpty(queryables);
        Assert.Equal("https://json-schema.org/draft/2019-09/schema", queryables["$schema"]);
        Assert.Equal("object", queryables["type"]);

        var properties = (Dictionary<string, object>)queryables["properties"];
        Assert.Contains("id", properties.Keys);
        Assert.Contains("geom", properties.Keys);
        Assert.Contains("name", properties.Keys);
    }

    private static DiscoveredTable CreateTestTable(string schema, string tableName, string geometryType)
    {
        return new DiscoveredTable
        {
            Schema = schema,
            TableName = tableName,
            GeometryColumn = "geom",
            SRID = 4326,
            GeometryType = geometryType,
            PrimaryKeyColumn = "id",
            Columns = new Dictionary<string, ColumnInfo>
            {
                ["id"] = new ColumnInfo
                {
                    Name = "id",
                    DataType = "int32",
                    StorageType = "integer",
                    IsPrimaryKey = true,
                    IsNullable = false
                },
                ["name"] = new ColumnInfo
                {
                    Name = "name",
                    DataType = "string",
                    StorageType = "text",
                    IsPrimaryKey = false,
                    IsNullable = true
                }
            },
            HasSpatialIndex = true,
            EstimatedRowCount = 1000,
            Extent = null
        };
    }

    private static HttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost", 5000);
        context.Request.PathBase = new PathString("");
        return context;
    }
}
