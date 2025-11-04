using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Tests.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Catalog;

[Trait("Category", "Unit")]
public sealed class CatalogProjectionServiceTests
{
    [Fact]
    public void GetGroups_ShouldReturnOrderedServicesAndLayers()
    {
        var snapshot = CreateSnapshot();
        var service = CreateProjectionService(snapshot);

        var groups = service.GetGroups();

        groups.Should().HaveCount(1);
        var group = groups[0];
        group.Id.Should().Be("transportation");
        group.Services.Should().HaveCount(2);
        group.Services.Should().BeInAscendingOrder(s => s.Service.Title);
        group.Services.Select(s => s.Service.Id).Should().ContainInOrder("roads", "trails");
        group.Services[0].Layers.Should().HaveCount(1);
        group.Services[0].Layers[0].Layer.Id.Should().Be("roads-primary");
    }

    [Fact]
    public void Search_ShouldFilterByKeywordAndGroup()
    {
        var snapshot = CreateSnapshot();
        var service = CreateProjectionService(snapshot);

        var keywordMatches = service.Search("primary", null);
        keywordMatches.Should().ContainSingle(record => record.Id == "roads:roads-primary");

        var groupMatches = service.Search(null, "transportation");
        groupMatches.Should().HaveCount(2);
    }

    [Fact]
    public void GetRecord_ShouldIncludeResolvedExtentAndContacts()
    {
        var snapshot = CreateSnapshot();
        var service = CreateProjectionService(snapshot);

        var record = service.GetRecord("roads:roads-primary");
        record.Should().NotBeNull();
        record!.SpatialExtent.Should().NotBeNull();
        record.SpatialExtent!.Bbox.Should().ContainSingle();
        record.TemporalExtent.Should().NotBeNull();
        record.Contacts.Should().NotBeEmpty();
        record.Keywords.Should().Contain(new[] { "roads", "transportation" });
    }

    private static CatalogProjectionService CreateProjectionService(MetadataSnapshot snapshot)
    {
        var registry = new StaticMetadataRegistry(snapshot);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CatalogProjectionService(registry, cache, NullLogger<CatalogProjectionService>.Instance);
        service.WarmupAsync().GetAwaiter().GetResult();
        return service;
    }

    private static MetadataSnapshot CreateSnapshot()
    {
        var catalog = new CatalogDefinition
        {
            Id = "catalog",
            Title = "Sample Catalog",
            Description = "Test catalog",
            Keywords = new[] { "transportation" },
            ThemeCategories = new[] { "transportation" },
            Contact = new CatalogContactDefinition { Name = "Support", Email = "support@example.org" },
            Extents = new CatalogExtentDefinition
            {
                Spatial = new CatalogSpatialExtentDefinition
                {
                    Bbox = new[] { new[] { -180d, -90d, 180d, 90d } },
                    Crs = "EPSG:4326"
                }
            }
        };

        var folder = new FolderDefinition { Id = "transportation", Title = "Transportation", Order = 5 };

        var roadsService = new ServiceDefinition
        {
            Id = "roads",
            Title = "Road Centerlines",
            FolderId = folder.Id,
            ServiceType = "FeatureServer",
            DataSourceId = "sqlite",
            Description = "Road datasets",
            Keywords = new[] { "roads" },
            Links = new[] { new LinkDefinition { Href = "https://example.org/roads" } },
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Road centerline collection",
                Keywords = new[] { "roads", "transportation" },
                Themes = new[] { "transportation" },
                Contacts = new[] { new CatalogContactDefinition { Name = "Road Steward" } },
                Links = new[] { new LinkDefinition { Href = "https://example.org/roads/info" } },
                Thumbnail = "https://example.org/thumbs/roads.png",
                Ordering = 20
            }
        };

        var trailsService = new ServiceDefinition
        {
            Id = "trails",
            Title = "Trail Network",
            FolderId = folder.Id,
            ServiceType = "feature",
            DataSourceId = "sqlite",
            Description = "Trails dataset",
            Keywords = new[] { "trails" },
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Trail features",
                Keywords = new[] { "trails", "transportation" },
                Ordering = 5
            }
        };

        var roadsLayer = new LayerDefinition
        {
            Id = "roads-primary",
            ServiceId = roadsService.Id,
            Title = "Primary Roads",
            Description = "Primary road segments",
            GeometryType = "Polyline",
            IdField = "id",
            GeometryField = "geom",
            Keywords = new[] { "roads", "primary" },
            Extent = new LayerExtentDefinition
            {
                Bbox = new[] { new[] { -10d, -5d, 10d, 5d } },
                Crs = "EPSG:4326",
                Temporal = new[]
                {
                    new TemporalIntervalDefinition
                    {
                        Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                        End = new DateTimeOffset(2021, 12, 31, 0, 0, 0, TimeSpan.Zero)
                    }
                }
            },
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Primary roads for routing",
                Keywords = new[] { "roads", "primary" },
                Themes = new[] { "transportation" },
                Contacts = new[] { new CatalogContactDefinition { Name = "Road Steward" } },
                SpatialExtent = new CatalogSpatialExtentDefinition
                {
                    Bbox = new[] { new[] { -10d, -5d, 10d, 5d } },
                    Crs = "EPSG:4326"
                },
                TemporalExtent = new CatalogTemporalExtentDefinition
                {
                    Start = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    End = new DateTimeOffset(2021, 12, 31, 0, 0, 0, TimeSpan.Zero)
                },
                Ordering = 10
            },
            Query = new LayerQueryDefinition { MaxRecordCount = 500 }
        };

        var trailsLayer = new LayerDefinition
        {
            Id = "trails-main",
            ServiceId = trailsService.Id,
            Title = "Trails",
            Description = "Recreational trails",
            GeometryType = "Polyline",
            IdField = "trail_id",
            GeometryField = "geom",
            Catalog = new CatalogEntryDefinition
            {
                Summary = "Trails for hiking",
                Ordering = 1
            },
            Query = new LayerQueryDefinition { MaxRecordCount = 200 }
        };

        var dataSources = new[]
        {
            new DataSourceDefinition { Id = "sqlite", Provider = "sqlite", ConnectionString = "Data Source=:memory:" }
        };

        var services = new[] { roadsService, trailsService };
        var layers = new[] { roadsLayer, trailsLayer };

        return new MetadataSnapshot(
            catalog,
            new[] { folder },
            dataSources,
            services,
            layers);
    }

    private sealed class StaticMetadataRegistry : IMetadataRegistry
    {
        public StaticMetadataRegistry(MetadataSnapshot snapshot)
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
