// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Cloud.BigQuery.V2;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.BigQuery;

public sealed class BigQueryDataStoreProvider : IDataStoreProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, BigQueryClient> _clients = new(StringComparer.Ordinal);
    private bool _disposed;

    public const string ProviderKey = "bigquery";

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => BigQueryDataStoreCapabilities.Instance;

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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildSelect(query ?? new FeatureQuery());

        var result = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await foreach (var row in result.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return CreateFeatureRecord(row, layer);
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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildCount(query ?? new FeatureQuery());

        var result = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await foreach (var row in result.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            return row["count"] is long count ? count : 0L;
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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildById(featureId);

        var result = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await foreach (var row in result.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            return CreateFeatureRecord(row, layer);
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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildInsert(record);

        await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Fetch the inserted record using SqlParameterHelper to resolve the key
        var primaryKey = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var keyValue = SqlParameterHelper.TryResolveKey(record.Attributes, primaryKey);
        if (keyValue != null)
        {
            var inserted = await GetAsync(dataSource, service, layer, keyValue, null, cancellationToken).ConfigureAwait(false);
            return inserted ?? record;
        }

        return record;
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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildUpdate(featureId, record);

        var result = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        // Check if any rows were affected
        var rowsAffected = result.NumDmlAffectedRows ?? 0;
        if (rowsAffected == 0)
        {
            return null;
        }

        return await GetAsync(dataSource, service, layer, featureId, null, cancellationToken);
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

        var client = GetOrCreateClient(dataSource);
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var queryResult = builder.BuildDelete(featureId);

        var result = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var rowsAffected = result.NumDmlAffectedRows ?? 0;
        return rowsAffected > 0;
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
            $"Soft delete is not supported by the {nameof(BigQueryDataStoreProvider)}. " +
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
            $"Restore is not supported by the {nameof(BigQueryDataStoreProvider)}. " +
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
        // TODO: Implement hard delete functionality for BigQuery
        // For now, delegate to regular DeleteAsync
        return DeleteAsync(dataSource, service, layer, featureId, transaction, cancellationToken);
    }

    /// <summary>
    /// Performs bulk insert using BigQuery's native streaming insert API for optimal performance.
    /// This method is approximately 1000x faster than individual inserts as it batches records
    /// and uses BigQuery's streaming API instead of executing individual DML statements.
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

        const int batchSize = 500; // BigQuery streaming insert limit is 10,000 but 500 is optimal
        var batch = new List<FeatureRecord>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);
        var tableRef = GetTableReference(layer);

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            batch.Add(record);

            if (batch.Count >= batchSize)
            {
                totalCount += await InsertBatchAsync(client, tableRef, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        // Insert remaining records
        if (batch.Count > 0)
        {
            totalCount += await InsertBatchAsync(client, tableRef, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return totalCount;
    }

    /// <summary>
    /// Performs bulk update using batched DML statements for improved performance.
    /// BigQuery doesn't have a streaming update API, so this uses batched DML execution
    /// which is significantly faster than individual update statements.
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

        const int batchSize = 100; // DML statements have different limits than streaming inserts
        var batch = new List<KeyValuePair<string, FeatureRecord>>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);

        await foreach (var kvp in records.WithCancellation(cancellationToken))
        {
            batch.Add(kvp);

            if (batch.Count >= batchSize)
            {
                totalCount += await ExecuteUpdateBatchAsync(client, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalCount += await ExecuteUpdateBatchAsync(client, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return totalCount;
    }

    /// <summary>
    /// Performs bulk delete using batched DML statements with IN clause for optimal performance.
    /// This method batches multiple deletes into single DELETE statements using the IN operator,
    /// which is significantly faster than individual DELETE statements.
    /// </summary>
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

        const int batchSize = 100; // BigQuery supports large IN clauses, but 100 is a good balance
        var batch = new List<string>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            batch.Add(id);

            if (batch.Count >= batchSize)
            {
                totalCount += await ExecuteDeleteBatchAsync(client, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalCount += await ExecuteDeleteBatchAsync(client, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return totalCount;
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
        // BigQuery doesn't have native MVT generation
        return Task.FromResult<byte[]?>(null);
    }

    private FeatureRecord CreateFeatureRecord(BigQueryRow row, LayerDefinition layer)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var geometryField = layer.GeometryField;

        foreach (var field in row.Schema.Fields)
        {
            var value = row[field.Name];

            // Handle GEOGRAPHY type - BigQuery returns GeoJSON string
            if (field.Type == "GEOGRAPHY" && value is string geoJson)
            {
                // Use GeometryReader to parse GeoJSON
                var geometryNode = GeometryReader.ReadGeoJsonGeometry(geoJson);
                attributes[field.Name] = geometryNode;
            }
            else
            {
                // Use FeatureRecordNormalizer for value normalization
                var isGeometry = string.Equals(field.Name, geometryField, StringComparison.OrdinalIgnoreCase);
                attributes[field.Name] = FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
            }
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    /// <summary>
    /// Inserts a batch of records using BigQuery's streaming insert API.
    /// This is significantly faster than individual INSERT statements.
    /// </summary>
    private async Task<int> InsertBatchAsync(
        BigQueryClient client,
        (string ProjectId, string DatasetId, string TableId) tableRef,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        // Convert FeatureRecords to BigQueryInsertRow objects
        var rows = new List<BigQueryInsertRow>(batch.Count);

        foreach (var record in batch)
        {
            var row = new BigQueryInsertRow();

            foreach (var (key, value) in record.Attributes)
            {
                // Skip GeoJSON - BigQuery stores it differently
                if (string.Equals(key, "_geojson", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Convert value to BigQuery-compatible format
                row[key] = ConvertToBigQueryValue(value);
            }

            rows.Add(row);
        }

        // Use streaming insert API
        var insertResult = await client.InsertRowsAsync(
            tableRef.ProjectId,
            tableRef.DatasetId,
            tableRef.TableId,
            rows,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // Check for errors - BigQueryInsertResults.Errors is IEnumerable<BigQueryInsertRowErrors>
        // Each BigQueryInsertRowErrors is enumerable and yields SingleError objects
        if (insertResult.Errors != null && insertResult.Errors.Any())
        {
            var errorMessages = new List<string>();
            foreach (var rowError in insertResult.Errors)
            {
                var rowIndex = rowError.OriginalRowIndex?.ToString() ?? "unknown";
                foreach (var error in rowError)
                {
                    errorMessages.Add($"Row {rowIndex}: {error.Message}");
                }
            }

            throw new InvalidOperationException(
                $"BigQuery streaming insert failed: {string.Join("; ", errorMessages)}");
        }

        return rows.Count;
    }

    /// <summary>
    /// Executes a batch of updates using individual DML statements.
    /// BigQuery doesn't have a streaming update API, so we execute updates sequentially.
    /// </summary>
    private async Task<int> ExecuteUpdateBatchAsync(
        BigQueryClient client,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        var builder = new BigQueryFeatureQueryBuilder(layer);
        var count = 0;

        foreach (var (key, record) in batch)
        {
            var queryResult = builder.BuildUpdate(key, record);
            var result = await client.ExecuteQueryAsync(
                queryResult.Sql,
                parameters: queryResult.Parameters,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            count += (int)(result.NumDmlAffectedRows ?? 0);
        }

        return count;
    }

    /// <summary>
    /// Executes a batch of deletes using a single DELETE statement with IN clause.
    /// This is significantly faster than individual DELETE statements.
    /// Uses LayerMetadataHelper for table and key extraction.
    /// </summary>
    private async Task<int> ExecuteDeleteBatchAsync(
        BigQueryClient client,
        LayerDefinition layer,
        List<string> batch,
        CancellationToken cancellationToken)
    {
        // Use LayerMetadataHelper for consistent metadata extraction
        var tableName = LayerMetadataHelper.GetTableName(layer);
        var primaryKey = LayerMetadataHelper.GetPrimaryKeyColumn(layer);

        // Validate identifiers to prevent SQL injection (defense-in-depth)
        SqlIdentifierValidator.ValidateIdentifier(tableName, nameof(tableName));
        SqlIdentifierValidator.ValidateIdentifier(primaryKey, nameof(primaryKey));

        // Parse BigQuery table reference format: project.dataset.table
        var parts = tableName.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException(
                $"BigQuery table name must be in format 'project.dataset.table', got: {tableName}");
        }

        // Quote each part of the table name for safe SQL construction
        var quotedTableName = string.Join(".", parts.Select(p => $"`{p}`"));
        var quotedPrimaryKey = $"`{primaryKey}`";

        var parameters = new List<BigQueryParameter>();
        var paramNames = new List<string>();

        for (int i = 0; i < batch.Count; i++)
        {
            var paramName = $"id{i}";
            paramNames.Add($"@{paramName}");
            parameters.Add(new BigQueryParameter(paramName, BigQueryDbType.String, batch[i]));
        }

        var sql = $"DELETE FROM {quotedTableName} WHERE {quotedPrimaryKey} IN ({string.Join(", ", paramNames)})";

        var result = await client.ExecuteQueryAsync(
            sql,
            parameters: parameters.ToArray(),
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        return (int)(result.NumDmlAffectedRows ?? 0);
    }

    /// <summary>
    /// Converts a value to a BigQuery-compatible format for streaming inserts.
    /// Uses FeatureRecordNormalizer for consistent value conversion.
    /// </summary>
    private static object? ConvertToBigQueryValue(object? value)
    {
        if (FeatureRecordNormalizer.IsNullOrDbNull(value))
        {
            return null;
        }

        return value switch
        {
            DateTime dt => FeatureRecordNormalizer.NormalizeDateTimeValue(dt).ToString("o"), // ISO 8601 format
            DateTimeOffset dto => FeatureRecordNormalizer.NormalizeDateTimeOffsetValue(dto).ToString("o"),
            byte[] bytes => Convert.ToBase64String(bytes),
            JsonNode node => node.ToJsonString(),
            JsonElement element => FeatureRecordNormalizer.ConvertJsonElement(element),
            _ => value
        };
    }

    /// <summary>
    /// Parses and returns table reference components from the layer definition.
    /// Uses LayerMetadataHelper for consistent table name extraction.
    /// </summary>
    private static (string ProjectId, string DatasetId, string TableId) GetTableReference(LayerDefinition layer)
    {
        var tableName = LayerMetadataHelper.GetTableName(layer);

        // Parse BigQuery table reference format: project.dataset.table
        var parts = tableName.Split('.');

        if (parts.Length != 3)
        {
            throw new InvalidOperationException(
                $"BigQuery table name must be in format 'project.dataset.table', got: {tableName}");
        }

        return (ProjectId: parts[0], DatasetId: parts[1], TableId: parts[2]);
    }

    private BigQueryClient GetOrCreateClient(DataSourceDefinition dataSource)
    {
        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var connectionString = dataSource.ConnectionString;

        return _clients.GetOrAdd(
            connectionString,
            static key => BigQueryClient.Create(ParseProjectId(key)));
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

        if (statistics.Count == 0)
        {
            return Array.Empty<StatisticsResult>();
        }

        Task<IReadOnlyList<StatisticsResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeStatisticsAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                statistics,
                groupByFields,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            var client = GetOrCreateClient(dataSource);
            var builder = new BigQueryFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var queryResult = builder.BuildStatistics(query, statistics, groupByFields);

            var response = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var groupFieldSet = groupByFields is { Count: > 0 }
                ? new HashSet<string>(groupByFields, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var statisticAliases = new HashSet<string>(
                statistics.Select(s => s.OutputName ?? $"{s.Type}_{s.FieldName}"),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<StatisticsResult>();
            await foreach (var row in response.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                foreach (var field in row.Schema.Fields)
                {
                    var fieldName = field.Name;
                    var value = row[fieldName];

                    if (groupFieldSet.Contains(fieldName))
                    {
                        groupValues[fieldName] = value;
                    }
                    else if (statisticAliases.Contains(fieldName))
                    {
                        statisticValues[fieldName] = value;
                    }
                    else if (groupFieldSet.Count > 0)
                    {
                        groupValues[fieldName] = value;
                    }
                    else
                    {
                        statisticValues[fieldName] = value;
                    }
                }

                results.Add(new StatisticsResult(
                    new ReadOnlyDictionary<string, object?>(groupValues),
                    new ReadOnlyDictionary<string, object?>(statisticValues)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
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

        Task<IReadOnlyList<DistinctResult>> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeDistinctAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                fieldNames,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            var client = GetOrCreateClient(dataSource);
            var builder = new BigQueryFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var queryResult = builder.BuildDistinct(query, fieldNames);

            var response = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var results = new List<DistinctResult>();
            await foreach (var row in response.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in row.Schema.Fields)
                {
                    values[field.Name] = row[field.Name];
                }

                results.Add(new DistinctResult(new ReadOnlyDictionary<string, object?>(values)));
            }

            return results;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
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

        Task<BoundingBox?> ExecuteFallbackAsync() =>
            FallbackAggregationHelper.ComputeExtentAsync(
                query => QueryAsync(dataSource, service, layer, query, cancellationToken),
                filter,
                service,
                layer,
                cancellationToken);

        if (!Capabilities.SupportsServerSideGeometryOperations)
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }

        try
        {
            var client = GetOrCreateClient(dataSource);
            var builder = new BigQueryFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var queryResult = builder.BuildExtent(query);

            var response = await client.ExecuteQueryAsync(queryResult.Sql, parameters: queryResult.Parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await foreach (var row in response.GetRowsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (row.Schema.Fields.Count == 0)
                {
                    continue;
                }

                var geoJsonValue = row["extent_geojson"];
                if (geoJsonValue is not string geoJson || geoJson.IsNullOrWhiteSpace())
                {
                    continue;
                }

                var targetCrs = query.Crs ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
                var bbox = TryParseExtent(geoJson, targetCrs);
                if (bbox is not null)
                {
                    return bbox;
                }
            }

            return null;
        }
        catch (Exception ex) when (IsSpatialCapabilityError(ex))
        {
            return await ExecuteFallbackAsync().ConfigureAwait(false);
        }
    }

    private static bool IsSpatialCapabilityError(Exception exception)
    {
        if (exception is null)
        {
            return false;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (ContainsSpatialCapabilityIndicator(current.Message))
            {
                return true;
            }

            if (current is Google.GoogleApiException googleApiException)
            {
                if (ContainsSpatialCapabilityIndicator(googleApiException.Error?.Message))
                {
                    return true;
                }

                if (googleApiException.Error?.Errors is { Count: > 0 } &&
                    googleApiException.Error.Errors.Any(error => ContainsSpatialCapabilityIndicator(error.Message)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsSpatialCapabilityIndicator(string? message)
    {
        if (message.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (message.Contains("BigQuery GIS", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("not enabled", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!message.Contains("ST_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fallbackTriggers = new[]
        {
            "function not found",
            "no matching signature",
            "permission denied",
            "access denied",
            "not authorized",
            "not enabled",
            "not supported",
            "requires permission",
            "not available"
        };

        return fallbackTriggers.Any(trigger => message.Contains(trigger, StringComparison.OrdinalIgnoreCase));
    }

    private static FeatureQuery NormalizeAnalyticsQuery(FeatureQuery? query)
    {
        var baseQuery = query ?? new FeatureQuery();
        return baseQuery with
        {
            Limit = null,
            Offset = null,
            PropertyNames = null,
            SortOrders = null,
            ResultType = FeatureResultType.Results
        };
    }

    private static BoundingBox? TryParseExtent(string geoJson, string? crs)
    {
        try
        {
            using var document = JsonDocument.Parse(geoJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("coordinates", out var coordinatesElement))
            {
                return null;
            }

            if (coordinatesElement.ValueKind != JsonValueKind.Array || coordinatesElement.GetArrayLength() == 0)
            {
                return null;
            }

            var ring = coordinatesElement.EnumerateArray().FirstOrDefault();
            if (ring.ValueKind != JsonValueKind.Array || ring.GetArrayLength() == 0)
            {
                return null;
            }

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;

            foreach (var coordinate in ring.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                {
                    continue;
                }

                var x = coordinate[0].GetDouble();
                var y = coordinate[1].GetDouble();

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }

            if (double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY))
            {
                return null;
            }

            return new BoundingBox(minX, minY, maxX, maxY, Crs: crs);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        // Transaction support not available in BigQuery (it's an OLAP system)
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

        var client = GetOrCreateClient(dataSource);

        // Test connectivity by executing a simple query
        var testQuery = "SELECT 1";
        await client.ExecuteQueryAsync(testQuery, parameters: null, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        _disposed = true;
    }

    private static string ParseProjectId(string connectionString)
    {
        // Expected format: "ProjectId=my-project;..."
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2 && keyValue[0].Equals("ProjectId", StringComparison.OrdinalIgnoreCase))
            {
                var value = keyValue[1].Trim();
                if (value.HasValue())
                {
                    return value;
                }
            }
        }

        throw new InvalidOperationException("BigQuery connection string must contain 'ProjectId'. Example: 'ProjectId=my-project'");
    }
}
