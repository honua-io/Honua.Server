using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Provides fluent builder methods for creating common test mocks.
/// Eliminates repetitive mock setup code across the test suite.
/// </summary>
/// <remarks>
/// <para>
/// This class consolidates mock creation patterns found throughout the test suite,
/// providing consistent, easy-to-use builders for:
/// <list type="bullet">
/// <item><see cref="IMetadataRegistry"/> - Service and layer metadata lookups</item>
/// <item><see cref="IMetadataProvider"/> - Metadata loading and change notifications</item>
/// <item><see cref="IDataStoreProvider"/> - Data store operations (query, CRUD)</item>
/// <item><see cref="IFeatureRepository"/> - Feature repository operations</item>
/// </list>
/// </para>
/// <para>
/// Each builder returns a configured <see cref="Mock{T}"/> instance that can be
/// further customized or used directly via the <c>.Object</c> property.
/// </para>
/// </remarks>
/// <example>
/// Creating a metadata registry mock:
/// <code>
/// var service = new ServiceDefinition { Id = "roads", ... };
/// var layer = new LayerDefinition { Id = "roads-primary", ... };
/// var mockRegistry = MockBuilders.CreateMetadataRegistry(service, layer);
///
/// // Use in tests
/// var registry = mockRegistry.Object;
/// var resolvedService = registry.ResolveService("roads");
/// </code>
/// </example>
public static class MockBuilders
{
    /// <summary>
    /// Creates a mock <see cref="IMetadataRegistry"/> configured with the specified service and layer.
    /// </summary>
    /// <param name="service">The service definition to return from registry lookups.</param>
    /// <param name="layer">The layer definition to return from registry lookups.</param>
    /// <returns>A configured mock metadata registry.</returns>
    /// <exception cref="ArgumentNullException">If service or layer is null.</exception>
    public static Mock<IMetadataRegistry> CreateMetadataRegistry(
        ServiceDefinition service,
        LayerDefinition layer)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(layer);

        return CreateMetadataRegistry(new[] { service }, new[] { layer });
    }

    /// <summary>
    /// Creates a mock <see cref="IMetadataRegistry"/> configured with multiple services and layers.
    /// </summary>
    /// <param name="services">The service definitions to include in the registry.</param>
    /// <param name="layers">The layer definitions to include in the registry.</param>
    /// <param name="catalog">Optional catalog definition (defaults to minimal catalog).</param>
    /// <returns>A configured mock metadata registry.</returns>
    public static Mock<IMetadataRegistry> CreateMetadataRegistry(
        IEnumerable<ServiceDefinition> services,
        IEnumerable<LayerDefinition> layers,
        CatalogDefinition? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(layers);

        var serviceList = services.ToList();
        var layerList = layers.ToList();

        var snapshot = BuildMetadataSnapshot(
            catalog ?? new CatalogDefinition
            {
                Id = "test-catalog",
                Title = "Test Catalog",
                Description = "Test metadata catalog"
            },
            serviceList,
            layerList);

        var mock = new Mock<IMetadataRegistry>();

        // Setup snapshot property
        mock.Setup(r => r.Snapshot).Returns(snapshot);
        mock.Setup(r => r.IsInitialized).Returns(true);
        mock.Setup(r => r.TryGetSnapshot(out It.Ref<MetadataSnapshot>.IsAny))
            .Returns(new TryGetSnapshotDelegate((out MetadataSnapshot s) =>
            {
                s = snapshot;
                return true;
            }));
        mock.Setup(r => r.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Setup EnsureInitializedAsync
        mock.Setup(r => r.EnsureInitializedAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup change token
        mock.Setup(r => r.GetChangeToken())
            .Returns(new CancellationChangeToken(CancellationToken.None));

        return mock;
    }

    private static MetadataSnapshot BuildMetadataSnapshot(
        CatalogDefinition catalog,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers)
    {
        var builder = new MetadataSnapshotBuilder()
            .WithCatalog(catalog);

        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in services)
        {
            AddService(builder, service, folderIds, dataSourceIds);
        }

        foreach (var layer in layers)
        {
            builder.WithLayer(layer);
        }

        return builder.Build();
    }

    private static void AddService(
        MetadataSnapshotBuilder builder,
        ServiceDefinition service,
        HashSet<string> folderIds,
        HashSet<string> dataSourceIds)
    {
        var folderId = string.IsNullOrWhiteSpace(service.FolderId) ? "default-folder" : service.FolderId;
        if (folderIds.Add(folderId))
        {
            builder.WithFolder(folderId, folderId);
        }

        var dataSourceId = string.IsNullOrWhiteSpace(service.DataSourceId) ? "default-datasource" : service.DataSourceId;
        if (dataSourceIds.Add(dataSourceId))
        {
            builder.WithDataSource(dataSourceId);
        }

        if (!string.Equals(service.FolderId, folderId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(service.DataSourceId, dataSourceId, StringComparison.OrdinalIgnoreCase))
        {
            service = service with { FolderId = folderId, DataSourceId = dataSourceId };
        }

        builder.WithService(service);
    }

    private static MetadataSnapshot RebuildSnapshot(
        MetadataSnapshot currentSnapshot,
        IReadOnlyList<ServiceDefinition>? services = null,
        IReadOnlyList<LayerDefinition>? layers = null)
    {
        var builder = new MetadataSnapshotBuilder()
            .WithCatalog(currentSnapshot.Catalog)
            .WithServer(currentSnapshot.Server);

        var folderIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in currentSnapshot.Folders)
        {
            if (folderIds.Add(folder.Id))
            {
                builder.WithFolder(folder);
            }
        }

        var dataSourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataSource in currentSnapshot.DataSources)
        {
            if (dataSourceIds.Add(dataSource.Id))
            {
                builder.WithDataSource(dataSource);
            }
        }

        foreach (var raster in currentSnapshot.RasterDatasets)
        {
            builder.WithRasterDataset(raster);
        }

        foreach (var style in currentSnapshot.Styles)
        {
            builder.WithStyle(style);
        }

        var serviceList = services ?? currentSnapshot.Services;
        foreach (var service in serviceList)
        {
            AddService(builder, service, folderIds, dataSourceIds);
        }

        var layerList = layers ?? currentSnapshot.Layers;
        foreach (var layer in layerList)
        {
            builder.WithLayer(layer);
        }

        return builder.Build();
    }

    private delegate bool TryGetSnapshotDelegate(out MetadataSnapshot snapshot);

    /// <summary>
    /// Creates a mock <see cref="IMetadataProvider"/> that returns the specified snapshot.
    /// </summary>
    /// <param name="snapshot">The metadata snapshot to return from LoadAsync.</param>
    /// <param name="supportsChangeNotifications">Whether the provider supports change notifications.</param>
    /// <returns>A configured mock metadata provider.</returns>
    public static Mock<IMetadataProvider> CreateMetadataProvider(
        MetadataSnapshot snapshot,
        bool supportsChangeNotifications = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var mock = new Mock<IMetadataProvider>();

        mock.Setup(p => p.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        mock.Setup(p => p.SupportsChangeNotifications)
            .Returns(supportsChangeNotifications);

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IMetadataProvider"/> from service and layer definitions.
    /// </summary>
    /// <param name="services">The services to include in the snapshot.</param>
    /// <param name="layers">The layers to include in the snapshot.</param>
    /// <param name="supportsChangeNotifications">Whether the provider supports change notifications.</param>
    /// <returns>A configured mock metadata provider.</returns>
    public static Mock<IMetadataProvider> CreateMetadataProvider(
        IEnumerable<ServiceDefinition> services,
        IEnumerable<LayerDefinition> layers,
        bool supportsChangeNotifications = false)
    {
        var snapshot = BuildMetadataSnapshot(
            new CatalogDefinition
            {
                Id = "test-catalog",
                Title = "Test Catalog",
                Description = "Test metadata catalog"
            },
            services.ToList(),
            layers.ToList());

        return CreateMetadataProvider(snapshot, supportsChangeNotifications);
    }

    /// <summary>
    /// Creates a mock <see cref="IDataStoreProvider"/> with specified capabilities.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "postgis", "mysql").</param>
    /// <param name="capabilities">The data store capabilities.</param>
    /// <returns>A configured mock data store provider.</returns>
    public static Mock<IDataStoreProvider> CreateDataStoreProvider(
        string providerName = "mock",
        IDataStoreCapabilities? capabilities = null)
    {
        var mock = new Mock<IDataStoreProvider>();

        mock.Setup(p => p.Provider).Returns(providerName);
        mock.Setup(p => p.Capabilities).Returns(capabilities ?? TestDataStoreCapabilities.Instance);

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IDataStoreProvider"/> that returns in-memory feature data.
    /// </summary>
    /// <param name="features">The features to return from query operations.</param>
    /// <param name="providerName">The provider name.</param>
    /// <returns>A configured mock data store provider.</returns>
    public static Mock<IDataStoreProvider> CreateDataStoreProviderWithFeatures(
        IReadOnlyList<FeatureRecord> features,
        string providerName = "mock")
    {
        var mock = CreateDataStoreProvider(providerName);

        // Setup QueryAsync to return features
        mock.Setup(p => p.QueryAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, FeatureQuery? query, CancellationToken ct) =>
            {
                var filtered = ApplyQuery(features, query);
                return ToAsyncEnumerable(filtered);
            });

        // Setup CountAsync
        mock.Setup(p => p.CountAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, FeatureQuery? query, CancellationToken ct) =>
            {
                var filtered = ApplyQuery(features, query);
                return filtered.Count;
            });

        // Setup GetAsync
        mock.Setup(p => p.GetAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<string>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, string id, FeatureQuery? query, CancellationToken ct) =>
            {
                var pkField = lyr.IdField ?? "id";
                return features.FirstOrDefault(f =>
                    f.Attributes.TryGetValue(pkField, out var value) &&
                    string.Equals(Convert.ToString(value), id, StringComparison.OrdinalIgnoreCase));
            });

        return mock;
    }

    /// <summary>
    /// Creates a mock <see cref="IFeatureRepository"/> that returns in-memory feature data.
    /// </summary>
    /// <param name="features">The features to return from query operations.</param>
    /// <returns>A configured mock feature repository.</returns>
    public static Mock<IFeatureRepository> CreateFeatureRepository(
        IReadOnlyList<FeatureRecord> features)
    {
        ArgumentNullException.ThrowIfNull(features);

        var mock = new Mock<IFeatureRepository>();

        // Setup QueryAsync
        mock.Setup(r => r.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns((string serviceId, string layerId, FeatureQuery? query, CancellationToken ct) =>
            {
                var filtered = ApplyQuery(features, query);
                return ToAsyncEnumerable(filtered);
            });

        // Setup CountAsync
        mock.Setup(r => r.CountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string serviceId, string layerId, FeatureQuery? query, CancellationToken ct) =>
            {
                var filtered = ApplyQuery(features, query);
                return filtered.Count;
            });

        // Setup GetAsync
        mock.Setup(r => r.GetAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<FeatureQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string serviceId, string layerId, string featureId, FeatureQuery? query, CancellationToken ct) =>
            {
                return features.FirstOrDefault(f =>
                    f.Attributes.TryGetValue("id", out var value) &&
                    string.Equals(Convert.ToString(value), featureId, StringComparison.OrdinalIgnoreCase));
            });

        return mock;
    }

    /// <summary>
    /// Creates an empty mock <see cref="IFeatureRepository"/> with no features.
    /// Useful for testing error conditions or empty datasets.
    /// </summary>
    /// <returns>A configured mock feature repository with no features.</returns>
    public static Mock<IFeatureRepository> CreateEmptyFeatureRepository()
    {
        return CreateFeatureRepository(Array.Empty<FeatureRecord>());
    }

    #region Helper Methods

    private static IReadOnlyList<FeatureRecord> ApplyQuery(
        IReadOnlyList<FeatureRecord> features,
        FeatureQuery? query)
    {
        if (query is null)
        {
            return features;
        }

        IEnumerable<FeatureRecord> filtered = features;

        // Apply offset
        if (query.Offset > 0)
        {
            filtered = filtered.Skip(query.Offset.Value);
        }

        // Apply limit
        if (query.Limit.HasValue && query.Limit.Value >= 0)
        {
            filtered = filtered.Take(query.Limit.Value);
        }

        return filtered.ToList();
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    #endregion
}

/// <summary>
/// Extension methods for fluent builder pattern on Mock objects.
/// </summary>
public static class MockBuilderExtensions
{
    /// <summary>
    /// Adds a service to the metadata registry mock.
    /// </summary>
    /// <param name="mock">The metadata registry mock.</param>
    /// <param name="service">The service to add.</param>
    /// <returns>The mock for fluent chaining.</returns>
    public static Mock<IMetadataRegistry> WithService(
        this Mock<IMetadataRegistry> mock,
        ServiceDefinition service)
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(service);

        var currentSnapshot = mock.Object.Snapshot;
        var newServices = currentSnapshot.Services.ToList();
        newServices.Add(service);

        var newSnapshot = RebuildSnapshot(currentSnapshot, newServices, currentSnapshot.Layers.ToList());

        mock.Setup(r => r.Snapshot).Returns(newSnapshot);
        mock.Setup(r => r.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSnapshot);

        return mock;
    }

    private static MetadataSnapshot RebuildSnapshot(
        MetadataSnapshot currentSnapshot,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers)
    {
        var builder = new MetadataSnapshotBuilder()
            .WithCatalog(currentSnapshot.Catalog)
            .WithServer(currentSnapshot.Server);

        foreach (var folder in currentSnapshot.Folders)
        {
            builder.WithFolder(folder);
        }

        foreach (var dataSource in currentSnapshot.DataSources)
        {
            builder.WithDataSource(dataSource);
        }

        foreach (var rasterDataset in currentSnapshot.RasterDatasets)
        {
            builder.WithRasterDataset(rasterDataset);
        }

        foreach (var style in currentSnapshot.Styles)
        {
            builder.WithStyle(style);
        }

        foreach (var service in services)
        {
            builder.WithService(service);
        }

        foreach (var layer in layers)
        {
            builder.WithLayer(layer);
        }

        return builder.Build();
    }

    /// <summary>
    /// Adds a layer to the metadata registry mock.
    /// </summary>
    /// <param name="mock">The metadata registry mock.</param>
    /// <param name="layer">The layer to add.</param>
    /// <returns>The mock for fluent chaining.</returns>
    public static Mock<IMetadataRegistry> WithLayer(
        this Mock<IMetadataRegistry> mock,
        LayerDefinition layer)
    {
        ArgumentNullException.ThrowIfNull(mock);
        ArgumentNullException.ThrowIfNull(layer);

        var currentSnapshot = mock.Object.Snapshot;
        var newLayers = currentSnapshot.Layers.ToList();
        newLayers.Add(layer);

        var newSnapshot = RebuildSnapshot(currentSnapshot, currentSnapshot.Services.ToList(), newLayers);

        mock.Setup(r => r.Snapshot).Returns(newSnapshot);
        mock.Setup(r => r.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(newSnapshot);

        return mock;
    }

    /// <summary>
    /// Configures the data store provider mock to support creating features.
    /// </summary>
    /// <param name="mock">The data store provider mock.</param>
    /// <param name="onCreateCallback">Optional callback invoked when a feature is created.</param>
    /// <returns>The mock for fluent chaining.</returns>
    public static Mock<IDataStoreProvider> WithCreateSupport(
        this Mock<IDataStoreProvider> mock,
        Action<FeatureRecord>? onCreateCallback = null)
    {
        ArgumentNullException.ThrowIfNull(mock);

        mock.Setup(p => p.CreateAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<FeatureRecord>(),
                It.IsAny<IDataStoreTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, FeatureRecord record, IDataStoreTransaction? txn, CancellationToken ct) =>
            {
                onCreateCallback?.Invoke(record);
                return record;
            });

        return mock;
    }

    /// <summary>
    /// Configures the data store provider mock to support updating features.
    /// </summary>
    /// <param name="mock">The data store provider mock.</param>
    /// <param name="onUpdateCallback">Optional callback invoked when a feature is updated.</param>
    /// <returns>The mock for fluent chaining.</returns>
    public static Mock<IDataStoreProvider> WithUpdateSupport(
        this Mock<IDataStoreProvider> mock,
        Action<string, FeatureRecord>? onUpdateCallback = null)
    {
        ArgumentNullException.ThrowIfNull(mock);

        mock.Setup(p => p.UpdateAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<string>(),
                It.IsAny<FeatureRecord>(),
                It.IsAny<IDataStoreTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, string id, FeatureRecord record, IDataStoreTransaction? txn, CancellationToken ct) =>
            {
                onUpdateCallback?.Invoke(id, record);
                return record;
            });

        return mock;
    }

    /// <summary>
    /// Configures the data store provider mock to support deleting features.
    /// </summary>
    /// <param name="mock">The data store provider mock.</param>
    /// <param name="onDeleteCallback">Optional callback invoked when a feature is deleted.</param>
    /// <returns>The mock for fluent chaining.</returns>
    public static Mock<IDataStoreProvider> WithDeleteSupport(
        this Mock<IDataStoreProvider> mock,
        Action<string>? onDeleteCallback = null)
    {
        ArgumentNullException.ThrowIfNull(mock);

        mock.Setup(p => p.DeleteAsync(
                It.IsAny<DataSourceDefinition>(),
                It.IsAny<ServiceDefinition>(),
                It.IsAny<LayerDefinition>(),
                It.IsAny<string>(),
                It.IsAny<IDataStoreTransaction?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DataSourceDefinition ds, ServiceDefinition svc, LayerDefinition lyr, string id, IDataStoreTransaction? txn, CancellationToken ct) =>
            {
                onDeleteCallback?.Invoke(id);
                return true;
            });

        return mock;
    }
}
