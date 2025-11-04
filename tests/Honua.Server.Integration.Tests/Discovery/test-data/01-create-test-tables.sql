-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

-- Enable PostGIS extension
CREATE EXTENSION IF NOT EXISTS postgis;

-- Create test schema
CREATE SCHEMA IF NOT EXISTS geo;

-- Public schema tables
CREATE TABLE IF NOT EXISTS public.cities (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    population INTEGER,
    country TEXT,
    geom geometry(Point, 4326)
);

COMMENT ON TABLE public.cities IS 'Major cities of the world';

CREATE INDEX idx_cities_geom ON public.cities USING GIST (geom);

INSERT INTO public.cities (name, population, country, geom) VALUES
    ('San Francisco', 873965, 'USA', ST_SetSRID(ST_MakePoint(-122.4194, 37.7749), 4326)),
    ('New York', 8336817, 'USA', ST_SetSRID(ST_MakePoint(-74.0060, 40.7128), 4326)),
    ('Chicago', 2693976, 'USA', ST_SetSRID(ST_MakePoint(-87.6298, 41.8781), 4326)),
    ('London', 8982000, 'UK', ST_SetSRID(ST_MakePoint(-0.1278, 51.5074), 4326)),
    ('Paris', 2161000, 'France', ST_SetSRID(ST_MakePoint(2.3522, 48.8566), 4326)),
    ('Tokyo', 13960000, 'Japan', ST_SetSRID(ST_MakePoint(139.6917, 35.6895), 4326));

CREATE TABLE IF NOT EXISTS public.roads (
    id SERIAL PRIMARY KEY,
    name TEXT NOT NULL,
    road_type TEXT,
    length_km DECIMAL(10, 2),
    geom geometry(LineString, 4326)
);

CREATE INDEX idx_roads_geom ON public.roads USING GIST (geom);

INSERT INTO public.roads (name, road_type, length_km, geom) VALUES
    ('Highway 1', 'highway', 123.45, ST_SetSRID(ST_GeomFromText('LINESTRING(-122.4 37.7, -122.3 37.8)'), 4326)),
    ('Main Street', 'street', 5.2, ST_SetSRID(ST_GeomFromText('LINESTRING(-122.41 37.77, -122.42 37.78)'), 4326));

CREATE TABLE IF NOT EXISTS public.parcels (
    id SERIAL PRIMARY KEY,
    parcel_id TEXT UNIQUE NOT NULL,
    owner TEXT,
    area_sqm DECIMAL(12, 2),
    zoning TEXT,
    geom geometry(Polygon, 4326)
);

CREATE INDEX idx_parcels_geom ON public.parcels USING GIST (geom);

INSERT INTO public.parcels (parcel_id, owner, area_sqm, zoning, geom) VALUES
    ('PARCEL-001', 'John Doe', 1000.5, 'residential',
     ST_SetSRID(ST_GeomFromText('POLYGON((-122.4 37.7, -122.4 37.71, -122.39 37.71, -122.39 37.7, -122.4 37.7))'), 4326)),
    ('PARCEL-002', 'Jane Smith', 1500.0, 'commercial',
     ST_SetSRID(ST_GeomFromText('POLYGON((-122.41 37.72, -122.41 37.73, -122.40 37.73, -122.40 37.72, -122.41 37.72))'), 4326));

-- Geo schema tables
CREATE TABLE IF NOT EXISTS geo.buildings (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    building_type TEXT,
    floors INTEGER,
    height_m DECIMAL(6, 2),
    year_built INTEGER,
    geom geometry(Polygon, 4326)
);

CREATE INDEX idx_buildings_geom ON geo.buildings USING GIST (geom);

INSERT INTO geo.buildings (name, building_type, floors, height_m, year_built, geom) VALUES
    ('Empire State Building', 'office', 102, 443.2, 1931,
     ST_SetSRID(ST_GeomFromText('POLYGON((-73.9857 40.7484, -73.9857 40.7486, -73.9855 40.7486, -73.9855 40.7484, -73.9857 40.7484))'), 4326)),
    ('Golden Gate Bridge', 'bridge', 1, 227.4, 1937,
     ST_SetSRID(ST_GeomFromText('POLYGON((-122.4784 37.8199, -122.4784 37.8201, -122.4782 37.8201, -122.4782 37.8199, -122.4784 37.8199))'), 4326));

-- Table without spatial index (for testing RequireSpatialIndex option)
CREATE TABLE IF NOT EXISTS public.unindexed_points (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom geometry(Point, 4326)
);

INSERT INTO public.unindexed_points (name, geom) VALUES
    ('Point 1', ST_SetSRID(ST_MakePoint(-122.0, 37.0), 4326)),
    ('Point 2', ST_SetSRID(ST_MakePoint(-122.1, 37.1), 4326));

-- Table matching exclusion pattern (for testing ExcludeTablePatterns)
CREATE TABLE IF NOT EXISTS public.temp_data (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom geometry(Point, 4326)
);

INSERT INTO public.temp_data (name, geom) VALUES
    ('Temp Point', ST_SetSRID(ST_MakePoint(-122.0, 37.0), 4326));

-- Table starting with underscore (should be excluded by default)
CREATE TABLE IF NOT EXISTS public._internal (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom geometry(Point, 4326)
);

-- Table without primary key (should be skipped)
CREATE TABLE IF NOT EXISTS public.no_pk_table (
    id INTEGER,
    name TEXT,
    geom geometry(Point, 4326)
);

-- Create topology schema for exclusion tests
CREATE SCHEMA IF NOT EXISTS topology;

CREATE TABLE IF NOT EXISTS topology.topo_test (
    id SERIAL PRIMARY KEY,
    name TEXT,
    geom geometry(Point, 4326)
);

-- Analyze tables for accurate statistics
ANALYZE;
