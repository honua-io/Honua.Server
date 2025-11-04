using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Honua.Server.Core.Tests.DataOperations.Editing;

[Trait("Category", "Unit")]
public class FeatureEditOrchestratorTests
{
    [Fact(Skip = "Feature edit orchestrator pending implementation")]
    public async Task ExecuteAsync_AddFeature_ShouldApplyDefaultsAndReturnIdentifier()
    {
        var layer = CreateLayerDefinition(allowAdd: true, allowUpdate: false, allowDelete: false);
        var snapshot = CreateSnapshot(layer);
        var metadataRegistry = new FakeMetadataRegistry(snapshot);
        var repository = new FakeFeatureRepository
        {
            CreateCallback = (_, _, record) => new FeatureRecord(new Dictionary<string, object?>(record.Attributes)
            {
                [layer.IdField] = 42
            })
        };

        var orchestrator = CreateOrchestrator(repository, metadataRegistry);

        var command = new AddFeatureCommand("service", "layer", new Dictionary<string, object?>
        {
            ["name"] = "Main"
        });

        var batch = new FeatureEditBatch(
            new[] { command },
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" });

        var result = await orchestrator.ExecuteAsync(batch, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        repository.Created.Should().HaveCount(1);
        repository.Created[0].Attributes.Should().ContainKey("status").WhoseValue.Should().Be("planned");
        result.Results.Should().HaveCount(1);
        result.Results[0].FeatureId.Should().Be("42");
    }

    [Fact(Skip = "Feature edit orchestrator pending implementation")]
    public async Task ExecuteAsync_AddFeature_ShouldFailWhenNotAuthorized()
    {
        var layer = CreateLayerDefinition(allowAdd: false, allowUpdate: true, allowDelete: true);
        var snapshot = CreateSnapshot(layer);
        var metadataRegistry = new FakeMetadataRegistry(snapshot);
        var repository = new FakeFeatureRepository();
        var orchestrator = CreateOrchestrator(repository, metadataRegistry);

        var command = new AddFeatureCommand("service", "layer", new Dictionary<string, object?>
        {
            ["name"] = "Blocked"
        });

        var batch = new FeatureEditBatch(
            new[] { command },
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" });

        var result = await orchestrator.ExecuteAsync(batch, CancellationToken.None);

        result.Results.Should().ContainSingle();
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Error.Should().NotBeNull();
        result.Results[0].Error!.Code.Should().Be("add_not_allowed");
        repository.Created.Should().BeEmpty();
    }

    [Fact(Skip = "Feature edit orchestrator pending implementation")]
    public async Task ExecuteAsync_UpdateFeature_ShouldFailWhenImmutableFieldProvided()
    {
        var layer = CreateLayerDefinition(allowAdd: true, allowUpdate: true, allowDelete: false);
        var snapshot = CreateSnapshot(layer);
        var metadataRegistry = new FakeMetadataRegistry(snapshot);
        var repository = new FakeFeatureRepository();
        var orchestrator = CreateOrchestrator(repository, metadataRegistry);

        var command = new UpdateFeatureCommand(
            "service",
            "layer",
            "100",
            new Dictionary<string, object?>
            {
                ["status"] = "closed"
            });

        var batch = new FeatureEditBatch(
            new[] { command },
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" });

        var result = await orchestrator.ExecuteAsync(batch, CancellationToken.None);

        result.Results.Should().ContainSingle();
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Error.Should().NotBeNull();
        result.Results[0].Error!.Code.Should().Be("immutable_field");
        repository.Updated.Should().BeEmpty();
    }

    [Fact(Skip = "Feature edit orchestrator pending implementation")]
    public async Task ExecuteAsync_DeleteFeature_ShouldPropagateRepositoryResult()
    {
        var layer = CreateLayerDefinition(allowAdd: false, allowUpdate: false, allowDelete: true);
        var snapshot = CreateSnapshot(layer);
        var metadataRegistry = new FakeMetadataRegistry(snapshot);
        var repository = new FakeFeatureRepository
        {
            DeleteCallback = (_, _, featureId) => featureId == "5"
        };
        var orchestrator = CreateOrchestrator(repository, metadataRegistry);

        var command = new DeleteFeatureCommand("service", "layer", "5");
        var batch = new FeatureEditBatch(
            new[] { command },
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" });

        var result = await orchestrator.ExecuteAsync(batch, CancellationToken.None);

        result.Results.Should().ContainSingle();
        result.Results[0].Success.Should().BeTrue();
        result.Results[0].FeatureId.Should().Be("5");
        repository.Deleted.Should().ContainSingle().Which.Should().Be("5");
    }

    [Fact(Skip = "Feature edit orchestrator pending implementation")]
    public async Task ExecuteAsync_ShouldAbortRemainingCommandsWhenRollbackEnabled()
    {
        var layer = CreateLayerDefinition(allowAdd: true, allowUpdate: true, allowDelete: true);
        var snapshot = CreateSnapshot(layer);
        var metadataRegistry = new FakeMetadataRegistry(snapshot);
        var repository = new FakeFeatureRepository
        {
            UpdateCallback = (_, _, _, _) => null,
            CreateCallback = (_, _, record) => record
        };
        var orchestrator = CreateOrchestrator(repository, metadataRegistry);

        var update = new UpdateFeatureCommand("service", "layer", "1", new Dictionary<string, object?>());
        var add = new AddFeatureCommand("service", "layer", new Dictionary<string, object?>
        {
            ["name"] = "Second"
        });

        var batch = new FeatureEditBatch(
            new FeatureEditCommand[] { update, add },
            rollbackOnFailure: true,
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" });

        var result = await orchestrator.ExecuteAsync(batch, CancellationToken.None);

        result.Results.Should().HaveCount(2);
        result.Results[0].Success.Should().BeFalse();
        result.Results[0].Error!.Code.Should().Be("not_found");
        result.Results[1].Success.Should().BeFalse();
        result.Results[1].Error!.Code.Should().Be("batch_aborted");
        repository.Created.Should().BeEmpty();
    }

    private static FeatureEditOrchestrator CreateOrchestrator(IFeatureRepository repository, IMetadataRegistry metadataRegistry)
    {
        var contextResolver = new StubContextResolver();
        return new FeatureEditOrchestrator(
            repository,
            metadataRegistry,
            new FeatureEditAuthorizationService(),
            new FeatureEditConstraintValidator(),
            contextResolver,
            NullLogger<FeatureEditOrchestrator>.Instance);
    }

    private sealed class StubContextResolver : IFeatureContextResolver
    {
        public Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default)
            => Task.FromException<FeatureContext>(new System.NotImplementedException());
    }

    private static MetadataSnapshot CreateSnapshot(LayerDefinition layer)
    {
        var catalog = new CatalogDefinition { Id = "catalog" };
        var folder = new FolderDefinition { Id = "folder", Title = "Folder" };
        var dataSource = new DataSourceDefinition { Id = "datasource", Provider = "sqlite", ConnectionString = "Data Source=:memory:" };
        var service = new ServiceDefinition
        {
            Id = "service",
            Title = "Service",
            FolderId = folder.Id,
            ServiceType = "feature",
            DataSourceId = dataSource.Id,
            Layers = new ReadOnlyCollection<LayerDefinition>(new[] { layer })
        };

        return new MetadataSnapshot(
            catalog,
            new ReadOnlyCollection<FolderDefinition>(new[] { folder }),
            new ReadOnlyCollection<DataSourceDefinition>(new[] { dataSource }),
            new ReadOnlyCollection<ServiceDefinition>(new[] { service }),
            new ReadOnlyCollection<LayerDefinition>(new[] { layer }));
    }

    private static LayerDefinition CreateLayerDefinition(bool allowAdd, bool allowUpdate, bool allowDelete)
    {
        var defaultValues = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>
        {
            ["status"] = "planned"
        });

        return new LayerDefinition
        {
            Id = "layer",
            ServiceId = "service",
            Title = "Layer",
            GeometryType = "Polygon",
            IdField = "OBJECTID",
            DisplayField = "name",
            GeometryField = "geom",
            Editing = new LayerEditingDefinition
            {
                Capabilities = new LayerEditCapabilitiesDefinition
                {
                    AllowAdd = allowAdd,
                    AllowUpdate = allowUpdate,
                    AllowDelete = allowDelete,
                    RequireAuthentication = true,
                    AllowedRoles = new ReadOnlyCollection<string>(new[] { "DataPublisher" })
                },
                Constraints = new LayerEditConstraintDefinition
                {
                    ImmutableFields = new ReadOnlyCollection<string>(new[] { "status" }),
                    RequiredFields = new ReadOnlyCollection<string>(new[] { "name" }),
                    DefaultValues = defaultValues
                }
            }
        };
    }

    private sealed class FakeMetadataRegistry : IMetadataRegistry
    {
        public FakeMetadataRegistry(MetadataSnapshot snapshot)
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

        public IChangeToken GetChangeToken() => NoopChangeToken.Instance;
        public void Update(MetadataSnapshot snapshot) { }
        public Task UpdateAsync(MetadataSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool TryGetSnapshot(out MetadataSnapshot snapshot)
        {
            snapshot = Snapshot;
            return true;
        }

        private sealed class NoopChangeToken : IChangeToken
        {
            public static IChangeToken Instance { get; } = new NoopChangeToken();

            public bool HasChanged => false;

            public bool ActiveChangeCallbacks => false;

            public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
                => NoopDisposable.Instance;
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static IDisposable Instance { get; } = new NoopDisposable();

            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeFeatureRepository : IFeatureRepository
    {
        public List<FeatureRecord> Created { get; } = new();
        public List<(string FeatureId, FeatureRecord Record)> Updated { get; } = new();
        public List<string> Deleted { get; } = new();

        public Func<string, string, FeatureRecord, FeatureRecord>? CreateCallback { get; set; }
        public Func<string, string, string, FeatureRecord, FeatureRecord?>? UpdateCallback { get; set; }
        public Func<string, string, string, bool>? DeleteCallback { get; set; }

        public IAsyncEnumerable<FeatureRecord> QueryAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<long> CountAsync(string serviceId, string layerId, FeatureQuery? query, CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<FeatureRecord?> GetAsync(string serviceId, string layerId, string featureId, FeatureQuery? query = null, CancellationToken cancellationToken = default)
            => Task.FromResult<FeatureRecord?>(null);

        public Task<FeatureRecord> CreateAsync(string serviceId, string layerId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Created.Add(record);
            var result = CreateCallback?.Invoke(serviceId, layerId, record) ?? record;
            return Task.FromResult(result);
        }

        public Task<FeatureRecord?> UpdateAsync(string serviceId, string layerId, string featureId, FeatureRecord record, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Updated.Add((featureId, record));
            var result = UpdateCallback?.Invoke(serviceId, layerId, featureId, record);
            return Task.FromResult(result);
        }

        public Task<bool> DeleteAsync(string serviceId, string layerId, string featureId, IDataStoreTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            Deleted.Add(featureId);
            var result = DeleteCallback?.Invoke(serviceId, layerId, featureId) ?? false;
            return Task.FromResult(result);
        }

        public Task<byte[]> GenerateMvtTileAsync(string serviceId, string layerId, int zoom, int x, int y, string? datetime = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(string serviceId, string layerId, IReadOnlyList<StatisticDefinition> statistics, IReadOnlyList<string>? groupByFields, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StatisticsResult>>(Array.Empty<StatisticsResult>());

        public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(string serviceId, string layerId, IReadOnlyList<string> fieldNames, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DistinctResult>>(Array.Empty<DistinctResult>());

        public Task<BoundingBox?> QueryExtentAsync(string serviceId, string layerId, FeatureQuery? filter, CancellationToken cancellationToken = default)
            => Task.FromResult<BoundingBox?>(null);
    }
}
