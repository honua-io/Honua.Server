// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Data;

/// <summary>
/// Decorator for IDataStoreProvider that adds bulkhead protection to prevent resource exhaustion.
/// All database operations are wrapped with bulkhead policies to limit concurrent connections.
/// </summary>
public class ResilientDataStoreProvider : IDataStoreProvider
{
    private readonly IDataStoreProvider _inner;
    private readonly BulkheadPolicyProvider _bulkheadProvider;
    private readonly ILogger<ResilientDataStoreProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientDataStoreProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner data store provider to wrap.</param>
    /// <param name="bulkheadProvider">The bulkhead policy provider.</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientDataStoreProvider(
        IDataStoreProvider inner,
        BulkheadPolicyProvider bulkheadProvider,
        ILogger<ResilientDataStoreProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(bulkheadProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _bulkheadProvider = bulkheadProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Provider => _inner.Provider;

    /// <inheritdoc />
    public IDataStoreCapabilities Capabilities => _inner.Capabilities;

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureRecord> QueryAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        // For streaming operations, we wrap the entire enumerable execution
        await foreach (var record in _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            // Convert IAsyncEnumerable to Task<List> for bulkhead wrapping
            var results = new List<FeatureRecord>();
            await foreach (var item in _inner.QueryAsync(dataSource, service, layer, query, cancellationToken))
            {
                results.Add(item);
            }
            return results;
        }))
        {
            yield return record;
        }
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing count query with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.CountAsync(dataSource, service, layer, query, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<FeatureRecord?> GetAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureQuery? query,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing get operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.GetAsync(dataSource, service, layer, featureId, query, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing create operation with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.CreateAsync(dataSource, service, layer, record, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing update operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.UpdateAsync(dataSource, service, layer, featureId, record, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing delete operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.DeleteAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> SoftDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing soft delete operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.SoftDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> RestoreAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing restore operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.RestoreAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing hard delete operation with bulkhead protection for feature {FeatureId}", featureId);
            return await _inner.HardDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing bulk insert operation with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.BulkInsertAsync(dataSource, service, layer, records, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing bulk update operation with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.BulkUpdateAsync(dataSource, service, layer, updates, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing bulk delete operation with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.BulkDeleteAsync(dataSource, service, layer, featureIds, cancellationToken);
        });
    }

    /// <inheritdoc />
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
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing MVT tile generation with bulkhead protection for tile {Zoom}/{X}/{Y}", zoom, x, y);
            return await _inner.GenerateMvtTileAsync(dataSource, service, layer, zoom, x, y, datetime, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<StatisticDefinition> statistics,
        IReadOnlyList<string>? groupByFields,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing statistics query with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.QueryStatisticsAsync(dataSource, service, layer, statistics, groupByFields, filter, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IReadOnlyList<string> fieldNames,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing distinct query with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.QueryDistinctAsync(dataSource, service, layer, fieldNames, filter, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Executing extent query with bulkhead protection for layer {Layer}", layer.Name);
            return await _inner.QueryExtentAsync(dataSource, service, layer, filter, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        return await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Beginning transaction with bulkhead protection");
            return await _inner.BeginTransactionAsync(dataSource, cancellationToken);
        });
    }

    /// <inheritdoc />
    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        await _bulkheadProvider.ExecuteDatabaseOperationAsync(async () =>
        {
            _logger.LogDebug("Testing connectivity with bulkhead protection");
            await _inner.TestConnectivityAsync(dataSource, cancellationToken);
        });
    }
}
