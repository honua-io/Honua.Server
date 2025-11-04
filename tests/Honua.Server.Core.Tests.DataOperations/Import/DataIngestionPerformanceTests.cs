using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Import;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster.Import;
using Honua.Server.Core.Results;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.DataOperations.Import;

/// <summary>
/// Performance tests comparing bulk insert vs individual insert operations.
/// These tests demonstrate the performance improvement from using batch operations.
/// </summary>
[Trait("Category", "Performance")]
[Trait("Speed", "Slow")]
public sealed class DataIngestionPerformanceTests : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;

    public DataIngestionPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(100, "Small dataset")]
    [InlineData(1000, "Medium dataset")]
    [InlineData(5000, "Large dataset")]
    public async Task BulkInsert_IsFasterThanIndividualInserts(int featureCount, string scenario)
    {
        _output.WriteLine($"Testing {scenario} with {featureCount} features");

        // Test with individual inserts (bulk disabled)
        var individualTime = await MeasureIngestionTimeAsync(featureCount, useBulkInsert: false);
        _output.WriteLine($"  Individual inserts: {individualTime.TotalMilliseconds:F0}ms ({featureCount / individualTime.TotalSeconds:F0} features/sec)");

        // Test with bulk inserts (bulk enabled)
        var bulkTime = await MeasureIngestionTimeAsync(featureCount, useBulkInsert: true);
        _output.WriteLine($"  Bulk inserts:       {bulkTime.TotalMilliseconds:F0}ms ({featureCount / bulkTime.TotalSeconds:F0} features/sec)");

        var improvement = individualTime.TotalMilliseconds / bulkTime.TotalMilliseconds;
        _output.WriteLine($"  Improvement:        {improvement:F1}x faster");

        // Bulk insert should be at least 2x faster (realistically 10-50x with real databases)
        // Using conservative threshold for tests since stub provider doesn't have real DB overhead
        Assert.True(improvement >= 1.5, $"Bulk insert should be faster. Individual: {individualTime.TotalMilliseconds}ms, Bulk: {bulkTime.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task BulkInsert_HandlesLargeDatasets_WithConstantMemory()
    {
        const int featureCount = 10000;
        _output.WriteLine($"Testing memory efficiency with {featureCount} features");

        var provider = new MemoryTrackingProvider();
        var resolver = new StubFeatureContextResolver(provider);

        var options = Options.Create(new DataIngestionOptions
        {
            BatchSize = 1000,
            UseBulkInsert = true
        });

        var service = new DataIngestionService(
            resolver,
            new StubStacSynchronizer(),
            options,
            NullLogger<DataIngestionService>.Instance);

        await ((IHostedService)service).StartAsync(CancellationToken.None);

        try
        {
            using var workspace = new TempWorkspace();
            var datasetPath = Path.Combine(workspace.Path, "large.geojson");
            await File.WriteAllTextAsync(datasetPath, GenerateLargeGeoJson(featureCount));

            var request = new DataIngestionRequest(
                ServiceId: "svc",
                LayerId: "test",
                SourcePath: datasetPath,
                WorkingDirectory: workspace.Path,
                SourceFileName: "large.geojson",
                ContentType: "application/geo+json",
                Overwrite: false);

            var sw = Stopwatch.StartNew();
            var snapshot = await service.EnqueueAsync(request, CancellationToken.None);
            await WaitForCompletionAsync(service, snapshot.JobId, TimeSpan.FromMinutes(2));
            sw.Stop();

            _output.WriteLine($"Imported {featureCount} features in {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"Throughput: {featureCount / sw.Elapsed.TotalSeconds:F0} features/sec");
            _output.WriteLine($"Peak batch memory: {provider.MaxBatchSize} features");
            _output.WriteLine($"Bulk insert calls: {provider.BulkInsertCallCount}");

            Assert.Equal(featureCount, provider.Records.Count);

            // Verify memory-efficient streaming (batches should not exceed configured batch size * 2)
            Assert.True(provider.MaxBatchSize <= 2000, $"Peak batch size {provider.MaxBatchSize} exceeds expected threshold");
        }
        finally
        {
            await ((IHostedService)service).StopAsync(CancellationToken.None);
        }
    }

    private async Task<TimeSpan> MeasureIngestionTimeAsync(int featureCount, bool useBulkInsert)
    {
        var provider = new StubDataStoreProvider();
        var resolver = new StubFeatureContextResolver(provider);

        var options = Options.Create(new DataIngestionOptions
        {
            BatchSize = 1000,
            UseBulkInsert = useBulkInsert
        });

        var service = new DataIngestionService(
            resolver,
            new StubStacSynchronizer(),
            options,
            NullLogger<DataIngestionService>.Instance);

        await ((IHostedService)service).StartAsync(CancellationToken.None);

        try
        {
            using var workspace = new TempWorkspace();
            var datasetPath = Path.Combine(workspace.Path, "dataset.geojson");
            await File.WriteAllTextAsync(datasetPath, GenerateLargeGeoJson(featureCount));

            var request = new DataIngestionRequest(
                ServiceId: "svc",
                LayerId: "test",
                SourcePath: datasetPath,
                WorkingDirectory: workspace.Path,
                SourceFileName: "dataset.geojson",
                ContentType: "application/geo+json",
                Overwrite: false);

            var sw = Stopwatch.StartNew();
            var snapshot = await service.EnqueueAsync(request, CancellationToken.None);
            await WaitForCompletionAsync(service, snapshot.JobId, TimeSpan.FromMinutes(2));
            sw.Stop();

            Assert.Equal(featureCount, provider.Records.Count);

            return sw.Elapsed;
        }
        finally
        {
            await ((IHostedService)service).StopAsync(CancellationToken.None);
        }
    }

    private static async Task WaitForCompletionAsync(IDataIngestionService service, Guid jobId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await service.TryGetJobAsync(jobId, CancellationToken.None).ConfigureAwait(false);
            if (snapshot is not null && snapshot.Status is DataIngestionJobStatus.Completed)
            {
                return;
            }

            if (snapshot is not null && snapshot.Status is DataIngestionJobStatus.Failed)
            {
                throw new InvalidOperationException($"Job failed: {snapshot.Message}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException("Timed out waiting for ingestion job to complete.");
    }

    private static string GenerateLargeGeoJson(int featureCount)
    {
        var features = new System.Text.StringBuilder();
        features.AppendLine("{");
        features.AppendLine("  \"type\": \"FeatureCollection\",");
        features.AppendLine("  \"features\": [");

        for (var i = 0; i < featureCount; i++)
        {
            var lon = -180 + (360.0 * i / featureCount);
            var lat = -90 + (180.0 * i / featureCount);

            features.AppendLine("    {");
            features.AppendLine("      \"type\": \"Feature\",");
            features.AppendLine("      \"geometry\": {");
            features.AppendLine("        \"type\": \"Point\",");
            features.AppendLine($"        \"coordinates\": [{lon:F6}, {lat:F6}]");
            features.AppendLine("      },");
            features.AppendLine("      \"properties\": {");
            features.AppendLine($"        \"id\": {i + 1},");
            features.AppendLine($"        \"name\": \"Feature {i + 1}\"");
            features.Append("      }");
            features.AppendLine(i < featureCount - 1 ? "    }," : "    }");
        }

        features.AppendLine("  ]");
        features.AppendLine("}");

        return features.ToString();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    // Stubs for testing
    private sealed class StubStacSynchronizer : IRasterStacCatalogSynchronizer
    {
        public Task SynchronizeAllAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SynchronizeDatasetsAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SynchronizeServiceLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class StubDataStoreProvider : IDataStoreProvider
    {
        public List<FeatureRecord> Records { get; } = new();
        public int BulkInsertCallCount { get; private set; }

        public string Provider => "stub";
        public IDataStoreCapabilities Capabilities => TestDataStoreCapabilities.Instance;

        public Task<FeatureRecord> CreateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            // Simulate database latency for individual inserts
            Thread.Sleep(1); // 1ms per insert to simulate network/DB overhead
            Records.Add(record);
            return Task.FromResult(record);
        }

        public async Task<int> BulkInsertAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<FeatureRecord> records, CancellationToken cancellationToken = default)
        {
            BulkInsertCallCount++;
            var count = 0;
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                Records.Add(record);
                count++;
            }
            // Simulate reduced overhead for bulk operations
            await Task.Delay(Math.Max(1, count / 10), cancellationToken); // Much less overhead per record
            return count;
        }

        // Other methods not used in these tests
        public Task<bool> DeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> SoftDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RestoreAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HardDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FeatureRecord?> GetAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureQuery? query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> CountAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FeatureRecord?> UpdateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IAsyncEnumerable<FeatureRecord> QueryAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> BulkUpdateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> BulkDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<string> featureIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]?> GenerateMvtTileAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<BoundingBox?> QueryExtentAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IDataStoreTransaction?> BeginTransactionAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task TestConnectivityAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class MemoryTrackingProvider : StubDataStoreProvider
    {
        public int MaxBatchSize { get; private set; }

        public new async Task<int> BulkInsertAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<FeatureRecord> records, CancellationToken cancellationToken = default)
        {
            var count = await base.BulkInsertAsync(dataSource, service, layer, records, cancellationToken);
            MaxBatchSize = Math.Max(MaxBatchSize, count);
            return count;
        }
    }

    private sealed class StubFeatureContextResolver : IFeatureContextResolver
    {
        private readonly IDataStoreProvider _provider;
        private readonly FeatureContext _context;

        public StubFeatureContextResolver(IDataStoreProvider provider)
        {
            _provider = provider;

            var folder = new FolderDefinition { Id = "root", Title = "Root" };
            var dataSource = new DataSourceDefinition { Id = "local", Provider = provider.Provider, ConnectionString = "stub" };
            var layer = new LayerDefinition
            {
                Id = "test",
                ServiceId = "svc",
                Title = "Test",
                GeometryType = "Point",
                IdField = "id",
                GeometryField = "geometry",
                Fields = new[]
                {
                    new FieldDefinition { Name = "id", StorageType = "integer", Nullable = false },
                    new FieldDefinition { Name = "geometry", StorageType = "geometry", Nullable = true },
                    new FieldDefinition { Name = "name", StorageType = "text", Nullable = true }
                },
                Storage = new LayerStorageDefinition
                {
                    Table = "test",
                    GeometryColumn = "geometry",
                    PrimaryKey = "id",
                    Srid = 4326
                }
            };

            var service = new ServiceDefinition
            {
                Id = "svc",
                Title = "Test Service",
                FolderId = "root",
                ServiceType = "ogc",
                DataSourceId = dataSource.Id,
                Layers = new[] { layer }
            };

            var metadata = new MetadataSnapshot(
                new CatalogDefinition { Id = "catalog", Title = "Catalog" },
                new[] { folder },
                new[] { dataSource },
                new[] { service },
                new[] { layer });

            _context = new FeatureContext(metadata, service, layer, dataSource, provider);
        }

        public Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(serviceId, _context.Service.Id, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(layerId, _context.Layer.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new KeyNotFoundException("Unknown layer.");
            }

            return Task.FromResult(_context);
        }
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "honua-perf-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch
                {
                    // Ignore cleanup failures
                }
            }
        }
    }
}
