-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

-- ============================================================================
-- Migration 033: Performance Indexes
-- ============================================================================
-- Purpose: Add missing indexes for time-series queries, alert history,
--          geofences, and entity state lookups
-- Created: 2025-11-14
-- Dependencies: Requires SensorThings API schema (001_InitialSchema.sql from Enterprise/Sensors),
--               Alert system tables, Geofence tables
-- Performance Impact: Moderate - index creation may take time on large tables
-- ============================================================================

-- SensorThings API Observations: Time-series query optimization
-- This composite index optimizes the most common query pattern:
-- "Get observations for a specific datastream, ordered by time"
CREATE INDEX IF NOT EXISTS idx_observations_datastream_phenomenon
ON sta_observations (datastream_id, phenomenon_time DESC)
WHERE datastream_id IS NOT NULL;

-- SensorThings API Observations: Time-based range queries
-- Supports queries filtering by time range across all datastreams
CREATE INDEX IF NOT EXISTS idx_observations_phenomenon_time
ON sta_observations (phenomenon_time DESC);

-- Alert History: Deduplication lookups
-- Fingerprint is used to detect duplicate alerts
CREATE INDEX IF NOT EXISTS idx_alert_history_fingerprint
ON alert_history (fingerprint)
WHERE fingerprint IS NOT NULL;

-- Alert History: Time-based queries and cleanup
-- Supports queries for recent alerts and cleanup of old records
CREATE INDEX IF NOT EXISTS idx_alert_history_received_at
ON alert_history (received_at DESC);

-- Geofences: Spatial queries
-- GIST index for efficient spatial intersection queries
CREATE INDEX IF NOT EXISTS idx_geofences_geometry_gist
ON geofences USING GIST (geometry)
WHERE geometry IS NOT NULL;

-- Entity State: Entity lookups
-- Supports fast lookups of entity state by entity ID
CREATE INDEX IF NOT EXISTS idx_entity_state_entity_id
ON entity_state (entity_id)
WHERE entity_id IS NOT NULL;

-- Add ANALYZE to update query planner statistics after index creation
ANALYZE sta_observations;
ANALYZE alert_history;
ANALYZE geofences;
ANALYZE entity_state;

-- ============================================================================
-- Performance Notes
-- ============================================================================
-- 1. Observations indexes use DESC ordering to optimize recent-data queries
-- 2. Partial indexes (WHERE ... IS NOT NULL) are smaller and faster
-- 3. GIST index on geofences is critical for ST_Intersects performance
-- 4. Entity state index supports the common pattern: lookup by entity_id
-- 5. Alert fingerprint index enables O(1) deduplication lookups
-- ============================================================================
