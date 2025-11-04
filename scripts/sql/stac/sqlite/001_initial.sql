CREATE TABLE IF NOT EXISTS stac_collections (
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
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS stac_items (
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
    datetime TEXT,
    start_datetime TEXT,
    end_datetime TEXT,
    raster_dataset_id TEXT,
    etag TEXT,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    PRIMARY KEY (collection_id, id),
    FOREIGN KEY (collection_id) REFERENCES stac_collections(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_stac_items_collection ON stac_items(collection_id);

-- Optimized temporal indexes for range queries (see migration 002_temporal_indexes.sql)
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_start ON stac_items(collection_id, COALESCE(start_datetime, datetime));
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_end ON stac_items(collection_id, COALESCE(end_datetime, datetime));
CREATE INDEX IF NOT EXISTS idx_stac_items_temporal_range ON stac_items(collection_id, COALESCE(start_datetime, datetime), COALESCE(end_datetime, datetime), id);
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_point ON stac_items(collection_id, datetime) WHERE datetime IS NOT NULL AND start_datetime IS NULL AND end_datetime IS NULL;
CREATE INDEX IF NOT EXISTS idx_stac_items_datetime_range ON stac_items(collection_id, start_datetime, end_datetime) WHERE start_datetime IS NOT NULL AND end_datetime IS NOT NULL;
