using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster;

[Trait("Category", "Unit")]
public sealed class RasterDatasetRegistryTests
{
    [Fact]
    public async Task GetAllAsync_ShouldReturnDatasetsFromMetadata()
    {
        var snapshot = CreateSnapshot();
        var registry = new RasterDatasetRegistry(new StubMetadataRegistry(snapshot));

        var datasets = await registry.GetAllAsync();

        datasets.Should().HaveCount(2);
        datasets.Should().Contain(d => d.Id == "imagery-naip");
        datasets.Should().Contain(d => d.Id == "imagery-dem");
    }

    [Fact]
    public async Task GetByServiceAsync_ShouldFilterDatasetsByServiceId()
    {
        var snapshot = CreateSnapshot();
        var registry = new RasterDatasetRegistry(new StubMetadataRegistry(snapshot));

        var datasets = await registry.GetByServiceAsync("imagery");

        datasets.Should().ContainSingle(d => d.Id == "imagery-naip");
    }

    [Fact]
    public async Task FindAsync_ShouldReturnDatasetWhenExists()
    {
        var snapshot = CreateSnapshot();
        var registry = new RasterDatasetRegistry(new StubMetadataRegistry(snapshot));

        var dataset = await registry.FindAsync("imagery-dem");

        dataset.Should().NotBeNull();
        dataset!.Source.Type.Should().Be("geotiff");
    }

    [Fact]
    public async Task FindAsync_ShouldReturnNullWhenDatasetMissing()
    {
        var snapshot = CreateSnapshot();
        var registry = new RasterDatasetRegistry(new StubMetadataRegistry(snapshot));

        var dataset = await registry.FindAsync("missing");

        dataset.Should().BeNull();
    }

    private static MetadataSnapshot CreateSnapshot()
    {
        var catalog = new CatalogDefinition { Id = "catalog" };
        var folders = new List<FolderDefinition> { new() { Id = "root", Title = "Root" } };
        var dataSources = new List<DataSourceDefinition> { new() { Id = "stub", Provider = "stub", ConnectionString = "stub" } };
        var services = new List<ServiceDefinition>
        {
            new()
            {
                Id = "imagery",
                Title = "Imagery",
                FolderId = "root",
                ServiceType = "raster",
                DataSourceId = "stub",
                Enabled = true,
                Layers = Array.Empty<LayerDefinition>()
            }
        };

        var datasets = new List<RasterDatasetDefinition>
        {
            new()
            {
                Id = "imagery-naip",
                Title = "NAIP 2022",
                ServiceId = "imagery",
                Source = new RasterSourceDefinition
                {
                    Type = "cog",
                    Uri = "file:///data/naip_2022.tif",
                    MediaType = "image/tiff"
                },
                Styles = new RasterStyleDefinition
                {
                    DefaultStyleId = "raster-multiband-naturalcolor",
                    StyleIds = new[]
                    {
                        "raster-multiband-naturalcolor",
                        "raster-multiband-falsecolor"
                    }
                }
            },
            new()
            {
                Id = "imagery-dem",
                Title = "DEM",
                Source = new RasterSourceDefinition
                {
                    Type = "geotiff",
                    Uri = "file:///data/dem.tif"
                }
            }
        };

        var styles = new List<StyleDefinition>
        {
            new()
            {
                Id = "raster-multiband-naturalcolor",
                Renderer = "simple",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "polygon",
                    FillColor = "#5AA06EFF",
                    StrokeColor = "#FFFFFFFF",
                    StrokeWidth = 1.5
                }
            },
            new()
            {
                Id = "raster-multiband-falsecolor",
                Renderer = "simple",
                Simple = new SimpleStyleDefinition
                {
                    SymbolType = "polygon",
                    FillColor = "#DC5578FF",
                    StrokeColor = "#FFFFFFFF",
                    StrokeWidth = 1.5
                }
            }
        };

        return new MetadataSnapshot(
            catalog,
            folders,
            dataSources,
            services,
            Array.Empty<LayerDefinition>(),
            datasets,
            styles);
    }

    private sealed class StubMetadataRegistry : IMetadataRegistry
    {
        public StubMetadataRegistry(MetadataSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public MetadataSnapshot Snapshot { get; }
        public bool IsInitialized => true;
        public ValueTask<MetadataSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Snapshot);
        public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReloadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public IChangeToken GetChangeToken() => TestChangeTokens.Noop;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = Snapshot;
            return true;
        }
    }
}
