using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Stac;
using Honua.Server.Core.Stac.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Stac;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterStacCatalogSynchronizerTests
{
    [Fact]
    public async Task SynchronizeAllAsync_PopulatesCatalog()
    {
        using var harness = CreateHarness();

        await harness.Synchronizer.SynchronizeAllAsync();

        var collections = await harness.Store.ListCollectionsAsync();
        collections.Should().HaveCount(4);
        collections.Select(c => c.Id).Should().Contain(new[] { "dataset-1", "dataset-2", "layer-1", "layer-2" });

        var vectorItems = await harness.Store.ListItemsAsync("layer-1", 10);
        vectorItems.Should().HaveCount(1);
        vectorItems[0].Id.Should().Be("layer-1-overview");
    }

    [Fact]
    public async Task SynchronizeAllAsync_RemovesAbsentCollections()
    {
        using var harness = CreateHarness();

        await harness.Synchronizer.SynchronizeAllAsync();

        harness.Provider.UpdateSnapshot(harness.SnapshotWithoutDataset1);
        await harness.Registry.ReloadAsync();

        await harness.Synchronizer.SynchronizeAllAsync();

        var collections = await harness.Store.ListCollectionsAsync();
        collections.Select(c => c.Id).Should().BeEquivalentTo(new[] { "dataset-2", "layer-1", "layer-2" });
    }

    [Fact]
    public async Task SynchronizeServiceLayerAsync_UpdatesTargetDataset()
    {
        using var harness = CreateHarness();

        await harness.Synchronizer.SynchronizeServiceLayerAsync("svc", "layer-2");

        var collections = await harness.Store.ListCollectionsAsync();
        collections.Select(c => c.Id).Should().BeEquivalentTo(new[] { "dataset-2", "layer-2" });
    }

    [Fact]
    public async Task SynchronizeDatasetsAsync_DeletesRemovedDataset()
    {
        using var harness = CreateHarness();

        await harness.Synchronizer.SynchronizeDatasetsAsync(new[] { "dataset-1" });

        harness.Provider.UpdateSnapshot(harness.SnapshotWithoutDataset1);
        await harness.Registry.ReloadAsync();

        await harness.Synchronizer.SynchronizeDatasetsAsync(new[] { "dataset-1" });

        var collections = await harness.Store.ListCollectionsAsync();
        collections.Should().BeEmpty();
    }

    private static SynchronizerHarness CreateHarness()
    {
        var folder = new FolderDefinition
        {
            Id = "root",
            Title = "Root"
        };

        var dataSource = new DataSourceDefinition
        {
            Id = "primary",
            Provider = "sqlite",
            ConnectionString = "Data Source=:memory:"
        };

        var layer1 = new LayerDefinition
        {
            Id = "layer-1",
            ServiceId = "svc",
            Title = "Layer 1",
            GeometryType = "polygon",
            IdField = "id",
            GeometryField = "geom",
            Crs = new[] { "EPSG:4326" },
            Stac = new StacMetadata
            {
                Enabled = true,
                CollectionId = "layer-1"
            }
        };

        var layer2 = layer1 with
        {
            Id = "layer-2",
            Title = "Layer 2",
            Stac = new StacMetadata
            {
                Enabled = true,
                CollectionId = "layer-2"
            }
        };

        var service = new ServiceDefinition
        {
            Id = "svc",
            Title = "Raster Service",
            FolderId = folder.Id,
            ServiceType = "raster",
            DataSourceId = dataSource.Id,
            Layers = new[] { layer1, layer2 }
        };

        var dataset1 = new RasterDatasetDefinition
        {
            Id = "dataset-1",
            Title = "Dataset 1",
            ServiceId = service.Id,
            LayerId = layer1.Id,
            Crs = new[] { "EPSG:4326" },
            Source = new RasterSourceDefinition
            {
                Type = "cog",
                Uri = "https://example.com/dataset-1.tif"
            }
        };

        var dataset2 = dataset1 with
        {
            Id = "dataset-2",
            Title = "Dataset 2",
            LayerId = layer2.Id,
            Source = dataset1.Source with { Uri = "https://example.com/dataset-2.tif" }
        };

        var snapshot = new MetadataSnapshot(
            new CatalogDefinition { Id = "catalog", Title = "Catalog" },
            new[] { folder },
            new[] { dataSource },
            new[] { service },
            new[] { layer1, layer2 },
            new[] { dataset1, dataset2 });

        var provider = new MutableMetadataProvider(snapshot);
        var registry = new MetadataRegistry(provider);

        var configuration = new HonuaConfiguration
        {
            Metadata = new MetadataConfiguration
            {
                Provider = "json",
                Path = "stub"
            },
            Services = new ServicesConfiguration
            {
                Stac = new StacCatalogConfiguration
                {
                    Enabled = true,
                    Provider = "sqlite",
                    ConnectionString = "Data Source=:memory:"
                }
            }
        };

        var configurationService = new HonuaConfigurationService(configuration);
        var store = new InMemoryStacCatalogStore();
        var builder = new RasterStacCatalogBuilder();
        var vectorBuilder = new VectorStacCatalogBuilder();
        var synchronizer = new RasterStacCatalogSynchronizer(store, configurationService, registry, builder, vectorBuilder, NullLogger<RasterStacCatalogSynchronizer>.Instance);

        var snapshotWithoutDataset1 = new MetadataSnapshot(
            snapshot.Catalog,
            snapshot.Folders,
            snapshot.DataSources,
            snapshot.Services,
            snapshot.Layers,
            new[] { dataset2 });

        return new SynchronizerHarness(store, synchronizer, registry, configurationService, provider, snapshotWithoutDataset1);
    }

    private sealed record SynchronizerHarness(
        InMemoryStacCatalogStore Store,
        RasterStacCatalogSynchronizer Synchronizer,
        MetadataRegistry Registry,
        HonuaConfigurationService ConfigurationService,
        MutableMetadataProvider Provider,
        MetadataSnapshot SnapshotWithoutDataset1) : IDisposable
    {
        public void Dispose()
        {
            ConfigurationService.Dispose();
        }
    }

    private sealed class MutableMetadataProvider : IMetadataProvider
    {
        private MetadataSnapshot _snapshot;

        public MutableMetadataProvider(MetadataSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_snapshot);
        }

        public void UpdateSnapshot(MetadataSnapshot snapshot)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public bool SupportsChangeNotifications => false;
#pragma warning disable CS0067
        public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
#pragma warning restore CS0067
    }
}
