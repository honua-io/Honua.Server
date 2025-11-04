-- ============================================================================
-- Honua Build Orchestrator - Performance Indexes
-- ============================================================================
-- Version: 004
-- Description: Additional composite indexes for common query patterns
-- Dependencies: 001_InitialSchema.sql, 002_BuildQueue.sql, 003_IntakeSystem.sql
-- ============================================================================

-- ============================================================================
-- Build Cache Registry - Advanced Indexes
-- ============================================================================

-- Index for cache lookup with tier filtering
CREATE INDEX IF NOT EXISTS idx_build_cache_lookup_with_tier
ON build_cache_registry(manifest_hash, tier)
WHERE deleted_at IS NULL;

-- Index for popular cache entries (frequently accessed)
CREATE INDEX IF NOT EXISTS idx_build_cache_popular
ON build_cache_registry(cache_hit_count DESC, last_accessed_at DESC)
WHERE deleted_at IS NULL AND cache_hit_count > 10;

-- Index for cache expiration cleanup
CREATE INDEX IF NOT EXISTS idx_build_cache_expiring
ON build_cache_registry(expires_at)
WHERE expires_at IS NOT NULL AND deleted_at IS NULL;

-- Index for multi-tenant cache statistics
CREATE INDEX IF NOT EXISTS idx_build_cache_customer_stats
ON build_cache_registry(first_built_by_customer, tier, first_built_at DESC)
WHERE deleted_at IS NULL;

-- Index for architecture-specific queries
CREATE INDEX IF NOT EXISTS idx_build_cache_arch_cloud
ON build_cache_registry(architecture, cloud_provider, tier)
WHERE deleted_at IS NULL;

-- Covering index for cache search queries (includes common SELECT columns)
CREATE INDEX IF NOT EXISTS idx_build_cache_search_covering
ON build_cache_registry(manifest_hash, tier, cloud_provider, architecture)
INCLUDE (full_image_url, cache_hit_count, image_size_bytes)
WHERE deleted_at IS NULL;

COMMENT ON INDEX idx_build_cache_lookup_with_tier IS 'Fast cache lookup with tier enforcement';
COMMENT ON INDEX idx_build_cache_popular IS 'Identifies frequently reused builds for optimization';
COMMENT ON INDEX idx_build_cache_expiring IS 'Supports background job for cache expiration cleanup';

-- ============================================================================
-- Build Access Log - Time-Series Indexes
-- ============================================================================

-- Index for recent access patterns
CREATE INDEX IF NOT EXISTS idx_build_access_recent
ON build_access_log(accessed_at DESC)
WHERE accessed_at >= NOW() - INTERVAL '7 days';

-- Index for customer cache hit analysis
CREATE INDEX IF NOT EXISTS idx_build_access_customer_analysis
ON build_access_log(customer_id, access_type, accessed_at DESC)
WHERE accessed_at >= NOW() - INTERVAL '30 days';

-- Index for cache efficiency metrics
CREATE INDEX IF NOT EXISTS idx_build_access_cache_metrics
ON build_access_log(build_cache_id, access_type, accessed_at DESC)
WHERE access_type IN ('cache_hit', 'cache_miss');

-- Index for deployment tracking
CREATE INDEX IF NOT EXISTS idx_build_access_deployment_tracking
ON build_access_log(deployment_id, accessed_at DESC)
WHERE deployment_id IS NOT NULL;

COMMENT ON INDEX idx_build_access_recent IS 'Optimizes queries for recent build access patterns';
COMMENT ON INDEX idx_build_access_cache_metrics IS 'Supports cache hit rate calculations';

-- ============================================================================
-- Licenses - Expiration and Validation Indexes
-- ============================================================================

-- Index for expired license detection
CREATE INDEX IF NOT EXISTS idx_licenses_expired
ON licenses(expires_at, status)
WHERE status = 'active' AND expires_at < NOW() + INTERVAL '30 days';

-- Index for trial conversion tracking
CREATE INDEX IF NOT EXISTS idx_licenses_trial_conversion
ON licenses(trial_expires_at, status, tier)
WHERE status = 'trial';

-- Index for feature flag queries
CREATE INDEX IF NOT EXISTS idx_licenses_features_gin
ON licenses USING GIN(features)
WHERE status = 'active';

-- Index for quota enforcement
CREATE INDEX IF NOT EXISTS idx_licenses_quota_check
ON licenses(customer_id, status)
INCLUDE (max_builds_per_month, max_registries, max_concurrent_builds)
WHERE status IN ('active', 'trial');

COMMENT ON INDEX idx_licenses_expired IS 'Identifies licenses expiring soon for renewal notifications';
COMMENT ON INDEX idx_licenses_trial_conversion IS 'Tracks trial licenses for conversion campaigns';

-- ============================================================================
-- Registry Credentials - Security and Validation Indexes
-- ============================================================================

-- Index for active credential lookup
CREATE INDEX IF NOT EXISTS idx_registry_credentials_active_lookup
ON registry_credentials(customer_id, is_active, is_default)
WHERE revoked_at IS NULL;

-- Index for credential validation scheduling
CREATE INDEX IF NOT EXISTS idx_registry_credentials_validation_due
ON registry_credentials(last_validated_at, is_active)
WHERE is_active = TRUE AND revoked_at IS NULL
  AND (last_validated_at IS NULL OR last_validated_at < NOW() - INTERVAL '1 day');

-- Index for registry provider statistics
CREATE INDEX IF NOT EXISTS idx_registry_credentials_provider_stats
ON registry_credentials(registry_provider, is_active, created_at DESC)
WHERE revoked_at IS NULL;

COMMENT ON INDEX idx_registry_credentials_validation_due IS 'Identifies credentials needing validation';

-- ============================================================================
-- Build Queue - Queue Processing Indexes
-- ============================================================================

-- Index for worker assignment
CREATE INDEX IF NOT EXISTS idx_build_queue_worker_assignment
ON build_queue(status, priority DESC, queued_at ASC)
WHERE status = 'queued';

-- Index for customer build history
CREATE INDEX IF NOT EXISTS idx_build_queue_customer_history
ON build_queue(customer_id, status, completed_at DESC)
INCLUDE (manifest_hash, retry_count);

-- Index for timeout monitoring
CREATE INDEX IF NOT EXISTS idx_build_queue_timeout_monitor
ON build_queue(timeout_at, status, worker_id)
WHERE status = 'building' AND timeout_at IS NOT NULL;

-- Index for retry analysis
CREATE INDEX IF NOT EXISTS idx_build_queue_retry_analysis
ON build_queue(retry_count, status, completed_at DESC)
WHERE retry_count > 0;

-- Index for architecture-specific queue metrics
CREATE INDEX IF NOT EXISTS idx_build_queue_architecture_metrics
ON build_queue(architecture, status, priority DESC, queued_at ASC);

-- Covering index for queue dashboard queries
CREATE INDEX IF NOT EXISTS idx_build_queue_dashboard_covering
ON build_queue(status, priority DESC, queued_at ASC)
INCLUDE (customer_id, progress_percent, current_step, estimated_duration_seconds)
WHERE status IN ('queued', 'building');

COMMENT ON INDEX idx_build_queue_worker_assignment IS 'Optimizes worker selection for next build job';
COMMENT ON INDEX idx_build_queue_timeout_monitor IS 'Supports timeout detection background job';
COMMENT ON INDEX idx_build_queue_dashboard_covering IS 'Covering index for real-time queue dashboard';

-- ============================================================================
-- Build Results - Analytics Indexes
-- ============================================================================

-- Index for build success rate analysis
CREATE INDEX IF NOT EXISTS idx_build_results_success_analysis
ON build_results(success, created_at DESC)
INCLUDE (actual_duration_seconds, final_image_size_bytes);

-- Index for performance metrics
CREATE INDEX IF NOT EXISTS idx_build_results_performance
ON build_results(created_at DESC)
INCLUDE (actual_duration_seconds, peak_memory_mb, final_image_size_bytes)
WHERE success = TRUE;

-- Index for cache effectiveness analysis
CREATE INDEX IF NOT EXISTS idx_build_results_cache_effectiveness
ON build_results(total_layers, cache_layers_used, created_at DESC)
WHERE success = TRUE AND total_layers > 0;

-- Index for image registry tracking
CREATE INDEX IF NOT EXISTS idx_build_results_registry_tracking
ON build_results(pushed_to_registry, image_digest, created_at DESC)
WHERE success = TRUE;

COMMENT ON INDEX idx_build_results_cache_effectiveness IS 'Analyzes Docker layer cache hit rates';

-- ============================================================================
-- Build Events - Time-Series Indexes
-- ============================================================================

-- Index for real-time event streaming
CREATE INDEX IF NOT EXISTS idx_build_events_streaming
ON build_events(build_queue_id, event_timestamp DESC)
WHERE event_timestamp >= NOW() - INTERVAL '1 hour';

-- Index for error analysis
CREATE INDEX IF NOT EXISTS idx_build_events_errors
ON build_events(severity, event_type, event_timestamp DESC)
WHERE severity IN ('error', 'critical');

-- Index for event type analytics
CREATE INDEX IF NOT EXISTS idx_build_events_type_analytics
ON build_events(event_type, event_timestamp DESC)
WHERE event_timestamp >= NOW() - INTERVAL '7 days';

COMMENT ON INDEX idx_build_events_streaming IS 'Optimizes real-time build event streaming';
COMMENT ON INDEX idx_build_events_errors IS 'Fast lookup for error investigation';

-- ============================================================================
-- Conversations - AI Performance Indexes
-- ============================================================================

-- Index for abandoned conversation cleanup
CREATE INDEX IF NOT EXISTS idx_conversations_cleanup
ON conversations(last_message_at, status)
WHERE status = 'active' AND last_message_at < NOW() - INTERVAL '24 hours';

-- Index for customer conversation history
CREATE INDEX IF NOT EXISTS idx_conversations_customer_history
ON conversations(customer_id, started_at DESC)
INCLUDE (status, total_messages, build_manifest_id)
WHERE customer_id IS NOT NULL;

-- Index for AI model performance comparison
CREATE INDEX IF NOT EXISTS idx_conversations_model_performance
ON conversations(ai_model, status, completed_at DESC)
INCLUDE (total_messages, total_tokens_used, total_cost_usd, user_satisfaction_score)
WHERE completed_at IS NOT NULL;

-- Index for session reconnection
CREATE INDEX IF NOT EXISTS idx_conversations_session_reconnect
ON conversations(session_id, status)
WHERE status = 'active';

-- Index for feedback analysis
CREATE INDEX IF NOT EXISTS idx_conversations_feedback
ON conversations(user_satisfaction_score, feedback_at DESC)
WHERE user_satisfaction_score IS NOT NULL;

COMMENT ON INDEX idx_conversations_cleanup IS 'Identifies stale conversations for cleanup';
COMMENT ON INDEX idx_conversations_model_performance IS 'Compares AI model efficiency';

-- ============================================================================
-- Conversation Messages - Message Retrieval Indexes
-- ============================================================================

-- Index for conversation replay (ordered messages)
CREATE INDEX IF NOT EXISTS idx_conversation_messages_replay
ON conversation_messages(conversation_id, sequence_number ASC)
INCLUDE (role, content, created_at);

-- Index for function call analysis
CREATE INDEX IF NOT EXISTS idx_conversation_messages_functions
ON conversation_messages(function_name, created_at DESC)
WHERE function_name IS NOT NULL;

-- Index for token usage analysis
CREATE INDEX IF NOT EXISTS idx_conversation_messages_tokens
ON conversation_messages(model_used, created_at DESC)
INCLUDE (prompt_tokens, completion_tokens, total_tokens)
WHERE total_tokens IS NOT NULL;

COMMENT ON INDEX idx_conversation_messages_replay IS 'Optimizes conversation message retrieval';

-- ============================================================================
-- Build Manifests - Manifest Management Indexes
-- ============================================================================

-- Index for customer manifest library
CREATE INDEX IF NOT EXISTS idx_build_manifests_customer_library
ON build_manifests(customer_id, deployment_status, created_at DESC)
WHERE deleted_at IS NULL;

-- Index for manifest deployment tracking
CREATE INDEX IF NOT EXISTS idx_build_manifests_deployment_tracking
ON build_manifests(deployment_status, deployed_at DESC)
WHERE deployment_status IN ('building', 'deployed');

-- Index for manifest validation queue
CREATE INDEX IF NOT EXISTS idx_build_manifests_validation_queue
ON build_manifests(is_validated, created_at ASC)
WHERE is_validated = FALSE AND deleted_at IS NULL;

-- Index for duplicate manifest detection
CREATE INDEX IF NOT EXISTS idx_build_manifests_duplicate_detection
ON build_manifests(manifest_hash, customer_id, deployment_status)
WHERE deleted_at IS NULL;

COMMENT ON INDEX idx_build_manifests_validation_queue IS 'Identifies manifests pending validation';
COMMENT ON INDEX idx_build_manifests_duplicate_detection IS 'Prevents duplicate manifest submissions';

-- ============================================================================
-- Requirement Extractions - Model Improvement Indexes
-- ============================================================================

-- Index for extraction accuracy analysis
CREATE INDEX IF NOT EXISTS idx_requirement_extractions_accuracy
ON requirement_extractions(requirement_type, was_corrected, created_at DESC)
INCLUDE (extraction_confidence);

-- Index for model retraining data collection
CREATE INDEX IF NOT EXISTS idx_requirement_extractions_training_data
ON requirement_extractions(was_corrected, is_confirmed, created_at DESC)
WHERE was_corrected = TRUE OR is_confirmed = TRUE;

-- Index for low-confidence extractions
CREATE INDEX IF NOT EXISTS idx_requirement_extractions_low_confidence
ON requirement_extractions(extraction_confidence, requirement_type, created_at DESC)
WHERE extraction_confidence < 0.7;

COMMENT ON INDEX idx_requirement_extractions_training_data IS 'Collects data for AI model retraining';
COMMENT ON INDEX idx_requirement_extractions_low_confidence IS 'Identifies extractions needing human review';

-- ============================================================================
-- Cross-Table Analytics Indexes
-- ============================================================================

-- Index for customer journey analysis (conversations -> manifests -> builds)
CREATE INDEX IF NOT EXISTS idx_customer_journey_conversations
ON conversations(customer_id, build_manifest_id, status, started_at DESC)
WHERE build_manifest_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_customer_journey_manifests
ON build_manifests(customer_id, deployment_id, deployment_status, created_at DESC)
WHERE deployment_id IS NOT NULL;

COMMENT ON INDEX idx_customer_journey_conversations IS 'Supports customer journey analytics';

-- ============================================================================
-- Vacuum and Maintenance Hints
-- ============================================================================

-- Tables with high insert volume - consider autovacuum tuning:
--   - build_access_log (time-series, high insert rate)
--   - build_events (time-series, high insert rate)
--   - conversation_messages (high insert rate during active conversations)
--   - build_metrics (time-series, very high insert rate if enabled)

-- Consider partitioning by date for:
--   - build_access_log (by accessed_at, monthly partitions)
--   - build_events (by event_timestamp, monthly partitions)
--   - conversation_messages (by created_at, monthly partitions)
--   - build_metrics (by metric_timestamp, daily partitions)

-- Index maintenance recommendations:
--   - REINDEX CONCURRENTLY on production databases
--   - Monitor index bloat with pg_stat_user_indexes
--   - Consider pg_repack for heavily updated tables
