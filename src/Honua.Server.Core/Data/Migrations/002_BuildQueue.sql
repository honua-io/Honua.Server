-- ============================================================================
-- Honua Build Orchestrator - Build Queue Schema
-- ============================================================================
-- Version: 002
-- Description: Build queue, results, and artifact tracking tables
-- Dependencies: 001_InitialSchema.sql
-- ============================================================================

-- ============================================================================
-- Build Queue Table
-- ============================================================================
-- Manages pending and in-progress builds with priority ordering

CREATE TABLE build_queue (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Customer context
    customer_id VARCHAR(100) NOT NULL,

    -- Build specification
    manifest_path TEXT NOT NULL,
    manifest_hash VARCHAR(16) NOT NULL,
    manifest_json JSONB NOT NULL,

    -- Queue management
    status VARCHAR(20) NOT NULL DEFAULT 'queued'
        CHECK (status IN ('queued', 'building', 'success', 'failed', 'cancelled', 'timeout')),
    priority INTEGER NOT NULL DEFAULT 5
        CHECK (priority BETWEEN 1 AND 10),

    -- Timing
    queued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ,

    -- Progress tracking
    progress_percent INTEGER DEFAULT 0 CHECK (progress_percent BETWEEN 0 AND 100),
    current_step VARCHAR(255),
    total_steps INTEGER,
    steps_completed INTEGER DEFAULT 0,

    -- Worker assignment
    worker_id VARCHAR(100),
    worker_hostname VARCHAR(255),
    worker_assigned_at TIMESTAMPTZ,

    -- Build configuration
    target_registry_id UUID,
    architecture VARCHAR(20) DEFAULT 'amd64'
        CHECK (architecture IN ('amd64', 'arm64')),
    build_args JSONB DEFAULT '{}',

    -- Resource requirements
    estimated_duration_seconds INTEGER,
    max_duration_seconds INTEGER DEFAULT 3600,
    estimated_memory_mb INTEGER,
    estimated_cpu_cores NUMERIC(3,1),

    -- Retry logic
    retry_count INTEGER DEFAULT 0,
    max_retries INTEGER DEFAULT 3,
    last_retry_at TIMESTAMPTZ,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_build_queue_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE CASCADE,
    CONSTRAINT fk_build_queue_registry FOREIGN KEY (target_registry_id)
        REFERENCES registry_credentials(id) ON DELETE SET NULL
);

CREATE INDEX idx_build_queue_status ON build_queue(status);
CREATE INDEX idx_build_queue_customer_id ON build_queue(customer_id);
CREATE INDEX idx_build_queue_queued_at ON build_queue(queued_at DESC);
CREATE INDEX idx_build_queue_manifest_hash ON build_queue(manifest_hash);
CREATE INDEX idx_build_queue_worker_id ON build_queue(worker_id) WHERE status = 'building';

-- Composite index for queue ordering: process highest priority queued items first
CREATE INDEX idx_build_queue_processing ON build_queue(priority DESC, queued_at ASC)
    WHERE status = 'queued';

-- Index for timeout detection
CREATE INDEX idx_build_queue_timeout ON build_queue(timeout_at)
    WHERE status = 'building' AND timeout_at IS NOT NULL;

COMMENT ON TABLE build_queue IS 'Build queue with priority ordering and progress tracking';
COMMENT ON COLUMN build_queue.manifest_hash IS 'MD5 hash - used to check build_cache_registry for existing builds';
COMMENT ON COLUMN build_queue.priority IS '1-10 scale: 1=lowest, 10=highest; enterprise customers get higher priority';
COMMENT ON COLUMN build_queue.current_step IS 'Human-readable current step (e.g., "Installing dependencies")';
COMMENT ON COLUMN build_queue.worker_id IS 'Unique ID of worker processing this build';
COMMENT ON COLUMN build_queue.timeout_at IS 'Absolute timeout - build will be failed after this time';
COMMENT ON COLUMN build_queue.retry_count IS 'Number of times this build has been retried after failure';

-- ============================================================================
-- Build Results Table
-- ============================================================================
-- Completed build results with output logs and metrics

CREATE TABLE build_results (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Reference to queue item
    build_queue_id UUID NOT NULL UNIQUE,

    -- Build outcome
    success BOOLEAN NOT NULL,
    exit_code INTEGER,

    -- Output
    stdout_log TEXT,
    stderr_log TEXT,
    error_message TEXT,
    error_code VARCHAR(50),

    -- Performance metrics
    actual_duration_seconds NUMERIC(10,2) NOT NULL,
    peak_memory_mb INTEGER,
    peak_cpu_percent NUMERIC(5,2),
    disk_usage_mb INTEGER,

    -- Build statistics
    total_layers INTEGER,
    cache_layers_used INTEGER,
    new_layers_built INTEGER,
    final_image_size_bytes BIGINT,

    -- Container image details (on success)
    image_digest VARCHAR(71), -- sha256:64-chars
    image_tags TEXT[],
    pushed_to_registry BOOLEAN DEFAULT FALSE,
    push_duration_seconds NUMERIC(10,2),

    -- Build cache reference (if this became a cached build)
    build_cache_id UUID,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_build_results_queue FOREIGN KEY (build_queue_id)
        REFERENCES build_queue(id) ON DELETE CASCADE,
    CONSTRAINT fk_build_results_cache FOREIGN KEY (build_cache_id)
        REFERENCES build_cache_registry(id) ON DELETE SET NULL
);

CREATE INDEX idx_build_results_build_queue_id ON build_results(build_queue_id);
CREATE INDEX idx_build_results_success ON build_results(success);
CREATE INDEX idx_build_results_created_at ON build_results(created_at DESC);
CREATE INDEX idx_build_results_cache_id ON build_results(build_cache_id);
CREATE INDEX idx_build_results_image_digest ON build_results(image_digest);

COMMENT ON TABLE build_results IS 'Completed build results with logs, metrics, and container image details';
COMMENT ON COLUMN build_results.stdout_log IS 'Standard output from build process - consider external storage for large logs';
COMMENT ON COLUMN build_results.stderr_log IS 'Standard error from build process';
COMMENT ON COLUMN build_results.error_code IS 'Structured error code for programmatic error handling';
COMMENT ON COLUMN build_results.cache_layers_used IS 'Number of Docker layers pulled from cache';
COMMENT ON COLUMN build_results.image_digest IS 'SHA-256 digest of final image (sha256:...)';
COMMENT ON COLUMN build_results.build_cache_id IS 'Reference to build_cache_registry if this build was added to cache';

-- ============================================================================
-- Build Artifacts Table
-- ============================================================================
-- Links to container images and other build artifacts

CREATE TABLE build_artifacts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Reference
    build_result_id UUID NOT NULL,

    -- Artifact details
    artifact_type VARCHAR(50) NOT NULL
        CHECK (artifact_type IN ('container_image', 'sbom', 'scan_report', 'build_log', 'test_report')),
    artifact_name VARCHAR(255) NOT NULL,

    -- Storage location
    storage_type VARCHAR(50) NOT NULL
        CHECK (storage_type IN ('registry', 's3', 'azure_blob', 'gcs', 'local')),
    storage_url TEXT NOT NULL,
    storage_path TEXT,

    -- Metadata
    size_bytes BIGINT,
    content_type VARCHAR(100),
    checksum_sha256 VARCHAR(64),

    -- Artifact-specific data (flexible JSON)
    metadata JSONB DEFAULT '{}',

    -- Lifecycle
    expires_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign key
    CONSTRAINT fk_build_artifacts_result FOREIGN KEY (build_result_id)
        REFERENCES build_results(id) ON DELETE CASCADE
);

CREATE INDEX idx_build_artifacts_build_result_id ON build_artifacts(build_result_id);
CREATE INDEX idx_build_artifacts_artifact_type ON build_artifacts(artifact_type);
CREATE INDEX idx_build_artifacts_created_at ON build_artifacts(created_at DESC);
CREATE INDEX idx_build_artifacts_expires_at ON build_artifacts(expires_at)
    WHERE expires_at IS NOT NULL AND deleted_at IS NULL;

COMMENT ON TABLE build_artifacts IS 'Build artifacts including container images, SBOMs, and scan reports';
COMMENT ON COLUMN build_artifacts.artifact_type IS 'Type of artifact: container_image, sbom, scan_report, etc.';
COMMENT ON COLUMN build_artifacts.storage_url IS 'Full URL to artifact (e.g., registry URL or S3 presigned URL)';
COMMENT ON COLUMN build_artifacts.metadata IS 'Artifact-specific metadata as JSON (e.g., image layers, vulnerabilities)';
COMMENT ON COLUMN build_artifacts.checksum_sha256 IS 'SHA-256 checksum for integrity verification';

-- ============================================================================
-- Build Events Table
-- ============================================================================
-- Detailed event log for build lifecycle (optional but recommended for debugging)

CREATE TABLE build_events (
    id BIGSERIAL PRIMARY KEY,

    -- Reference
    build_queue_id UUID NOT NULL,

    -- Event details
    event_type VARCHAR(50) NOT NULL,
    event_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    event_data JSONB,

    -- Event source
    source VARCHAR(100), -- e.g., 'builder', 'scheduler', 'api'
    severity VARCHAR(20) DEFAULT 'info'
        CHECK (severity IN ('debug', 'info', 'warning', 'error', 'critical')),

    -- Message
    message TEXT,

    -- Foreign key
    CONSTRAINT fk_build_events_queue FOREIGN KEY (build_queue_id)
        REFERENCES build_queue(id) ON DELETE CASCADE
);

CREATE INDEX idx_build_events_build_queue_id ON build_events(build_queue_id);
CREATE INDEX idx_build_events_event_timestamp ON build_events(event_timestamp DESC);
CREATE INDEX idx_build_events_event_type ON build_events(event_type);
CREATE INDEX idx_build_events_severity ON build_events(severity);

COMMENT ON TABLE build_events IS 'Detailed event log for build lifecycle - useful for debugging and auditing';
COMMENT ON COLUMN build_events.event_type IS 'Event type (e.g., queued, started, layer_cached, layer_built, completed)';
COMMENT ON COLUMN build_events.event_data IS 'Structured event data as JSON';

-- ============================================================================
-- Build Metrics Table
-- ============================================================================
-- Time-series metrics during build execution (optional for advanced monitoring)

CREATE TABLE build_metrics (
    id BIGSERIAL PRIMARY KEY,

    -- Reference
    build_queue_id UUID NOT NULL,

    -- Metric details
    metric_name VARCHAR(100) NOT NULL,
    metric_value NUMERIC(20,4) NOT NULL,
    metric_unit VARCHAR(50),
    metric_timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Metric metadata
    tags JSONB DEFAULT '{}',

    -- Foreign key
    CONSTRAINT fk_build_metrics_queue FOREIGN KEY (build_queue_id)
        REFERENCES build_queue(id) ON DELETE CASCADE
);

CREATE INDEX idx_build_metrics_build_queue_id ON build_metrics(build_queue_id);
CREATE INDEX idx_build_metrics_metric_name ON build_metrics(metric_name);
CREATE INDEX idx_build_metrics_timestamp ON build_metrics(metric_timestamp DESC);

-- Composite index for time-series queries
CREATE INDEX idx_build_metrics_timeseries ON build_metrics(build_queue_id, metric_name, metric_timestamp DESC);

COMMENT ON TABLE build_metrics IS 'Time-series metrics during build execution (CPU, memory, network, disk)';
COMMENT ON COLUMN build_metrics.metric_name IS 'Metric name (e.g., cpu_percent, memory_mb, network_rx_bytes)';
COMMENT ON COLUMN build_metrics.tags IS 'Additional tags for metric filtering (e.g., {"layer": "3", "step": "npm_install"})';

-- ============================================================================
-- Views for Monitoring and Analytics
-- ============================================================================

-- Active builds view
CREATE OR REPLACE VIEW active_builds AS
SELECT
    bq.id,
    bq.customer_id,
    c.organization_name,
    bq.status,
    bq.priority,
    bq.progress_percent,
    bq.current_step,
    bq.worker_id,
    bq.queued_at,
    bq.started_at,
    bq.timeout_at,
    EXTRACT(EPOCH FROM (NOW() - bq.started_at))::INTEGER AS elapsed_seconds,
    EXTRACT(EPOCH FROM (bq.timeout_at - NOW()))::INTEGER AS remaining_seconds,
    bq.estimated_duration_seconds,
    bq.retry_count
FROM build_queue bq
JOIN customers c ON bq.customer_id = c.customer_id
WHERE bq.status IN ('queued', 'building')
ORDER BY bq.priority DESC, bq.queued_at ASC;

COMMENT ON VIEW active_builds IS 'Current queued and building items with customer context';

-- Build performance summary
CREATE OR REPLACE VIEW build_performance_summary AS
SELECT
    DATE(bq.completed_at) AS build_date,
    COUNT(*) AS total_builds,
    COUNT(*) FILTER (WHERE br.success = TRUE) AS successful_builds,
    COUNT(*) FILTER (WHERE br.success = FALSE) AS failed_builds,
    ROUND(AVG(br.actual_duration_seconds)::NUMERIC, 2) AS avg_duration_seconds,
    ROUND(PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY br.actual_duration_seconds)::NUMERIC, 2) AS median_duration_seconds,
    ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY br.actual_duration_seconds)::NUMERIC, 2) AS p95_duration_seconds,
    ROUND(AVG(br.final_image_size_bytes / 1024.0 / 1024.0)::NUMERIC, 2) AS avg_image_size_mb,
    ROUND(AVG(br.cache_layers_used::NUMERIC / NULLIF(br.total_layers, 0) * 100)::NUMERIC, 2) AS avg_cache_hit_rate_percent
FROM build_queue bq
JOIN build_results br ON bq.id = br.build_queue_id
WHERE bq.completed_at >= NOW() - INTERVAL '30 days'
GROUP BY DATE(bq.completed_at)
ORDER BY build_date DESC;

COMMENT ON VIEW build_performance_summary IS 'Daily build performance statistics for monitoring';

-- Customer build usage
CREATE OR REPLACE VIEW customer_build_usage AS
SELECT
    c.customer_id,
    c.organization_name,
    c.tier,
    COUNT(*) AS total_builds,
    COUNT(*) FILTER (WHERE bq.completed_at >= DATE_TRUNC('month', NOW())) AS builds_this_month,
    COUNT(*) FILTER (WHERE br.success = TRUE) AS successful_builds,
    ROUND(AVG(br.actual_duration_seconds)::NUMERIC, 2) AS avg_build_duration_seconds,
    ROUND(SUM(br.actual_duration_seconds)::NUMERIC, 2) AS total_build_time_seconds,
    MAX(bq.completed_at) AS last_build_at
FROM customers c
LEFT JOIN build_queue bq ON c.customer_id = bq.customer_id
LEFT JOIN build_results br ON bq.id = br.build_queue_id
WHERE c.deleted_at IS NULL
GROUP BY c.customer_id, c.organization_name, c.tier
ORDER BY total_builds DESC;

COMMENT ON VIEW customer_build_usage IS 'Per-customer build usage statistics';
