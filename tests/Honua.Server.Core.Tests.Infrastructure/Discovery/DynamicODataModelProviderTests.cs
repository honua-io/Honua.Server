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
using Honua.Server.Host.OData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Discovery;

/// <summary>
/// Unit tests for DynamicODataModelProvider.
/// </summary>
public sealed class DynamicODataModelProviderTests
{
    [Fact]
    public void Constructor_WithNullDiscoveryService_ThrowsArgumentNullException()
    {
        // Arrange
        var metadataRegistry = new Mock<IMetadataRegistry>().Object;
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DynamicODataModelProvider(
                null!,
                metadataRegistry,
                typeMapper,
                options,
                NullLogger<DynamicODataModelProvider>.Instance));
    }

    [Fact]
    public void Constructor_WithNullMetadataRegistry_ThrowsArgumentNullException()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>().Object;
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DynamicODataModelProvider(
                discoveryService,
                null!,
                typeMapper,
                options,
                NullLogger<DynamicODataModelProvider>.Instance));
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_WhenDisabled_ReturnsEmpty()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions { Enabled = false });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        Assert.Empty(services);
        Assert.Empty(layers);
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_WhenODataDiscoveryDisabled_ReturnsEmpty()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = false
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        Assert.Empty(services);
        Assert.Empty(layers);
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_CreatesServicePerSchema()
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

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        Assert.Equal(2, services.Count); // public and geo schemas
        Assert.Equal(3, layers.Count); // 3 tables total

        Assert.Contains(services, s => s.Id == "discovered_public");
        Assert.Contains(services, s => s.Id == "discovered_geo");
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_CreatesFriendlyNames()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "user_locations", "Point")
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true,
            UseFriendlyNames = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        var service = Assert.Single(services);
        Assert.Contains("Public", service.Title); // Schema name is humanized

        var layer = Assert.Single(layers);
        Assert.Equal("User Locations", layer.Title); // Table name is humanized
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_CreatesLayerWithCorrectMetadata()
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
                    IsNullable = true,
                    Alias = "Name"
                }
            },
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

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        var layer = Assert.Single(layers);
        Assert.Equal("public_cities", layer.Id);
        Assert.Equal("geom", layer.GeometryField);
        Assert.Equal("id", layer.IdField);
        Assert.Equal("point", layer.GeometryType);
        Assert.Equal(4326, layer.Storage?.Srid);
        Assert.Equal("public.cities", layer.Storage?.Table);
        Assert.NotNull(layer.Extent);
        Assert.Equal(-122.5, layer.Extent.Bbox[0][0]);
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_IncludesFieldDefinitions()
    {
        // Arrange
        var testTable = CreateTestTable("public", "cities", "Point");

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testTable });

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true,
            UseFriendlyNames = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        var layer = Assert.Single(layers);
        Assert.Equal(2, layer.Fields.Count); // id and name (geom excluded)

        var idField = layer.Fields.First(f => f.Name == "id");
        Assert.Equal("int32", idField.DataType);

        var nameField = layer.Fields.First(f => f.Name == "name");
        Assert.Equal("string", nameField.DataType);
        Assert.Equal("Name", nameField.Alias); // Humanized
    }

    [Fact]
    public async Task GenerateMetadataFromDiscoveryAsync_NormalizesGeometryTypes()
    {
        // Arrange
        var testTables = new[]
        {
            CreateTestTable("public", "points", "POINT"),
            CreateTestTable("public", "lines", "LINESTRING"),
            CreateTestTable("public", "multilines", "MULTILINESTRING"),
            CreateTestTable("public", "polys", "POLYGON"),
            CreateTestTable("public", "multipolys", "MULTIPOLYGON")
        };

        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testTables);

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act
        var (services, layers) = await provider.GenerateMetadataFromDiscoveryAsync("test-datasource");

        // Assert
        Assert.Equal(5, layers.Count);
        Assert.Contains(layers, l => l.Storage!.Table == "public.points" && l.GeometryType == "point");
        Assert.Contains(layers, l => l.Storage!.Table == "public.lines" && l.GeometryType == "polyline");
        Assert.Contains(layers, l => l.Storage!.Table == "public.multilines" && l.GeometryType == "polyline");
        Assert.Contains(layers, l => l.Storage!.Table == "public.polys" && l.GeometryType == "polygon");
        Assert.Contains(layers, l => l.Storage!.Table == "public.multipolys" && l.GeometryType == "polygon");
    }

    [Fact]
    public async Task BuildModelFromDiscoveryAsync_WhenNoTables_ThrowsInvalidOperationException()
    {
        // Arrange
        var discoveryService = new Mock<ITableDiscoveryService>();
        discoveryService.Setup(s => s.DiscoverTablesAsync("test-datasource", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiscoveredTable>());

        var metadataRegistry = CreateTestMetadataRegistry();
        var typeMapper = new Mock<IODataFieldTypeMapper>().Object;
        var options = Options.Create(new AutoDiscoveryOptions
        {
            Enabled = true,
            DiscoverPostGISTablesAsODataCollections = true
        });

        var provider = new DynamicODataModelProvider(
            discoveryService.Object,
            metadataRegistry,
            typeMapper,
            options,
            NullLogger<DynamicODataModelProvider>.Instance);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.BuildModelFromDiscoveryAsync("test-datasource"));
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
                    IsNullable = true,
                    Alias = "Name"
                }
            },
            HasSpatialIndex = true,
            EstimatedRowCount = 1000
        };
    }

    private static IMetadataRegistry CreateTestMetadataRegistry()
    {
        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "test" },
            Array.Empty<FolderDefinition>(),
            new[] { new DataSourceDefinition { Id = "test-datasource", Provider = "postgis", ConnectionString = "Host=localhost" } },
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>());

        var mock = new Mock<IMetadataRegistry>();
        mock.Setup(m => m.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        return mock.Object;
    }
}
