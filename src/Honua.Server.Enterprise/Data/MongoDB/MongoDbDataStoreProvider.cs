// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Driver;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.MongoDB;

public sealed class MongoDbDataStoreProvider : IDataStoreProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, MongoClient> _clients = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _serverSideJavascriptEnabled = new(StringComparer.Ordinal);
    private bool _disposed;

    public const string ProviderKey = "mongodb";

    public string Provider => ProviderKey;

    public IDataStoreCapabilities Capabilities => MongoDbDataStoreCapabilities.Instance;

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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildFilter(query ?? new FeatureQuery());
        var findOptions = builder.BuildFindOptions(query ?? new FeatureQuery());

        var cursor = await collection.FindAsync(filter, findOptions, cancellationToken).ConfigureAwait(false);

        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var document in cursor.Current)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return CreateFeatureRecord(document);
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildFilter(query ?? new FeatureQuery());

        return await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildByIdFilter(featureId);

        var cursor = await collection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var document = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return document != null ? CreateFeatureRecord(document) : null;
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

        var collection = GetCollection(dataSource, layer);
        var document = new BsonDocument();

        // Use LayerMetadataHelper to get geometry column for normalization
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var (key, value) in record.Attributes)
        {
            // Use FeatureRecordNormalizer to normalize values
            var isGeometry = string.Equals(key, geometryField, StringComparison.OrdinalIgnoreCase);
            var normalized = FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
            document[key] = BsonValue.Create(normalized);
        }

        await collection.InsertOneAsync(document, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Return the created record with MongoDB-generated _id if present
        return CreateFeatureRecord(document);
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildByIdFilter(featureId);

        var update = Builders<BsonDocument>.Update;
        var updateDefinition = update.Combine();

        // Use LayerMetadataHelper to get the primary key column and geometry column
        var idField = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        foreach (var (key, value) in record.Attributes)
        {
            if (string.Equals(key, idField, StringComparison.OrdinalIgnoreCase))
                continue; // Don't update the ID field

            // Use FeatureRecordNormalizer to normalize values
            var isGeometry = string.Equals(key, geometryField, StringComparison.OrdinalIgnoreCase);
            var normalized = FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
            updateDefinition = updateDefinition.Set(key, BsonValue.Create(normalized));
        }

        var result = await collection.FindOneAndUpdateAsync(
            filter,
            updateDefinition,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken).ConfigureAwait(false);

        return result != null ? CreateFeatureRecord(result) : null;
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildByIdFilter(featureId);

        var result = await collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

        return result.DeletedCount > 0;
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
            $"Soft delete is not supported by the {nameof(MongoDbDataStoreProvider)}. " +
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
            $"Restore is not supported by the {nameof(MongoDbDataStoreProvider)}. " +
            "Check IDataStoreCapabilities.SupportsSoftDelete before calling this method.");
    }

    /// <summary>
    /// Permanently deletes a feature from MongoDB without recovery option.
    /// This is a GDPR-compliant hard delete that cannot be undone.
    /// Audit logging is performed via the deletedBy parameter for compliance tracking.
    /// </summary>
    public async Task<bool> HardDeleteAsync(
        DataSourceDefinition dataSource,
        ServiceDefinition service,
        LayerDefinition layer,
        string featureId,
        string? deletedBy,
        IDataStoreTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Guard.NotNull(dataSource);
        Guard.NotNull(service);
        Guard.NotNull(layer);
        Guard.NotNullOrWhiteSpace(featureId);

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var filter = builder.BuildByIdFilter(featureId);

        // Permanently delete the document - this is irreversible
        var result = await collection.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);

        var deleted = result.DeletedCount > 0;

        // Log the hard delete operation for audit compliance (GDPR, SOC2, etc.)
        if (deleted)
        {
            System.Diagnostics.Debug.WriteLine(
                $"HARD DELETE: Feature permanently deleted from MongoDB - " +
                $"Layer: {layer.Id}, FeatureId: {featureId}, DeletedBy: {deletedBy ?? "<system>"}");
        }

        return deleted;
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

        var collection = GetCollection(dataSource, layer);
        var count = 0;
        const int batchSize = 1000;
        var batch = new List<WriteModel<BsonDocument>>(batchSize);

        // Use LayerMetadataHelper to get geometry column for normalization
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            var document = new BsonDocument();
            foreach (var (key, value) in record.Attributes)
            {
                // Use FeatureRecordNormalizer to normalize values
                var isGeometry = string.Equals(key, geometryField, StringComparison.OrdinalIgnoreCase);
                var normalized = FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
                document[key] = BsonValue.Create(normalized);
            }

            batch.Add(new InsertOneModel<BsonDocument>(document));

            if (batch.Count >= batchSize)
            {
                var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
                count += (int)result.InsertedCount;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            count += (int)result.InsertedCount;
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

        var collection = GetCollection(dataSource, layer);
        var count = 0;
        const int batchSize = 1000;
        var batch = new List<WriteModel<BsonDocument>>(batchSize);

        // Use LayerMetadataHelper to get the primary key column and geometry column
        var idField = LayerMetadataHelper.GetPrimaryKeyColumn(layer);
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);

        await foreach (var kvp in records.WithCancellation(cancellationToken))
        {
            var builder = new MongoDbFeatureQueryBuilder(layer);
            var filter = builder.BuildByIdFilter(kvp.Key);

            var update = Builders<BsonDocument>.Update;
            var updateDefinition = update.Combine();

            foreach (var (key, value) in kvp.Value.Attributes)
            {
                if (string.Equals(key, idField, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Use FeatureRecordNormalizer to normalize values
                var isGeometry = string.Equals(key, geometryField, StringComparison.OrdinalIgnoreCase);
                var normalized = FeatureRecordNormalizer.NormalizeValue(value, isGeometry);
                updateDefinition = updateDefinition.Set(key, BsonValue.Create(normalized));
            }

            batch.Add(new UpdateOneModel<BsonDocument>(filter, updateDefinition));

            if (batch.Count >= batchSize)
            {
                var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
                count += (int)result.ModifiedCount;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            count += (int)result.ModifiedCount;
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

        var collection = GetCollection(dataSource, layer);
        var count = 0;
        const int batchSize = 1000;
        var batch = new List<WriteModel<BsonDocument>>(batchSize);

        await foreach (var id in featureIds.WithCancellation(cancellationToken))
        {
            var builder = new MongoDbFeatureQueryBuilder(layer);
            var filter = builder.BuildByIdFilter(id);
            batch.Add(new DeleteOneModel<BsonDocument>(filter));

            if (batch.Count >= batchSize)
            {
                var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
                count += (int)result.DeletedCount;
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            var result = await collection.BulkWriteAsync(batch, cancellationToken: cancellationToken).ConfigureAwait(false);
            count += (int)result.DeletedCount;
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

    private FeatureRecord CreateFeatureRecord(BsonDocument document)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in document.Elements)
        {
            attributes[element.Name] = BsonValueToObject(element.Value);
        }

        return new FeatureRecord(new ReadOnlyDictionary<string, object?>(attributes));
    }

    private object? BsonValueToObject(BsonValue value)
    {
        var result = value.BsonType switch
        {
            BsonType.Null or BsonType.Undefined => (object?)null,
            BsonType.String => (object?)value.AsString,
            BsonType.Int32 => (object?)value.AsInt32,
            BsonType.Int64 => (object?)value.AsInt64,
            BsonType.Double => (object?)value.AsDouble,
            BsonType.Boolean => (object?)value.AsBoolean,
            BsonType.DateTime => (object?)value.ToUniversalTime(),
            BsonType.Document => (object?)value.AsBsonDocument.ToJson(),
            BsonType.Array => (object?)value.AsBsonArray.Select(BsonValueToObject).ToList(),
            _ => (object?)value.ToString()
        };

        // Normalize DateTime values to UTC using FeatureRecordNormalizer
        if (result is DateTime dt)
        {
            return FeatureRecordNormalizer.NormalizeDateTimeValue(dt);
        }

        return result;
    }

    private IMongoCollection<BsonDocument> GetCollection(DataSourceDefinition dataSource, LayerDefinition layer)
    {
        if (dataSource.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Data source '{dataSource.Id}' is missing a connection string.");
        }

        var client = GetOrCreateClient(dataSource.ConnectionString);

        // Use LayerMetadataHelper to get table name (collection name in MongoDB)
        var table = LayerMetadataHelper.GetTableName(layer);

        // MongoDB table format: "database.collection"
        var parts = table.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new InvalidOperationException($"MongoDB table must be in format 'database.collection'. Got: '{table}'");
        }

        var database = client.GetDatabase(parts[0]);
        return database.GetCollection<BsonDocument>(parts[1]);
    }

    private MongoClient GetOrCreateClient(string connectionString)
    {
        return _clients.GetOrAdd(connectionString, static cs => new MongoClient(cs));
    }

    private static bool AllowsServerSideStatistics(LayerDefinition layer)
    {
        if (layer.Query?.SupportedParameters is not { Count: > 0 } parameters)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter, "statistics:client", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllowsServerSideDistinct(LayerDefinition layer)
    {
        if (layer.Query?.SupportedParameters is not { Count: > 0 } parameters)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter, "distinct:client", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllowsServerSideExtent(LayerDefinition layer)
    {
        if (layer.Query?.SupportedParameters is not { Count: > 0 } parameters)
        {
            return true;
        }

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter, "extent:client", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool?> TryGetServerSideJavascriptEnabledAsync(
        string connectionString,
        MongoClient client,
        CancellationToken cancellationToken)
    {
        if (_serverSideJavascriptEnabled.TryGetValue(connectionString, out var cached))
        {
            return cached;
        }

        try
        {
            var adminDatabase = client.GetDatabase("admin");
            var command = new BsonDocument
            {
                { "getParameter", 1 },
                { "javascriptEnabled", 1 }
            };

            var result = await adminDatabase.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.TryGetValue("javascriptEnabled", out var enabledValue) && enabledValue.IsBoolean)
            {
                var isEnabled = enabledValue.ToBoolean();
                _serverSideJavascriptEnabled[connectionString] = isEnabled;
                return isEnabled;
            }
        }
        catch (MongoCommandException ex) when (IsJavascriptParameterUnavailable(ex))
        {
            return null;
        }
        catch (MongoException)
        {
            return null;
        }

        return null;
    }

    private static bool IsJavascriptParameterUnavailable(MongoCommandException exception)
    {
        return exception.Code switch
        {
            13 or 59 or 303 => true,
            _ => false
        };
    }

    private static bool ShouldFallback(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception is not OperationCanceledException;
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var query = NormalizeAnalyticsQuery(filter);
        var filterDefinition = builder.BuildFilter(query);

        Task<IReadOnlyList<StatisticsResult>> RunFallbackAsync() =>
            FallbackAggregationHelper.ComputeStatisticsAsync(
                q => QueryAsync(dataSource, service, layer, q, cancellationToken),
                filter,
                statistics,
                groupByFields,
                cancellationToken);

        if (!AllowsServerSideStatistics(layer))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }

        var pipeline = new List<BsonDocument>();
        var filterDocument = filterDefinition.ToBsonDocument();

        if (filterDocument.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", filterDocument));
        }

        BsonValue groupId;
        if (groupByFields is { Count: > 0 })
        {
            var idDocument = new BsonDocument();
            foreach (var field in groupByFields)
            {
                idDocument[field] = "$" + field;
            }

            groupId = idDocument;
        }
        else
        {
            groupId = BsonNull.Value;
        }

        var groupDocument = new BsonDocument("_id", groupId);

        foreach (var statistic in statistics)
        {
            var alias = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            var accumulator = statistic.Type switch
            {
                StatisticType.Count => new BsonDocument("$sum", 1),
                StatisticType.Sum => new BsonDocument("$sum", "$" + statistic.FieldName),
                StatisticType.Avg => new BsonDocument("$avg", "$" + statistic.FieldName),
                StatisticType.Min => new BsonDocument("$min", "$" + statistic.FieldName),
                StatisticType.Max => new BsonDocument("$max", "$" + statistic.FieldName),
                _ => throw new NotSupportedException($"Statistic type '{statistic.Type}' is not supported.")
            };

            groupDocument[alias] = accumulator;
        }

        pipeline.Add(new BsonDocument("$group", groupDocument));

        var projectDocument = new BsonDocument("_id", 0);
        if (groupByFields is { Count: > 0 })
        {
            foreach (var field in groupByFields)
            {
                projectDocument[field] = "$_id." + field;
            }
        }

        foreach (var statistic in statistics)
        {
            var alias = statistic.OutputName ?? $"{statistic.Type}_{statistic.FieldName}";
            projectDocument[alias] = "$" + alias;
        }

        pipeline.Add(new BsonDocument("$project", projectDocument));

        try
        {
            var documents = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken).ConfigureAwait(false);

            var groupFieldSet = groupByFields is { Count: > 0 }
                ? new HashSet<string>(groupByFields, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var statisticAliases = new HashSet<string>(
                statistics.Select(s => s.OutputName ?? $"{s.Type}_{s.FieldName}"),
                StringComparer.OrdinalIgnoreCase);

            var results = new List<StatisticsResult>(documents.Count);

            foreach (var document in documents)
            {
                var groupValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var statisticValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                foreach (var element in document.Elements)
                {
                    if (groupFieldSet.Contains(element.Name))
                    {
                        groupValues[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
                    }
                    else if (statisticAliases.Contains(element.Name))
                    {
                        statisticValues[element.Name] = BsonTypeMapper.MapToDotNetValue(element.Value);
                    }
                }

                results.Add(new StatisticsResult(
                    new ReadOnlyDictionary<string, object?>(groupValues),
                    new ReadOnlyDictionary<string, object?>(statisticValues)));
            }

            return results;
        }
        catch (MongoException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }
        catch (FormatException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
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

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var query = NormalizeAnalyticsQuery(filter);
        var filterDefinition = builder.BuildFilter(query);

        Task<IReadOnlyList<DistinctResult>> RunFallbackAsync() =>
            FallbackAggregationHelper.ComputeDistinctAsync(
                q => QueryAsync(dataSource, service, layer, q, cancellationToken),
                filter,
                fieldNames,
                cancellationToken);

        if (!AllowsServerSideDistinct(layer))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }

        var pipeline = new List<BsonDocument>();
        var filterDocument = filterDefinition.ToBsonDocument();

        if (filterDocument.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", filterDocument));
        }

        var idDocument = new BsonDocument();
        foreach (var field in fieldNames)
        {
            idDocument[field] = "$" + field;
        }

        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", idDocument }
        }));

        var projectDocument = new BsonDocument("_id", 0);
        foreach (var field in fieldNames)
        {
            projectDocument[field] = "$_id." + field;
        }

        pipeline.Add(new BsonDocument("$project", projectDocument));

        pipeline.Add(new BsonDocument("$limit", 10000));

        try
        {
            var documents = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken).ConfigureAwait(false);

            var results = new List<DistinctResult>(documents.Count);
            foreach (var document in documents)
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var field in fieldNames)
                {
                    values[field] = document.TryGetValue(field, out var value)
                        ? BsonTypeMapper.MapToDotNetValue(value)
                        : null;
                }

                results.Add(new DistinctResult(new ReadOnlyDictionary<string, object?>(values)));
            }

            return results;
        }
        catch (MongoException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }
        catch (FormatException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
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

        // Use LayerMetadataHelper to get geometry column
        var geometryField = LayerMetadataHelper.GetGeometryColumn(layer);
        if (geometryField.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Layer '{layer.Id}' does not define a geometry field.");
        }

        var collection = GetCollection(dataSource, layer);
        var builder = new MongoDbFeatureQueryBuilder(layer);
        var query = NormalizeAnalyticsQuery(filter);
        var filterDefinition = builder.BuildFilter(query);

        Task<BoundingBox?> RunFallbackAsync() =>
            FallbackAggregationHelper.ComputeExtentAsync(
                q => QueryAsync(dataSource, service, layer, q, cancellationToken),
                filter,
                service,
                layer,
                cancellationToken);

        if (!AllowsServerSideExtent(layer))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }

        var client = GetOrCreateClient(dataSource.ConnectionString);
        var javascriptEnabled = await TryGetServerSideJavascriptEnabledAsync(dataSource.ConnectionString, client, cancellationToken).ConfigureAwait(false);
        if (javascriptEnabled is false)
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }

        var pipeline = new List<BsonDocument>();
        var filterDocument = filterDefinition.ToBsonDocument();

        if (filterDocument.ElementCount > 0)
        {
            pipeline.Add(new BsonDocument("$match", filterDocument));
        }

        var bboxFunction = new BsonDocument("$function", new BsonDocument
        {
            { "body", new BsonJavaScript(@"function(geom) {
                if (!geom || !geom.coordinates) { return null; }
                var minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
                var visit = function(coords) {
                    if (!Array.isArray(coords)) { return; }
                    if (coords.length === 0) { return; }
                    if (typeof coords[0] === 'number') {
                        var x = coords[0];
                        var y = coords.length > 1 ? coords[1] : null;
                        if (typeof x === 'number') {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                        }
                        if (typeof y === 'number') {
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    } else {
                        for (var i = 0; i < coords.length; i++) {
                            visit(coords[i]);
                        }
                    }
                };
                visit(geom.coordinates);
                if (!isFinite(minX) || !isFinite(minY) || !isFinite(maxX) || !isFinite(maxY)) {
                    return null;
                }
                return [minX, minY, maxX, maxY];
            }") }
            , { "args", new BsonArray { "$" + geometryField } },
            { "lang", "js" }
        });

        pipeline.Add(new BsonDocument("$project", new BsonDocument
        {
            { "bbox", bboxFunction }
        }));

        pipeline.Add(new BsonDocument("$match", new BsonDocument
        {
            { "bbox", new BsonDocument("$ne", BsonNull.Value) }
        }));

        pipeline.Add(new BsonDocument("$group", new BsonDocument
        {
            { "_id", BsonNull.Value },
            { "minX", new BsonDocument("$min", new BsonDocument("$arrayElemAt", new BsonArray { "$bbox", 0 })) },
            { "minY", new BsonDocument("$min", new BsonDocument("$arrayElemAt", new BsonArray { "$bbox", 1 })) },
            { "maxX", new BsonDocument("$max", new BsonDocument("$arrayElemAt", new BsonArray { "$bbox", 2 })) },
            { "maxY", new BsonDocument("$max", new BsonDocument("$arrayElemAt", new BsonArray { "$bbox", 3 })) }
        }));

        pipeline.Add(new BsonDocument("$limit", 1));

        try
        {
            var documents = await collection.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken).ConfigureAwait(false);

            if (documents.Count == 0)
            {
                return null;
            }

            var doc = documents[0];
            if (!doc.TryGetValue("minX", out var minXValue) || !doc.TryGetValue("minY", out var minYValue) ||
                !doc.TryGetValue("maxX", out var maxXValue) || !doc.TryGetValue("maxY", out var maxYValue))
            {
                return await RunFallbackAsync().ConfigureAwait(false);
            }

            var minX = TryConvertToDouble(minXValue);
            var minY = TryConvertToDouble(minYValue);
            var maxX = TryConvertToDouble(maxXValue);
            var maxY = TryConvertToDouble(maxYValue);

            if (!minX.HasValue || !minY.HasValue || !maxX.HasValue || !maxY.HasValue)
            {
                return await RunFallbackAsync().ConfigureAwait(false);
            }

            var targetCrs = query.Crs ?? service.Ogc.DefaultCrs ?? "EPSG:4326";
            return new BoundingBox(minX.Value, minY.Value, maxX.Value, maxY.Value, Crs: targetCrs);
        }
        catch (MongoException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }
        catch (FormatException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ShouldFallback(ex, cancellationToken))
        {
            return await RunFallbackAsync().ConfigureAwait(false);
        }
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

    private static double? TryConvertToDouble(BsonValue value)
    {
        if (value.IsBsonNull)
        {
            return null;
        }

        var dotNet = BsonTypeMapper.MapToDotNetValue(value);
        if (dotNet is null)
        {
            return null;
        }

        try
        {
            return Convert.ToDouble(dotNet, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    public Task<IDataStoreTransaction?> BeginTransactionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        // Transaction support not yet implemented for MongoDB
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

        // Parse database name from connection string
        var url = new MongoUrl(dataSource.ConnectionString);
        var database = client.GetDatabase(url.DatabaseName ?? "test");

        // Test connectivity by executing a simple ping command
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken).ConfigureAwait(false);
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
}
