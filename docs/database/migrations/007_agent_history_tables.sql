-- Migration: Agent History Persistence
-- Purpose: Store agent interactions and session summaries for long-term learning
-- Date: 2025-01-10

-- Agent Interactions Table
-- Stores individual agent interactions with full context
CREATE TABLE IF NOT EXISTS agent_interactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    session_id VARCHAR(255) NOT NULL,
    agent_name VARCHAR(255) NOT NULL,
    user_request TEXT NOT NULL,
    agent_response TEXT,
    success BOOLEAN NOT NULL,
    confidence_score DOUBLE PRECISION,
    task_type VARCHAR(100),
    execution_time_ms INTEGER,
    error_message TEXT,
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),

    -- Indexes for efficient queries
    INDEX idx_agent_interactions_session_id (session_id),
    INDEX idx_agent_interactions_agent_name (agent_name),
    INDEX idx_agent_interactions_created_at (created_at),
    INDEX idx_agent_interactions_task_type (task_type),
    INDEX idx_agent_interactions_success (success)
);

-- Agent Session Summaries Table
-- Stores high-level session summaries for quick analysis
CREATE TABLE IF NOT EXISTS agent_session_summaries (
    session_id VARCHAR(255) PRIMARY KEY,
    initial_request TEXT NOT NULL,
    final_outcome VARCHAR(50) NOT NULL,  -- 'success' | 'failure' | 'abandoned'
    agents_used VARCHAR(255)[] NOT NULL,
    interaction_count INTEGER NOT NULL,
    total_duration_ms INTEGER NOT NULL,
    user_satisfaction SMALLINT,  -- 1-5 rating (optional)
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    metadata JSONB,

    -- Indexes for analytics
    INDEX idx_agent_session_summaries_created_at (created_at),
    INDEX idx_agent_session_summaries_final_outcome (final_outcome),
    INDEX idx_agent_session_summaries_agents_used USING GIN (agents_used)
);

-- Comments for documentation
COMMENT ON TABLE agent_interactions IS 'Stores individual agent interactions for long-term learning and analysis';
COMMENT ON TABLE agent_session_summaries IS 'Stores session-level summaries for quick analytics and reporting';

COMMENT ON COLUMN agent_interactions.session_id IS 'Unique session identifier linking multiple interactions';
COMMENT ON COLUMN agent_interactions.agent_name IS 'Name of the agent that processed this interaction';
COMMENT ON COLUMN agent_interactions.confidence_score IS 'Agent confidence score (0.0 to 1.0)';
COMMENT ON COLUMN agent_interactions.task_type IS 'Classified task type (deployment, security, performance, etc.)';
COMMENT ON COLUMN agent_interactions.metadata IS 'Additional context as JSON (workspace info, patterns used, etc.)';

COMMENT ON COLUMN agent_session_summaries.final_outcome IS 'Session outcome: success, failure, or abandoned';
COMMENT ON COLUMN agent_session_summaries.agents_used IS 'Array of agent names used during the session';
COMMENT ON COLUMN agent_session_summaries.user_satisfaction IS 'Optional user satisfaction rating (1-5)';

-- Example queries for analytics:

-- Get agent success rates by task type
-- SELECT
--     agent_name,
--     task_type,
--     COUNT(*) as total,
--     SUM(CASE WHEN success THEN 1 ELSE 0 END) as successful,
--     ROUND(100.0 * SUM(CASE WHEN success THEN 1 ELSE 0 END) / COUNT(*), 2) as success_rate_pct
-- FROM agent_interactions
-- WHERE created_at >= NOW() - INTERVAL '30 days'
-- GROUP BY agent_name, task_type
-- ORDER BY total DESC;

-- Get session summaries with interaction counts
-- SELECT
--     session_id,
--     initial_request,
--     final_outcome,
--     array_to_string(agents_used, ', ') as agents,
--     interaction_count,
--     ROUND(total_duration_ms / 1000.0, 2) as duration_seconds,
--     user_satisfaction,
--     created_at
-- FROM agent_session_summaries
-- ORDER BY created_at DESC
-- LIMIT 20;

-- Find most used agents
-- SELECT
--     unnest(agents_used) as agent_name,
--     COUNT(*) as times_used,
--     AVG(user_satisfaction) as avg_satisfaction
-- FROM agent_session_summaries
-- WHERE completed_at IS NOT NULL
-- GROUP BY agent_name
-- ORDER BY times_used DESC;
