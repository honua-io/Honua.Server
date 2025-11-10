// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

/// <summary>
/// Base class for relational database-backed STAC catalog storage implementations.
/// This partial class contains core infrastructure including initialization, helpers, and SQL constants.
/// </summary>
internal abstract partial class RelationalStacCatalogStore : DisposableBase, IStacCatalogStore
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Configuration options for STAC search operations. Can be set by derived classes or dependency injection.
    /// </summary>
    protected StacSearchOptions SearchOptions { get; set; } = new();

    /// <summary>
    /// Optional logger for diagnostics. Can be null if logging is not available.
    /// </summary>
    protected ILogger? Logger { get; set; }

    protected abstract string ProviderName { get; }
    protected abstract IReadOnlyList<string> InitializationStatements { get; }
    protected abstract DbConnection CreateConnection();
    protected virtual ValueTask ConfigureConnectionAsync(DbConnection connection, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    protected virtual bool SupportsBboxFiltering => false;
    protected virtual string? GetBboxCoordinateExpression(int index) => null;

    /// <summary>
    /// Indicates whether this provider supports optimized bulk insert operations.
    /// Override in derived classes to enable database-specific bulk insert (COPY, BulkCopy, etc).
    /// </summary>
    protected virtual bool SupportsBulkInsert => false;

    /// <summary>
    /// Performs optimized bulk insert using database-specific APIs.
    /// Override in derived classes to implement COPY, BulkCopy, or other optimizations.
    /// </summary>
    protected virtual Task BulkInsertItemsAsync(DbConnection connection, DbTransaction transaction, IReadOnlyList<StacItemRecord> items, CancellationToken cancellationToken)
    {
        throw new NotSupportedException($"Bulk insert is not supported by {ProviderName}");
    }

    /// <summary>
    /// Builds the LIMIT clause for the database. Default uses standard SQL LIMIT syntax.
    /// SQL Server should override this to use OFFSET/FETCH NEXT syntax.
    /// </summary>
    /// <param name="limit">The maximum number of rows to return. Must be between 0 and 100000.</param>
    /// <returns>A SQL LIMIT clause string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when limit is outside the valid range.</exception>
    protected virtual string BuildLimitClause(int limit)
    {
        if (limit < 0 || limit > 100000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 0 and 100000.");
        }
        return $"LIMIT {limit}";
    }

    /// <summary>
    /// Builds the pagination clause using offset and limit. Default uses standard SQL syntax.
    /// SQL Server should override this to use OFFSET/FETCH NEXT syntax.
    /// </summary>
    protected virtual string BuildPaginationClause(int offset, int limit) => $"LIMIT {limit} OFFSET {offset}";

    #region SQL Constants

    private const string UpdateCollectionSql = @"update stac_collections
set title = @title,
    description = @description,
    license = @license,
    version = @version,
    keywords_json = @keywords,
    extent_json = @extent,
    properties_json = @properties,
    links_json = @links,
    extensions_json = @extensions,
    conforms_to = @conformsTo,
    data_source_id = @dataSourceId,
    service_id = @serviceId,
    layer_id = @layerId,
    etag = @newEtag,
    updated_at = @updatedAt
where id = @id
    and (@expectedETag is null or etag = @expectedETag)";

    private const string InsertCollectionSql = @"insert into stac_collections (
    id,
    title,
    description,
    license,
    version,
    keywords_json,
    extent_json,
    properties_json,
    links_json,
    extensions_json,
    conforms_to,
    data_source_id,
    service_id,
    layer_id,
    etag,
    created_at,
    updated_at)
values (
    @id,
    @title,
    @description,
    @license,
    @version,
    @keywords,
    @extent,
    @properties,
    @links,
    @extensions,
    @conformsTo,
    @dataSourceId,
    @serviceId,
    @layerId,
    @etag,
    @createdAt,
    @updatedAt)";

    private const string SelectCollectionsSql = @"select id, title, description, license, version, keywords_json, extent_json, properties_json, links_json, extensions_json, conforms_to, data_source_id, service_id, layer_id, etag, created_at, updated_at from stac_collections order by id";

    private const string SelectCollectionByIdSql = @"select id, title, description, license, version, keywords_json, extent_json, properties_json, links_json, extensions_json, conforms_to, data_source_id, service_id, layer_id, etag, created_at, updated_at from stac_collections where id = @id";

    private const string DeleteCollectionSql = @"delete from stac_collections where id = @id";

    private const string UpdateItemSql = @"update stac_items
set title = @title,
    description = @description,
    properties_json = @properties,
    assets_json = @assets,
    links_json = @links,
    extensions_json = @extensions,
    bbox_json = @bbox,
    geometry_json = @geometry,
    datetime = @datetime,
    start_datetime = @start,
    end_datetime = @end,
    raster_dataset_id = @rasterDatasetId,
    etag = @newEtag,
    updated_at = @updatedAt
where collection_id = @collectionId and id = @id
    and (@expectedETag is null or etag = @expectedETag)";

    private const string InsertItemSql = @"insert into stac_items (
    collection_id,
    id,
    title,
    description,
    properties_json,
    assets_json,
    links_json,
    extensions_json,
    bbox_json,
    geometry_json,
    datetime,
    start_datetime,
    end_datetime,
    raster_dataset_id,
    etag,
    created_at,
    updated_at)
values (
    @collectionId,
    @id,
    @title,
    @description,
    @properties,
    @assets,
    @links,
    @extensions,
    @bbox,
    @geometry,
    @datetime,
    @start,
    @end,
    @rasterDatasetId,
    @etag,
    @createdAt,
    @updatedAt)";

    private const string SelectItemsSql = @"select collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at from stac_items where collection_id = @collectionId order by id";

    private const string SelectItemByIdSql = @"select collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at from stac_items where collection_id = @collectionId and id = @id";

    private const string DeleteItemSql = @"delete from stac_items where collection_id = @collectionId and id = @id";

    #endregion

    #region Initialization

    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ConfigureConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

            foreach (var statement in InitializationStatements)
            {
                if (statement.IsNullOrWhiteSpace())
                {
                    continue;
                }

                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (HandleInitializationException(ex, statement))
                {
                    // Exception handled by provider-specific logic; continue with next statement.
                }
            }

            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    /// <summary>
    /// Allows provider-specific implementations to gracefully handle initialization errors such as duplicate index creation.
    /// </summary>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="statement">The statement being executed.</param>
    /// <returns>True if the exception was handled and execution should continue; otherwise false.</returns>
    protected virtual bool HandleInitializationException(Exception exception, string statement) => false;

    #endregion

    #region Helper Methods

    protected static async Task<int> ExecuteNonQueryAsync(DbConnection connection, DbTransaction transaction, string sql, IReadOnlyList<(string Name, object? Value)> parameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            AddParameter(command, parameter.Name, parameter.Value);
        }

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected;
    }

    protected static IReadOnlyList<(string Name, object? Value)> BuildCollectionParameters(StacCollectionRecord collection, string? newETag = null, string? expectedETag = null)
    {
        var keywordsJson = StacJsonSerializer.SerializeKeywords(collection.Keywords);
        var extentJson = StacJsonSerializer.SerializeExtent(collection.Extent);
        var propertiesJson = StacJsonSerializer.SerializeNode(collection.Properties);
        var linksJson = StacJsonSerializer.SerializeLinks(collection.Links);
        var extensionsJson = StacJsonSerializer.SerializeExtensions(collection.Extensions);

        // Use the new ETag if provided, otherwise fall back to the collection's ETag
        var etagToUse = newETag ?? collection.ETag;

        return new List<(string, object?)>
        {
            ("@id", collection.Id),
            ("@title", collection.Title),
            ("@description", collection.Description),
            ("@license", collection.License),
            ("@version", collection.Version),
            ("@keywords", keywordsJson),
            ("@extent", extentJson),
            ("@properties", propertiesJson),
            ("@links", linksJson),
            ("@extensions", extensionsJson),
            ("@conformsTo", collection.ConformsTo),
            ("@dataSourceId", collection.DataSourceId),
            ("@serviceId", collection.ServiceId),
            ("@layerId", collection.LayerId),
            ("@etag", etagToUse),
            ("@newEtag", etagToUse),
            ("@expectedETag", expectedETag),
            ("@createdAt", collection.CreatedAtUtc.UtcDateTime),
            ("@updatedAt", collection.UpdatedAtUtc.UtcDateTime)
        };
    }

    protected static IReadOnlyList<(string Name, object? Value)> BuildItemParameters(StacItemRecord item, string? newETag = null, string? expectedETag = null)
    {
        var propertiesJson = StacJsonSerializer.SerializeNode(item.Properties);
        var assetsJson = StacJsonSerializer.SerializeAssets(item.Assets);
        var linksJson = StacJsonSerializer.SerializeLinks(item.Links);
        var extensionsJson = StacJsonSerializer.SerializeExtensions(item.Extensions);
        var bboxJson = item.Bbox is null ? null : StacJsonSerializer.SerializeBbox(item.Bbox);

        // Use the new ETag if provided, otherwise fall back to the item's ETag
        var etagToUse = newETag ?? item.ETag;

        return new List<(string, object?)>
        {
            ("@collectionId", item.CollectionId),
            ("@id", item.Id),
            ("@title", item.Title),
            ("@description", item.Description),
            ("@properties", propertiesJson),
            ("@assets", assetsJson),
            ("@links", linksJson),
            ("@extensions", extensionsJson),
            ("@bbox", bboxJson),
            ("@geometry", item.Geometry),
            ("@datetime", item.Datetime?.UtcDateTime),
            ("@start", item.StartDatetime?.UtcDateTime),
            ("@end", item.EndDatetime?.UtcDateTime),
            ("@rasterDatasetId", item.RasterDatasetId),
            ("@etag", etagToUse),
            ("@newEtag", etagToUse),
            ("@expectedETag", expectedETag),
            ("@createdAt", item.CreatedAtUtc.UtcDateTime),
            ("@updatedAt", item.UpdatedAtUtc.UtcDateTime)
        };
    }

    protected static StacCollectionRecord ReadCollection(DbDataReader reader)
    {
        var id = reader.GetString(0);
        var title = reader.IsDBNull(1) ? null : reader.GetString(1);
        var description = reader.IsDBNull(2) ? null : reader.GetString(2);
        var license = reader.IsDBNull(3) ? null : reader.GetString(3);
        var version = reader.IsDBNull(4) ? null : reader.GetString(4);
        var keywordsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
        var extentJson = reader.IsDBNull(6) ? null : reader.GetString(6);
        var propertiesJson = reader.IsDBNull(7) ? null : reader.GetString(7);
        var linksJson = reader.IsDBNull(8) ? null : reader.GetString(8);
        var extensionsJson = reader.IsDBNull(9) ? null : reader.GetString(9);
        var conformsTo = reader.IsDBNull(10) ? null : reader.GetString(10);
        var dataSourceId = reader.IsDBNull(11) ? null : reader.GetString(11);
        var serviceId = reader.IsDBNull(12) ? null : reader.GetString(12);
        var layerId = reader.IsDBNull(13) ? null : reader.GetString(13);
        var etag = reader.IsDBNull(14) ? null : reader.GetString(14);
        var createdAt = ReadDateTimeOffset(reader, 15);
        var updatedAt = ReadDateTimeOffset(reader, 16);

        return new StacCollectionRecord
        {
            Id = id,
            Title = title,
            Description = description,
            License = license,
            Version = version,
            Keywords = StacJsonSerializer.DeserializeKeywords(keywordsJson),
            Extent = StacJsonSerializer.DeserializeExtent(extentJson),
            Properties = StacJsonSerializer.DeserializeNode(propertiesJson),
            Links = StacJsonSerializer.DeserializeLinks(linksJson),
            Extensions = StacJsonSerializer.DeserializeExtensions(extensionsJson),
            ConformsTo = conformsTo,
            DataSourceId = dataSourceId,
            ServiceId = serviceId,
            LayerId = layerId,
            ETag = etag,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = updatedAt
        };
    }

    protected static StacItemRecord ReadItem(DbDataReader reader)
    {
        var collectionId = reader.GetString(0);
        var id = reader.GetString(1);
        var title = reader.IsDBNull(2) ? null : reader.GetString(2);
        var description = reader.IsDBNull(3) ? null : reader.GetString(3);
        var propertiesJson = reader.IsDBNull(4) ? null : reader.GetString(4);
        var assetsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
        var linksJson = reader.IsDBNull(6) ? null : reader.GetString(6);
        var extensionsJson = reader.IsDBNull(7) ? null : reader.GetString(7);
        var bboxJson = reader.IsDBNull(8) ? null : reader.GetString(8);
        var geometryJson = reader.IsDBNull(9) ? null : reader.GetString(9);
        var datetime = ReadNullableDateTimeOffset(reader, 10);
        var start = ReadNullableDateTimeOffset(reader, 11);
        var end = ReadNullableDateTimeOffset(reader, 12);
        var rasterDatasetId = reader.IsDBNull(13) ? null : reader.GetString(13);
        var etag = reader.IsDBNull(14) ? null : reader.GetString(14);
        var createdAt = ReadDateTimeOffset(reader, 15);
        var updatedAt = ReadDateTimeOffset(reader, 16);

        return new StacItemRecord
        {
            Id = id,
            CollectionId = collectionId,
            Title = title,
            Description = description,
            Properties = StacJsonSerializer.DeserializeNode(propertiesJson),
            Assets = StacJsonSerializer.DeserializeAssets(assetsJson),
            Links = StacJsonSerializer.DeserializeLinks(linksJson),
            Extensions = StacJsonSerializer.DeserializeExtensions(extensionsJson),
            Bbox = StacJsonSerializer.DeserializeBbox(bboxJson),
            Geometry = geometryJson,
            Datetime = datetime,
            StartDatetime = start,
            EndDatetime = end,
            RasterDatasetId = rasterDatasetId,
            ETag = etag,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = updatedAt
        };
    }

    protected static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static DateTimeOffset ReadDateTimeOffset(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return DateTimeOffset.MinValue;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => DateTimeOffset.MinValue
        };
    }

    private static DateTimeOffset? ReadNullableDateTimeOffset(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            string s when DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) => parsed,
            _ => null
        };
    }

    #endregion

    #region Disposal

    protected override void DisposeCore()
    {
        _initializationLock.Dispose();
    }

    #endregion
}
