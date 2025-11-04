using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Tests.Shared;

namespace Honua.Server.Core.Tests.Shared;

/// <summary>
/// Stub implementation of IDataStoreProvider for testing.
/// All operations throw NotSupportedException by default.
/// </summary>
/// <remarks>
/// This stub is useful when you need to satisfy dependency injection requirements
/// but don't actually need functional data store operations. It reports full
/// capabilities via TestDataStoreCapabilities.
///
/// For functional testing with in-memory data, use InMemoryEditableFeatureRepository
/// or StubFeatureRepository instead.
/// </remarks>
public sealed class StubDataStoreProvider : IDataStoreProvider
{
    /// <summary>
    /// Initializes a new instance with the specified provider name.
    /// </summary>
    /// <param name="providerName">The provider name (e.g., "stub", "memory").</param>
    public StubDataStoreProvider(string providerName)
    {
        Provider = providerName ?? throw new ArgumentNullException(nameof(providerName));
    }

    /// <summary>
    /// Gets the provider name.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the capabilities. Returns TestDataStoreCapabilities with full support flags.
    /// </summary>
    public IDataStoreCapabilities Capabilities => TestDataStoreCapabilities.Instance;

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support query operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support count operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support get operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support create operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support update operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support delete operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support soft delete operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support restore operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support hard delete operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support bulk insert operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support bulk update operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support bulk delete operations. Use a real provider or stub repository.");

    /// <summary>
    /// Returns an empty MVT tile.
    /// </summary>
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

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support statistics operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support distinct operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support extent operations. Use a real provider or stub repository.");

    /// <summary>
    /// Not supported. Throws NotSupportedException.
    /// </summary>
    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(StubDataStoreProvider)} does not support transactions. Use a real provider.");

    /// <summary>
    /// Returns a completed task indicating connectivity is OK.
    /// </summary>
    public Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Factory that creates StubDataStoreProvider instances.
/// </summary>
public sealed class StubDataStoreProviderFactory : IDataStoreProviderFactory
{
    /// <summary>
    /// Creates a new StubDataStoreProvider with the specified provider name.
    /// </summary>
    public IDataStoreProvider Create(string providerName)
    {
        return new StubDataStoreProvider(providerName);
    }
}
