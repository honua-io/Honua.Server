// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Data.Common;
using Honua.Server.Core.Data.MySql;
using MySqlConnector;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Stac.Storage;

internal sealed class MySqlStacCatalogStore : RelationalStacCatalogStore
{
    private static readonly IReadOnlyList<string> SchemaStatements = new[]
    {
        @"CREATE TABLE IF NOT EXISTS stac_collections (
    id VARCHAR(256) PRIMARY KEY,
    title VARCHAR(512) NULL,
    description LONGTEXT NULL,
    license VARCHAR(256) NULL,
    version VARCHAR(128) NULL,
    keywords_json LONGTEXT NOT NULL,
    extent_json LONGTEXT NOT NULL,
    properties_json LONGTEXT NULL,
    links_json LONGTEXT NOT NULL,
    extensions_json LONGTEXT NOT NULL,
    conforms_to VARCHAR(512) NULL,
    data_source_id VARCHAR(256) NULL,
    service_id VARCHAR(256) NULL,
    layer_id VARCHAR(256) NULL,
    etag VARCHAR(256) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL
) ENGINE=InnoDB;",
        @"CREATE TABLE IF NOT EXISTS stac_items (
    collection_id VARCHAR(256) NOT NULL,
    id VARCHAR(256) NOT NULL,
    title VARCHAR(512) NULL,
    description LONGTEXT NULL,
    properties_json LONGTEXT NULL,
    assets_json LONGTEXT NOT NULL,
    links_json LONGTEXT NOT NULL,
    extensions_json LONGTEXT NOT NULL,
    bbox_json LONGTEXT NULL,
    geometry_json LONGTEXT NULL,
    datetime DATETIME(6) NULL,
    start_datetime DATETIME(6) NULL,
    end_datetime DATETIME(6) NULL,
    raster_dataset_id VARCHAR(256) NULL,
    etag VARCHAR(256) NULL,
    created_at DATETIME(6) NOT NULL,
    updated_at DATETIME(6) NOT NULL,
    computed_start_datetime DATETIME(6) GENERATED ALWAYS AS (COALESCE(start_datetime, datetime)) STORED,
    computed_end_datetime DATETIME(6) GENERATED ALWAYS AS (COALESCE(end_datetime, datetime)) STORED,
    PRIMARY KEY (collection_id, id),
    CONSTRAINT FK_stac_items_collections FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE
) ENGINE=InnoDB;",
        @"CREATE INDEX idx_stac_items_collection ON stac_items(collection_id);",
        @"CREATE INDEX idx_stac_items_temporal_start ON stac_items(collection_id, computed_start_datetime);",
        @"CREATE INDEX idx_stac_items_temporal_end ON stac_items(collection_id, computed_end_datetime);",
        @"CREATE INDEX idx_stac_items_temporal_range ON stac_items(collection_id, computed_start_datetime, computed_end_datetime, id);",
        @"CREATE INDEX idx_stac_items_datetime_point ON stac_items(collection_id, datetime);",
        @"CREATE INDEX idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime);"
    };

    private readonly string _connectionString;

    public MySqlStacCatalogStore(string connectionString)
    {
        Guard.NotNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    protected override string ProviderName => MySqlDataStoreProvider.ProviderKey;
    protected override IReadOnlyList<string> InitializationStatements => SchemaStatements;

    protected override DbConnection CreateConnection() => new MySqlConnection(_connectionString);

    protected override bool SupportsBboxFiltering => true;

    protected override string? GetBboxCoordinateExpression(int index)
    {
        return $"CAST(JSON_EXTRACT(bbox_json, '$[{index}]') AS double)";
    }

    protected override string? MapPropertyFieldToColumn(string propertyName)
    {
        // Remove "properties." prefix if present
        var prop = propertyName.StartsWith("properties.")
            ? propertyName.Substring("properties.".Length)
            : propertyName;

        // MySQL JSON path syntax: JSON_EXTRACT(properties_json, '$.field_name')
        // MySQL uses $ as the root reference and . for object properties
        return $"JSON_UNQUOTE(JSON_EXTRACT(properties_json, '$.{prop}'))";
    }

    protected override bool HandleInitializationException(Exception exception, string statement)
    {
        if (exception is MySqlException mysqlException)
        {
            // 1061: duplicate key name (index already exists)
            if (mysqlException.Number == 1061 && statement.Contains("CREATE INDEX", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return base.HandleInitializationException(exception, statement);
    }
}
