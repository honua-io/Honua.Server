// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Microsoft.Azure.Cosmos;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.CosmosDb;

public sealed class CosmosDbDataStoreProvider : IDataStoreProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, CosmosClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<string[][]>>> _partitionKeyCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;


    public const string ProviderKey = "cosmosdb";

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => CosmosDbDataStoreCapabilities.Instance;

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

        var effectiveQuery = query ?? new FeatureQuery();
        if (effectiveQuery.ResultType == FeatureResultType.Hits)
        {
            yield break;
        }

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var queryDefinition = builder.BuildSelect(effectiveQuery);

        var requestOptions = new QueryRequestOptions
        {
            MaxConcurrency = -1,
            MaxItemCount = effectiveQuery.Limit ?? -1
        };

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition, requestOptions: requestOptions);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in response)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return CreateFeatureRecord(item);
            }
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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var queryDefinition = builder.BuildCount(query ?? new FeatureQuery());

        using var iterator = context.Container.GetItemQueryIterator<long>(queryDefinition);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            return response.FirstOrDefault();
        }

        return 0L;
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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var queryDefinition = builder.BuildById(featureId);

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            var document = response.FirstOrDefault();
            if (document is not null)
            {
                return CreateFeatureRecord(document);
            }
        }

        return null;
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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var document = CreateWritableDocument(layer, record, featureIdOverride: null);
        var partitionKey = ResolvePartitionKey(context, document);

        var response = await context.Container.CreateItemAsync(document, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        return CreateFeatureRecord(response.Resource);
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
        Guard.NotNull(record);
        Guard.NotNullOrWhiteSpace(featureId);

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var document = CreateWritableDocument(layer, record, featureId);
        var partitionKey = ResolvePartitionKey(context, document);

        try
        {
            var response = await context.Container.ReplaceItemAsync(
                document,
                featureId,
                partitionKey,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return CreateFeatureRecord(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
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
        Guard.NotNullOrWhiteSpace(featureId);

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);

        var existing = await FetchDocumentAsync(context, layer, featureId, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        var partitionKey = ResolvePartitionKey(context, existing);

        try
        {
            await context.Container.DeleteItemAsync<Dictionary<string, object?>>(featureId, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
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
            $"Soft delete is not supported by the {nameof(CosmosDbDataStoreProvider)}. " +
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
            $"Restore is not supported by the {nameof(CosmosDbDataStoreProvider)}. " +
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
        // TODO: Implement hard delete functionality for CosmosDB
        // For now, delegate to regular DeleteAsync
        return DeleteAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
    }

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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var count = 0;

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            var document = CreateWritableDocument(layer, record, featureIdOverride: null);
            var partitionKey = ResolvePartitionKey(context, document);
            await context.Container.CreateItemAsync(document, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
            count++;
        }

        return count;
    }

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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var count = 0;

        await foreach (var pair in records.WithCancellation(cancellationToken))
        {
            var document = CreateWritableDocument(layer, pair.Value, pair.Key);
            var partitionKey = ResolvePartitionKey(context, document);

            try
            {
                await context.Container.ReplaceItemAsync(
                    document,
                    pair.Key,
                    partitionKey,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                count++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Skip missing documents
            }
        }

        return count;
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

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var count = 0;

        await foreach (var featureId in featureIds.WithCancellation(cancellationToken))
        {
            var existing = await FetchDocumentAsync(context, layer, featureId, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                continue;
            }

            var partitionKey = ResolvePartitionKey(context, existing);

            try
            {
                await context.Container.DeleteItemAsync<Dictionary<string, object?>>(featureId, partitionKey, cancellationToken: cancellationToken).ConfigureAwait(false);
                count++;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Skip missing documents
            }
        }

        return count;
    }

    public Task<byte[]?> GenerateMvtTileAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        int zoom,
        int x,
        int y,
        string? datetime = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<byte[]?>(null);
    }

    public async Task<IReadOnlyList<StatisticsResult>> QueryStatisticsAsync(
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
        Guard.NotNull(statistics);

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var baseQuery = CreateFilterOnlyQuery(filter);

        var queryDefinition = builder.BuildStatisticsQuery(
            baseQuery,
            groupByFields ?? Array.Empty<string>(),
            statistics,
            out var groupAliasMap,
            out var statisticAliasMap);

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition);
        var results = new List<StatisticsResult>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in response)
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (alias, fieldName) in groupAliasMap)
                {
                    groupValues[fieldName] = item.TryGetValue(alias, out var value)
                        ? NormalizeValue(value)
                        : null;
                }

                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (alias, definition) in statisticAliasMap)
                {
                    var outputName = GetStatisticOutputName(definition);
                    var rawValue = item.TryGetValue(alias, out var value) ? NormalizeValue(value) : null;
                    statisticValues[outputName] = CastStatisticValue(definition.Type, rawValue);
                }

                results.Add(new StatisticsResult(
                    new ReadOnlyDictionary<string, object?>(groupValues),
                    new ReadOnlyDictionary<string, object?>(statisticValues)));
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<DistinctResult>> QueryDistinctAsync(
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
        Guard.NotNull(fieldNames);

        if (fieldNames.Count == 0)
        {
            return Array.Empty<DistinctResult>();
        }

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var baseQuery = CreateFilterOnlyQuery(filter);

        var queryDefinition = builder.BuildDistinctQuery(baseQuery, fieldNames, out var aliasMap);

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition);
        var results = new List<DistinctResult>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in response)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (alias, fieldName) in aliasMap)
                {
                    values[fieldName] = item.TryGetValue(alias, out var value)
                        ? NormalizeValue(value)
                        : null;
                }

                results.Add(new DistinctResult(new ReadOnlyDictionary<string, object?>(values)));
            }
        }

        return results;
    }

    public async Task<BoundingBox?> QueryExtentAsync(
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

        // Use LayerMetadataHelper to get geometry column
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);
        if (geometryField.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not define a geometry field.");
        }

        var context = await GetContainerContextAsync(dataSource, layer, cancellationToken).ConfigureAwait(false);
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var baseQuery = CreateFilterOnlyQuery(filter);

        var extentQuery = new FeatureQuery(
            PropertyNames: new[] { geometryField },
            Bbox: baseQuery.Bbox,
            Temporal: baseQuery.Temporal,
            Filter: baseQuery.Filter,
            Crs: baseQuery.Crs);

        var queryDefinition = builder.BuildSelect(extentQuery);

        double? minX = null, minY = null, maxX = null, maxY = null;

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in response)
            {
                if (!item.TryGetValue(geometryField, out var geometryValue))
                {
                    continue;
                }

                var normalizedGeometry = NormalizeValue(geometryValue);
                TryUpdateEnvelope(normalizedGeometry, ref minX, ref minY, ref maxX, ref maxY);
            }
        }

        if (minX is null || minY is null || maxX is null || maxY is null)
        {
            return null;
        }

        var crs = layer.Storage?.Crs ?? (layer.Storage?.Srid is { } srid ? $"EPSG:{srid}" : null);
        return new BoundingBox(minX.Value, minY.Value, maxX.Value, maxY.Value, Crs: crs);
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IDataStoreTransaction?>(null);
    }

    public async Task TestConnectivityAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);

        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' has no connection string configured.");
        }

        var client = GetOrCreateClient(dataSource.ConnectionString);

        using var iterator = client.GetDatabaseQueryIterator<DatabaseProperties>("SELECT TOP 1 * FROM c");
        await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }

        _clients.Clear();
        _partitionKeyCache.Clear();
        _disposed = true;
    }

    private CosmosClient GetOrCreateClient(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Cosmos DB connection string is not configured.");
        }

        return _clients.GetOrAdd(connectionString, cs => new CosmosClient(cs));
    }

    private async Task<CosmosContainerContext> GetContainerContextAsync(
        DataSourceDefinition dataSource,
        LayerDefinition layer,
        CancellationToken cancellationToken)
    {
        if (layer.Storage?.Table.IsNullOrWhiteSpace() == true)
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not define a Cosmos DB container. Specify 'database/container' in storage.table.");
        }

        var (databaseId, containerId) = ParseContainerIdentifier(layer.Storage.Table);
        var client = GetOrCreateClient(dataSource.ConnectionString);
        var container = client.GetContainer(databaseId, containerId);
        var partitionKeyPaths = await GetPartitionKeyPathsAsync(container, cancellationToken).ConfigureAwait(false);

        return new CosmosContainerContext(container, partitionKeyPaths);
    }

    private static (string DatabaseId, string ContainerId) ParseContainerIdentifier(string table)
    {
        var parts = table.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"Cosmos DB table '{table}' must be specified as 'database/container'.");
        }

        return (parts[0], parts[1]);
    }

    private Task<string[][]> GetPartitionKeyPathsAsync(Container container, CancellationToken cancellationToken)
    {
        var cacheKey = $"{container.Database.Id}/{container.Id}";

        var lazy = _partitionKeyCache.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<string[][]>>(
                async () =>
                {
                    var response = await container.ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    var properties = response.Resource;

                    if (properties.PartitionKeyPaths is { Count: > 0 })
                    {
                        return properties.PartitionKeyPaths
                            .Select(path => path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            .Where(array => array.Length > 0)
                            .ToArray();
                    }

                    if (properties.PartitionKeyPath.HasValue())
                    {
                        var segments = properties.PartitionKeyPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        if (segments.Length > 0)
                        {
                            return new[] { segments };
                        }
                    }

                    return Array.Empty<string[]>();
                },
                LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private PartitionKey ResolvePartitionKey(
        CosmosContainerContext context,
        IReadOnlyDictionary<string, object?> document)
    {
        if (context.PartitionKeyPaths.Length == 0)
        {
            return PartitionKey.None;
        }

        if (context.PartitionKeyPaths.Length == 1)
        {
            var value = ExtractPartitionKeyValue(document, context.PartitionKeyPaths[0]);
            if (value is null)
            {
                throw new InvalidOperationException($"Partition key value '{string.Join("/", context.PartitionKeyPaths[0])}' was not found in the feature attributes.");
            }

            var component = NormalizePartitionKeyComponent(value);
            return BuildPartitionKey(component);
        }

        var components = new object[context.PartitionKeyPaths.Length];
        for (var i = 0; i < context.PartitionKeyPaths.Length; i++)
        {
            var value = ExtractPartitionKeyValue(document, context.PartitionKeyPaths[i]);
            if (value is null)
            {
                throw new InvalidOperationException($"Partition key value '{string.Join("/", context.PartitionKeyPaths[i])}' was not found in the feature attributes.");
            }

            components[i] = NormalizePartitionKeyComponent(value);
        }

        var builder = new PartitionKeyBuilder();
        foreach (var component in components)
        {
            AddPartitionComponent(builder, component);
        }

        return builder.Build();
    }

    private static object NormalizePartitionKeyComponent(object value)
    {
        return value switch
        {
            JsonElement element => NormalizePartitionKeyComponent(NormalizeJsonElement(element) ?? string.Empty),
            string s => s,
            bool b => b,
            int i => (double)i,
            long l => (double)l,
            short sValue => (double)sValue,
            float f => (double)f,
            double d => d,
            decimal m => (double)m,
            Guid g => g.ToString("N", CultureInfo.InvariantCulture),
            // Use FeatureRecordNormalizer for DateTime UTC conversion
            DateTimeOffset dto => FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(dto).ToString("o", CultureInfo.InvariantCulture),
            DateTime dt => FeatureRecordNormalizer.NormalizeDateTimeValue(dt).ToString("o", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static PartitionKey BuildPartitionKey(object component)
    {
        var builder = new PartitionKeyBuilder();
        AddPartitionComponent(builder, component);
        return builder.Build();
    }

    private static void AddPartitionComponent(PartitionKeyBuilder builder, object component)
    {
        switch (component)
        {
            case string s:
                builder.Add(s);
                break;
            case double d:
                builder.Add(d);
                break;
            case bool b:
                builder.Add(b);
                break;
            default:
                builder.Add(component?.ToString() ?? string.Empty);
                break;
        }
    }

    private static object? ExtractPartitionKeyValue(IReadOnlyDictionary<string, object?> document, string[] pathSegments)
    {
        object? current = document;

        foreach (var segment in pathSegments)
        {
            current = current switch
            {
                IReadOnlyDictionary<string, object?> readOnlyDict when readOnlyDict.TryGetValue(segment, out var value) => value,
                IDictionary<string, object?> dict when dict.TryGetValue(segment, out var value) => value,
                JsonElement element => ExtractFromJsonElement(element, segment),
                _ => null
            };

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static object? ExtractFromJsonElement(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.TryGetProperty(propertyName, out var result) ? result : null;
    }

    private static Dictionary<string, object?> CreateWritableDocument(
        LayerDefinition layer,
        FeatureRecord record,
        string? featureIdOverride)
    {
        var document = new Dictionary<string, object?>(record.Attributes.Count + 1, StringComparer.Ordinal);

        // Use LayerMetadataHelper to get geometry column for normalization
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var (key, value) in record.Attributes)
        {
            // CosmosDB uses custom NormalizeValue for nested documents/arrays
            // but we ensure DateTime UTC conversion via FeatureRecordNormalizer for atomic values
            var isGeometry = string.Equals(key, geometryField, StringComparison.OrdinalIgnoreCase);

            // Apply FeatureRecordNormalizer for DateTime UTC and basic normalization
            var normalizedValue = value switch
            {
                DateTimeOffset dto => FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(dto),
                DateTime dt => FeatureRecordNormalizer.NormalizeDateTimeValue(dt),
                _ => NormalizeValue(value) // Use CosmosDB-specific normalization for complex types
            };

            document[key] = normalizedValue;
        }

        if (!document.TryGetValue("id", out var idValue) || idValue?.ToString().IsNullOrWhiteSpace() == true)
        {
            // Use LayerMetadataHelper to get primary key column
            var keyField = featureIdOverride ?? LayerMetadataHelper.GetPrimaryKeyColumn(layer);
            if (keyField.HasValue() && document.TryGetValue(keyField, out var keyFieldValue) && keyFieldValue is not null)
            {
                document["id"] = keyFieldValue.ToString();
            }
            else
            {
                document["id"] = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            }
        }
        else if (featureIdOverride.HasValue())
        {
            document["id"] = featureIdOverride;
        }

        return document;
    }

    private static FeatureRecord CreateFeatureRecord(Dictionary<string, object?> document)
    {
        var normalized = NormalizeDocument(document);
        return new FeatureRecord(normalized);
    }

    private static IReadOnlyDictionary<string, object?> NormalizeDocument(IEnumerable<KeyValuePair<string, object?>> document)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in document)
        {
            result[key] = NormalizeValue(value);
        }

        return new ReadOnlyDictionary<string, object?>(result);
    }

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            // Use FeatureRecordNormalizer for DateTime UTC conversion
            DateTimeOffset dto => FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(dto),
            DateTime dt => FeatureRecordNormalizer.NormalizeDateTimeValue(dt),
            JsonElement element => NormalizeJsonElement(element),
            IReadOnlyDictionary<string, object?> readOnlyDict => NormalizeDocument(readOnlyDict),
            IDictionary<string, object?> dict => NormalizeDocument(dict.Select(kvp => kvp)),
            IEnumerable enumerable when value is not string => enumerable.Cast<object?>().Select(NormalizeValue).ToList(),
            _ => value
        };
    }

    private static object? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => NormalizeDocument(element.EnumerateObject().Select(p => new KeyValuePair<string, object?>(p.Name, NormalizeJsonElement(p.Value)))),
            JsonValueKind.Array => element.EnumerateArray().Select(NormalizeJsonElement).ToList(),
            // Use FeatureRecordNormalizer for DateTime UTC conversion
            JsonValueKind.String when element.TryGetDateTimeOffset(out var dto) => FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(dto),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private async Task<Dictionary<string, object?>?> FetchDocumentAsync(
        CosmosContainerContext context,
        LayerDefinition layer,
        string featureId,
        CancellationToken cancellationToken)
    {
        var builder = new CosmosDbFeatureQueryBuilder(layer, context.PartitionKeyPaths);
        var queryDefinition = builder.BuildById(featureId);

        using var iterator = context.Container.GetItemQueryIterator<Dictionary<string, object?>>(queryDefinition, requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
        return response.FirstOrDefault();
    }

    private static FeatureQuery CreateFilterOnlyQuery(FeatureQuery? query)
    {
        if (query is null)
        {
            return new FeatureQuery();
        }

        return new FeatureQuery(
            Bbox: query.Bbox,
            Temporal: query.Temporal,
            Filter: query.Filter,
            Crs: query.Crs);
    }

    private static string GetStatisticOutputName(StatisticDefinition statistic)
    {
        if (statistic.OutputName.HasValue())
        {
            return statistic.OutputName;
        }

        if (statistic.FieldName.HasValue())
        {
            return $"{statistic.Type.ToString().ToLower(CultureInfo.InvariantCulture)}_{statistic.FieldName}";
        }

        return statistic.Type.ToString().ToLower(CultureInfo.InvariantCulture);
    }

    private static object? CastStatisticValue(StatisticType type, object? value)
    {
        if (value is null)
        {
            return null;
        }

        return type switch
        {
            StatisticType.Count => ConvertToLong(value),
            StatisticType.Sum or StatisticType.Avg or StatisticType.Min or StatisticType.Max => ConvertToDouble(value),
            _ => value
        };
    }

    private static object? ConvertToLong(object value)
    {
        return value switch
        {
            long l => l,
            int i => (long)i,
            short s => (long)s,
            double d => (long)Math.Round(d, MidpointRounding.AwayFromZero),
            float f => (long)Math.Round(f, MidpointRounding.AwayFromZero),
            decimal m => (long)Math.Round(m, MidpointRounding.AwayFromZero),
            string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
        };
    }

    private static object? ConvertToDouble(object value)
    {
        return value switch
        {
            double d => d,
            float f => (double)f,
            long l => (double)l,
            int i => (double)i,
            short s => (double)s,
            decimal m => (double)m,
            string s when double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => value
        };
    }

    private static void TryUpdateEnvelope(object? geometry, ref double? minX, ref double? minY, ref double? maxX, ref double? maxY)
    {
        if (geometry is IReadOnlyDictionary<string, object?> dict)
        {
            if (!dict.TryGetValue("type", out var typeValue) || typeValue is not string type)
            {
                return;
            }

            if (string.Equals(type, "GeometryCollection", StringComparison.OrdinalIgnoreCase))
            {
                if (dict.TryGetValue("geometries", out var geometriesObj) && geometriesObj is IEnumerable enumerable)
                {
                    foreach (var child in enumerable.Cast<object?>())
                    {
                        TryUpdateEnvelope(child, ref minX, ref minY, ref maxX, ref maxY);
                    }
                }
                return;
            }

            if (!dict.TryGetValue("coordinates", out var coordinates))
            {
                return;
            }

            double? localMinX = minX;
            double? localMinY = minY;
            double? localMaxX = maxX;
            double? localMaxY = maxY;

            TryEnumeratePositions(coordinates, (x, y) =>
            {
                if (!double.IsFinite(x) || !double.IsFinite(y))
                {
                    return;
                }

                localMinX = localMinX.HasValue ? Math.Min(localMinX.Value, x) : x;
                localMinY = localMinY.HasValue ? Math.Min(localMinY.Value, y) : y;
                localMaxX = localMaxX.HasValue ? Math.Max(localMaxX.Value, x) : x;
                localMaxY = localMaxY.HasValue ? Math.Max(localMaxY.Value, y) : y;
            });

            minX = localMinX;
            minY = localMinY;
            maxX = localMaxX;
            maxY = localMaxY;
        }
    }

    private static void TryEnumeratePositions(object? node, Action<double, double> accumulator)
    {
        if (node is IReadOnlyList<object?> list)
        {
            if (list.Count >= 2 && TryReadDouble(list[0], out var x) && TryReadDouble(list[1], out var y))
            {
                accumulator(x, y);
                return;
            }

            foreach (var child in list)
            {
                TryEnumeratePositions(child, accumulator);
            }
            return;
        }

        if (node is IEnumerable enumerable and not string)
        {
            foreach (var child in enumerable.Cast<object?>())
            {
                TryEnumeratePositions(child, accumulator);
            }
        }
    }

    private static bool TryReadDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;
            case float f:
                result = f;
                return true;
            case long l:
                result = l;
                return true;
            case int i:
                result = i;
                return true;
            case short s:
                result = s;
                return true;
            case decimal m:
                result = (double)m;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private sealed record CosmosContainerContext(Container Container, string[][] PartitionKeyPaths);
}
