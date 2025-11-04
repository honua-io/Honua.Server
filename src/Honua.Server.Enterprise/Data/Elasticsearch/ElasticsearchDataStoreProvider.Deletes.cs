// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    public async Task<bool> DeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Delete,
            $"{Encode(indexName)}/_doc/{Encode(featureId)}",
            body: null,
            cancellationToken,
            allowNotFound: true).ConfigureAwait(false);

        if (response.RootElement.TryGetProperty("result", out var resultElement))
        {
            var result = resultElement.GetString();
            return string.Equals(result, "deleted", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(result, "not_found", StringComparison.OrdinalIgnoreCase) == false;
        }

        return response.RootElement.ValueKind != JsonValueKind.Undefined;
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
            $"Soft delete is not supported by the {nameof(ElasticsearchDataStoreProvider)}. " +
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
            $"Restore is not supported by the {nameof(ElasticsearchDataStoreProvider)}. " +
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
        // TODO: Implement hard delete functionality for Elasticsearch
        // For now, delegate to regular DeleteAsync
        return DeleteAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
    }

    public async Task<int> BulkDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<string> featureIds,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(featureIds);

        var count = 0;
        await foreach (var featureId in featureIds.WithCancellation(cancellationToken))
        {
            if (await DeleteAsync(dataSource, service, layer, featureId, null, cancellationToken).ConfigureAwait(false))
            {
                count++;
            }
        }

        return count;
    }
}
