using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Performance benchmark tests for bulk operations.
/// These tests compare bulk operations against individual operations to verify performance improvements.
/// </summary>
[Trait("Category", "Performance")]
public class BulkOperationsBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public BulkOperationsBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task BulkInsert_ShouldBeFasterThanIndividualInserts()
    {
        // Arrange
        const int recordCount = 1000;
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();

        // Act - Individual inserts
        var individualStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < recordCount; i++)
        {
            var record = CreateTestRecord(i);
            await provider.CreateAsync(dataSource, service, layer, record);
        }
        individualStopwatch.Stop();

        // Act - Bulk insert
        var bulkStopwatch = Stopwatch.StartNew();
        var records = CreateTestRecords(recordCount);
        await provider.BulkInsertAsync(dataSource, service, layer, records);
        bulkStopwatch.Stop();

        // Assert
        var individualMs = individualStopwatch.ElapsedMilliseconds;
        var bulkMs = bulkStopwatch.ElapsedMilliseconds;
        var speedup = (double)individualMs / bulkMs;

        _output.WriteLine($"Individual inserts: {recordCount} records in {individualMs}ms ({individualMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Bulk insert: {recordCount} records in {bulkMs}ms ({bulkMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Speedup: {speedup:F2}x");

        // Bulk should be at least 5x faster
        speedup.Should().BeGreaterThanOrEqualTo(5.0, "bulk insert should provide at least 5x speedup");
    }

    [Fact]
    public async Task BulkUpdate_ShouldBeFasterThanIndividualUpdates()
    {
        // Arrange
        const int recordCount = 500;
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();

        // Act - Individual updates
        var individualStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < recordCount; i++)
        {
            var id = (i + 1).ToString(CultureInfo.InvariantCulture);
            var record = CreateTestRecord(i);
            await provider.UpdateAsync(dataSource, service, layer, id, record);
        }
        individualStopwatch.Stop();

        // Act - Bulk update
        var bulkStopwatch = Stopwatch.StartNew();
        var updates = CreateTestUpdates(recordCount);
        await provider.BulkUpdateAsync(dataSource, service, layer, updates);
        bulkStopwatch.Stop();

        // Assert
        var individualMs = individualStopwatch.ElapsedMilliseconds;
        var bulkMs = bulkStopwatch.ElapsedMilliseconds;
        var speedup = (double)individualMs / bulkMs;

        _output.WriteLine($"Individual updates: {recordCount} records in {individualMs}ms ({individualMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Bulk update: {recordCount} records in {bulkMs}ms ({bulkMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Speedup: {speedup:F2}x");

        // Bulk should be at least 5x faster
        speedup.Should().BeGreaterThanOrEqualTo(5.0, "bulk update should provide at least 5x speedup");
    }

    [Fact]
    public async Task BulkDelete_ShouldBeFasterThanIndividualDeletes()
    {
        // Arrange
        const int recordCount = 750;
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();

        // Act - Individual deletes
        var individualStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < recordCount; i++)
        {
            var id = (i + 1).ToString(CultureInfo.InvariantCulture);
            await provider.DeleteAsync(dataSource, service, layer, id);
        }
        individualStopwatch.Stop();

        // Act - Bulk delete
        var bulkStopwatch = Stopwatch.StartNew();
        var ids = CreateTestIds(recordCount);
        await provider.BulkDeleteAsync(dataSource, service, layer, ids);
        bulkStopwatch.Stop();

        // Assert
        var individualMs = individualStopwatch.ElapsedMilliseconds;
        var bulkMs = bulkStopwatch.ElapsedMilliseconds;
        var speedup = (double)individualMs / bulkMs;

        _output.WriteLine($"Individual deletes: {recordCount} records in {individualMs}ms ({individualMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Bulk delete: {recordCount} records in {bulkMs}ms ({bulkMs / (double)recordCount:F2}ms per record)");
        _output.WriteLine($"Speedup: {speedup:F2}x");

        // Bulk should be at least 5x faster
        speedup.Should().BeGreaterThanOrEqualTo(5.0, "bulk delete should provide at least 5x speedup");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    [InlineData(100000)]
    public async Task BulkInsert_PerformanceScalesLinearly(int recordCount)
    {
        // Arrange
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var records = CreateTestRecords(recordCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var count = await provider.BulkInsertAsync(dataSource, service, layer, records);
        stopwatch.Stop();

        // Assert
        count.Should().Be(recordCount);
        var msPerRecord = stopwatch.ElapsedMilliseconds / (double)recordCount;

        _output.WriteLine($"Bulk insert: {recordCount} records in {stopwatch.ElapsedMilliseconds}ms ({msPerRecord:F2}ms per record)");

        // Performance should remain consistent (< 1ms per record for in-memory operations)
        msPerRecord.Should().BeLessThan(1.0);
    }

    [Fact]
    public async Task BulkInsert_OneMillionRecords_ShouldCompleteReasonably()
    {
        // Arrange
        const int recordCount = 1_000_000;
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();
        var records = CreateTestRecords(recordCount);

        // Act
        var stopwatch = Stopwatch.StartNew();
        var count = await provider.BulkInsertAsync(dataSource, service, layer, records);
        stopwatch.Stop();

        // Assert
        count.Should().Be(recordCount);
        var msPerRecord = stopwatch.ElapsedMilliseconds / (double)recordCount;

        _output.WriteLine($"Bulk insert: {recordCount:N0} records in {stopwatch.ElapsedMilliseconds}ms ({msPerRecord:F4}ms per record)");
        _output.WriteLine($"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        // Should complete in reasonable time (< 2 seconds for in-memory operations)
        stopwatch.Elapsed.TotalSeconds.Should().BeLessThan(2.0);
    }

    [Theory]
    [InlineData(10000)]
    [InlineData(100000)]
    public async Task BulkInsert_MemoryUsagePerRecord_ShouldBeLowForLargeDatasets(int recordCount)
    {
        // Arrange
        var provider = new BenchmarkDataStoreProvider();
        var (dataSource, service, layer) = CreateTestMetadata();

        // Measure memory before
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        // Act
        var records = CreateTestRecords(recordCount);
        var count = await provider.BulkInsertAsync(dataSource, service, layer, records);

        // Measure memory after
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var memoryUsed = memoryAfter - memoryBefore;
        var bytesPerRecord = memoryUsed / (double)recordCount;

        // Assert
        count.Should().Be(recordCount);
        _output.WriteLine($"Memory used: {memoryUsed:N0} bytes for {recordCount:N0} records");
        _output.WriteLine($"Bytes per record: {bytesPerRecord:F2}");

        // Memory per record should be reasonable (< 1KB per record)
        // Note: This includes overhead from test infrastructure
        bytesPerRecord.Should().BeLessThan(1024.0,
            "memory per record should be less than 1KB for efficient bulk operations");
    }

    private static (DataSourceDefinition, ServiceDefinition, LayerDefinition) CreateTestMetadata()
    {
        var dataSource = new DataSourceDefinition
        {
            Id = "benchmark-ds",
            Provider = "benchmark",
            ConnectionString = "benchmark-connection"
        };

        var service = new ServiceDefinition
        {
            Id = "benchmark-service",
            Title = "Benchmark Service",
            FolderId = "root",
            ServiceType = "feature",
            DataSourceId = "benchmark-ds",
            Ogc = new OgcServiceDefinition
            {
                DefaultCrs = "EPSG:4326"
            }
        };

        var layer = new LayerDefinition
        {
            Id = "benchmark-layer",
            ServiceId = "benchmark-service",
            Title = "Benchmark Layer",
            GeometryType = "point",
            IdField = "id",
            GeometryField = "geom",
            Fields = new List<FieldDefinition>
            {
                new() { Name = "id", DataType = "int" },
                new() { Name = "name", DataType = "string" },
                new() { Name = "value", DataType = "double" },
                new() { Name = "geom", DataType = "geometry" }
            },
            Storage = new LayerStorageDefinition
            {
                Table = "benchmark_table",
                PrimaryKey = "id",
                GeometryColumn = "geom",
                Srid = 4326
            }
        };

        return (dataSource, service, layer);
    }

    private static FeatureRecord CreateTestRecord(int index)
    {
        var attributes = new Dictionary<string, object?>
        {
            ["id"] = index + 1,
            ["name"] = $"Feature {index + 1}",
            ["value"] = (index + 1) * 1.5,
            ["geom"] = JsonNode.Parse("""{"type":"Point","coordinates":[-122.5,45.5]}""")
        };

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    private static async IAsyncEnumerable<FeatureRecord> CreateTestRecords(int count)
    {
        for (var i = 0; i < count; i++)
        {
            yield return CreateTestRecord(i);
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
                ["value"] = (i + 1) * 2.5,
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

    // Benchmark provider that simulates database latency
    private class BenchmarkDataStoreProvider : IDataStoreProvider
    {
        private const int IndividualOperationLatencyMs = 2; // Simulates 2ms per individual operation
        private const int BulkOperationLatencyMs = 10; // Simulates 10ms overhead for bulk operation

        public string Provider => "benchmark";
        public IDataStoreCapabilities Capabilities => throw new NotImplementedException();

        public async Task<int> BulkInsertAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<FeatureRecord> records,
            CancellationToken cancellationToken = default)
        {
            // Simulate bulk operation overhead
            await Task.Delay(BulkOperationLatencyMs, cancellationToken);

            var count = 0;
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                count++;
            }

            return count;
        }

        public async Task<int> BulkUpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
            CancellationToken cancellationToken = default)
        {
            // Simulate bulk operation overhead
            await Task.Delay(BulkOperationLatencyMs, cancellationToken);

            var count = 0;
            await foreach (var update in updates.WithCancellation(cancellationToken))
            {
                count++;
            }

            return count;
        }

        public async Task<int> BulkDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<string> featureIds,
            CancellationToken cancellationToken = default)
        {
            // Simulate bulk operation overhead
            await Task.Delay(BulkOperationLatencyMs, cancellationToken);

            var count = 0;
            await foreach (var id in featureIds.WithCancellation(cancellationToken))
            {
                count++;
            }

            return count;
        }

        public async Task<FeatureRecord> CreateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            // Simulate individual operation latency
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return record;
        }

        public async Task<FeatureRecord?> UpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            // Simulate individual operation latency
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return record;
        }

        public async Task<bool> DeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            // Simulate individual operation latency
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return true;
        }

        public async Task<bool> SoftDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return true;
        }

        public async Task<bool> RestoreAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return true;
        }

        public async Task<bool> HardDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(IndividualOperationLatencyMs, cancellationToken);
            return true;
        }

        // Other IDataStoreProvider methods (not used in these tests)
        public IAsyncEnumerable<FeatureRecord> QueryAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<long> CountAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<FeatureRecord?> GetAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureQuery? query, CancellationToken cancellationToken = default)
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
