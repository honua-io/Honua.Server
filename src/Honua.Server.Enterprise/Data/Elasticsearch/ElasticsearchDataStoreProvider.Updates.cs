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
    public async Task<FeatureRecord?> UpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);
        Guard.NotNullOrWhiteSpace(featureId);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);
        var payload = CreateDocument(record, layer);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Put,
            $"{Encode(indexName)}/_doc/{Encode(featureId)}",
            payload,
            cancellationToken,
            allowNotFound: true).ConfigureAwait(false);

        if (response.RootElement.TryGetProperty("result", out var resultElement) &&
            string.Equals(resultElement.GetString(), "not_found", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> BulkUpdateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<KeyValuePair<string, FeatureRecord>> updates,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(updates);

        var count = 0;
        await foreach (var update in updates.WithCancellation(cancellationToken))
        {
            var result = await UpdateAsync(dataSource, service, layer, update.Key, update.Value, null, cancellationToken).ConfigureAwait(false);
            if (result is not null)
            {
                count++;
            }
        }

        return count;
    }
}
