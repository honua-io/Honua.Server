-- Copyright (c) 2025 HonuaIO
-- Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

-- Migration: 032_GeoprocessingIdempotency
-- Purpose: Add idempotency tracking for geoprocessing jobs to prevent duplicate execution
-- Author: Claude Code
-- Date: 2025-11-14

-- Create geoprocessing_idempotency table for tracking completed jobs
CREATE TABLE IF NOT EXISTS geoprocessing_idempotency (
    -- Primary idempotency key computed from job.Id + job.Inputs + job.ProcessId
    idempotency_key TEXT PRIMARY KEY,

    -- Reference to the original job ID
    job_id TEXT NOT NULL,

    -- SHA256 hash of the result payload for integrity verification
    result_hash TEXT NOT NULL,

    -- Cached result payload (JSONB for queryability)
    result_payload JSONB NOT NULL,

    -- When the job completed
    completed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- When this entry should expire (7-day TTL)
    expires_at TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '7 days'),

    -- Additional metadata
    tenant_id UUID,
    process_id TEXT NOT NULL,

    -- Resource usage for auditing
    duration_ms BIGINT,
    features_processed BIGINT
);

-- Index for cleanup queries (finding expired entries)
CREATE INDEX IF NOT EXISTS idx_geoprocessing_idempotency_expires_at
    ON geoprocessing_idempotency(expires_at);

-- Index for tenant-based queries
CREATE INDEX IF NOT EXISTS idx_geoprocessing_idempotency_tenant_id
    ON geoprocessing_idempotency(tenant_id);

-- Index for job_id lookups
CREATE INDEX IF NOT EXISTS idx_geoprocessing_idempotency_job_id
    ON geoprocessing_idempotency(job_id);

-- Composite index for tenant + process queries
CREATE INDEX IF NOT EXISTS idx_geoprocessing_idempotency_tenant_process
    ON geoprocessing_idempotency(tenant_id, process_id);

-- Add comment for documentation
COMMENT ON TABLE geoprocessing_idempotency IS
    'Idempotency cache for geoprocessing jobs. Prevents duplicate execution when workers crash and restart. TTL: 7 days.';

COMMENT ON COLUMN geoprocessing_idempotency.idempotency_key IS
    'SHA256 hash of: job.Id + job.Inputs (serialized) + job.ProcessId';

COMMENT ON COLUMN geoprocessing_idempotency.result_payload IS
    'Cached ProcessResult serialized as JSONB. Returned if job is re-submitted within TTL window.';

COMMENT ON COLUMN geoprocessing_idempotency.expires_at IS
    'Expiration timestamp (7 days from completion). Expired entries removed by cleanup service.';
