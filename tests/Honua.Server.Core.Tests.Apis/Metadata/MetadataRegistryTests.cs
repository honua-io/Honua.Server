using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.Metadata;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class MetadataRegistryTests
{
    [Fact]
    public async Task EnsureInitializedAsync_ShouldLoadInitialSnapshot()
    {
        var provider = new TestMetadataProvider();
        provider.SetSnapshot(CreateSnapshot("v1"));

        var registry = new MetadataRegistry(provider, NullLogger<MetadataRegistry>.Instance);
        await registry.EnsureInitializedAsync();

        var snapshot = await registry.GetSnapshotAsync();
        snapshot.Catalog.Id.Should().Be("catalog-v1");
    }

    [Fact]
    public async Task ReloadAsync_ShouldUpdateSnapshot()
    {
        var provider = new TestMetadataProvider();
        provider.SetSnapshot(CreateSnapshot("v1"));
        var registry = new MetadataRegistry(provider, NullLogger<MetadataRegistry>.Instance);
        await registry.EnsureInitializedAsync();

        provider.SetSnapshot(CreateSnapshot("v2"));
        await registry.ReloadAsync();

        var snapshot = await registry.GetSnapshotAsync();
        snapshot.Catalog.Id.Should().Be("catalog-v2");
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldLoadMetadata_WhenNotInitialized()
    {
        var provider = new TestMetadataProvider();
        provider.SetSnapshot(CreateSnapshot("v3"));

        var registry = new MetadataRegistry(provider, NullLogger<MetadataRegistry>.Instance);

        var snapshot = await registry.GetSnapshotAsync();

        snapshot.Catalog.Id.Should().Be("catalog-v3");
        registry.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public void Snapshot_ShouldThrow_WhenNotInitialized()
    {
        var provider = new TestMetadataProvider();
        provider.SetSnapshot(CreateSnapshot("v4"));

        var registry = new MetadataRegistry(provider, NullLogger<MetadataRegistry>.Instance);

        Action act = () => _ = registry.Snapshot;

        act.Should().Throw<NotSupportedException>()
            .WithMessage("Synchronous Snapshot property removed*");
    }

    private static MetadataSnapshot CreateSnapshot(string version)
    {
        var catalog = new CatalogDefinition { Id = $"catalog-{version}", Title = version };
        return new MetadataSnapshot(
            catalog,
            Array.Empty<FolderDefinition>(),
            Array.Empty<DataSourceDefinition>(),
            Array.Empty<ServiceDefinition>(),
            Array.Empty<LayerDefinition>());
    }

    private sealed class TestMetadataProvider : IMetadataProvider
    {
        private MetadataSnapshot? _snapshot;

        public void SetSnapshot(MetadataSnapshot snapshot) => _snapshot = snapshot;

        public Task<MetadataSnapshot> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_snapshot is null)
            {
                throw new InvalidOperationException("Snapshot not set");
            }

            return Task.FromResult(_snapshot);
        }

        public bool SupportsChangeNotifications => false;
#pragma warning disable CS0067
        public event EventHandler<MetadataChangedEventArgs>? MetadataChanged;
#pragma warning restore CS0067
    }
}
