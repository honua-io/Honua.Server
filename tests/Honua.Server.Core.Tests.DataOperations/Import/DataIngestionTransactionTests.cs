using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace Honua.Server.Core.Tests.DataOperations.Import;

[Trait("Category", "Unit")]
[Trait("Feature", "DataIngestion")]
[Trait("Priority", "P0")]
public sealed class DataIngestionTransactionTests : IAsyncDisposable
{
    private readonly StubDataStoreProvider _provider = new();
    private readonly StubFeatureContextResolver _resolver;
    private readonly StubStacSynchronizer _stacSynchronizer = new();
    private readonly string _queueDirectory;
    private readonly FileDataIngestionQueueStore _queueStore;
    private readonly DataIngestionService _service;

    public DataIngestionTransactionTests()
    {
        _resolver = new StubFeatureContextResolver(_provider);
        _queueDirectory = Path.Combine(Path.GetTempPath(), "honua-tests", "ingestion-txn", Guid.NewGuid().ToString("N"));
        _queueStore = new FileDataIngestionQueueStore(_queueDirectory, NullLogger<FileDataIngestionQueueStore>.Instance);

        var options = Options.Create(new DataIngestionOptions
        {
            UseBulkInsert = true,
            UseTransactionalIngestion = true,
            BatchSize = 1000,
            ProgressReportInterval = 100,
            TransactionTimeout = TimeSpan.FromMinutes(5)
        });

        _service = new DataIngestionService(_resolver, _stacSynchronizer, options, NullLogger<DataIngestionService>.Instance);
        ((IHostedService)_service).StartAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task SuccessfulIngestion_CommitsTransaction()
    {
        using var workspace = new TempWorkspace();
        var datasetPath = Path.Combine(workspace.Path, "test.geojson");
        await File.WriteAllTextAsync(datasetPath, SampleGeoJson);

        var request = new DataIngestionRequest(
            ServiceId: "svc",
            LayerId: "roads",
            SourcePath: datasetPath,
            WorkingDirectory: workspace.Path,
            SourceFileName: "test.geojson",
            ContentType: "application/geo+json",
            Overwrite: false);

        var snapshot = await _service.EnqueueAsync(request, CancellationToken.None);
        await WaitForCompletionAsync(snapshot.JobId, TimeSpan.FromSeconds(30));

        // Verify transaction was committed
        Assert.True(_provider.LastTransaction?.IsCommitted ?? false);
        Assert.False(_provider.LastTransaction?.IsRolledBack ?? false);

        // Verify features were inserted
        Assert.Equal(2, _provider.Records.Count);
    }

    [Fact]
    public async Task FailedIngestion_RollsBackTransaction()
    {
        using var workspace = new TempWorkspace();
        var datasetPath = Path.Combine(workspace.Path, "test.geojson");
        await File.WriteAllTextAsync(datasetPath, SampleGeoJson);

        // Configure provider to throw on 2nd feature
        _provider.FailOnFeatureNumber = 2;

        var request = new DataIngestionRequest(
            ServiceId: "svc",
            LayerId: "roads",
            SourcePath: datasetPath,
            WorkingDirectory: workspace.Path,
            SourceFileName: "test.geojson",
            ContentType: "application/geo+json",
            Overwrite: false);

        var snapshot = await _service.EnqueueAsync(request, CancellationToken.None);
        await WaitForFailureAsync(snapshot.JobId, TimeSpan.FromSeconds(30));

        // Verify transaction was rolled back
        Assert.False(_provider.LastTransaction?.IsCommitted ?? false);
        Assert.True(_provider.LastTransaction?.IsRolledBack ?? false);

        // Verify no features were persisted (all-or-nothing)
        Assert.Empty(_provider.CommittedRecords);
    }

    [Fact]
    public async Task CancelledIngestion_RollsBackTransaction()
    {
        using var workspace = new TempWorkspace();
        var datasetPath = Path.Combine(workspace.Path, "large.geojson");
        await File.WriteAllTextAsync(datasetPath, GenerateLargeGeoJson(1000));

        var request = new DataIngestionRequest(
            ServiceId: "svc",
            LayerId: "roads",
            SourcePath: datasetPath,
            WorkingDirectory: workspace.Path,
            SourceFileName: "large.geojson",
            ContentType: "application/geo+json",
            Overwrite: false);

        var snapshot = await _service.EnqueueAsync(request, CancellationToken.None);

        // Cancel after a brief delay
        await Task.Delay(100);
        await _service.CancelAsync(snapshot.JobId, "Test cancellation");

        await WaitForCancellationAsync(snapshot.JobId, TimeSpan.FromSeconds(30));

        // Verify transaction was rolled back
        Assert.False(_provider.LastTransaction?.IsCommitted ?? false);
        Assert.True(_provider.LastTransaction?.IsRolledBack ?? false);

        // Verify no features were persisted
        Assert.Empty(_provider.CommittedRecords);
    }

    [Fact]
    public async Task TransactionsDisabled_NoTransactionUsed()
    {
        await DisposeAsync();

        var provider = new StubDataStoreProvider();
        var resolver = new StubFeatureContextResolver(provider);
        var stacSynchronizer = new StubStacSynchronizer();

        var options = Options.Create(new DataIngestionOptions
        {
            UseBulkInsert = true,
            UseTransactionalIngestion = false, // Disabled
            BatchSize = 1000
        });

        var service = new DataIngestionService(resolver, stacSynchronizer, options, NullLogger<DataIngestionService>.Instance);
        await ((IHostedService)service).StartAsync(CancellationToken.None);

        try
        {
            using var workspace = new TempWorkspace();
            var datasetPath = Path.Combine(workspace.Path, "test.geojson");
            await File.WriteAllTextAsync(datasetPath, SampleGeoJson);

            var request = new DataIngestionRequest(
                ServiceId: "svc",
                LayerId: "roads",
                SourcePath: datasetPath,
                WorkingDirectory: workspace.Path,
                SourceFileName: "test.geojson",
                ContentType: "application/geo+json",
                Overwrite: false);

            var snapshot = await service.EnqueueAsync(request, CancellationToken.None);
            await WaitForCompletionAsync(snapshot.JobId, TimeSpan.FromSeconds(30), service);

            // Verify no transaction was created
            Assert.Null(provider.LastTransaction);

            // But features were still inserted
            Assert.Equal(2, provider.Records.Count);
        }
        finally
        {
            await ((IHostedService)service).StopAsync(CancellationToken.None);
        }
    }

    private async Task WaitForCompletionAsync(Guid jobId, TimeSpan timeout, IDataIngestionService? serviceOverride = null)
    {
        var service = serviceOverride ?? _service;
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await service.TryGetJobAsync(jobId, CancellationToken.None).ConfigureAwait(false);
            if (snapshot is not null && snapshot.Status is DataIngestionJobStatus.Completed)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("Timed out waiting for ingestion job to complete.");
    }

    private async Task WaitForFailureAsync(Guid jobId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await _service.TryGetJobAsync(jobId, CancellationToken.None).ConfigureAwait(false);
            if (snapshot is not null && snapshot.Status is DataIngestionJobStatus.Failed)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("Timed out waiting for ingestion job to fail.");
    }

    private async Task WaitForCancellationAsync(Guid jobId, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await _service.TryGetJobAsync(jobId, CancellationToken.None).ConfigureAwait(false);
            if (snapshot is not null && snapshot.Status is DataIngestionJobStatus.Cancelled)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }

        throw new TimeoutException("Timed out waiting for ingestion job to be cancelled.");
    }

    private sealed class StubStacSynchronizer : IRasterStacCatalogSynchronizer
    {
        public ConcurrentBag<(string ServiceId, string LayerId)> Calls { get; } = new();

        public Task SynchronizeAllAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SynchronizeDatasetsAsync(IEnumerable<string> datasetIds, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SynchronizeServiceLayerAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
        {
            Calls.Add((serviceId, layerId));
            return Task.CompletedTask;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ((IHostedService)_service).StopAsync(CancellationToken.None);
        if (Directory.Exists(_queueDirectory))
        {
            Directory.Delete(_queueDirectory, recursive: true);
        }
    }

    private sealed class StubDataStoreProvider : IDataStoreProvider
    {
        public ConcurrentBag<FeatureRecord> Records { get; } = new();
        public ConcurrentBag<FeatureRecord> CommittedRecords { get; } = new();
        public StubDataStoreTransaction? LastTransaction { get; private set; }
        public int? FailOnFeatureNumber { get; set; }
        private int _featureCount = 0;

        public string Provider => "stub";
        public IDataStoreCapabilities Capabilities => TestDataStoreCapabilities.Instance;

        public Task<bool> DeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord> CreateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.FromResult(record);
        }

        public Task<FeatureRecord?> GetAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord?> UpdateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<FeatureRecord> QueryAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async Task<int> BulkInsertAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<FeatureRecord> records, CancellationToken cancellationToken = default)
        {
            var count = 0;
            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                _featureCount++;
                if (FailOnFeatureNumber.HasValue && _featureCount == FailOnFeatureNumber.Value)
                {
                    throw new InvalidOperationException($"Simulated failure at feature {_featureCount}");
                }

                Records.Add(record);
                count++;
            }
            return count;
        }

        public Task<int> BulkUpdateAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IAsyncEnumerable<string> featureIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SoftDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RestoreAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HardDeleteAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, string featureId, string? deletedBy, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<byte[]?> GenerateMvtTileAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BoundingBox?> QueryExtentAsync(DataSourceDefinition dataSource, ServiceDefinition service, LayerDefinition layer, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IDataStoreTransaction?> BeginTransactionAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default)
        {
            LastTransaction = new StubDataStoreTransaction(this);
            return Task.FromResult<IDataStoreTransaction?>(LastTransaction);
        }

        public Task TestConnectivityAsync(DataSourceDefinition dataSource, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubDataStoreTransaction : IDataStoreTransaction
    {
        private readonly StubDataStoreProvider _provider;
        private bool _committed;
        private bool _rolledBack;

        public StubDataStoreTransaction(StubDataStoreProvider provider)
        {
            _provider = provider;
        }

        public bool IsCommitted => _committed;
        public bool IsRolledBack => _rolledBack;

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            _committed = true;
            // Move records to committed collection
            foreach (var record in _provider.Records)
            {
                _provider.CommittedRecords.Add(record);
            }
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            _rolledBack = true;
            // Clear uncommitted records
            _provider.Records.Clear();
            return Task.CompletedTask;
        }

        public object GetUnderlyingTransaction() => this;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubFeatureContextResolver : IFeatureContextResolver
    {
        private readonly StubDataStoreProvider _provider;
        private readonly FeatureContext _context;

        public StubFeatureContextResolver(StubDataStoreProvider provider)
        {
            _provider = provider;

            var folder = new FolderDefinition
            {
                Id = "root",
                Title = "Root"
            };

            var dataSource = new DataSourceDefinition
            {
                Id = "local",
                Provider = provider.Provider,
                ConnectionString = "stub"
            };

            var layer = new LayerDefinition
            {
                Id = "roads",
                ServiceId = "svc",
                Title = "Roads",
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
                    Table = "roads",
                    GeometryColumn = "geometry",
                    PrimaryKey = "id",
                    Srid = 4326
                }
            };

            var service = new ServiceDefinition
            {
                Id = "svc",
                Title = "Transport",
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "honua-ingest-txn-test", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private static string GenerateLargeGeoJson(int featureCount)
    {
        var features = new List<string>();
        for (var i = 1; i <= featureCount; i++)
        {
            features.Add($@"
    {{
      ""type"": ""Feature"",
      ""geometry"": {{
        ""type"": ""Point"",
        ""coordinates"": [{i * 0.1}, {i * 0.1}]
      }},
      ""properties"": {{
        ""id"": {i},
        ""name"": ""Feature_{i}""
      }}
    }}");
        }

        return $@"{{
  ""type"": ""FeatureCollection"",
  ""features"": [{string.Join(",", features)}]
}}";
    }

    private const string SampleGeoJson = """
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [0.0, 0.0]
      },
      "properties": {
        "id": 1,
        "name": "Alpha"
      }
    },
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [1.0, 1.0]
      },
      "properties": {
        "id": 2,
        "name": "Bravo"
      }
    }
  ]
}
""";
}
