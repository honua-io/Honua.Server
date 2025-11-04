-- Migration: 001_create_build_queue_table.sql
-- Description: Creates the build_queue table for managing asynchronous build jobs
-- Author: Build Queue System
-- Date: 2025-10-29

-- Create build_queue table
CREATE TABLE IF NOT EXISTS build_queue (
    -- Primary identification
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Customer information
    customer_id VARCHAR(255) NOT NULL,
    customer_name VARCHAR(255) NOT NULL,
    customer_email VARCHAR(255) NOT NULL,

    -- Build configuration
    manifest_path TEXT NOT NULL,
    configuration_name VARCHAR(255) NOT NULL,
    tier VARCHAR(50) NOT NULL, -- starter, professional, enterprise
    architecture VARCHAR(50) NOT NULL, -- linux-x64, linux-arm64, etc.
    cloud_provider VARCHAR(50) NOT NULL, -- aws, azure, gcp, on-premises

    -- Build status
    status VARCHAR(50) NOT NULL DEFAULT 'pending', -- pending, building, success, failed, cancelled, timedout
    priority INTEGER NOT NULL DEFAULT 1, -- 0=low, 1=normal, 2=high, 3=critical
    progress_percent INTEGER NOT NULL DEFAULT 0,
    current_step TEXT,

    -- Build results
    output_path TEXT,
    image_url TEXT,
    download_url TEXT,
    error_message TEXT,

    -- Retry tracking
    retry_count INTEGER NOT NULL DEFAULT 0,

    -- Timestamps
    enqueued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Metrics
    build_duration_seconds DOUBLE PRECISION,

    -- Constraints
    CONSTRAINT chk_status CHECK (status IN ('pending', 'building', 'success', 'failed', 'cancelled', 'timedout')),
    CONSTRAINT chk_priority CHECK (priority BETWEEN 0 AND 3),
    CONSTRAINT chk_progress CHECK (progress_percent BETWEEN 0 AND 100),
    CONSTRAINT chk_retry_count CHECK (retry_count >= 0)
);

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_build_queue_status ON build_queue(status);
CREATE INDEX IF NOT EXISTS idx_build_queue_customer ON build_queue(customer_id);
CREATE INDEX IF NOT EXISTS idx_build_queue_priority_enqueued ON build_queue(priority DESC, enqueued_at ASC) WHERE status = 'pending';
CREATE INDEX IF NOT EXISTS idx_build_queue_enqueued_at ON build_queue(enqueued_at);
CREATE INDEX IF NOT EXISTS idx_build_queue_completed_at ON build_queue(completed_at) WHERE completed_at IS NOT NULL;

-- Create a function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_build_queue_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to call the function
DROP TRIGGER IF EXISTS trigger_update_build_queue_updated_at ON build_queue;
CREATE TRIGGER trigger_update_build_queue_updated_at
    BEFORE UPDATE ON build_queue
    FOR EACH ROW
    EXECUTE FUNCTION update_build_queue_updated_at();

-- Add comments for documentation
COMMENT ON TABLE build_queue IS 'Queue for managing asynchronous build jobs for custom Honua Server builds';
COMMENT ON COLUMN build_queue.id IS 'Unique identifier for the build job';
COMMENT ON COLUMN build_queue.customer_id IS 'Customer identifier who requested the build';
COMMENT ON COLUMN build_queue.customer_name IS 'Customer name for notifications';
COMMENT ON COLUMN build_queue.customer_email IS 'Customer email for notifications';
COMMENT ON COLUMN build_queue.manifest_path IS 'Path to the build manifest JSON file';
COMMENT ON COLUMN build_queue.configuration_name IS 'Name of the build configuration';
COMMENT ON COLUMN build_queue.tier IS 'License tier: starter, professional, enterprise';
COMMENT ON COLUMN build_queue.architecture IS 'Target architecture: linux-x64, linux-arm64, etc.';
COMMENT ON COLUMN build_queue.cloud_provider IS 'Cloud provider: aws, azure, gcp, on-premises';
COMMENT ON COLUMN build_queue.status IS 'Current status: pending, building, success, failed, cancelled, timedout';
COMMENT ON COLUMN build_queue.priority IS 'Build priority: 0=low, 1=normal, 2=high, 3=critical';
COMMENT ON COLUMN build_queue.progress_percent IS 'Build progress percentage (0-100)';
COMMENT ON COLUMN build_queue.current_step IS 'Description of current build step';
COMMENT ON COLUMN build_queue.output_path IS 'Path where build artifacts are stored';
COMMENT ON COLUMN build_queue.image_url IS 'Container image URL (when complete)';
COMMENT ON COLUMN build_queue.download_url IS 'Download URL for standalone binary';
COMMENT ON COLUMN build_queue.error_message IS 'Error message if build failed';
COMMENT ON COLUMN build_queue.retry_count IS 'Number of retry attempts made';
COMMENT ON COLUMN build_queue.enqueued_at IS 'When the build was enqueued';
COMMENT ON COLUMN build_queue.started_at IS 'When the build started processing';
COMMENT ON COLUMN build_queue.completed_at IS 'When the build completed (success or failure)';
COMMENT ON COLUMN build_queue.updated_at IS 'Last update timestamp (auto-updated)';
COMMENT ON COLUMN build_queue.build_duration_seconds IS 'Build duration in seconds (when complete)';

-- Grant permissions (adjust as needed for your security model)
-- GRANT SELECT, INSERT, UPDATE ON build_queue TO honua_app;
-- GRANT USAGE ON SEQUENCE build_queue_id_seq TO honua_app;
