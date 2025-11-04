-- ============================================================================
-- Honua Build Orchestrator - AI Intake System Schema
-- ============================================================================
-- Version: 003
-- Description: AI conversation management and build manifest generation
-- Dependencies: 001_InitialSchema.sql, 002_BuildQueue.sql
-- ============================================================================

-- ============================================================================
-- Build Manifests Table
-- ============================================================================
-- Generated build manifests from AI intake conversations

CREATE TABLE build_manifests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Customer context
    customer_id VARCHAR(100),
    customer_email VARCHAR(255),

    -- Manifest content
    manifest_json JSONB NOT NULL,
    manifest_hash VARCHAR(16) NOT NULL,

    -- Manifest metadata
    manifest_version VARCHAR(20) NOT NULL DEFAULT '1.0',
    manifest_format VARCHAR(20) NOT NULL DEFAULT 'honua-v1'
        CHECK (manifest_format IN ('honua-v1', 'honua-v2')),

    -- Generation details
    generated_by VARCHAR(50) NOT NULL DEFAULT 'ai_intake'
        CHECK (generated_by IN ('ai_intake', 'manual', 'api', 'template', 'import')),
    generation_method VARCHAR(100),

    -- Validation
    is_validated BOOLEAN DEFAULT FALSE,
    validation_errors JSONB,
    validated_at TIMESTAMPTZ,
    validated_by VARCHAR(100),

    -- Deployment status
    deployment_status VARCHAR(20) DEFAULT 'draft'
        CHECK (deployment_status IN ('draft', 'approved', 'building', 'deployed', 'failed', 'archived')),
    deployed_at TIMESTAMPTZ,
    deployment_id UUID,

    -- Build cache reference (if built)
    build_cache_id UUID,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    deleted_at TIMESTAMPTZ,

    -- Foreign keys
    CONSTRAINT fk_build_manifests_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE SET NULL,
    CONSTRAINT fk_build_manifests_cache FOREIGN KEY (build_cache_id)
        REFERENCES build_cache_registry(id) ON DELETE SET NULL,
    CONSTRAINT fk_build_manifests_deployment FOREIGN KEY (deployment_id)
        REFERENCES build_queue(id) ON DELETE SET NULL
);

CREATE INDEX idx_build_manifests_customer_id ON build_manifests(customer_id) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_manifests_customer_email ON build_manifests(customer_email) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_manifests_manifest_hash ON build_manifests(manifest_hash);
CREATE INDEX idx_build_manifests_deployment_status ON build_manifests(deployment_status) WHERE deleted_at IS NULL;
CREATE INDEX idx_build_manifests_created_at ON build_manifests(created_at DESC);
CREATE INDEX idx_build_manifests_cache_id ON build_manifests(build_cache_id);

COMMENT ON TABLE build_manifests IS 'Generated build manifests from AI intake or manual creation';
COMMENT ON COLUMN build_manifests.manifest_json IS 'Complete Honua build manifest as JSON';
COMMENT ON COLUMN build_manifests.manifest_hash IS 'MD5 hash for cache lookup and deduplication';
COMMENT ON COLUMN build_manifests.generated_by IS 'Source of manifest generation';
COMMENT ON COLUMN build_manifests.generation_method IS 'Detailed generation method (e.g., "ai_gpt4_turbo", "manual_ui", "api_v1")';
COMMENT ON COLUMN build_manifests.deployment_status IS 'Lifecycle status of the manifest';
COMMENT ON COLUMN build_manifests.deployment_id IS 'Reference to build_queue if this manifest was queued for building';

-- ============================================================================
-- Conversations Table
-- ============================================================================
-- AI intake conversation sessions

CREATE TABLE conversations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Customer identification
    customer_id VARCHAR(100),
    customer_email VARCHAR(255),
    session_id VARCHAR(100),

    -- Conversation status
    status VARCHAR(20) NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'completed', 'abandoned', 'error')),

    -- Conversation metadata
    conversation_type VARCHAR(50) DEFAULT 'build_intake'
        CHECK (conversation_type IN ('build_intake', 'troubleshooting', 'support', 'upgrade_consultation')),

    -- AI model details
    ai_model VARCHAR(100),
    ai_provider VARCHAR(50) DEFAULT 'openai',

    -- Results
    build_manifest_id UUID,
    extracted_requirements JSONB,
    confidence_score NUMERIC(3,2) CHECK (confidence_score BETWEEN 0 AND 1),

    -- Timing
    started_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    last_message_at TIMESTAMPTZ,
    abandoned_at TIMESTAMPTZ,

    -- Duration tracking
    total_messages INTEGER DEFAULT 0,
    total_tokens_used INTEGER DEFAULT 0,
    total_cost_usd NUMERIC(10,6) DEFAULT 0,

    -- Session metadata
    user_agent TEXT,
    ip_address INET,
    referrer TEXT,
    locale VARCHAR(10) DEFAULT 'en-US',

    -- Feedback
    user_satisfaction_score INTEGER CHECK (user_satisfaction_score BETWEEN 1 AND 5),
    user_feedback TEXT,
    feedback_at TIMESTAMPTZ,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_conversations_customer FOREIGN KEY (customer_id)
        REFERENCES customers(customer_id) ON DELETE SET NULL,
    CONSTRAINT fk_conversations_manifest FOREIGN KEY (build_manifest_id)
        REFERENCES build_manifests(id) ON DELETE SET NULL
);

CREATE INDEX idx_conversations_customer_id ON conversations(customer_id);
CREATE INDEX idx_conversations_customer_email ON conversations(customer_email);
CREATE INDEX idx_conversations_status ON conversations(status);
CREATE INDEX idx_conversations_last_message_at ON conversations(last_message_at DESC);
CREATE INDEX idx_conversations_started_at ON conversations(started_at DESC);
CREATE INDEX idx_conversations_session_id ON conversations(session_id);

-- Index for abandoned conversation cleanup
CREATE INDEX idx_conversations_abandoned ON conversations(last_message_at)
    WHERE status = 'active' AND last_message_at < NOW() - INTERVAL '24 hours';

COMMENT ON TABLE conversations IS 'AI intake conversation sessions with metadata and results';
COMMENT ON COLUMN conversations.session_id IS 'Frontend session identifier for reconnection';
COMMENT ON COLUMN conversations.extracted_requirements IS 'Structured requirements extracted from conversation as JSON';
COMMENT ON COLUMN conversations.confidence_score IS 'AI confidence in extracted requirements (0.0-1.0)';
COMMENT ON COLUMN conversations.total_tokens_used IS 'Total OpenAI tokens consumed in this conversation';
COMMENT ON COLUMN conversations.total_cost_usd IS 'Estimated total cost of AI API calls';

-- ============================================================================
-- Conversation Messages Table
-- ============================================================================
-- Individual messages in AI conversations

CREATE TABLE conversation_messages (
    id BIGSERIAL PRIMARY KEY,

    -- Reference
    conversation_id UUID NOT NULL,

    -- Message details
    role VARCHAR(20) NOT NULL
        CHECK (role IN ('system', 'user', 'assistant', 'function', 'tool')),
    content TEXT NOT NULL,
    content_type VARCHAR(50) DEFAULT 'text/plain'
        CHECK (content_type IN ('text/plain', 'text/markdown', 'application/json')),

    -- Message ordering
    sequence_number INTEGER NOT NULL,

    -- Function/tool calls (for agent actions)
    function_name VARCHAR(100),
    function_arguments JSONB,
    function_response JSONB,

    -- Token tracking
    prompt_tokens INTEGER,
    completion_tokens INTEGER,
    total_tokens INTEGER,

    -- Metadata
    model_used VARCHAR(100),
    temperature NUMERIC(3,2),
    finish_reason VARCHAR(50),

    -- Timing
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processing_time_ms INTEGER,

    -- Foreign key
    CONSTRAINT fk_conversation_messages_conversation FOREIGN KEY (conversation_id)
        REFERENCES conversations(id) ON DELETE CASCADE,

    -- Unique constraint for message ordering
    CONSTRAINT uq_conversation_message_sequence UNIQUE (conversation_id, sequence_number)
);

CREATE INDEX idx_conversation_messages_conversation_id ON conversation_messages(conversation_id);
CREATE INDEX idx_conversation_messages_created_at ON conversation_messages(created_at DESC);
CREATE INDEX idx_conversation_messages_role ON conversation_messages(role);

-- Index for efficient message retrieval in order
CREATE INDEX idx_conversation_messages_ordered ON conversation_messages(conversation_id, sequence_number ASC);

COMMENT ON TABLE conversation_messages IS 'Individual messages in AI conversations with token tracking';
COMMENT ON COLUMN conversation_messages.role IS 'Message role in OpenAI format: system, user, assistant, function, tool';
COMMENT ON COLUMN conversation_messages.sequence_number IS 'Message order within conversation (1, 2, 3, ...)';
COMMENT ON COLUMN conversation_messages.function_name IS 'Function name if this is a function/tool call';
COMMENT ON COLUMN conversation_messages.finish_reason IS 'OpenAI finish reason: stop, length, function_call, content_filter, etc.';

-- ============================================================================
-- Conversation Intents Table
-- ============================================================================
-- Detected user intents during conversation (for analytics and improvement)

CREATE TABLE conversation_intents (
    id BIGSERIAL PRIMARY KEY,

    -- Reference
    conversation_id UUID NOT NULL,
    message_id BIGINT,

    -- Intent detection
    intent_type VARCHAR(100) NOT NULL,
    intent_confidence NUMERIC(3,2) CHECK (intent_confidence BETWEEN 0 AND 1),
    intent_parameters JSONB,

    -- Detection details
    detected_by VARCHAR(50) DEFAULT 'ai_classifier',
    detected_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_conversation_intents_conversation FOREIGN KEY (conversation_id)
        REFERENCES conversations(id) ON DELETE CASCADE,
    CONSTRAINT fk_conversation_intents_message FOREIGN KEY (message_id)
        REFERENCES conversation_messages(id) ON DELETE CASCADE
);

CREATE INDEX idx_conversation_intents_conversation_id ON conversation_intents(conversation_id);
CREATE INDEX idx_conversation_intents_intent_type ON conversation_intents(intent_type);
CREATE INDEX idx_conversation_intents_detected_at ON conversation_intents(detected_at DESC);

COMMENT ON TABLE conversation_intents IS 'Detected user intents for analytics and conversation routing';
COMMENT ON COLUMN conversation_intents.intent_type IS 'Intent category (e.g., "specify_cloud_provider", "request_pricing", "ask_question")';
COMMENT ON COLUMN conversation_intents.intent_parameters IS 'Extracted parameters from intent as JSON';

-- ============================================================================
-- Requirement Extractions Table
-- ============================================================================
-- Track how requirements were extracted from conversation (for model improvement)

CREATE TABLE requirement_extractions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    -- Reference
    conversation_id UUID NOT NULL,
    build_manifest_id UUID,

    -- Extracted requirement
    requirement_type VARCHAR(100) NOT NULL,
    requirement_value TEXT NOT NULL,
    requirement_json JSONB,

    -- Extraction details
    extracted_from_message_id BIGINT,
    extraction_method VARCHAR(50) NOT NULL DEFAULT 'ai_extraction',
    extraction_confidence NUMERIC(3,2) CHECK (extraction_confidence BETWEEN 0 AND 1),

    -- Validation
    is_confirmed BOOLEAN DEFAULT FALSE,
    confirmed_at TIMESTAMPTZ,
    confirmation_source VARCHAR(50),

    -- User override
    was_corrected BOOLEAN DEFAULT FALSE,
    corrected_value TEXT,
    corrected_at TIMESTAMPTZ,

    -- Audit
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    -- Foreign keys
    CONSTRAINT fk_requirement_extractions_conversation FOREIGN KEY (conversation_id)
        REFERENCES conversations(id) ON DELETE CASCADE,
    CONSTRAINT fk_requirement_extractions_manifest FOREIGN KEY (build_manifest_id)
        REFERENCES build_manifests(id) ON DELETE CASCADE,
    CONSTRAINT fk_requirement_extractions_message FOREIGN KEY (extracted_from_message_id)
        REFERENCES conversation_messages(id) ON DELETE SET NULL
);

CREATE INDEX idx_requirement_extractions_conversation_id ON requirement_extractions(conversation_id);
CREATE INDEX idx_requirement_extractions_manifest_id ON requirement_extractions(build_manifest_id);
CREATE INDEX idx_requirement_extractions_requirement_type ON requirement_extractions(requirement_type);
CREATE INDEX idx_requirement_extractions_was_corrected ON requirement_extractions(was_corrected);

COMMENT ON TABLE requirement_extractions IS 'Tracks requirement extraction from conversations for model improvement';
COMMENT ON COLUMN requirement_extractions.requirement_type IS 'Type of requirement (e.g., "cloud_provider", "data_volume", "modules")';
COMMENT ON COLUMN requirement_extractions.was_corrected IS 'Whether user corrected the AI extraction - used for model retraining';
COMMENT ON COLUMN requirement_extractions.extraction_confidence IS 'AI confidence in extraction accuracy (0.0-1.0)';

-- ============================================================================
-- Views for Conversation Analytics
-- ============================================================================

-- Active conversations view
CREATE OR REPLACE VIEW active_conversations AS
SELECT
    c.id,
    c.customer_id,
    c.customer_email,
    c.status,
    c.conversation_type,
    c.total_messages,
    c.started_at,
    c.last_message_at,
    EXTRACT(EPOCH FROM (NOW() - c.last_message_at))::INTEGER AS seconds_since_last_message,
    c.ai_model,
    c.build_manifest_id,
    bm.deployment_status AS manifest_deployment_status
FROM conversations c
LEFT JOIN build_manifests bm ON c.build_manifest_id = bm.id
WHERE c.status = 'active'
ORDER BY c.last_message_at DESC;

COMMENT ON VIEW active_conversations IS 'Currently active AI conversations with latest activity';

-- Conversation performance summary
CREATE OR REPLACE VIEW conversation_performance_summary AS
SELECT
    DATE(c.created_at) AS conversation_date,
    c.conversation_type,
    c.ai_model,
    COUNT(*) AS total_conversations,
    COUNT(*) FILTER (WHERE c.status = 'completed') AS completed_conversations,
    COUNT(*) FILTER (WHERE c.status = 'abandoned') AS abandoned_conversations,
    ROUND(AVG(c.total_messages)::NUMERIC, 1) AS avg_messages_per_conversation,
    ROUND(AVG(c.total_tokens_used)::NUMERIC, 0) AS avg_tokens_per_conversation,
    ROUND(AVG(c.total_cost_usd)::NUMERIC, 6) AS avg_cost_per_conversation,
    ROUND(AVG(EXTRACT(EPOCH FROM (c.completed_at - c.started_at)) / 60)::NUMERIC, 1) AS avg_duration_minutes,
    ROUND(AVG(c.user_satisfaction_score)::NUMERIC, 2) AS avg_satisfaction_score,
    COUNT(*) FILTER (WHERE c.build_manifest_id IS NOT NULL) AS manifests_generated
FROM conversations c
WHERE c.created_at >= NOW() - INTERVAL '30 days'
GROUP BY DATE(c.created_at), c.conversation_type, c.ai_model
ORDER BY conversation_date DESC;

COMMENT ON VIEW conversation_performance_summary IS 'Daily conversation performance metrics';

-- Intent analytics view
CREATE OR REPLACE VIEW intent_analytics AS
SELECT
    ci.intent_type,
    COUNT(*) AS occurrence_count,
    ROUND(AVG(ci.intent_confidence)::NUMERIC, 3) AS avg_confidence,
    COUNT(DISTINCT ci.conversation_id) AS unique_conversations,
    DATE_TRUNC('day', ci.detected_at) AS detection_date
FROM conversation_intents ci
WHERE ci.detected_at >= NOW() - INTERVAL '30 days'
GROUP BY ci.intent_type, DATE_TRUNC('day', ci.detected_at)
ORDER BY detection_date DESC, occurrence_count DESC;

COMMENT ON VIEW intent_analytics IS 'Intent detection analytics for conversation improvement';

-- Requirement extraction accuracy view
CREATE OR REPLACE VIEW extraction_accuracy AS
SELECT
    re.requirement_type,
    COUNT(*) AS total_extractions,
    COUNT(*) FILTER (WHERE re.is_confirmed = TRUE) AS confirmed_extractions,
    COUNT(*) FILTER (WHERE re.was_corrected = TRUE) AS corrected_extractions,
    ROUND(AVG(re.extraction_confidence)::NUMERIC, 3) AS avg_confidence,
    ROUND((COUNT(*) FILTER (WHERE re.is_confirmed = TRUE)::NUMERIC / NULLIF(COUNT(*), 0) * 100), 2) AS confirmation_rate_percent,
    ROUND((COUNT(*) FILTER (WHERE re.was_corrected = TRUE)::NUMERIC / NULLIF(COUNT(*), 0) * 100), 2) AS correction_rate_percent
FROM requirement_extractions re
WHERE re.created_at >= NOW() - INTERVAL '30 days'
GROUP BY re.requirement_type
ORDER BY total_extractions DESC;

COMMENT ON VIEW extraction_accuracy IS 'Requirement extraction accuracy metrics for model improvement';
