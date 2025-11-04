using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.Data.BigQuery;
using Xunit;

namespace Honua.Server.Enterprise.Tests.BigQuery;

/// <summary>
/// Integration tests for BigQuery data provider using BigQuery emulator.
/// These tests verify actual query execution, not just SQL generation.
/// Tests are automatically skipped if Docker is not available.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EnterpriseDB")]
[Collection("BigQueryEmulator")]
public class BigQueryIntegrationTests : IAsyncLifetime
{
    private readonly BigQueryEmulatorFixture _fixture;
    private BigQueryDataStoreProvider? _provider;
    private DataSourceDefinition? _dataSource;
    private ServiceDefinition? _service;
    private LayerDefinition? _layer;

    public BigQueryIntegrationTests(BigQueryEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        Skip.IfNot(_fixture.IsAvailable, "Docker is not available or BigQuery emulator failed to start");

        _provider = new BigQueryDataStoreProvider();

        _dataSource = new DataSourceDefinition
        {
            Id = "bigquery-test",
            Provider = BigQueryDataStoreProvider.ProviderKey,
            ConnectionString = $"ProjectId={BigQueryEmulatorFixture.TestProjectId}"
        };

        _service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "feature",
            DataSourceId = "bigquery-test"
        };

        _layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "Point",
            IdField = "id",
            GeometryField = "geom",
            Storage = new LayerStorageDefinition
            {
                Table = $"{BigQueryEmulatorFixture.TestProjectId}.{BigQueryEmulatorFixture.TestDatasetId}.{BigQueryEmulatorFixture.TestTableId}",
                PrimaryKey = "id",
                Srid = 4326
            }
        };

        await _fixture.InsertTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        if (_provider != null)
        {
            _provider.Dispose();
        }

        if (_fixture.IsAvailable)
        {
            await _fixture.ClearTestDataAsync();
        }
    }

    [SkippableFact]
    public async Task QueryAsync_WithoutFilters_ReturnsAllFeatures()
    {
        // Arrange
        var query = new FeatureQuery();

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Attributes.Should().ContainKey("id");
            r.Attributes.Should().ContainKey("name");
            r.Attributes.Should().ContainKey("_geojson");
        });
    }

    [SkippableFact]
    public async Task QueryAsync_WithLimit_ReturnsLimitedResults()
    {
        // Arrange
        var query = new FeatureQuery(Limit: 2);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(2);
    }

    [SkippableFact]
    public async Task QueryAsync_WithOffset_SkipsResults()
    {
        // Arrange
        var query = new FeatureQuery(Limit: 10, Offset: 1);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(2);
    }

    [SkippableFact]
    public async Task QueryAsync_WithSorting_ReturnsSortedResults()
    {
        // Arrange
        var sortOrders = new List<FeatureSortOrder>
        {
            new("value", FeatureSortDirection.Descending)
        };
        var query = new FeatureQuery(SortOrders: sortOrders);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCount(3);
        var values = results.Select(r => (long)r.Attributes["value"]!).ToList();
        values.Should().BeInDescendingOrder();
        values.Should().Equal(300, 200, 100);
    }

    [SkippableFact]
    public async Task QueryAsync_WithBbox_ReturnsFilteredResults()
    {
        // Arrange - bbox around San Francisco area only
        var bbox = new BoundingBox(-123, 37, -122, 38);
        var query = new FeatureQuery(Bbox: bbox);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert - should only return San Francisco point
        results.Should().HaveCount(1);
        results[0].Attributes["name"].Should().Be("Test Feature 1");
    }

    [SkippableFact]
    public async Task CountAsync_WithoutFilters_ReturnsCorrectCount()
    {
        // Arrange
        var query = new FeatureQuery();

        // Act
        var count = await _provider!.CountAsync(_dataSource!, _service!, _layer!, query);

        // Assert
        count.Should().Be(3);
    }

    [SkippableFact]
    public async Task CountAsync_WithBbox_ReturnsFilteredCount()
    {
        // Arrange - bbox around San Francisco area only
        var bbox = new BoundingBox(-123, 37, -122, 38);
        var query = new FeatureQuery(Bbox: bbox);

        // Act
        var count = await _provider!.CountAsync(_dataSource!, _service!, _layer!, query);

        // Assert
        count.Should().Be(1);
    }

    [SkippableFact]
    public async Task GetAsync_WithValidId_ReturnsFeature()
    {
        // Arrange
        var featureId = "feature-1";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, featureId, null);

        // Assert
        result.Should().NotBeNull();
        result!.Attributes["id"].Should().Be("feature-1");
        result.Attributes["name"].Should().Be("Test Feature 1");
        result.Attributes.Should().ContainKey("_geojson");
    }

    [SkippableFact]
    public async Task GetAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var featureId = "non-existent";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, featureId, null);

        // Assert
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task CreateAsync_WithValidFeature_InsertsAndReturnsFeature()
    {
        // Arrange
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = "feature-new",
            ["name"] = "New Feature",
            ["description"] = "Newly created feature",
            ["value"] = 999,
            ["created_at"] = DateTime.UtcNow,
            ["geom"] = "POINT(-122.4194 37.7749)"
        });

        // Act
        var result = await _provider!.CreateAsync(_dataSource!, _service!, _layer!, record);

        // Assert
        result.Should().NotBeNull();
        result.Attributes["name"].Should().Be("New Feature");

        // Verify it was inserted
        var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, "feature-new", null);
        fetched.Should().NotBeNull();
        fetched!.Attributes["name"].Should().Be("New Feature");
    }

    [SkippableFact]
    public async Task UpdateAsync_WithValidFeature_UpdatesAndReturnsFeature()
    {
        // Arrange
        var featureId = "feature-2";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["name"] = "Updated Feature 2",
            ["description"] = "This feature was updated",
            ["value"] = 2000
        });

        // Act
        var result = await _provider!.UpdateAsync(_dataSource!, _service!, _layer!, featureId, record);

        // Assert
        result.Should().NotBeNull();
        result!.Attributes["name"].Should().Be("Updated Feature 2");
        result.Attributes["value"].Should().Be(2000L);

        // Verify the update persisted
        var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, featureId, null);
        fetched!.Attributes["name"].Should().Be("Updated Feature 2");
    }

    [SkippableFact]
    public async Task UpdateAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var featureId = "non-existent";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["name"] = "Should Not Work"
        });

        // Act
        var result = await _provider!.UpdateAsync(_dataSource!, _service!, _layer!, featureId, record);

        // Assert
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_WithValidId_DeletesFeature()
    {
        // Arrange
        var featureId = "feature-3";

        // Act
        var result = await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, featureId);

        // Assert
        result.Should().BeTrue();

        // Verify it was deleted
        var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, featureId, null);
        fetched.Should().BeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_WithInvalidId_ReturnsFalse()
    {
        // Arrange
        var featureId = "non-existent";

        // Act
        var result = await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, featureId);

        // Assert
        result.Should().BeFalse();
    }

    [SkippableFact]
    public async Task BulkInsertAsync_WithMultipleFeatures_InsertsAll()
    {
        // Arrange
        var records = new[]
        {
            new FeatureRecord(new Dictionary<string, object?>
            {
                ["id"] = "bulk-1",
                ["name"] = "Bulk Feature 1",
                ["value"] = 1000,
                ["geom"] = "POINT(-122.4194 37.7749)"
            }),
            new FeatureRecord(new Dictionary<string, object?>
            {
                ["id"] = "bulk-2",
                ["name"] = "Bulk Feature 2",
                ["value"] = 2000,
                ["geom"] = "POINT(-118.2437 34.0522)"
            })
        };

        // Act
        var count = await _provider!.BulkInsertAsync(_dataSource!, _service!, _layer!, records.ToAsyncEnumerable());

        // Assert
        count.Should().Be(2);

        // Verify they were inserted
        var totalCount = await _provider.CountAsync(_dataSource!, _service!, _layer!, new FeatureQuery());
        totalCount.Should().BeGreaterThanOrEqualTo(5); // Original 3 + 2 new
    }

    [SkippableFact]
    public async Task TestConnectivityAsync_WithValidConnection_Succeeds()
    {
        // Act & Assert
        await _provider!.Invoking(p => p.TestConnectivityAsync(_dataSource!))
            .Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task TestConnectivityAsync_WithInvalidConnection_Throws()
    {
        // Arrange
        var invalidDataSource = new DataSourceDefinition
        {
            Id = "invalid",
            Provider = BigQueryDataStoreProvider.ProviderKey,
            ConnectionString = "ProjectId=non-existent-project"
        };

        // Act & Assert
        await _provider!.Invoking(p => p.TestConnectivityAsync(invalidDataSource))
            .Should().ThrowAsync<Exception>();
    }

    [SkippableFact]
    public void Capabilities_ReturnsCorrectCapabilities()
    {
        // Act
        var capabilities = _provider!.Capabilities;

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsTransactions.Should().BeFalse(); // BigQuery doesn't support transactions
        capabilities.SupportsSpatialIndexes.Should().BeTrue();
    }

    [SkippableFact]
    public async Task GeometryHandling_WithSTGEOGPOINT_WorksCorrectly()
    {
        // Arrange - Insert using ST_GEOGPOINT format
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = "geom-test",
            ["name"] = "Geometry Test",
            ["value"] = 500,
            ["geom"] = "POINT(-122.4194 37.7749)"
        });

        // Act
        await _provider!.CreateAsync(_dataSource!, _service!, _layer!, record);
        var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, "geom-test", null);

        // Assert
        fetched.Should().NotBeNull();
        fetched!.Attributes.Should().ContainKey("_geojson");
        var geojson = fetched.Attributes["_geojson"];
        geojson.Should().NotBeNull();
    }

    [SkippableFact]
    public async Task ParameterizedQueries_PreventSqlInjection()
    {
        // Arrange - Attempt SQL injection through feature ID
        var maliciousId = "feature-1' OR '1'='1";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, maliciousId, null);

        // Assert - Should not return any results due to parameterization
        result.Should().BeNull();
    }
}
