// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Amazon.RedshiftDataAPIService;
using Amazon.RedshiftDataAPIService.Model;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Enterprise.Data.Redshift;

/// <summary>
/// Redshift data provider using AWS Redshift Data API
/// </summary>
public sealed class RedshiftDataStoreProvider : IDataStoreProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, AmazonRedshiftDataAPIServiceClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RedshiftConnectionInfo> _connectionInfos = new(StringComparer.Ordinal);
    private bool _disposed;

    public const string ProviderKey = "redshift";

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => RedshiftDataStoreCapabilities.Instance;

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
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildSelect(query ?? new FeatureQuery());

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);

        // Wait for the query to complete
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        // Fetch results
        var resultRequest = new GetStatementResultRequest { Id = response.Id };
        var resultResponse = await client.GetStatementResultAsync(resultRequest, cancellationToken).ConfigureAwait(false);

        foreach (var row in resultResponse.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return CreateFeatureRecord(row, resultResponse.ColumnMetadata, layer);
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
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildCount(query ?? new FeatureQuery());

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        var resultRequest = new GetStatementResultRequest { Id = response.Id };
        var resultResponse = await client.GetStatementResultAsync(resultRequest, cancellationToken).ConfigureAwait(false);

        if (resultResponse.Records.Count > 0 && resultResponse.Records[0].Count > 0)
        {
            var countField = resultResponse.Records[0][0];
            return countField.LongValue ?? 0L;
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
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildById(featureId);

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        var resultRequest = new GetStatementResultRequest { Id = response.Id };
        var resultResponse = await client.GetStatementResultAsync(resultRequest, cancellationToken).ConfigureAwait(false);

        if (resultResponse.Records.Count > 0)
        {
            return CreateFeatureRecord(resultResponse.Records[0], resultResponse.ColumnMetadata, layer);
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
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildInsert(record);

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        // Fetch the inserted record
        var idValue = record.Attributes.TryGetValue(layer.IdField, out var id) ? id : null;
        if (idValue != null)
        {
            var inserted = await GetAsync(dataSource, service, layer, idValue.ToString()!, null, cancellationToken).ConfigureAwait(false);
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
        Guard.NotNullOrWhiteSpace(featureId);
        Guard.NotNull(record);

        var client = GetOrCreateClient(dataSource);
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildUpdate(featureId, record);

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        // Check result status to see if rows were affected
        var describeRequest = new DescribeStatementRequest { Id = response.Id };
        var describeResponse = await client.DescribeStatementAsync(describeRequest, cancellationToken).ConfigureAwait(false);

        if (describeResponse.Status == StatusString.FAILED)
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
        Guard.NotNullOrWhiteSpace(featureId);

        var client = GetOrCreateClient(dataSource);
        var connectionInfo = GetConnectionInfo(dataSource);
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var (sql, parameters) = builder.BuildDelete(featureId);

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        var describeRequest = new DescribeStatementRequest { Id = response.Id };
        var describeResponse = await client.DescribeStatementAsync(describeRequest, cancellationToken).ConfigureAwait(false);

        return describeResponse.Status == StatusString.FINISHED;
    }

    /// <summary>
    /// Inserts multiple feature records using batched multi-row INSERT VALUES statements.
    /// Reduces API calls by 100x compared to individual inserts (1 batch + 1 poll vs N * (1 execute + N polls)).
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

        const int batchSize = 100; // Redshift Data API works well with 100-row batches
        var batch = new List<FeatureRecord>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);
        var connectionInfo = GetConnectionInfo(dataSource);

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            batch.Add(record);

            if (batch.Count >= batchSize)
            {
                totalCount += await ExecuteMultiRowInsertAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalCount += await ExecuteMultiRowInsertAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return totalCount;
    }

    /// <summary>
    /// Updates multiple feature records using batched UPDATE statements.
    /// Reduces API calls by batching updates into transaction blocks (1 batch + 1 poll vs N * (1 execute + N polls)).
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

        const int batchSize = 50; // Smaller batch for updates due to complexity
        var batch = new List<KeyValuePair<string, FeatureRecord>>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);
        var connectionInfo = GetConnectionInfo(dataSource);

        await foreach (var kvp in records.WithCancellation(cancellationToken))
        {
            batch.Add(kvp);

            if (batch.Count >= batchSize)
            {
                totalCount += await ExecuteBatchedUpdateAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalCount += await ExecuteBatchedUpdateAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
        }

        return totalCount;
    }

    /// <summary>
    /// Deletes multiple features using batched DELETE IN clauses.
    /// Reduces API calls by 100x compared to individual deletes (1 batch + 1 poll vs N * (1 execute + N polls)).
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

        const int batchSize = 1000; // DELETE IN can handle more IDs
        var batch = new List<string>(batchSize);
        var totalCount = 0;

        var client = GetOrCreateClient(dataSource);
        var connectionInfo = GetConnectionInfo(dataSource);

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            batch.Add(id);

            if (batch.Count >= batchSize)
            {
                totalCount += await ExecuteBatchedDeleteAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            totalCount += await ExecuteBatchedDeleteAsync(client, connectionInfo, layer, batch, cancellationToken).ConfigureAwait(false);
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
        return Task.FromResult<byte[]?>(null);
    }

    private FeatureRecord CreateFeatureRecord(List<Field> row, List<ColumnMetadata> columnMetadata, LayerDefinition layer)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var geometryField = layer.GeometryField;

        for (var i = 0; i < row.Count && i < columnMetadata.Count; i++)
        {
            var columnName = columnMetadata[i].Name;
            var field = row[i];

            // Handle _geojson column specially
            if (string.Equals(columnName, "_geojson", StringComparison.OrdinalIgnoreCase) && field.StringValue != null)
            {
                // Use FeatureRecordNormalizer for consistent geometry parsing
                attributes[geometryField] = FeatureRecordNormalizer.ParseGeometry(field.StringValue);
            }
            else if (!string.Equals(columnName, "_geojson", StringComparison.OrdinalIgnoreCase))
            {
                // Use FeatureRecordNormalizer for consistent value normalization
                var normalizedValue = ExtractFieldValue(field);
                attributes[columnName] = FeatureRecordNormalizer.NormalizeValue(normalizedValue, isGeometry: false);
            }
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    private static object? ExtractFieldValue(Field field)
    {
        if (field.IsNull == true) return null;
        if (field.StringValue != null) return field.StringValue;
        if (field.LongValue != null && field.LongValue != 0) return field.LongValue;
        if (field.DoubleValue != null && field.DoubleValue != 0.0) return field.DoubleValue;
        if (field.BooleanValue == true) return field.BooleanValue;
        if (field.BlobValue != null) return field.BlobValue.ToArray();
        return null;
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

    private async Task<GetStatementResultResponse> ExecuteStatementAsync(
        AmazonRedshiftDataAPIServiceClient client,
        RedshiftConnectionInfo connectionInfo,
        string sql,
        List<SqlParameter> parameters,
        CancellationToken cancellationToken)
    {
        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        var resultRequest = new GetStatementResultRequest { Id = response.Id };
        return await client.GetStatementResultAsync(resultRequest, cancellationToken).ConfigureAwait(false);
    }

    private static BoundingBox? TryParseExtent(string geoJson, string? crs)
    {
        // Use GeometryReader for consistent geometry parsing and extent extraction
        return GeometryReader.TryParseExtentFromGeoJson(geoJson, crs);
    }

    /// <summary>
    /// Executes a batched DELETE statement using an IN clause for multiple feature IDs.
    /// Uses unique parameter names per ID to prevent parameter collision bugs.
    /// </summary>
    private async Task<int> ExecuteBatchedDeleteAsync(
        AmazonRedshiftDataAPIServiceClient client,
        RedshiftConnectionInfo connectionInfo,
        LayerDefinition layer,
        List<string> featureIds,
        CancellationToken cancellationToken)
    {
        if (featureIds.Count == 0)
        {
            return 0;
        }

        var builder = new RedshiftFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();

        // Validate ID field name to prevent SQL injection
        SqlIdentifierValidator.ValidateIdentifier(layer.IdField, nameof(layer.IdField));
        var keyColumn = QuoteIdentifier(layer.IdField);

        var parameters = new List<SqlParameter>();
        var paramNames = new List<string>();

        // Validate all feature IDs are non-null/empty before building query
        for (int i = 0; i < featureIds.Count; i++)
        {
            if (featureIds[i].IsNullOrWhiteSpace())
            {
                throw new ArgumentException($"Feature ID at index {i} is null or empty.", nameof(featureIds));
            }

            var paramName = $"id{i}";
            paramNames.Add($":{paramName}");
            parameters.Add(new SqlParameter { Name = paramName, Value = featureIds[i] });
        }

        var sql = $"DELETE FROM {table} WHERE {keyColumn} IN ({string.Join(", ", paramNames)})";

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        return featureIds.Count;
    }

    /// <summary>
    /// Executes batched UPDATE statements in a single SQL statement with semicolon-separated commands.
    /// Uses unique parameter names per batch item to prevent parameter collision bugs.
    /// </summary>
    private async Task<int> ExecuteBatchedUpdateAsync(
        AmazonRedshiftDataAPIServiceClient client,
        RedshiftConnectionInfo connectionInfo,
        LayerDefinition layer,
        List<KeyValuePair<string, FeatureRecord>> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        var builder = new RedshiftFeatureQueryBuilder(layer);
        var sqlStatements = new List<string>();
        var allParameters = new List<SqlParameter>();

        for (int batchIndex = 0; batchIndex < batch.Count; batchIndex++)
        {
            var kvp = batch[batchIndex];
            var (updateSql, updateParams) = builder.BuildUpdate(kvp.Key, kvp.Value);

            // Create parameter name mapping to avoid conflicts across batched statements
            var parameterMapping = new Dictionary<string, string>(StringComparer.Ordinal);
            var renamedSql = updateSql;

            // Build mapping and rename parameters with unique batch-specific prefix
            foreach (var param in updateParams)
            {
                var oldParamName = $":{param.Name}";
                var newParamName = $":u{batchIndex}_{param.Name}";
                parameterMapping[oldParamName] = newParamName;

                // Add parameter with unique name to avoid collisions
                allParameters.Add(new SqlParameter
                {
                    Name = $"u{batchIndex}_{param.Name}",
                    Value = param.Value
                });
            }

            // Replace parameter names in SQL using exact matches to avoid partial replacements
            // Sort by length descending to handle overlapping names correctly (e.g., :p0 vs :p01)
            foreach (var mapping in parameterMapping.OrderByDescending(m => m.Key.Length))
            {
                renamedSql = renamedSql.Replace(mapping.Key, mapping.Value);
            }

            sqlStatements.Add(renamedSql);
        }

        // Combine all UPDATE statements with semicolons
        var combinedSql = string.Join("; ", sqlStatements);

        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = combinedSql,
            Parameters = allParameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        return batch.Count;
    }

    /// <summary>
    /// Executes a multi-row INSERT VALUES statement for a batch of records.
    /// </summary>
    private async Task<int> ExecuteMultiRowInsertAsync(
        AmazonRedshiftDataAPIServiceClient client,
        RedshiftConnectionInfo connectionInfo,
        LayerDefinition layer,
        List<FeatureRecord> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return 0;
        }

        // Build multi-row INSERT VALUES statement
        var builder = new RedshiftFeatureQueryBuilder(layer);
        var table = builder.GetTableExpression();

        // Get columns from first record (all records should have same structure)
        var columns = batch[0].Attributes.Keys
            .Where(k => !string.Equals(k, "_geojson", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Validate all column names to prevent SQL injection
        foreach (var column in columns)
        {
            SqlIdentifierValidator.ValidateIdentifier(column, nameof(column));
        }

        var columnList = string.Join(", ", columns.Select(QuoteIdentifier));

        // Build VALUES clauses and parameters
        var valuesClauses = new List<string>();
        var parameters = new List<SqlParameter>();

        for (int i = 0; i < batch.Count; i++)
        {
            var record = batch[i];
            var paramNames = new List<string>();

            for (int colIdx = 0; colIdx < columns.Count; colIdx++)
            {
                var column = columns[colIdx];
                var paramName = $"p{i}_{colIdx}";
                paramNames.Add($":{paramName}");

                var value = record.Attributes.TryGetValue(column, out var v) ? v : null;
                // Use SqlParameterHelper for consistent parameter value formatting
                parameters.Add(new SqlParameter
                {
                    Name = paramName,
                    Value = SqlParameterHelper.FormatParameterValue(value)
                });
            }

            valuesClauses.Add($"({string.Join(", ", paramNames)})");
        }

        var sql = $"INSERT INTO {table} ({columnList}) VALUES {string.Join(", ", valuesClauses)}";

        // Execute with single API call
        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = sql,
            Parameters = parameters
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);

        return batch.Count;
    }

    /// <summary>
    /// Quotes a Redshift identifier using double quotes, with SQL injection protection.
    /// Handles qualified names (e.g., schema.table) by quoting each part individually.
    /// </summary>
    private static string QuoteIdentifier(string identifier)
    {
        SqlIdentifierValidator.ValidateIdentifier(identifier, nameof(identifier));

        // Split on dots to handle qualified names (schema.table, database.schema.table)
        var parts = identifier.Split('.', StringSplitOptions.RemoveEmptyEntries);

        // Quote each part individually
        for (var i = 0; i < parts.Length; i++)
        {
            var unquoted = parts[i].Trim('"'); // Remove existing quotes if any
            parts[i] = $"\"{unquoted.Replace("\"", "\"\"")}\"";
        }

        return string.Join('.', parts);
    }

    /// <summary>
    /// Waits for a Redshift statement to complete using exponential backoff polling.
    /// Starts at 100ms and increases to a maximum of 2000ms to reduce API calls.
    /// </summary>
    private async Task WaitForStatementCompletionAsync(
        AmazonRedshiftDataAPIServiceClient client,
        string statementId,
        CancellationToken cancellationToken)
    {
        var delayMs = 100;
        const int maxDelayMs = 2000;
        const double backoffMultiplier = 1.5;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var describeRequest = new DescribeStatementRequest { Id = statementId };
            var describeResponse = await client.DescribeStatementAsync(describeRequest, cancellationToken).ConfigureAwait(false);

            if (describeResponse.Status == StatusString.FINISHED)
            {
                return;
            }

            if (describeResponse.Status == StatusString.FAILED)
            {
                throw new InvalidOperationException($"Redshift statement failed: {describeResponse.Error}");
            }

            if (describeResponse.Status == StatusString.ABORTED)
            {
                throw new OperationCanceledException("Redshift statement was aborted.");
            }

            // Exponential backoff: 100ms -> 150ms -> 225ms -> ... -> 2000ms (max)
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            delayMs = Math.Min((int)(delayMs * backoffMultiplier), maxDelayMs);
        }
    }

    private AmazonRedshiftDataAPIServiceClient GetOrCreateClient(DataSourceDefinition dataSource)
    {
        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var connectionString = dataSource.ConnectionString;

        _connectionInfos.GetOrAdd(connectionString, key => ParseConnectionInfo(dataSource.Id, key));

        return _clients.GetOrAdd(connectionString, static _ => new AmazonRedshiftDataAPIServiceClient());
    }

    private RedshiftConnectionInfo GetConnectionInfo(DataSourceDefinition dataSource)
    {
        var connectionString = dataSource.ConnectionString;
        return _connectionInfos.GetOrAdd(connectionString!, key => ParseConnectionInfo(dataSource.Id, key));
    }

    private static RedshiftConnectionInfo ParseConnectionInfo(string dataSourceId, string connectionString)
    {
        // Expected format: "ClusterIdentifier=my-cluster;Database=mydb;DbUser=admin"
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        string? clusterIdentifier = null;
        string? database = null;
        string? dbUser = null;

        foreach (var part in parts)
        {
            var keyValue = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
            if (keyValue.Length == 2)
            {
                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                if (key.Equals("ClusterIdentifier", StringComparison.OrdinalIgnoreCase))
                {
                    clusterIdentifier = value;
                }
                else if (key.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    database = value;
                }
                else if (key.Equals("DbUser", StringComparison.OrdinalIgnoreCase))
                {
                    dbUser = value;
                }
            }
        }

        if (clusterIdentifier.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Redshift connection string for '{dataSourceId}' must contain 'ClusterIdentifier'.");
        }

        if (database.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Redshift connection string for '{dataSourceId}' must contain 'Database'.");
        }

        if (dbUser.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Redshift connection string for '{dataSourceId}' must contain 'DbUser'.");
        }

        return new RedshiftConnectionInfo(clusterIdentifier, database, dbUser);
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
            var connectionInfo = GetConnectionInfo(dataSource);
            var builder = new RedshiftFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildStatistics(query, statistics, groupByFields);

            var response = await ExecuteStatementAsync(client, connectionInfo, sql, parameters, cancellationToken).ConfigureAwait(false);

            var groupFieldSet = groupByFields is { Count: > 0 }
                ? new HashSet<string>(groupByFields, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var statisticAliases = new HashSet<string>(
                statistics.Select(s => s.OutputName ?? $"{s.Type}_{s.FieldName}"),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<StatisticsResult>();
            var metadata = response.ColumnMetadata;

            foreach (var record in response.Records)
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < record.Count && index < metadata.Count; index++)
                {
                    var columnName = metadata[index].Name;
                    var value = ExtractFieldValue(record[index]);

                    if (groupFieldSet.Contains(columnName))
                    {
                        groupValues[columnName] = value;
                    }
                    else if (statisticAliases.Contains(columnName))
                    {
                        statisticValues[columnName] = value;
                    }
                    else if (groupFieldSet.Count > 0)
                    {
                        groupValues[columnName] = value;
                    }
                    else
                    {
                        statisticValues[columnName] = value;
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
            var connectionInfo = GetConnectionInfo(dataSource);
            var builder = new RedshiftFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildDistinct(query, fieldNames);

            var response = await ExecuteStatementAsync(client, connectionInfo, sql, parameters, cancellationToken).ConfigureAwait(false);
            var metadata = response.ColumnMetadata;

            var results = new List<DistinctResult>();
            foreach (var record in response.Records)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                for (var index = 0; index < record.Count && index < metadata.Count; index++)
                {
                    var columnName = metadata[index].Name;
                    values[columnName] = ExtractFieldValue(record[index]);
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
        Guard.NotNull(service);

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
            var connectionInfo = GetConnectionInfo(dataSource);
            var builder = new RedshiftFeatureQueryBuilder(layer);
            var query = NormalizeAnalyticsQuery(filter);
            var (sql, parameters) = builder.BuildExtent(query);

            var response = await ExecuteStatementAsync(client, connectionInfo, sql, parameters, cancellationToken).ConfigureAwait(false);

            foreach (var record in response.Records)
            {
                if (record.Count == 0)
                {
                    continue;
                }

                var geoJson = ExtractFieldValue(record[0]) as string;
                if (geoJson.IsNullOrWhiteSpace())
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
        }

        return false;
    }

    private static bool ContainsSpatialCapabilityIndicator(string? message)
    {
        if (message.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (message.Contains("geometry", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("disabled", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var spatialTokens = new[]
        {
            "st_",
            "listagg",
            "st_groupby",
            "geospatial",
            "geography"
        };

        if (!spatialTokens.Any(token => message.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var failureIndicators = new[]
        {
            "does not exist",
            "not found",
            "not enabled",
            "disabled",
            "permission denied",
            "not authorized",
            "not supported",
            "requires superuser",
            "unsupported",
            "syntax error",
            "failed"
        };

        return failureIndicators.Any(indicator => message.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        // Transaction support not yet implemented for Redshift
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
        var connectionInfo = GetConnectionInfo(dataSource);

        // Test connectivity by executing a simple query
        var executeRequest = new ExecuteStatementRequest
        {
            ClusterIdentifier = connectionInfo.ClusterIdentifier,
            Database = connectionInfo.Database,
            DbUser = connectionInfo.DbUser,
            Sql = "SELECT 1"
        };

        var response = await client.ExecuteStatementAsync(executeRequest, cancellationToken).ConfigureAwait(false);
        await WaitForStatementCompletionAsync(client, response.Id, cancellationToken).ConfigureAwait(false);
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
            $"Soft delete is not supported by the {nameof(RedshiftDataStoreProvider)}. " +
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
            $"Restore is not supported by the {nameof(RedshiftDataStoreProvider)}. " +
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
            $"Hard delete is not supported by the {nameof(RedshiftDataStoreProvider)}. " +
            "Use the standard DeleteAsync method for permanent deletion.");
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
        _connectionInfos.Clear();
        _disposed = true;
    }

    private sealed record RedshiftConnectionInfo(string ClusterIdentifier, string Database, string DbUser);
}
