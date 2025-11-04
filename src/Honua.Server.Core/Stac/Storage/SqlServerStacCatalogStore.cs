// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.SqlServer;
using Microsoft.Data.SqlClient;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

internal sealed class SqlServerStacCatalogStore : RelationalStacCatalogStore
{
    private static readonly IReadOnlyList<string> SchemaStatements = new[]
    {
        @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'stac_collections')
BEGIN
    CREATE TABLE stac_collections (
        id NVARCHAR(256) NOT NULL PRIMARY KEY,
        title NVARCHAR(512) NULL,
        description NVARCHAR(MAX) NULL,
        license NVARCHAR(256) NULL,
        version NVARCHAR(128) NULL,
        keywords_json NVARCHAR(MAX) NOT NULL,
        extent_json NVARCHAR(MAX) NOT NULL,
        properties_json NVARCHAR(MAX) NULL,
        links_json NVARCHAR(MAX) NOT NULL,
        extensions_json NVARCHAR(MAX) NOT NULL,
        conforms_to NVARCHAR(512) NULL,
        data_source_id NVARCHAR(256) NULL,
        service_id NVARCHAR(256) NULL,
        layer_id NVARCHAR(256) NULL,
        etag NVARCHAR(256) NULL,
        created_at DATETIMEOFFSET NOT NULL,
        updated_at DATETIMEOFFSET NOT NULL
    );
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'stac_items')
BEGIN
    CREATE TABLE stac_items (
        collection_id NVARCHAR(256) NOT NULL,
        id NVARCHAR(256) NOT NULL,
        title NVARCHAR(512) NULL,
        description NVARCHAR(MAX) NULL,
        properties_json NVARCHAR(MAX) NULL,
        assets_json NVARCHAR(MAX) NOT NULL,
        links_json NVARCHAR(MAX) NOT NULL,
        extensions_json NVARCHAR(MAX) NOT NULL,
        bbox_json NVARCHAR(MAX) NULL,
        geometry_json NVARCHAR(MAX) NULL,
        datetime DATETIMEOFFSET NULL,
        start_datetime DATETIMEOFFSET NULL,
        end_datetime DATETIMEOFFSET NULL,
        raster_dataset_id NVARCHAR(256) NULL,
        etag NVARCHAR(256) NULL,
        created_at DATETIMEOFFSET NOT NULL,
        updated_at DATETIMEOFFSET NOT NULL,
        computed_start_datetime AS COALESCE(start_datetime, datetime) PERSISTED,
        computed_end_datetime AS COALESCE(end_datetime, datetime) PERSISTED,
        CONSTRAINT PK_stac_items PRIMARY KEY (collection_id, id),
        CONSTRAINT FK_stac_items_collections FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE
    );
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_collection' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_collection ON stac_items(collection_id);
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_start' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_start ON stac_items(collection_id, computed_start_datetime);
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_end' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_end ON stac_items(collection_id, computed_end_datetime);
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_range ON stac_items(collection_id, computed_start_datetime, computed_end_datetime) INCLUDE (id);
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_point' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;
END;",
        @"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;
END;"
    };

    private readonly string _connectionString;

    public SqlServerStacCatalogStore(string connectionString)
    {
        Guard.NotNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    protected override string ProviderName => SqlServerDataStoreProvider.ProviderKey;
    protected override IReadOnlyList<string> InitializationStatements => SchemaStatements;

    protected override DbConnection CreateConnection() => new SqlConnection(_connectionString);

    protected override bool SupportsBboxFiltering => true;

    protected override string? GetBboxCoordinateExpression(int index)
    {
        return $"CAST(JSON_VALUE(bbox_json, '$[{index}]') AS float)";
    }

    protected override string? MapPropertyFieldToColumn(string propertyName)
    {
        // Remove "properties." prefix if present
        var prop = propertyName.StartsWith("properties.")
            ? propertyName.Substring("properties.".Length)
            : propertyName;

        // SQL Server JSON path syntax: JSON_VALUE(properties_json, '$.field_name')
        // Returns string value from JSON property
        return $"JSON_VALUE(properties_json, '$.{prop}')";
    }

    protected override string BuildLimitClause(int limit)
    {
        return $"OFFSET 0 ROWS FETCH NEXT {limit} ROWS ONLY";
    }

    protected override string BuildPaginationClause(int offset, int limit)
    {
        return $"OFFSET {offset} ROWS FETCH NEXT {limit} ROWS ONLY";
    }

    protected override bool SupportsBulkInsert => true;

    protected override async Task BulkInsertItemsAsync(DbConnection connection, DbTransaction transaction, IReadOnlyList<StacItemRecord> items, CancellationToken cancellationToken)
    {
        if (connection is not SqlConnection sqlConnection)
        {
            throw new InvalidOperationException("Connection must be SqlConnection for SQL Server bulk insert");
        }

        // First, delete items that will be updated (upsert behavior)
        const int deleteBatchSize = 500;
        for (var i = 0; i < items.Count; i += deleteBatchSize)
        {
            var batch = items.Skip(i).Take(deleteBatchSize).ToList();
            await using var deleteCmd = sqlConnection.CreateCommand();
            deleteCmd.Transaction = (SqlTransaction)transaction;

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

        // Create DataTable for bulk copy
        var dataTable = new DataTable();
        dataTable.Columns.Add("collection_id", typeof(string));
        dataTable.Columns.Add("id", typeof(string));
        dataTable.Columns.Add("title", typeof(string));
        dataTable.Columns.Add("description", typeof(string));
        dataTable.Columns.Add("properties_json", typeof(string));
        dataTable.Columns.Add("assets_json", typeof(string));
        dataTable.Columns.Add("links_json", typeof(string));
        dataTable.Columns.Add("extensions_json", typeof(string));
        dataTable.Columns.Add("bbox_json", typeof(string));
        dataTable.Columns.Add("geometry_json", typeof(string));
        dataTable.Columns.Add("datetime", typeof(DateTimeOffset));
        dataTable.Columns.Add("start_datetime", typeof(DateTimeOffset));
        dataTable.Columns.Add("end_datetime", typeof(DateTimeOffset));
        dataTable.Columns.Add("raster_dataset_id", typeof(string));
        dataTable.Columns.Add("etag", typeof(string));
        dataTable.Columns.Add("created_at", typeof(DateTimeOffset));
        dataTable.Columns.Add("updated_at", typeof(DateTimeOffset));

        foreach (var item in items)
        {
            var newETag = Guid.NewGuid().ToString("N");
            var propertiesJson = StacJsonSerializer.SerializeNode(item.Properties);
            var assetsJson = StacJsonSerializer.SerializeAssets(item.Assets);
            var linksJson = StacJsonSerializer.SerializeLinks(item.Links);
            var extensionsJson = StacJsonSerializer.SerializeExtensions(item.Extensions);
            var bboxJson = item.Bbox is null ? null : StacJsonSerializer.SerializeBbox(item.Bbox);

            var row = dataTable.NewRow();
            row["collection_id"] = item.CollectionId;
            row["id"] = item.Id;
            row["title"] = (object?)item.Title ?? DBNull.Value;
            row["description"] = (object?)item.Description ?? DBNull.Value;
            row["properties_json"] = (object?)propertiesJson ?? DBNull.Value;
            row["assets_json"] = assetsJson;
            row["links_json"] = linksJson;
            row["extensions_json"] = extensionsJson;
            row["bbox_json"] = (object?)bboxJson ?? DBNull.Value;
            row["geometry_json"] = (object?)item.Geometry ?? DBNull.Value;
            row["datetime"] = item.Datetime.HasValue ? item.Datetime.Value : DBNull.Value;
            row["start_datetime"] = item.StartDatetime.HasValue ? item.StartDatetime.Value : DBNull.Value;
            row["end_datetime"] = item.EndDatetime.HasValue ? item.EndDatetime.Value : DBNull.Value;
            row["raster_dataset_id"] = (object?)item.RasterDatasetId ?? DBNull.Value;
            row["etag"] = newETag;
            row["created_at"] = item.CreatedAtUtc;
            row["updated_at"] = item.UpdatedAtUtc;

            dataTable.Rows.Add(row);
        }

        // Perform bulk copy
        using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, (SqlTransaction)transaction)
        {
            DestinationTableName = "stac_items",
            BulkCopyTimeout = 300 // 5 minutes timeout
        };

        // Map columns
        bulkCopy.ColumnMappings.Add("collection_id", "collection_id");
        bulkCopy.ColumnMappings.Add("id", "id");
        bulkCopy.ColumnMappings.Add("title", "title");
        bulkCopy.ColumnMappings.Add("description", "description");
        bulkCopy.ColumnMappings.Add("properties_json", "properties_json");
        bulkCopy.ColumnMappings.Add("assets_json", "assets_json");
        bulkCopy.ColumnMappings.Add("links_json", "links_json");
        bulkCopy.ColumnMappings.Add("extensions_json", "extensions_json");
        bulkCopy.ColumnMappings.Add("bbox_json", "bbox_json");
        bulkCopy.ColumnMappings.Add("geometry_json", "geometry_json");
        bulkCopy.ColumnMappings.Add("datetime", "datetime");
        bulkCopy.ColumnMappings.Add("start_datetime", "start_datetime");
        bulkCopy.ColumnMappings.Add("end_datetime", "end_datetime");
        bulkCopy.ColumnMappings.Add("raster_dataset_id", "raster_dataset_id");
        bulkCopy.ColumnMappings.Add("etag", "etag");
        bulkCopy.ColumnMappings.Add("created_at", "created_at");
        bulkCopy.ColumnMappings.Add("updated_at", "updated_at");

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken).ConfigureAwait(false);
    }
}
