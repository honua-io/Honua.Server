CREATE TABLE IF NOT EXISTS stac_collections (
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
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS stac_items (
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
) ENGINE=InnoDB;

CREATE INDEX IF NOT EXISTS idx_stac_items_collection ON stac_items(collection_id);

-- Optimized temporal indexes for range queries (see migration 002_temporal_indexes.sql)
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start ON stac_items(collection_id, computed_start_datetime);
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end ON stac_items(collection_id, computed_end_datetime);
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range ON stac_items(collection_id, computed_start_datetime, computed_end_datetime, id);
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point ON stac_items(collection_id, datetime);
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime);
