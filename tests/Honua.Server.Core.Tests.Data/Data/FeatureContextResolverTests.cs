using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Exceptions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Data;

[Trait("Category", "Unit")]
public class FeatureContextResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReturnContext_WhenServiceAndLayerExist()
    {
        var snapshot = CreateSnapshot();
        var registry = new InMemoryMetadataRegistry(snapshot);
        var providerFactory = new StubProviderFactory();
        var resolver = new FeatureContextResolver(registry, providerFactory);

        var context = await resolver.ResolveAsync("roads", "roads-primary");

        context.Service.Id.Should().Be("roads");
        context.Layer.Id.Should().Be("roads-primary");
        context.DataSource.Id.Should().Be("sqlite-primary");
        context.Provider.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNotFound_WhenServiceMissing()
    {
        var snapshot = CreateSnapshot();
        var registry = new InMemoryMetadataRegistry(snapshot);
        var providerFactory = new StubProviderFactory();
        var resolver = new FeatureContextResolver(registry, providerFactory);

        var act = async () => await resolver.ResolveAsync("unknown", "roads-primary");

        await act.Should().ThrowAsync<ServiceNotFoundException>()
            .WithMessage("Service 'unknown' was not found in metadata.");
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnInvalid_WhenProviderNotRegistered()
    {
        var snapshot = CreateSnapshot(dataSourceProvider: "oracle");
        var registry = new InMemoryMetadataRegistry(snapshot);
        var providerFactory = new StubProviderFactory(throwOnProvider: "oracle");
        var resolver = new FeatureContextResolver(registry, providerFactory);

        var act = async () => await resolver.ResolveAsync("roads", "roads-primary");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("No data store provider registered for 'oracle'.");
    }

    private static MetadataSnapshot CreateSnapshot(string dataSourceProvider = "sqlite")
    {
        var catalog = new CatalogDefinition { Id = "catalog", Title = "Catalog" };
        var folder = new FolderDefinition { Id = "transportation", Title = "Transportation" };
        var dataSource = new DataSourceDefinition
        {
            Id = "sqlite-primary",
            Provider = dataSourceProvider,
            ConnectionString = "Data Source=:memory:"
        };

        var service = new ServiceDefinition
        {
            Id = "roads",
            Title = "Roads",
            FolderId = "transportation",
            ServiceType = "feature",
            DataSourceId = dataSource.Id,
            Enabled = true,
            Ogc = new OgcServiceDefinition { CollectionsEnabled = true, DefaultCrs = "EPSG:4326" }
        };

        var layer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = service.Id,
            Title = "Primary Roads",
            GeometryType = "LineString",
            IdField = "road_id",
            GeometryField = "geom",
            Fields = Array.Empty<FieldDefinition>()
        };

        return new MetadataSnapshot(
            catalog,
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            new[] { layer });
    }

    private sealed class InMemoryMetadataRegistry : IMetadataRegistry
    {
        public InMemoryMetadataRegistry(MetadataSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public MetadataSnapshot Snapshot { get; }

        public bool IsInitialized => true;

        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Snapshot);

        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReloadAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public IChangeToken GetChangeToken() => TestChangeTokens.Noop;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = Snapshot;
            return true;
        }
    }

    private sealed class StubProviderFactory : IDataStoreProviderFactory
    {
        private readonly string? _throwOnProvider;

        public StubProviderFactory(string? throwOnProvider = null)
        {
            _throwOnProvider = throwOnProvider;
        }

        public IDataStoreProvider Create(string providerName)
        {
            if (string.Equals(providerName, _throwOnProvider, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"No data store provider registered for '{providerName}'.");
            }

            return new NullDataStoreProvider(providerName);
        }
    }

    private sealed class NullDataStoreProvider : IDataStoreProvider
    {
        public NullDataStoreProvider(string provider)
        {
            Provider = provider;
        }

        public string Provider { get; }

        public IDataStoreCapabilities Capabilities => TestDataStoreCapabilities.Instance;

        public IAsyncEnumerable<FeatureRecord> QueryAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord?> GetAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            FeatureQuery? query,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord> CreateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<FeatureRecord?> UpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            FeatureRecord record,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> SoftDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> RestoreAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> HardDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            string featureId,
            string? deletedBy,
            IDataStoreTransaction? transaction = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkInsertAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<FeatureRecord> records,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkUpdateAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkDeleteAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IAsyncEnumerable<string> featureIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<byte[]?> GenerateMvtTileAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            int zoom,
            int x,
            int y,
            string? datetime = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<byte[]?>(null);

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IReadOnlyList<StatisticDefinition> statistics,
            IReadOnlyList<string>? groupByFields,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            IReadOnlyList<string> fieldNames,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<BoundingBox?> QueryExtentAsync(
            DataSourceDefinition dataSource,
            ServiceDefinition service,
            LayerDefinition layer,
            FeatureQuery? filter,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IDataStoreTransaction?> BeginTransactionAsync(
            DataSourceDefinition dataSource,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task TestConnectivityAsync(
            DataSourceDefinition dataSource,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
