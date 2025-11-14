// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Resilience;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Data;

/// <summary>
/// Decorator for IDataStoreProvider that adds comprehensive resilience patterns:
/// - Retry policy for transient database failures (exponential backoff with jitter)
/// - Bulkhead protection to prevent connection pool exhaustion
/// - Metrics and logging for observability
/// </summary>
public class ResilientDataStoreProvider : IDataStoreProvider
{
    private readonly IDataStoreProvider _inner;
    private readonly ResilientDatabaseOperationExecutor _executor;
    private readonly ILogger<ResilientDataStoreProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientDataStoreProvider"/> class.
    /// </summary>
    /// <param name="inner">The inner data store provider to wrap.</param>
    /// <param name="executor">The resilient database operation executor (retry + bulkhead).</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientDataStoreProvider(
        IDataStoreProvider inner,
        ResilientDatabaseOperationExecutor executor,
        ILogger<ResilientDataStoreProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(logger);

        _inner = inner;
        _executor = executor;
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For streaming operations, we wrap the entire enumerable execution with retry + bulkhead
        var records = await _executor.ExecuteAsync(
            async ct =>
            {
                // Convert IAsyncEnumerable to Task<List> for resilience wrapping
                var results = new List<FeatureRecord>();
                await foreach (var item in _inner.QueryAsync(dataSource, service, layer, query, ct))
                {
                    results.Add(item);
                }
                return results;
            },
            $"QueryAsync:{layer.Title}",
            cancellationToken);

        foreach (var record in records)
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
        return await _executor.ExecuteAsync(
            ct => _inner.CountAsync(dataSource, service, layer, query, ct),
            $"CountAsync:{layer.Title}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.GetAsync(dataSource, service, layer, featureId, query, ct),
            $"GetAsync:{layer.Title}:{featureId}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.CreateAsync(dataSource, service, layer, record, transaction, ct),
            $"CreateAsync:{layer.Title}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.UpdateAsync(dataSource, service, layer, featureId, record, transaction, ct),
            $"UpdateAsync:{layer.Title}:{featureId}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.DeleteAsync(dataSource, service, layer, featureId, transaction, ct),
            $"DeleteAsync:{layer.Title}:{featureId}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.SoftDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, ct),
            $"SoftDeleteAsync:{layer.Title}:{featureId}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.RestoreAsync(dataSource, service, layer, featureId, transaction, ct),
            $"RestoreAsync:{layer.Title}:{featureId}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.HardDeleteAsync(dataSource, service, layer, featureId, deletedBy, transaction, ct),
            $"HardDeleteAsync:{layer.Title}:{featureId}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteAsync(
            ct => _inner.BulkInsertAsync(dataSource, service, layer, records, ct),
            $"BulkInsertAsync:{layer.Title}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteAsync(
            ct => _inner.BulkUpdateAsync(dataSource, service, layer, updates, ct),
            $"BulkUpdateAsync:{layer.Title}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteAsync(
            ct => _inner.BulkDeleteAsync(dataSource, service, layer, featureIds, ct),
            $"BulkDeleteAsync:{layer.Title}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.GenerateMvtTileAsync(dataSource, service, layer, zoom, x, y, datetime, ct),
            $"GenerateMvtTileAsync:{layer.Title}:{zoom}/{x}/{y}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.QueryStatisticsAsync(dataSource, service, layer, statistics, groupByFields, filter, ct),
            $"QueryStatisticsAsync:{layer.Title}",
            cancellationToken);
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
        return await _executor.ExecuteAsync(
            ct => _inner.QueryDistinctAsync(dataSource, service, layer, fieldNames, filter, ct),
            $"QueryDistinctAsync:{layer.Title}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BoundingBox?> QueryExtentAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureQuery? filter,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteAsync(
            ct => _inner.QueryExtentAsync(dataSource, service, layer, filter, ct),
            $"QueryExtentAsync:{layer.Title}",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        return await _executor.ExecuteAsync(
            ct => _inner.BeginTransactionAsync(dataSource, ct),
            "BeginTransactionAsync",
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        await _executor.ExecuteAsync(
            ct => _inner.TestConnectivityAsync(dataSource, ct),
            "TestConnectivityAsync",
            cancellationToken);
    }
}
