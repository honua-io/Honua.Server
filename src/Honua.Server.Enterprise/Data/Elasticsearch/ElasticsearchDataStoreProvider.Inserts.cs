// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Elasticsearch;

public sealed partial class ElasticsearchDataStoreProvider
{
    public async Task<FeatureRecord> CreateAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        FeatureRecord record,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(record);

        var connection = GetConnection(dataSource);
        var indexName = ResolveIndexName(layer, connection.Info);
        var documentId = ResolveDocumentId(layer, record);
        var payload = CreateDocument(record, layer);

        using var response = await SendAsync(
            connection.Client,
            HttpMethod.Put,
            $"{Encode(indexName)}/_doc/{Encode(documentId)}",
            payload,
            cancellationToken).ConfigureAwait(false);

        return await GetAsync(dataSource, service, layer, documentId, null, cancellationToken).ConfigureAwait(false)
               ?? record;
    }

    public async Task<int> BulkInsertAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var count = 0;
        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            await CreateAsync(dataSource, service, layer, record, null, cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }
}
