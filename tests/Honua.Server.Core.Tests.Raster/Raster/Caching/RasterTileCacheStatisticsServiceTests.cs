using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Caching;
using Xunit;

namespace Honua.Server.Core.Tests.Raster.Raster.Caching;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class RasterTileCacheStatisticsServiceTests
{
    private readonly RasterTileCacheStatisticsService _service;
    private readonly InMemoryRasterTileCacheMetadataStore _metadataStore;
    private readonly StubRasterDatasetRegistry _datasetRegistry;

    public RasterTileCacheStatisticsServiceTests()
    {
        _metadataStore = new InMemoryRasterTileCacheMetadataStore();
        _datasetRegistry = new StubRasterDatasetRegistry();
        _service = new RasterTileCacheStatisticsService(_metadataStore, _datasetRegistry);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldReturnZeroForEmptyCache()
    {
        var stats = await _service.GetStatisticsAsync();

        stats.TotalTiles.Should().Be(0);
        stats.TotalSizeBytes.Should().Be(0);
        stats.HitRate.Should().Be(0);
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.DatasetCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldCalculateHitRate()
    {
        _service.RecordHit();
        _service.RecordHit();
        _service.RecordHit();
        _service.RecordMiss();

        var stats = await _service.GetStatisticsAsync();

        stats.Hits.Should().Be(3);
        stats.Misses.Should().Be(1);
        stats.HitRate.Should().BeApproximately(0.75, 0.01);
    }

    [Fact]
    public async Task GetDatasetStatisticsAsync_ShouldReturnNullForEmptyDataset()
    {
        var stats = await _service.GetDatasetStatisticsAsync("test-dataset");

        stats.Should().BeNull();
    }

    [Fact]
    public async Task GetDatasetStatisticsAsync_ShouldReturnStatsForDatasetWithTiles()
    {
        var key = CreateCacheKey("test-dataset", 0, 0, 0);
        await _metadataStore.RecordTileCreationAsync(key, 1024);

        var stats = await _service.GetDatasetStatisticsAsync("test-dataset");

        stats.Should().NotBeNull();
        stats!.DatasetId.Should().Be("test-dataset");
        stats.TileCount.Should().Be(1);
        stats.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public async Task GetAllDatasetStatisticsAsync_ShouldReturnAllDatasets()
    {
        _datasetRegistry.AddDataset("dataset-1");
        _datasetRegistry.AddDataset("dataset-2");

        var key1 = CreateCacheKey("dataset-1", 0, 0, 0);
        var key2 = CreateCacheKey("dataset-2", 0, 0, 0);

        await _metadataStore.RecordTileCreationAsync(key1, 1024);
        await _metadataStore.RecordTileCreationAsync(key2, 2048);

        var allStats = await _service.GetAllDatasetStatisticsAsync();

        allStats.Should().HaveCount(2);
        allStats.Should().Contain(s => s.DatasetId == "dataset-1" && s.SizeBytes == 1024);
        allStats.Should().Contain(s => s.DatasetId == "dataset-2" && s.SizeBytes == 2048);
    }

    [Fact]
    public async Task ResetStatisticsAsync_ShouldResetHitsAndMisses()
    {
        _service.RecordHit();
        _service.RecordHit();
        _service.RecordMiss();

        await _service.ResetStatisticsAsync();

        var stats = await _service.GetStatisticsAsync();
        stats.Hits.Should().Be(0);
        stats.Misses.Should().Be(0);
        stats.HitRate.Should().Be(0);
    }

    private static RasterTileCacheKey CreateCacheKey(string datasetId, int zoom, int row, int column)
    {
        return new RasterTileCacheKey(
            datasetId: datasetId,
            tileMatrixSetId: "WebMercatorQuad",
            zoom: zoom,
            row: row,
            column: column,
            styleId: "default",
            format: "image/png",
            transparent: true,
            tileSize: 256);
    }

    private sealed class StubRasterDatasetRegistry : IRasterDatasetRegistry
    {
        private readonly List<RasterDatasetDefinition> _datasets = new();

        public void AddDataset(string id)
        {
            _datasets.Add(new RasterDatasetDefinition
            {
                Id = id,
                Title = $"Test Dataset {id}",
                Source = new RasterSourceDefinition
                {
                    Type = "geotiff",
                    Uri = "/test/data.tif"
                }
            });
        }

        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetAllAsync(System.Threading.CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<RasterDatasetDefinition>>(_datasets);

        public ValueTask<IReadOnlyList<RasterDatasetDefinition>> GetByServiceAsync(string serviceId, System.Threading.CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<RasterDatasetDefinition>>(Array.Empty<RasterDatasetDefinition>());

        public ValueTask<RasterDatasetDefinition?> FindAsync(string id, System.Threading.CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_datasets.FirstOrDefault(d => d.Id == id));
    }
}
