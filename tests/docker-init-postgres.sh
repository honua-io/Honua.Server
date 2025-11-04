#!/bin/bash
# PostgreSQL initialization script for integration tests
# This script sets up PostGIS and runs the optimization migration

set -e

echo "Initializing PostGIS..."
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    -- Enable PostGIS extension
    CREATE EXTENSION IF NOT EXISTS postgis;
    CREATE EXTENSION IF NOT EXISTS postgis_topology;

    -- Create test schema
    CREATE SCHEMA IF NOT EXISTS test;

    -- Create a simple test table with geometry
    CREATE TABLE IF NOT EXISTS test.spatial_features (
        id SERIAL PRIMARY KEY,
        name TEXT,
        collection_name TEXT,
        geom GEOMETRY(GEOMETRY, 4326),
        properties JSONB DEFAULT '{}'::jsonb,
        created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    );

    -- Create spatial index
    CREATE INDEX IF NOT EXISTS idx_spatial_features_geom
        ON test.spatial_features USING GIST(geom);

    -- Create index on collection name for faster queries
    CREATE INDEX IF NOT EXISTS idx_spatial_features_collection
        ON test.spatial_features(collection_name);

    -- Insert some test data
    INSERT INTO test.spatial_features (name, collection_name, geom, properties)
    VALUES
        ('Test Point 1', 'test_collection', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326), '{"type": "poi", "category": "landmark"}'::jsonb),
        ('Test Point 2', 'test_collection', ST_SetSRID(ST_MakePoint(-118.2437, 34.0522), 4326), '{"type": "poi", "category": "park"}'::jsonb),
        ('Test LineString', 'test_collection', ST_SetSRID(ST_MakeLine(ST_MakePoint(-122.4194, 37.7749), ST_MakePoint(-118.2437, 34.0522)), 4326), '{"type": "road", "name": "Test Road"}'::jsonb),
        ('Test Polygon', 'test_collection', ST_SetSRID(ST_MakePolygon(ST_MakeLine(ARRAY[
            ST_MakePoint(-122.5, 37.7),
            ST_MakePoint(-122.3, 37.7),
            ST_MakePoint(-122.3, 37.9),
            ST_MakePoint(-122.5, 37.9),
            ST_MakePoint(-122.5, 37.7)
        ])), 4326), '{"type": "area", "name": "Test Area"}'::jsonb)
    ON CONFLICT DO NOTHING;

    -- Grant permissions
    GRANT ALL PRIVILEGES ON SCHEMA test TO $POSTGRES_USER;
    GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA test TO $POSTGRES_USER;
    GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA test TO $POSTGRES_USER;
EOSQL

echo "Running PostgreSQL optimization migrations..."
# Run the optimization migration if it exists
if [ -f "/docker-entrypoint-initdb.d/migrations/014_PostgresOptimizations.sql" ]; then
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -f /docker-entrypoint-initdb.d/migrations/014_PostgresOptimizations.sql
    echo "Optimization functions installed successfully"
else
    echo "Warning: 014_PostgresOptimizations.sql not found"
fi

echo "PostgreSQL initialization complete!"
