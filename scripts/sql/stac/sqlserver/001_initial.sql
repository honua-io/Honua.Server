IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'stac_collections')
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
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'stac_items')
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
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_collection' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_collection ON stac_items(collection_id);
END;

-- Optimized temporal indexes for range queries (see migration 002_temporal_indexes.sql)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_start' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_start ON stac_items(collection_id, computed_start_datetime);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_end' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_end ON stac_items(collection_id, computed_end_datetime);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_temporal_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_temporal_range ON stac_items(collection_id, computed_start_datetime, computed_end_datetime) INCLUDE (id);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_point' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_stac_items_datetime_range' AND object_id = OBJECT_ID('stac_items'))
BEGIN
    CREATE INDEX idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;
END;
