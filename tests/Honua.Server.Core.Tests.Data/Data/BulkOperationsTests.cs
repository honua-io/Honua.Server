using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Unit tests for bulk operation methods across all data store providers.
/// These tests verify bulk insert, update, and delete operations.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Data")]
[Trait("Speed", "Fast")]
public class BulkOperationsTests
{
    [Fact]
    public async Task BulkInsertAsync_WithMultipleRecords_ShouldReturnCorrectCount()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var records = CreateTestRecords(100);

        // Act
        var count = await provider.BulkInsertAsync(dataSource, service, layer, records);

        // Assert
        count.Should().Be(100);
        provider.InsertedCount.Should().Be(100);
    }

    [Fact]
    public async Task BulkInsertAsync_WithEmptyRecords_ShouldReturnZero()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var records = CreateEmptyRecords();

        // Act
        var count = await provider.BulkInsertAsync(dataSource, service, layer, records);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithMultipleRecords_ShouldReturnCorrectCount()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var updates = CreateTestUpdates(50);

        // Act
        var count = await provider.BulkUpdateAsync(dataSource, service, layer, updates);

        // Assert
        count.Should().Be(50);
        provider.UpdatedCount.Should().Be(50);
    }

    [Fact]
    public async Task BulkUpdateAsync_WithEmptyUpdates_ShouldReturnZero()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var updates = CreateEmptyUpdates();

        // Act
        var count = await provider.BulkUpdateAsync(dataSource, service, layer, updates);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithMultipleIds_ShouldReturnCorrectCount()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var ids = CreateTestIds(75);

        // Act
        var count = await provider.BulkDeleteAsync(dataSource, service, layer, ids);

        // Assert
        count.Should().Be(75);
        provider.DeletedCount.Should().Be(75);
    }

    [Fact]
    public async Task BulkDeleteAsync_WithEmptyIds_ShouldReturnZero()
    {
        // Arrange
        var provider = new MockDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var ids = CreateEmptyIds();

        // Act
        var count = await provider.BulkDeleteAsync(dataSource, service, layer, ids);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkOperations_ShouldSupportCancellation()
    {
        // Arrange
        var provider = new MockDataStoreProvider { SimulateSlowOperation = true };
        var (dataSource, service, layer) = CreateTestMetadata();
        var records = CreateTestRecords(1000);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));
        var act = async () => await provider.BulkInsertAsync(dataSource, service, layer, records, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static (DataSourceDefinition, ServiceDefinition, LayerDefinition) CreateTestMetadata()
    {
        var dataSource = new DataSourceDefinition
        {
            Id = "test-ds",
            Provider = "mock",
            ConnectionString = "test-connection"
        };

        var service = new ServiceDefinition
        {
            Id = "test-service",
            Title = "Test Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "test-ds",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            }
        };

        var layer = new LayerDefinition
        {
            Id = "test-layer",
            ServiceId = "test-service",
            Title = "Test Layer",
            GeometryType = "point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "name", DataType = "string" },
                new() { Name = "geom", DataType = "geometry" }
            },
            Storage = new LayerStorageDefinition
            {
                Table = "test_table",
                PrimaryKey = "id",
                GeometryColumn = "geom",
                Srid = 4326
            }
        };

        return (dataSource, service, layer);
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateTestRecords(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var attributes = new Dictionary<string, object?>
            {
                ["id"] = i + 1,
                ["name"] = $"Feature {i + 1}",
                ["geom"] = JsonNode.Parse("""{"type":"Point","coordinates":[-122.5,45.5]}""")
            };

            yield return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> CreateTestUpdates(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var id = (i + 1).ToString(CultureInfo.InvariantCulture);
            var attributes = new Dictionary<string, object?>
            {
                ["name"] = $"Updated Feature {i + 1}",
                ["geom"] = JsonNode.Parse("""{"type":"Point","coordinates":[-122.6,45.6]}""")
            };

            var record = new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
            yield return new KeyValuePair<string, FeatureRecord>(id, record);
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<string> CreateTestIds(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return (i + 1).ToString(CultureInfo.InvariantCulture);
        }

        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateEmptyRecords()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> CreateEmptyUpdates()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<string> CreateEmptyIds()
    {
        await Task.CompletedTask;
        yield break;
    }

    // Mock provider for testing
    private class MockDataStoreProvider : IDataStoreProvider
    {
        public int InsertedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int DeletedCount { get; private set; }
        public bool SimulateSlowOperation { get; set; }

        public string Provider => "mock";
        public IDataStoreCapabilities Capabilities => throw new NotImplementedException();

        public async Task<int> BulkInsertAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<FeatureRecord> records,
            CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                if (SimulateSlowOperation)
                {
                    await Task.Delay(10, cancellationToken);
                }
                count++;
            }
            InsertedCount = count;
            return count;
        }

        public async Task<int> BulkUpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
            CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var update in updates.WithCancellation(cancellationToken))
            {
                if (SimulateSlowOperation)
                {
                    await Task.Delay(10, cancellationToken);
                }
                count++;
            }
            UpdatedCount = count;
            return count;
        }

        public async Task<int> BulkDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<string> featureIds,
            CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var id in featureIds.WithCancellation(cancellationToken))
            {
                if (SimulateSlowOperation)
                {
                    await Task.Delay(10, cancellationToken);
                }
                count++;
            }
            DeletedCount = count;
            return count;
        }

        // Other IDataStoreProvider methods (not used in these tests)
        public IAsyncEnumerable<FeatureRecord> QueryAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<long> CountAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<FeatureRecord?> GetAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<FeatureRecord> CreateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<FeatureRecord?> UpdateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> DeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> SoftDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> RestoreAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<bool> HardDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<byte[]?> GenerateMvtTileAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<BoundingBox?> QueryExtentAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<IDataStoreTransaction?> BeginTransactionAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task TestConnectivityAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
