// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.Postgres;
using Npgsql;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

internal sealed class PostgresStacCatalogStore : RelationalStacCatalogStore
{
    private static readonly IReadOnlyList<string> SchemaStatements = new[]
    {
        @"CREATE TABLE IF NOT EXISTS stac_collections (
    id TEXT PRIMARY KEY,
    title TEXT,
    description TEXT,
    license TEXT,
    version TEXT,
    keywords_json TEXT NOT NULL,
    extent_json TEXT NOT NULL,
    properties_json TEXT,
    links_json TEXT NOT NULL,
    extensions_json TEXT NOT NULL,
    conforms_to TEXT,
    data_source_id TEXT,
    service_id TEXT,
    layer_id TEXT,
    etag TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);",
        @"CREATE TABLE IF NOT EXISTS stac_items (
    collection_id TEXT NOT NULL,
    id TEXT NOT NULL,
    title TEXT,
    description TEXT,
    properties_json TEXT,
    assets_json TEXT NOT NULL,
    links_json TEXT NOT NULL,
    extensions_json TEXT NOT NULL,
    bbox_json TEXT,
    geometry_json TEXT,
    datetime TIMESTAMPTZ,
    start_datetime TIMESTAMPTZ,
    end_datetime TIMESTAMPTZ,
    raster_dataset_id TEXT,
    etag TEXT,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (collection_id, id),
    FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE
);",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_collection ON stac_items(collection_id);",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start ON stac_items(collection_id, COALESCE(start_datetime, datetime));",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end ON stac_items(collection_id, COALESCE(end_datetime, datetime));",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range ON stac_items(collection_id, COALESCE(start_datetime, datetime), COALESCE(end_datetime, datetime), id);",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;",
        @"CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;"
    };

    private readonly string _connectionString;

    public PostgresStacCatalogStore(string connectionString)
    {
        Guard.NotNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    protected override string ProviderName => PostgresDataStoreProvider.ProviderKey;
    protected override IReadOnlyList<string> InitializationStatements => SchemaStatements;

    protected override DbConnection CreateConnection() => new NpgsqlConnection(_connectionString);

    protected override bool SupportsBboxFiltering => true;

    protected override string? GetBboxCoordinateExpression(int index)
    {
        return $"CAST((bbox_json::json->>{index}) AS double precision)";
    }

    protected override bool SupportsBulkInsert => true;

    protected override string? MapPropertyFieldToColumn(string propertyName)
    {
        // Remove "properties." prefix if present
        var prop = propertyName.StartsWith("properties.")
            ? propertyName.Substring("properties.".Length)
            : propertyName;

        // PostgreSQL JSON path syntax: properties_json::json->>'field_name'
        // This extracts the property as text which can be sorted
        // For numeric properties, we would ideally cast, but text sorting works for most cases
        return $"properties_json::json->>'{prop}'";
    }

    protected override async Task BulkInsertItemsAsync(DbConnection connection, DbTransaction transaction, IReadOnlyList<StacItemRecord> items, CancellationToken cancellationToken)
    {
        if (connection is not NpgsqlConnection npgsqlConnection)
        {
            throw new InvalidOperationException("Connection must be NpgsqlConnection for PostgreSQL bulk insert");
        }

        // First, delete items that will be updated (upsert behavior)
        // We'll do this in batches to avoid creating an overly long IN clause
        const int deleteBatchSize = 500;
        for (var i = 0; i < items.Count; i += deleteBatchSize)
        {
            var batch = items.Skip(i).Take(deleteBatchSize).ToList();
            await using var deleteCmd = npgsqlConnection.CreateCommand();
            deleteCmd.Transaction = (NpgsqlTransaction)transaction;

            var deleteConditions = new List<string>();
            for (var j = 0; j < batch.Count; j++)
            {
                var paramCollectionId = $"@delCol{j}";
                var paramItemId = $"@delItem{j}";
                deleteConditions.Add($"(collection_id = {paramCollectionId} AND id = {paramItemId})");
                deleteCmd.Parameters.AddWithValue(paramCollectionId, batch[j].CollectionId);
                deleteCmd.Parameters.AddWithValue(paramItemId, batch[j].Id);
            }

            deleteCmd.CommandText = $"DELETE FROM stac_items WHERE {string.Join(" OR ", deleteConditions)}";
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Now bulk insert all items using COPY
        await using var writer = await npgsqlConnection.BeginBinaryImportAsync(
            "COPY stac_items (collection_id, id, title, description, properties_json, assets_json, links_json, extensions_json, bbox_json, geometry_json, datetime, start_datetime, end_datetime, raster_dataset_id, etag, created_at, updated_at) FROM STDIN (FORMAT BINARY)",
            cancellationToken).ConfigureAwait(false);

        foreach (var item in items)
        {
            await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);

            var newETag = Guid.NewGuid().ToString("N");
            var propertiesJson = StacJsonSerializer.SerializeNode(item.Properties);
            var assetsJson = StacJsonSerializer.SerializeAssets(item.Assets);
            var linksJson = StacJsonSerializer.SerializeLinks(item.Links);
            var extensionsJson = StacJsonSerializer.SerializeExtensions(item.Extensions);
            var bboxJson = item.Bbox is null ? null : StacJsonSerializer.SerializeBbox(item.Bbox);

            await writer.WriteAsync(item.CollectionId, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.Id, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.Title, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.Description, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(propertiesJson, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(assetsJson, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(linksJson, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(extensionsJson, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(bboxJson, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.Geometry, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.Datetime?.UtcDateTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.StartDatetime?.UtcDateTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.EndDatetime?.UtcDateTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.RasterDatasetId, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(newETag, NpgsqlTypes.NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.CreatedAtUtc.UtcDateTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(item.UpdatedAtUtc.UtcDateTime, NpgsqlTypes.NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
        }

        await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
