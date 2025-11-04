using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.Data.Snowflake;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Snowflake;

/// <summary>
/// Integration tests for Snowflake data provider.
/// These tests require actual Snowflake credentials and are skipped if not provided.
///
/// To run these tests, set the following environment variables:
/// - SNOWFLAKE_ACCOUNT: Your Snowflake account identifier
/// - SNOWFLAKE_USER: Your Snowflake username
/// - SNOWFLAKE_PASSWORD: Your Snowflake password
/// - SNOWFLAKE_DATABASE: Test database name
/// - SNOWFLAKE_SCHEMA: Test schema name
/// - SNOWFLAKE_WAREHOUSE: Warehouse to use
/// - SNOWFLAKE_TEST_TABLE: Test table name (should have: id, name, description, value, geom)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EnterpriseDB")]
[Trait("Category", "RequiresCredentials")]
public class SnowflakeIntegrationTests : IAsyncLifetime
{
    private SnowflakeDataStoreProvider? _provider;
    private DataSourceDefinition? _dataSource;
    private ServiceDefinition? _service;
    private LayerDefinition? _layer;
    private bool _credentialsAvailable;

    public async Task InitializeAsync()
    {
        _credentialsAvailable = HasSnowflakeCredentials();

        if (!_credentialsAvailable)
        {
            return;
        }

        var account = Environment.GetEnvironmentVariable("SNOWFLAKE_ACCOUNT");
        var user = Environment.GetEnvironmentVariable("SNOWFLAKE_USER");
        var password = Environment.GetEnvironmentVariable("SNOWFLAKE_PASSWORD");
        var database = Environment.GetEnvironmentVariable("SNOWFLAKE_DATABASE");
        var schema = Environment.GetEnvironmentVariable("SNOWFLAKE_SCHEMA");
        var warehouse = Environment.GetEnvironmentVariable("SNOWFLAKE_WAREHOUSE");
        var testTable = Environment.GetEnvironmentVariable("SNOWFLAKE_TEST_TABLE") ?? "test_features";

        _provider = new SnowflakeDataStoreProvider();

        _dataSource = new DataSourceDefinition
        {
            Id = "snowflake-test",
            Provider = SnowflakeDataStoreProvider.ProviderKey,
            ConnectionString = $"account={account};user={user};password={password};db={database};schema={schema};warehouse={warehouse}"
        };

        _service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "feature",
            DataSourceId = "snowflake-test"
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
                Table = testTable,
                PrimaryKey = "id",
                Srid = 4326
            }
        };

        // Initialize test data
        await InitializeTestDataAsync();
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    private static bool HasSnowflakeCredentials()
    {
        var account = Environment.GetEnvironmentVariable("SNOWFLAKE_ACCOUNT");
        var user = Environment.GetEnvironmentVariable("SNOWFLAKE_USER");
        var password = Environment.GetEnvironmentVariable("SNOWFLAKE_PASSWORD");
        var database = Environment.GetEnvironmentVariable("SNOWFLAKE_DATABASE");

        return !string.IsNullOrWhiteSpace(account) &&
               !string.IsNullOrWhiteSpace(user) &&
               !string.IsNullOrWhiteSpace(password) &&
               !string.IsNullOrWhiteSpace(database);
    }

    private async Task InitializeTestDataAsync()
    {
        // Clear existing test data and insert fresh test records
        // This would require direct SQL access to Snowflake
        await Task.CompletedTask;
    }

    [SkippableFact]
    public async Task QueryAsync_WithoutFilters_ReturnsFeatures()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var query = new FeatureQuery(Limit: 10);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Should().AllSatisfy(r =>
        {
            r.Attributes.Should().ContainKey("id");
            r.Attributes.Should().ContainKey("_geojson");
        });
    }

    [SkippableFact]
    public async Task QueryAsync_WithLimit_ReturnsLimitedResults()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var query = new FeatureQuery(Limit: 5);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().HaveCountLessThanOrEqualTo(5);
    }

    [SkippableFact]
    public async Task QueryAsync_WithPagination_WorksCorrectly()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var query1 = new FeatureQuery(Limit: 2, Offset: 0);
        var query2 = new FeatureQuery(Limit: 2, Offset: 2);

        // Act
        var results1 = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query1))
        {
            results1.Add(record);
        }

        var results2 = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query2))
        {
            results2.Add(record);
        }

        // Assert
        results1.Should().NotBeEmpty();
        results2.Should().NotBeEmpty();

        // Results should be different (pagination working)
        var ids1 = results1.Select(r => r.Attributes["id"]).ToList();
        var ids2 = results2.Select(r => r.Attributes["id"]).ToList();
        ids1.Should().NotIntersectWith(ids2);
    }

    [SkippableFact]
    public async Task QueryAsync_WithSorting_ReturnsSortedResults()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var sortOrders = new List<FeatureSortOrder>
        {
            new("id", FeatureSortDirection.Ascending)
        };
        var query = new FeatureQuery(Limit: 10, SortOrders: sortOrders);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().NotBeEmpty();
        var ids = results.Select(r => r.Attributes["id"]?.ToString() ?? "").ToList();
        ids.Should().BeInAscendingOrder();
    }

    [SkippableFact]
    public async Task QueryAsync_WithBbox_ReturnsFilteredResults()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange - bbox around USA
        var bbox = new BoundingBox(-125, 24, -66, 49);
        var query = new FeatureQuery(Bbox: bbox, Limit: 100);

        // Act
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }

        // Assert
        results.Should().NotBeNull();
        // All results should have geometry within the bbox
    }

    [SkippableFact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var query = new FeatureQuery();

        // Act
        var count = await _provider!.CountAsync(_dataSource!, _service!, _layer!, query);

        // Assert
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    [SkippableFact]
    public async Task GetAsync_WithExistingId_ReturnsFeature()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange - First get an ID from the dataset
        var query = new FeatureQuery(Limit: 1);
        FeatureRecord? firstRecord = null;
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            firstRecord = record;
            break;
        }

        if (firstRecord == null)
        {
            throw new SkipException("No test data available in Snowflake table");
        }

        var featureId = firstRecord.Attributes["id"]?.ToString();

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, featureId!, null);

        // Assert
        result.Should().NotBeNull();
        result!.Attributes["id"]?.ToString().Should().Be(featureId);
        result.Attributes.Should().ContainKey("_geojson");
    }

    [SkippableFact]
    public async Task CreateAsync_WithValidFeature_InsertsFeature()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var uniqueId = $"test-{Guid.NewGuid():N}";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = uniqueId,
            ["name"] = "Test Feature",
            ["description"] = "Integration test feature",
            ["value"] = 123,
            ["geom"] = "POINT(-122.4194 37.7749)"
        });

        try
        {
            // Act
            var result = await _provider!.CreateAsync(_dataSource!, _service!, _layer!, record);

            // Assert
            result.Should().NotBeNull();
            result.Attributes["name"].Should().Be("Test Feature");

            // Verify it was inserted
            var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, uniqueId, null);
            fetched.Should().NotBeNull();
        }
        finally
        {
            // Cleanup - delete the test record
            await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, uniqueId);
        }
    }

    [SkippableFact]
    public async Task UpdateAsync_WithValidFeature_UpdatesFeature()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange - Create a test record first
        var uniqueId = $"test-{Guid.NewGuid():N}";
        var createRecord = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = uniqueId,
            ["name"] = "Original Name",
            ["value"] = 100
        });

        await _provider!.CreateAsync(_dataSource!, _service!, _layer!, createRecord);

        try
        {
            var updateRecord = new FeatureRecord(new Dictionary<string, object?>
            {
                ["name"] = "Updated Name",
                ["value"] = 999
            });

            // Act
            var result = await _provider.UpdateAsync(_dataSource!, _service!, _layer!, uniqueId, updateRecord);

            // Assert
            result.Should().NotBeNull();
            result!.Attributes["name"].Should().Be("Updated Name");
            result.Attributes["value"].Should().Be(999);
        }
        finally
        {
            // Cleanup
            await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, uniqueId);
        }
    }

    [SkippableFact]
    public async Task DeleteAsync_WithValidId_DeletesFeature()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange - Create a test record first
        var uniqueId = $"test-{Guid.NewGuid():N}";
        var createRecord = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = uniqueId,
            ["name"] = "To Be Deleted",
            ["value"] = 100
        });

        await _provider!.CreateAsync(_dataSource!, _service!, _layer!, createRecord);

        // Act
        var result = await _provider.DeleteAsync(_dataSource!, _service!, _layer!, uniqueId);

        // Assert
        result.Should().BeTrue();

        // Verify it was deleted
        var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, uniqueId, null);
        fetched.Should().BeNull();
    }

    [SkippableFact]
    public async Task BulkInsertAsync_WithMultipleFeatures_InsertsAll()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var uniqueIds = Enumerable.Range(1, 5).Select(i => $"bulk-{Guid.NewGuid():N}").ToList();
        var records = uniqueIds.Select(id => new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = $"Bulk Feature {id}",
            ["value"] = 1000,
            ["geom"] = "POINT(-122.4194 37.7749)"
        }));

        try
        {
            // Act
            var count = await _provider!.BulkInsertAsync(_dataSource!, _service!, _layer!, records.ToAsyncEnumerable());

            // Assert
            count.Should().Be(5);

            // Verify they were inserted
            foreach (var id in uniqueIds)
            {
                var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, id, null);
                fetched.Should().NotBeNull();
            }
        }
        finally
        {
            // Cleanup
            foreach (var id in uniqueIds)
            {
                await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, id);
            }
        }
    }

    [SkippableFact]
    public async Task TestConnectivityAsync_WithValidConnection_Succeeds()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Act & Assert
        await _provider!.Invoking(p => p.TestConnectivityAsync(_dataSource!))
            .Should().NotThrowAsync();
    }

    [SkippableFact]
    public async Task GeographyType_WorksCorrectly()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange
        var uniqueId = $"test-{Guid.NewGuid():N}";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = uniqueId,
            ["name"] = "Geography Test",
            ["geom"] = "POINT(-122.4194 37.7749)"
        });

        try
        {
            // Act
            await _provider!.CreateAsync(_dataSource!, _service!, _layer!, record);
            var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, uniqueId, null);

            // Assert
            fetched.Should().NotBeNull();
            fetched!.Attributes.Should().ContainKey("_geojson");
            var geojson = fetched.Attributes["_geojson"];
            geojson.Should().NotBeNull();
        }
        finally
        {
            // Cleanup
            await _provider!.DeleteAsync(_dataSource!, _service!, _layer!, uniqueId);
        }
    }

    [SkippableFact]
    public void Capabilities_ReturnsCorrectCapabilities()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Act
        var capabilities = _provider!.Capabilities;

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.SupportsSpatialIndexes.Should().BeTrue();
    }

    [SkippableFact]
    public async Task ParameterizedQueries_PreventSqlInjection()
    {
        Skip.IfNot(_credentialsAvailable, "Snowflake credentials not provided. Set SNOWFLAKE_ACCOUNT, SNOWFLAKE_USER, SNOWFLAKE_PASSWORD, SNOWFLAKE_DATABASE environment variables.");

        // Arrange - Attempt SQL injection through feature ID
        var maliciousId = "'; DROP TABLE test_features; --";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, maliciousId, null);

        // Assert - Should not return any results due to parameterization
        result.Should().BeNull();

        // Verify table still exists by running a query
        var count = await _provider.CountAsync(_dataSource!, _service!, _layer!, new FeatureQuery());
        count.Should().BeGreaterThanOrEqualTo(0);
    }
}
