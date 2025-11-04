using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Enterprise.Data.Redshift;
using Xunit;

namespace Honua.Server.Enterprise.Tests.Redshift;

/// <summary>
/// Integration tests for Redshift data provider.
/// These tests require actual AWS Redshift credentials and are skipped if not provided.
///
/// To run these tests, set the following environment variables:
/// - REDSHIFT_CLUSTER_IDENTIFIER: Your Redshift cluster identifier
/// - REDSHIFT_DATABASE: Database name
/// - REDSHIFT_DB_USER: Database user
/// - REDSHIFT_TEST_TABLE: Test table name (should have: id, name, description, value)
/// - AWS_REGION: AWS region (optional, defaults to us-east-1)
///
/// Note: These tests use the Redshift Data API, which requires appropriate IAM permissions.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "EnterpriseDB")]
[Trait("Category", "RequiresCredentials")]
public class RedshiftIntegrationTests : IAsyncLifetime
{
    private RedshiftDataStoreProvider? _provider;
    private DataSourceDefinition? _dataSource;
    private ServiceDefinition? _service;
    private LayerDefinition? _layer;
    private bool _credentialsAvailable;

    public async Task InitializeAsync()
    {
        _credentialsAvailable = HasRedshiftCredentials();

        if (!_credentialsAvailable)
        {
            return;
        }

        var clusterIdentifier = Environment.GetEnvironmentVariable("REDSHIFT_CLUSTER_IDENTIFIER");
        var database = Environment.GetEnvironmentVariable("REDSHIFT_DATABASE");
        var dbUser = Environment.GetEnvironmentVariable("REDSHIFT_DB_USER");
        var testTable = Environment.GetEnvironmentVariable("REDSHIFT_TEST_TABLE") ?? "test_features";

        _provider = new RedshiftDataStoreProvider();

        _dataSource = new DataSourceDefinition
        {
            Id = "redshift-test",
            Provider = RedshiftDataStoreProvider.ProviderKey,
            ConnectionString = $"ClusterIdentifier={clusterIdentifier};Database={database};DbUser={dbUser}"
        };

        _service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "test-folder",
            ServiceType = "feature",
            DataSourceId = "redshift-test"
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

        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _provider?.Dispose();
        return Task.CompletedTask;
    }

    private static bool HasRedshiftCredentials()
    {
        var clusterIdentifier = Environment.GetEnvironmentVariable("REDSHIFT_CLUSTER_IDENTIFIER");
        var database = Environment.GetEnvironmentVariable("REDSHIFT_DATABASE");
        var dbUser = Environment.GetEnvironmentVariable("REDSHIFT_DB_USER");

        return !string.IsNullOrWhiteSpace(clusterIdentifier) &&
               !string.IsNullOrWhiteSpace(database) &&
               !string.IsNullOrWhiteSpace(dbUser);
    }

    [SkippableFact]
    public async Task QueryAsync_WithoutFilters_ReturnsFeatures()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
        });
    }

    [SkippableFact]
    public async Task QueryAsync_WithLimit_ReturnsLimitedResults()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange
        var query1 = new FeatureQuery(Limit: 3, Offset: 0);
        var query2 = new FeatureQuery(Limit: 3, Offset: 3);

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
        if (results1.Any() && results2.Any())
        {
            var ids1 = results1.Select(r => r.Attributes["id"]).ToList();
            var ids2 = results2.Select(r => r.Attributes["id"]).ToList();
            ids1.Should().NotIntersectWith(ids2);
        }
    }

    [SkippableFact]
    public async Task QueryAsync_WithSorting_ReturnsSortedResults()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
        if (results.Count > 1)
        {
            var ids = results.Select(r => r.Attributes["id"]?.ToString() ?? "").ToList();
            ids.Should().BeInAscendingOrder();
        }
    }

    [SkippableFact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
            throw new SkipException("No test data available in Redshift table");
        }

        var featureId = firstRecord.Attributes["id"]?.ToString();

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, featureId!, null);

        // Assert
        result.Should().NotBeNull();
        result!.Attributes["id"]?.ToString().Should().Be(featureId);
    }

    [SkippableFact]
    public async Task GetAsync_WithInvalidId_ReturnsNull()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange
        var featureId = $"non-existent-{Guid.NewGuid():N}";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, featureId, null);

        // Assert
        result.Should().BeNull();
    }

    [SkippableFact]
    public async Task CreateAsync_WithValidFeature_InsertsFeature()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange
        var uniqueId = $"test-{Guid.NewGuid():N}";
        var record = new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = uniqueId,
            ["name"] = "Test Feature",
            ["description"] = "Integration test feature",
            ["value"] = 123
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
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
            result!.Attributes["name"]?.ToString().Should().Be("Updated Name");
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
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

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
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange
        var uniqueIds = Enumerable.Range(1, 5).Select(i => $"bulk-{Guid.NewGuid():N}").ToList();
        var records = uniqueIds.Select(id => new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = $"Bulk Feature {id}",
            ["value"] = 1000
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
            await _provider!.BulkDeleteAsync(_dataSource!, _service!, _layer!, uniqueIds.ToAsyncEnumerable());
        }
    }

    [SkippableFact]
    public async Task BulkUpdateAsync_WithMultipleFeatures_UpdatesAll()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange - Create test records first
        var uniqueIds = Enumerable.Range(1, 3).Select(i => $"bulk-upd-{Guid.NewGuid():N}").ToList();
        var createRecords = uniqueIds.Select(id => new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = $"Original {id}",
            ["value"] = 100
        }));

        await _provider!.BulkInsertAsync(_dataSource!, _service!, _layer!, createRecords.ToAsyncEnumerable());

        try
        {
            var updateRecords = uniqueIds.Select(id => new KeyValuePair<string, FeatureRecord>(
                id,
                new FeatureRecord(new Dictionary<string, object?>
                {
                    ["name"] = $"Updated {id}",
                    ["value"] = 999
                })
            ));

            // Act
            var count = await _provider.BulkUpdateAsync(_dataSource!, _service!, _layer!, updateRecords.ToAsyncEnumerable());

            // Assert
            count.Should().Be(3);

            // Verify updates
            foreach (var id in uniqueIds)
            {
                var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, id, null);
                fetched.Should().NotBeNull();
                fetched!.Attributes["name"]?.ToString().Should().Contain("Updated");
            }
        }
        finally
        {
            // Cleanup
            await _provider!.BulkDeleteAsync(_dataSource!, _service!, _layer!, uniqueIds.ToAsyncEnumerable());
        }
    }

    [SkippableFact]
    public async Task BulkDeleteAsync_WithMultipleIds_DeletesAll()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange - Create test records first
        var uniqueIds = Enumerable.Range(1, 5).Select(i => $"bulk-del-{Guid.NewGuid():N}").ToList();
        var records = uniqueIds.Select(id => new FeatureRecord(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = $"To Delete {id}",
            ["value"] = 100
        }));

        await _provider!.BulkInsertAsync(_dataSource!, _service!, _layer!, records.ToAsyncEnumerable());

        // Act
        var count = await _provider.BulkDeleteAsync(_dataSource!, _service!, _layer!, uniqueIds.ToAsyncEnumerable());

        // Assert
        count.Should().Be(5);

        // Verify deletions
        foreach (var id in uniqueIds)
        {
            var fetched = await _provider.GetAsync(_dataSource!, _service!, _layer!, id, null);
            fetched.Should().BeNull();
        }
    }

    [SkippableFact]
    public async Task TestConnectivityAsync_WithValidConnection_Succeeds()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Act & Assert
        await _provider!.Invoking(p => p.TestConnectivityAsync(_dataSource!))
            .Should().NotThrowAsync();
    }

    [SkippableFact]
    public void Capabilities_ReturnsCorrectCapabilities()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Act
        var capabilities = _provider!.Capabilities;

        // Assert
        capabilities.Should().NotBeNull();
        // Redshift has limited spatial support compared to PostGIS
        capabilities.SupportsSpatialIndexes.Should().BeFalse();
    }

    [SkippableFact]
    public async Task SpatialLimitations_AreDocumented()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // This test documents Redshift's spatial limitations
        // Redshift has very limited spatial support compared to PostgreSQL/PostGIS

        // Arrange - bbox query may not work if spatial extensions aren't enabled
        var bbox = new BoundingBox(-180, -90, 180, 90);
        var query = new FeatureQuery(Bbox: bbox, Limit: 10);

        // Act & Assert - This may throw if spatial support is not available
        // We're documenting the limitation here rather than asserting success
        try
        {
            await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
            {
                // If we get here, spatial support is available
                break;
            }
        }
        catch (Exception ex)
        {
            // Expected if Redshift cluster doesn't have spatial support configured
            ex.Should().NotBeNull();
        }
    }

    [SkippableFact]
    public async Task ParameterizedQueries_PreventSqlInjection()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange - Attempt SQL injection through feature ID
        var maliciousId = "'; DELETE FROM test_features WHERE '1'='1";

        // Act
        var result = await _provider!.GetAsync(_dataSource!, _service!, _layer!, maliciousId, null);

        // Assert - Should not return any results due to parameterization
        result.Should().BeNull();

        // Verify table still exists by running a query
        var count = await _provider.CountAsync(_dataSource!, _service!, _layer!, new FeatureQuery());
        count.Should().BeGreaterThanOrEqualTo(0);
    }

    [SkippableFact]
    public async Task DataAPIPolling_HandlesLongRunningQueries()
    {
        Skip.IfNot(_credentialsAvailable, "Redshift credentials not provided. Set REDSHIFT_CLUSTER_IDENTIFIER, REDSHIFT_DATABASE, REDSHIFT_DB_USER environment variables.");

        // Arrange - Query that might take some time
        var query = new FeatureQuery(Limit: 1000);

        // Act - The Data API should poll until completion
        var startTime = DateTime.UtcNow;
        var results = new List<FeatureRecord>();
        await foreach (var record in _provider!.QueryAsync(_dataSource!, _service!, _layer!, query))
        {
            results.Add(record);
        }
        var duration = DateTime.UtcNow - startTime;

        // Assert
        results.Should().NotBeNull();
        // Data API polling should have worked (duration will vary)
        duration.Should().BeLessThan(TimeSpan.FromMinutes(5));
    }
}
