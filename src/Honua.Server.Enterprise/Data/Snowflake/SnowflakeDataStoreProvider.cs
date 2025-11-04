// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Snowflake;

/// <summary>
/// Snowflake data store provider with connection pooling and optimized bulk operations.
/// Refactored to use focused service classes for better maintainability and separation of concerns.
/// </summary>
public sealed class SnowflakeDataStoreProvider : IDataStoreProvider, IDisposable
{
    private readonly SnowflakeConnectionManager _connectionManager;
    private readonly SnowflakeFeatureOperations _featureOperations;
    private readonly SnowflakeBulkOperations _bulkOperations;
    private readonly SnowflakeVectorTileGenerator _vectorTileGenerator;
    private bool _disposed;

    public const string ProviderKey = "snowflake";

    public SnowflakeDataStoreProvider()
        : this(null)
    {
    }

    public SnowflakeDataStoreProvider(IConnectionStringEncryptionService? encryptionService)
    {
        _connectionManager = new SnowflakeConnectionManager(encryptionService);
        _featureOperations = new SnowflakeFeatureOperations(_connectionManager);
        _bulkOperations = new SnowflakeBulkOperations(_connectionManager);
        _vectorTileGenerator = new SnowflakeVectorTileGenerator(_connectionManager);
    }

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => SnowflakeDataStoreCapabilities.Instance;

    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        await foreach (var record in _featureOperations.QueryAsync(dataSource, service, layer, query, cancellationToken).ConfigureAwait(false))
        {
            yield return record;
        }
    }

    public async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return await _featureOperations.CountAsync(dataSource, service, layer, query, cancellationToken);
    }

    public async Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        return await _featureOperations.GetAsync(dataSource, service, layer, featureId, query, cancellationToken);
    }

    public async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        return await _featureOperations.CreateAsync(dataSource, service, layer, record, cancellationToken);
    }

    public async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureId);
        Guard.NotNull(record);

        return await _featureOperations.UpdateAsync(dataSource, service, layer, featureId, record, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureId);

        return await _featureOperations.DeleteAsync(dataSource, service, layer, featureId, cancellationToken);
    }

    /// <summary>
    /// Performs bulk inserts using batched multi-row INSERT statements.
    /// This is significantly faster than individual INSERT statements by reducing round trips.
    /// </summary>
    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        return await _bulkOperations.BulkInsertAsync(dataSource, service, layer, records, cancellationToken);
    }

    /// <summary>
    /// Performs bulk updates using batched UPDATE FROM VALUES statements.
    /// This is 10-50x faster than individual UPDATE statements by reducing round trips.
    /// </summary>
    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> records,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        return await _bulkOperations.BulkUpdateAsync(dataSource, service, layer, records, cancellationToken);
    }

    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureIds);

        return await _bulkOperations.BulkDeleteAsync(dataSource, service, layer, featureIds, cancellationToken);
    }

    public async Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return await _vectorTileGenerator.GenerateMvtTileAsync(dataSource, service, layer, zoom, x, y, datetime, cancellationToken);
    }

    public Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return _featureOperations.QueryStatisticsAsync(
            dataSource,
            service,
            layer,
            statistics,
            groupByFields,
            filter,
            cancellationToken);
    }

    public Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return _featureOperations.QueryDistinctAsync(
            dataSource,
            service,
            layer,
            fieldNames,
            filter,
            cancellationToken);
    }

    public Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);

        return _featureOperations.QueryExtentAsync(
            dataSource,
            service,
            layer,
            filter,
            cancellationToken);
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Transaction support not yet implemented for Snowflake
        return Task.FromResult<IDataStoreTransaction?>(null);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);

        await _featureOperations.TestConnectivityAsync(dataSource, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Soft delete is not supported by the {nameof(SnowflakeDataStoreProvider)}. " +
            "Check IDataStoreCapabilities.SupportsSoftDelete before calling this method.");
    }

    public Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Restore is not supported by the {nameof(SnowflakeDataStoreProvider)}. " +
            "Check IDataStoreCapabilities.SupportsSoftDelete before calling this method.");
    }

    public Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Hard delete is not supported by the {nameof(SnowflakeDataStoreProvider)}. " +
            "Use the standard DeleteAsync method for permanent deletion.");
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connectionManager.Dispose();
        _disposed = true;
    }
}
